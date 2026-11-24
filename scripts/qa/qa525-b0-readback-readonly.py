#!/usr/bin/env python3
"""QA525 B0 read-only readback of the launcher custody state on glmdch (stdlib only, no writes).

Reports, as EVIDENCE only (never as authority): the shim hash/mode, the hashes of the retained launcher,
child and config modules, the classification of every journal entry, pending markers, and the receipt graph
(original -> live).  It never opens a secret, never mutates, and never calls the installer's scan_history.
Exit code: 0 PASS (no pending, single complete chain, live == shim, expected pins match), 1 STOP.
"""
import argparse
import hashlib
import json
import os
import re
import stat
import sys

SHIM = "/usr/local/sbin/pmgm-qa-update"
STATE = "/opt/centenario/qa525-launcher-replacement"
STORE = "/opt/centenario/qa525-signed-stage"
MODULES = ("qa525-vnext-root-launcher.py", "qa525-vnext-root-launcher-child.py", "qa525-vnext-root-launch-config.py")
HEX64, SHA40 = re.compile(r"[0-9a-f]{64}"), re.compile(r"[0-9a-f]{40}")
MAX_FILE, MAX_ENTRIES = 1 << 20, 1000
PENDING = (re.compile(r"inflight\.json"), re.compile(r"intent-[0-9a-f]{64}\.json"),
           re.compile(r"rollback-intent-[0-9a-f]{64}\.json"))


def sha256_file(path):
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode) or st.st_size > MAX_FILE:
            raise ValueError("NOT_REGULAR_OR_TOO_LARGE")
        data = os.read(fd, MAX_FILE + 1)
        if len(data) != st.st_size:
            raise ValueError("SHORT_READ")
        return hashlib.sha256(data).hexdigest(), data, st
    finally:
        os.close(fd)


def meta(st):
    return {"mode": "%04o" % stat.S_IMODE(st.st_mode), "uid": st.st_uid, "nlink": st.st_nlink}


def digest_hex(value):
    return value[7:] if isinstance(value, str) and value.startswith("sha256:") else value


def classify(name, data):
    """Return ('legacy'|'modern'|'bad', old, new) for a receipt body."""
    try:
        rec = json.loads(data.decode("ascii"))
    except (UnicodeError, ValueError):
        return "bad", None, None
    if not isinstance(rec, dict) or rec.get("status") != "LAUNCHER_ATOMIC_SWAP_COMMITTED":
        return "bad", None, None
    old, new = digest_hex(rec.get("old_launcher_sha256")), digest_hex(rec.get("new_launcher_sha256"))
    if not all(isinstance(v, str) and HEX64.fullmatch(v) for v in (old, new)) or old == new:
        return "bad", None, None
    if set(rec) == {"status", "source_sha", "release_asset_sha256", "old_launcher_sha256", "new_launcher_sha256",
                    "candidate_operator_sha256", "nonce"}:
        return "legacy", old, new
    if set(rec) == {"status", "nonce", "old_launcher_sha256", "new_launcher_sha256", "evidence"}:
        return "modern", old, new
    return "bad", None, None


def read_state(state):
    report = {"entries": 0, "kinds": {}, "pending": [], "unknown": [], "receipts": [], "problems": []}
    names = sorted(os.listdir(state))
    if len(names) > MAX_ENTRIES:
        report["problems"].append("TOO_MANY_ENTRIES")
        return report, {}
    report["entries"] = len(names)
    edges, nonces = {}, set()
    for name in names:
        path = os.path.join(state, name)
        if os.path.islink(path):
            report["problems"].append("SYMLINK:" + name)
            continue
        if any(p.fullmatch(name) for p in PENDING):
            report["pending"].append(name)
        m = re.fullmatch(r"(receipt|old|intent|inflight-swap|inflight-rollback|rollback)-([0-9a-f]{64})\.(json|bin|superseded|done)", name)
        if m is None and not any(p.fullmatch(name) for p in PENDING):
            report["unknown"].append(name)
        kind = m.group(1) + "." + m.group(3) if m else name
        report["kinds"][kind] = report["kinds"].get(kind, 0) + 1
        if m and m.group(1) == "receipt":
            try:
                digest, data, st = sha256_file(path)
                fmt, old, new = classify(name, data)
            except (OSError, ValueError) as exc:
                report["problems"].append("RECEIPT_UNREADABLE:" + name)
                continue
            nonce = m.group(2)
            report["receipts"].append({"nonce": nonce, "format": fmt, "old": old, "new": new,
                                       "receipt_sha256": digest, "mode": meta(st)["mode"]})
            if fmt == "bad" or nonce in nonces or old in edges:
                report["problems"].append("RECEIPT_INVALID_OR_AMBIGUOUS:" + nonce)
                continue
            nonces.add(nonce)
            edges[old] = new
            try:  # the backup must hash to the receipt's old launcher
                bd, _, _ = sha256_file(os.path.join(state, "old-" + nonce + ".bin"))
                if bd != old:
                    report["problems"].append("BACKUP_HASH_MISMATCH:" + nonce)
            except (OSError, ValueError):
                report["problems"].append("BACKUP_MISSING:" + nonce)
    return report, edges


def chain(edges):
    """Single complete path original->live, or None with the reason."""
    if not edges:
        return None, None, "NO_EDGES"
    news, olds = set(edges.values()), set(edges)
    originals = sorted(olds - news)
    if len(originals) != 1 or len(news) != len(edges):
        return None, None, "NOT_A_SINGLE_PATH"
    cur, seen, path = originals[0], set(), [originals[0]]
    while cur in edges:
        if cur in seen:
            return None, None, "CYCLE"
        seen.add(cur)
        cur = edges[cur]
        path.append(cur)
    if len(seen) != len(edges):
        return None, None, "ORPHAN_EDGES"
    return originals[0], cur, path


def readback(*, shim=SHIM, state=STATE, store=STORE, source_sha=None, expect_shim=None, expect_modules=None,
             expect_original=None):
    out = {"status": None, "problems": []}
    try:
        digest, _, st = sha256_file(shim)
        out["shim"] = dict(meta(st), sha256=digest)
    except (OSError, ValueError):
        out["problems"].append("SHIM_UNREADABLE")
        digest = None
    if source_sha:
        mods = {}
        for mod in MODULES:
            try:
                d, _, st = sha256_file(os.path.join(store, source_sha, "scripts/qa", mod))
                mods[mod] = dict(meta(st), sha256=d)
            except (OSError, ValueError):
                out["problems"].append("MODULE_UNREADABLE:" + mod)
        out["modules"] = mods
        for mod, want in (expect_modules or {}).items():
            if mods.get(mod, {}).get("sha256") != want:
                out["problems"].append("MODULE_PIN_MISMATCH:" + mod)
    try:
        rep, edges = read_state(state)
    except OSError:
        out["problems"].append("STATE_UNREADABLE")
        rep, edges = {"pending": [], "unknown": [], "problems": []}, {}
    out["state"] = rep
    original, live, path = chain(edges)
    out["chain"] = {"original": original, "live": live, "length": len(edges),
                    "reason": None if original else path}
    out["problems"] += rep["problems"] + (["PENDING:" + n for n in rep["pending"]]) + ["UNKNOWN_ENTRY:" + n for n in rep["unknown"]]
    if original is None:
        out["problems"].append("CHAIN_" + str(path))
    if digest and live and live != digest:
        out["problems"].append("LIVE_NEQ_SHIM")
    if expect_shim and digest != expect_shim:
        out["problems"].append("SHIM_PIN_MISMATCH")
    if expect_original and original != expect_original:
        out["problems"].append("ORIGINAL_PIN_MISMATCH")
    out["status"] = "B0_READBACK_PASS_EVIDENCE_ONLY" if not out["problems"] else "B0_READBACK_STOP"
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--source-sha", help="40-hex release retained under the store (e.g. da274d65...)")
    ap.add_argument("--expect-shim-sha256")
    ap.add_argument("--expect-original-sha256")
    ap.add_argument("--expect-module", action="append", default=[], metavar="NAME=SHA256")
    ap.add_argument("--out")
    ap.add_argument("--allow-non-root", action="store_true", help=argparse.SUPPRESS)
    a = ap.parse_args(argv)
    if os.geteuid() != 0 and not a.allow_non_root:
        print("B0_REQUIRES_ROOT", file=sys.stderr)
        return 2
    if a.source_sha and not SHA40.fullmatch(a.source_sha):
        print("B0_BAD_SOURCE_SHA", file=sys.stderr)
        return 2
    mods = {}
    for item in a.expect_module:
        name, _, want = item.partition("=")
        if name not in MODULES or not HEX64.fullmatch(want):
            print("B0_BAD_EXPECT_MODULE", file=sys.stderr)
            return 2
        mods[name] = want
    rep = readback(source_sha=a.source_sha, expect_shim=a.expect_shim_sha256, expect_modules=mods,
                   expect_original=a.expect_original_sha256)
    body = (json.dumps(rep, sort_keys=True, indent=2) + "\n").encode("ascii")
    if a.out:
        fd = os.open(a.out, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "wb") as fh:
            fh.write(body)
    else:
        sys.stdout.write(body.decode("ascii"))
    print("b0_readback_sha256=" + hashlib.sha256(body).hexdigest(), file=sys.stderr)
    return 0 if rep["status"].endswith("PASS_EVIDENCE_ONLY") else 1


if __name__ == "__main__":
    sys.exit(main())
