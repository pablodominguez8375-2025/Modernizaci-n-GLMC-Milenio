#!/usr/bin/env python3
"""QA525 B0 read-only readback of the launcher custody state on glmdch (stdlib only, no writes).

Reports, as EVIDENCE only (never as admission authority): shim hash, retained launcher/child/config hashes, a strict
classification of every journal entry, pending markers and the receipt graph original -> live.  Every pin is REQUIRED
on invocation.  It opens only regular files (O_NONBLOCK|O_NOFOLLOW, fstat before read), rejects symlinked ancestors,
duplicate JSON keys, wrong type/extension/mode/owner/nlink, receipts whose internal nonce differs from the file
name, and any change of the state or shim between two snapshots.  It never calls the installer's scan_history.
Exit code: 0 = PASS (evidence only), 1 = STOP, 2 = bad invocation.
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
MAX_FILE, MAX_ENTRIES, MAX_TOTAL = 1 << 20, 1000, 16 << 20
N = "([0-9a-f]{64})"
# name pattern -> (kind, expected mode).  Everything else is UNKNOWN_ENTRY.
KNOWN = (
    (re.compile("receipt-" + N + r"\.json"), "receipt", 0o600),
    (re.compile("old-" + N + r"\.bin"), "backup", 0o700),
    (re.compile("intent-" + N + r"\.superseded"), "intent_archived", 0o600),
    (re.compile("inflight-swap-" + N + r"\.done"), "done_swap", 0o600),
    (re.compile("inflight-rollback-" + N + r"\.done"), "done_rollback", 0o600),
    (re.compile("rollback-intent-" + N + r"\.superseded"), "rb_intent_archived", 0o600),
    (re.compile("rollback-" + N + r"\.json"), "rolled_back", 0o600),
    (re.compile(r"inflight\.json()"), "pending", 0o600),
    (re.compile("intent-" + N + r"\.json"), "pending", 0o600),
    (re.compile("rollback-intent-" + N + r"\.json"), "pending", 0o600),
)


class Dup(ValueError):
    pass


def _pairs(items):
    out = {}
    for key, value in items:
        if key in out:
            raise Dup("DUPLICATE_JSON_KEY")
        out[key] = value
    return out


def read_regular(path, limit=MAX_FILE):
    """Open without blocking, require a regular file via fstat, read fully.  Returns (sha256, bytes, stat)."""
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC)
    try:
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode):
            raise ValueError("NOT_REGULAR")
        if st.st_size > limit:
            raise ValueError("TOO_LARGE")
        data = os.read(fd, limit + 1)
        if len(data) != st.st_size:
            raise ValueError("SHORT_READ")
        return hashlib.sha256(data).hexdigest(), data, st
    finally:
        os.close(fd)


def meta(st):
    return {"mode": "%04o" % stat.S_IMODE(st.st_mode), "uid": st.st_uid, "nlink": st.st_nlink}


def ancestors_clean(path):
    """True when no component of the absolute path (including the last) is a symlink."""
    parts = os.path.abspath(path).split(os.sep)
    cur = os.sep
    for part in parts[1:]:
        cur = os.path.join(cur, part)
        try:
            if stat.S_ISLNK(os.lstat(cur).st_mode):
                return False
        except FileNotFoundError:
            return True
    return True


PFX, BARE, SRC40 = re.compile(r"sha256:[0-9a-f]{64}"), re.compile(r"[0-9a-f]{64}"), re.compile(r"[0-9a-f]{40}")
LEGACY_KEYS = {"status", "source_sha", "release_asset_sha256", "old_launcher_sha256", "new_launcher_sha256",
               "candidate_operator_sha256", "nonce"}
MODERN_KEYS = {"status", "nonce", "old_launcher_sha256", "new_launcher_sha256", "evidence"}
EVIDENCE_KEYS = {"app_archive_sha256", "app_source_sha", "candidate_operator_sha256", "shim_sha256"}


def _is(pattern, value):
    return isinstance(value, str) and pattern.fullmatch(value) is not None


def classify(data, nonce):
    """('legacy'|'modern'|'bad', old, new).  Generation is chosen by exact key schema; every field is validated
    against ITS generation's format BEFORE any normalisation:
      legacy: old/new/operator bare 64-hex, release_asset 'sha256:'+64-hex, source_sha 40-hex
      modern: old/new 'sha256:'+64-hex and evidence with exactly four typed fields."""
    try:
        rec = json.loads(data.decode("ascii"), object_pairs_hook=_pairs)
    except (UnicodeError, ValueError, RecursionError):
        return "bad", None, None
    if not isinstance(rec, dict) or rec.get("status") != "LAUNCHER_ATOMIC_SWAP_COMMITTED" or rec.get("nonce") != nonce \
            or not _is(BARE, nonce):
        return "bad", None, None
    keys = set(rec)
    if keys == LEGACY_KEYS:
        ok = (_is(BARE, rec["old_launcher_sha256"]) and _is(BARE, rec["new_launcher_sha256"])
              and _is(BARE, rec["candidate_operator_sha256"]) and _is(PFX, rec["release_asset_sha256"])
              and _is(SRC40, rec["source_sha"]))
        old, new = rec["old_launcher_sha256"], rec["new_launcher_sha256"]
        fmt = "legacy"
    elif keys == MODERN_KEYS:
        ev = rec["evidence"]
        ok = (_is(PFX, rec["old_launcher_sha256"]) and _is(PFX, rec["new_launcher_sha256"])
              and isinstance(ev, dict) and set(ev) == EVIDENCE_KEYS
              and all(_is(PFX, ev[k]) for k in ("app_archive_sha256", "candidate_operator_sha256", "shim_sha256"))
              and _is(SRC40, ev["app_source_sha"]) and ev["shim_sha256"] == rec["new_launcher_sha256"])
        old, new = (rec["old_launcher_sha256"][7:], rec["new_launcher_sha256"][7:]) if ok else (None, None)
        fmt = "modern"
    else:
        return "bad", None, None
    if not ok or old == new:
        return "bad", None, None
    return fmt, old, new


def snapshot(state):
    """Stable fingerprint of the state directory (names + lstat identity), or raises OSError."""
    rows = []
    for entry in sorted(os.scandir(state), key=lambda e: e.name):
        st = entry.stat(follow_symlinks=False)
        rows.append((entry.name, st.st_mode, st.st_uid, st.st_nlink, st.st_ino, st.st_size, st.st_mtime_ns))
    return rows


def read_state(state, expect_uid):
    rep = {"entries": 0, "kinds": {}, "pending": [], "unknown": [], "receipts": [], "problems": []}
    if not ancestors_clean(state):
        rep["problems"].append("STATE_PATH_HAS_SYMLINK")
        return rep, {}
    snap1 = snapshot(state)
    rep["entries"] = len(snap1)
    if len(snap1) > MAX_ENTRIES:
        rep["problems"].append("TOO_MANY_ENTRIES")
        return rep, {}
    edges, nonces, total = {}, set(), 0
    backups, receipts = {}, {}
    for name, mode, uid, nlink, _, size, _ in snap1:
        kind = expected = nonce = None
        for pattern, k, m in KNOWN:
            hit = pattern.fullmatch(name)
            if hit:
                kind, expected, nonce = k, m, hit.group(1)
                break
        if kind is None:
            rep["unknown"].append(name)
            continue
        if not stat.S_ISREG(mode):
            rep["problems"].append("NOT_REGULAR:" + name)
            continue
        if uid != expect_uid or nlink != 1 or stat.S_IMODE(mode) != expected:
            rep["problems"].append("METADATA_VIOLATION:" + name)
        rep["kinds"][kind] = rep["kinds"].get(kind, 0) + 1
        if kind == "pending":
            rep["pending"].append(name)
        if kind == "rolled_back":
            rep["problems"].append("ROLLBACK_RECORD_PRESENT:" + name)
        total += size
        if total > MAX_TOTAL:  # aggregate budget enforced BEFORE reading further files
            rep["problems"].append("AGGREGATE_TOO_LARGE")
            return rep, {}
        if kind in ("receipt", "backup"):
            try:
                digest, data, st = read_regular(os.path.join(state, name))
            except (OSError, ValueError):
                rep["problems"].append("UNREADABLE:" + name)
                continue
            (receipts if kind == "receipt" else backups)[nonce] = (digest, data, st)
    for nonce, (digest, data, st) in sorted(receipts.items()):
        fmt, old, new = classify(data, nonce)
        rep["receipts"].append({"nonce": nonce, "format": fmt, "old": old, "new": new, "receipt_sha256": digest})
        if fmt == "bad" or old in edges:
            rep["problems"].append("RECEIPT_INVALID_OR_AMBIGUOUS:" + nonce)
            continue
        edges[old] = new
        if nonce not in backups:
            rep["problems"].append("BACKUP_MISSING:" + nonce)
        elif backups[nonce][0] != old:
            rep["problems"].append("BACKUP_HASH_MISMATCH:" + nonce)
    for nonce in backups:
        if nonce not in receipts:
            rep["problems"].append("BACKUP_WITHOUT_RECEIPT:" + nonce)
    if snapshot(state) != snap1:
        rep["problems"].append("STATE_CHANGED_DURING_READ")
    return rep, edges


def chain(edges):
    if not edges:
        return None, None, "NO_EDGES"
    news, olds = set(edges.values()), set(edges)
    originals = sorted(olds - news)
    if len(originals) != 1 or len(news) != len(edges):
        return None, None, "NOT_A_SINGLE_PATH"
    cur, seen = originals[0], set()
    while cur in edges:
        if cur in seen:
            return None, None, "CYCLE"
        seen.add(cur)
        cur = edges[cur]
    if len(seen) != len(edges):
        return None, None, "ORPHAN_EDGES"
    return originals[0], cur, len(edges)


def readback(*, shim=SHIM, state=STATE, store=STORE, source_sha, expect_shim, expect_original, expect_modules,
             expect_uid=0):
    """All pins are mandatory; a missing pin is a programming error, not a PASS."""
    if not (SHA40.fullmatch(source_sha or "") and HEX64.fullmatch(expect_shim or "")
            and HEX64.fullmatch(expect_original or "") and set(expect_modules or {}) == set(MODULES)
            and all(HEX64.fullmatch(v) for v in expect_modules.values())):
        raise ValueError("B0_PINS_INCOMPLETE")
    out = {"status": None, "problems": [], "pins": {"source_sha": source_sha, "shim": expect_shim,
                                                   "original": expect_original, "modules": dict(expect_modules)}}
    shim_digest = shim_st = None
    if not ancestors_clean(shim):
        out["problems"].append("SHIM_PATH_HAS_SYMLINK")
    else:
        try:
            shim_digest, _, shim_st = read_regular(shim)
            out["shim"] = dict(meta(shim_st), sha256=shim_digest)
            if shim_st.st_uid != expect_uid or shim_st.st_nlink != 1 or stat.S_IMODE(shim_st.st_mode) != 0o700:
                out["problems"].append("SHIM_METADATA_VIOLATION")
        except (OSError, ValueError):
            out["problems"].append("SHIM_UNREADABLE")
    mods = {}
    base = os.path.join(store, source_sha, "scripts/qa")
    if not ancestors_clean(base):
        out["problems"].append("STORE_PATH_HAS_SYMLINK")
    else:
        for mod in MODULES:
            try:
                d, _, st = read_regular(os.path.join(base, mod))
                mods[mod] = dict(meta(st), sha256=d)
                if st.st_uid != expect_uid or st.st_nlink != 1:
                    out["problems"].append("MODULE_METADATA_VIOLATION:" + mod)
            except (OSError, ValueError):
                out["problems"].append("MODULE_UNREADABLE:" + mod)
    out["modules"] = mods
    for mod, want in expect_modules.items():
        if mods.get(mod, {}).get("sha256") != want:
            out["problems"].append("MODULE_PIN_MISMATCH:" + mod)
    try:
        rep, edges = read_state(state, expect_uid)
    except OSError:
        out["problems"].append("STATE_UNREADABLE")
        rep, edges = {"pending": [], "unknown": [], "problems": []}, {}
    out["state"] = rep
    original, live, info = chain(edges)
    out["chain"] = {"original": original, "live": live, "length": info if original else 0,
                    "reason": None if original else info}
    out["problems"] += rep["problems"] + ["PENDING:" + n for n in rep["pending"]] \
        + ["UNKNOWN_ENTRY:" + n for n in rep["unknown"]]
    if original is None:
        out["problems"].append("CHAIN_" + str(info))
    if shim_digest:
        if live and live != shim_digest:
            out["problems"].append("LIVE_NEQ_SHIM")
        if shim_digest != expect_shim:
            out["problems"].append("SHIM_PIN_MISMATCH")
        try:  # the shim must not change while the report is produced
            if read_regular(shim)[0] != shim_digest:
                out["problems"].append("SHIM_CHANGED_DURING_READ")
        except (OSError, ValueError):
            out["problems"].append("SHIM_CHANGED_DURING_READ")
    if original != expect_original:
        out["problems"].append("ORIGINAL_PIN_MISMATCH")
    out["status"] = "B0_READBACK_PASS_EVIDENCE_ONLY" if not out["problems"] else "B0_READBACK_STOP"
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--source-sha", required=True, help="40-hex release retained under the store (da274d65...)")
    ap.add_argument("--expect-shim-sha256", required=True)
    ap.add_argument("--expect-original-sha256", required=True)
    ap.add_argument("--expect-module", action="append", required=True, metavar="NAME=SHA256",
                    help="required for each of the three launcher modules")
    ap.add_argument("--out")
    ap.add_argument("--allow-non-root", action="store_true", help=argparse.SUPPRESS)
    ap.add_argument("--state", default=STATE, help=argparse.SUPPRESS)
    ap.add_argument("--shim", default=SHIM, help=argparse.SUPPRESS)
    ap.add_argument("--store", default=STORE, help=argparse.SUPPRESS)
    a = ap.parse_args(argv)
    if os.geteuid() != 0 and not a.allow_non_root:
        print("B0_REQUIRES_ROOT", file=sys.stderr)
        return 2
    mods = {}
    for item in a.expect_module:
        name, _, want = item.partition("=")
        if name not in MODULES or not HEX64.fullmatch(want) or name in mods:
            print("B0_BAD_EXPECT_MODULE", file=sys.stderr)
            return 2
        mods[name] = want
    try:
        rep = readback(shim=a.shim, state=a.state, store=a.store, source_sha=a.source_sha,
                       expect_shim=a.expect_shim_sha256, expect_original=a.expect_original_sha256,
                       expect_modules=mods, expect_uid=os.geteuid())
    except ValueError:
        print("B0_PINS_INCOMPLETE", file=sys.stderr)
        return 2
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
