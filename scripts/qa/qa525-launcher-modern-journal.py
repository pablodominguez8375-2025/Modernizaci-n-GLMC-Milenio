"""Pure modern swap journal validation. No filesystem access or authority.
Caller must hold the existing locks, authenticate GO evidence and securely
read root-owned journal files; this module checks their internal consistency.
"""
import hashlib
import json
import re

HEX = re.compile(r"[0-9a-f]{64}")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}")
KEYS = {"status", "nonce", "old_launcher_sha256", "new_launcher_sha256"}
EVIDENCE = {"app_archive_sha256", "app_source_sha", "candidate_operator_sha256", "shim_sha256"}

def canonical(raw):
    if type(raw) is not bytes or not 0 < len(raw) <= 16384:
        raise ValueError("JOURNAL_SIZE")
    def pairs(items):
        out = {}
        for k, v in items:
            if k in out:
                raise ValueError("JOURNAL_DUPLICATE")
            out[k] = v
        return out
    obj = json.loads(raw.decode("ascii"), object_pairs_hook=pairs)
    if type(obj) is not dict or (json.dumps(obj, sort_keys=True, separators=(",", ":")) + "\n").encode("ascii") != raw:
        raise ValueError("JOURNAL_CANONICAL")
    return obj

def verify_modern_swap(*, nonce, receipt, intent, marker, backup, expected_evidence):
    """Return a normalized edge only after matching independently pinned evidence.
    This is not a GO verifier, filesystem scanner, or authorization check.
    """
    if type(nonce) is not str or not HEX.fullmatch(nonce):
        raise ValueError("JOURNAL_NONCE")
    r, i, m = map(canonical, (receipt, intent, marker))
    if set(r) != KEYS | {"evidence"} or r["status"] != "LAUNCHER_ATOMIC_SWAP_COMMITTED" or r["nonce"] != nonce:
        raise ValueError("JOURNAL_RECEIPT")
    if i != dict(r, status="SWAP_INTENT_NO_DEPLOYMENT") or m != {"op":"swap","nonce":nonce,"status":"IN_FLIGHT"}:
        raise ValueError("JOURNAL_MISMATCH")
    e = r["evidence"]
    if type(e) is not dict or set(e) != EVIDENCE or type(expected_evidence) is not dict or e != expected_evidence:
        raise ValueError("JOURNAL_EVIDENCE")
    for k, v in e.items():
        if type(v) is not str or not re.fullmatch(r"[0-9a-f]{40}" if k == "app_source_sha" else r"sha256:[0-9a-f]{64}", v):
            raise ValueError("JOURNAL_EVIDENCE_FORMAT")
    old, new = r["old_launcher_sha256"], r["new_launcher_sha256"]
    if any(type(v) is not str or not DIGEST.fullmatch(v) for v in (old,new)) or old == new or e["shim_sha256"] != new:
        raise ValueError("JOURNAL_DIGEST")
    if type(backup) is not bytes or not 0 < len(backup) <= 1048576 or "sha256:" + hashlib.sha256(backup).hexdigest() != old:
        raise ValueError("JOURNAL_BACKUP")
    return nonce, old[7:], new[7:]
