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
    try:
        obj = json.loads(raw.decode("ascii"), object_pairs_hook=pairs)
    except (UnicodeError, RecursionError) as exc:
        raise ValueError("JOURNAL_ENCODING_OR_DEPTH") from exc
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


def verify_legacy_swap(*, nonce, receipt, backup, expected_evidence):
    """Normalize a legacy receipt only against independently authenticated pins.
    The caller must enforce legacy filesystem modes, locks and recovery checks.
    """
    if type(nonce) is not str or not HEX.fullmatch(nonce):
        raise ValueError("LEGACY_NONCE")
    if type(receipt) is not bytes or not 0 < len(receipt) <= 4096:
        raise ValueError("LEGACY_RECEIPT_SIZE")
    def pairs(items):
        out = {}
        for k, v in items:
            if k in out:
                raise ValueError("LEGACY_DUPLICATE")
            out[k] = v
        return out
    try:
        r = json.loads(receipt.decode("ascii"), object_pairs_hook=pairs)
    except (UnicodeError, RecursionError) as exc:
        raise ValueError("LEGACY_ENCODING_OR_DEPTH") from exc
    keys = {"source_sha", "release_asset_sha256", "candidate_operator_sha256"}
    if type(r) is not dict or set(r) != KEYS | keys or r["status"] != "LAUNCHER_ATOMIC_SWAP_COMMITTED" or r["nonce"] != nonce:
        raise ValueError("LEGACY_RECEIPT")
    evidence = {k: r[k] for k in keys}
    if type(expected_evidence) is not dict or evidence != expected_evidence:
        raise ValueError("LEGACY_EVIDENCE")
    for k, pattern in (("source_sha", r"[0-9a-f]{40}"), ("release_asset_sha256", r"sha256:[0-9a-f]{64}"), ("candidate_operator_sha256", r"[0-9a-f]{64}")):
        if type(evidence[k]) is not str or not re.fullmatch(pattern, evidence[k]):
            raise ValueError("LEGACY_EVIDENCE_FORMAT")
    old, new = r["old_launcher_sha256"], r["new_launcher_sha256"]
    if any(type(v) is not str or not HEX.fullmatch(v) for v in (old, new)) or old == new:
        raise ValueError("LEGACY_DIGEST")
    if type(backup) is not bytes or not 0 < len(backup) <= 4096 or hashlib.sha256(backup).hexdigest() != old:
        raise ValueError("LEGACY_BACKUP")
    return nonce, old, new


def reconcile_snapshot(*, original, live, records, authenticated_pins, verify_chain):
    """Validate a complete snapshot supplied by a trusted, locked reader.

    Pins must be independently authenticated, never extracted from receipts.
    This function performs no filesystem access and grants no permission.
    """
    if type(records) not in (list, tuple) or len(records) > 1000:
        raise ValueError("SNAPSHOT_RECORDS")
    if type(authenticated_pins) is not dict or len(authenticated_pins) != len(records):
        raise ValueError("SNAPSHOT_PIN_COVERAGE")
    # Bound aggregate input before parsing any journal. Individual backup
    # limits alone permit a gigabyte of attacker-controlled input.
    total = 0
    for record in records:
        if type(record) is not dict:
            raise ValueError("SNAPSHOT_RECORD")
        for field in ("receipt", "backup", "intent", "marker"):
            if field in record:
                raw = record[field]
                if type(raw) is not bytes:
                    raise ValueError("SNAPSHOT_BYTES")
                total += len(raw)
                if total > 16 * 1024 * 1024:
                    raise ValueError("SNAPSHOT_TOTAL_SIZE")
    seen, edges = set(), []
    for record in records:
        if type(record) is not dict:
            raise ValueError("SNAPSHOT_RECORD")
        generation = record.get("generation")
        fields = {"generation", "nonce", "receipt", "backup"}
        if generation == "modern":
            fields |= {"intent", "marker"}
        elif generation != "legacy":
            raise ValueError("SNAPSHOT_GENERATION")
        if set(record) != fields:
            raise ValueError("SNAPSHOT_FIELDS")
        nonce = record["nonce"]
        if type(nonce) is not str or not HEX.fullmatch(nonce) or nonce in seen or nonce not in authenticated_pins:
            raise ValueError("SNAPSHOT_NONCE")
        pin = authenticated_pins[nonce]
        if type(pin) is not dict or set(pin) != {"generation", "evidence", "old_sha256", "new_sha256"} or pin["generation"] != generation:
            raise ValueError("SNAPSHOT_PIN_GENERATION")
        args = {k: v for k, v in record.items() if k != "generation"}
        args["expected_evidence"] = pin["evidence"]
        validator = verify_modern_swap if generation == "modern" else verify_legacy_swap
        edge = validator(**args)
        if edge[1:] != (pin["old_sha256"], pin["new_sha256"]):
            raise ValueError("SNAPSHOT_PIN_TRANSITION")
        edges.append(edge)
        seen.add(nonce)
    if seen != set(authenticated_pins):
        raise ValueError("SNAPSHOT_UNUSED_PIN")
    return verify_chain(original=original, live=live, edges=edges)


def collect_legacy_snapshot(*, directory, authenticated_pins, read_bound,
                            root_chain, residual_intent_is_committed):
    """Read legacy records using the existing trusted root reader.
    Caller MUST hold all five locks and authenticate pins independently.
    Unknown or incomplete entries block; no journal is modified.
    """
    root_chain(directory, directory=True, exact_mode=0o700)
    entries = {}
    for entry in directory.iterdir():
        if len(entries) >= 1000 or entry.is_symlink():
            raise ValueError("SNAPSHOT_DIRECTORY")
        if not re.fullmatch(r"(?:receipt-[0-9a-f]{64}\.json|old-[0-9a-f]{64}\.bin|intent-[0-9a-f]{64}\.(?:json|superseded))", entry.name):
            raise ValueError("SNAPSHOT_UNKNOWN_ENTRY")
        entries[entry.name] = entry
    names = set(entries)
    records = []
    for name, entry in sorted(entries.items()):
        nonce = name.split("-", 1)[1].split(".", 1)[0]
        if nonce not in authenticated_pins or authenticated_pins[nonce].get("generation") != "legacy":
            raise ValueError("SNAPSHOT_UNPINNED_ENTRY")
        receipt, backup = "receipt-"+nonce+".json", "old-"+nonce+".bin"
        if receipt not in names or backup not in names:
            raise ValueError("SNAPSHOT_INCOMPLETE")
        if name.startswith("intent-"):
            if not residual_intent_is_committed(entry, names):
                raise ValueError("SNAPSHOT_PENDING")
        elif name == receipt:
            records.append(dict(generation="legacy", nonce=nonce,
                receipt=read_bound(entry, maximum=4096, exact_mode=0o600),
                backup=read_bound(entries[backup], maximum=4096, exact_mode=0o700)))
    if set(entries) != {entry.name for entry in directory.iterdir()}:
        raise ValueError("SNAPSHOT_DIRECTORY_CHANGED")
    if {record["nonce"] for record in records} != {n for n,p in authenticated_pins.items() if p.get("generation") == "legacy"}:
        raise ValueError("SNAPSHOT_LEGACY_COVERAGE")
    return records


def reconcile_locked_custody(*, lock_context, authenticate_pins, read_endpoints,
                             collect_legacy, collect_modern, verify_chain):
    """Orchestrate read-only reconciliation inside the existing lock context.
    All callbacks must be trusted by the caller; this is not an admission gate.
    """
    with lock_context(read_only=True):
        pins = authenticate_pins()
        if type(pins) is not dict or len(pins) > 1000:
            raise ValueError("CUSTODY_PINS")
        for nonce, pin in pins.items():
            if type(nonce) is not str or not HEX.fullmatch(nonce) or type(pin) is not dict or pin.get("generation") not in ("legacy", "modern"):
                raise ValueError("CUSTODY_PIN_INVALID")
        original, live = read_endpoints()
        legacy = collect_legacy(authenticated_pins=pins)
        modern = collect_modern(authenticated_pins=pins)
        if type(legacy) is not list or type(modern) is not list:
            raise ValueError("CUSTODY_COLLECTOR_TYPE")
        if any(type(r) is not dict or r.get("generation") != generation
               for generation, records in (("legacy", legacy), ("modern", modern))
               for r in records):
            raise ValueError("CUSTODY_COLLECTOR_GENERATION")
        result = reconcile_snapshot(original=original, live=live,
            records=legacy + modern, authenticated_pins=pins, verify_chain=verify_chain)
        if read_endpoints() != (original, live):
            raise ValueError("CUSTODY_ENDPOINT_CHANGED")
        return result
