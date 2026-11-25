#!/usr/bin/env python3
"""QA525 B0 read-only readback of the launcher custody state on glmdch (stdlib only, no writes).

Reports, as EVIDENCE only (never as admission authority): shim hash, retained launcher/child/config hashes, a strict
classification of every journal entry, pending markers and the receipt graph original -> live.  Every pin is REQUIRED
on invocation.  It opens only regular files (O_NONBLOCK|O_NOFOLLOW, fstat before read), rejects symlinked ancestors,
duplicate JSON keys, wrong type/extension/mode/owner/nlink, receipts whose internal nonce differs from the file
name, and any change of the state or shim between two snapshots.  It never calls the installer's scan_history.
All paths are walked with dirfd (O_DIRECTORY|O_NOFOLLOW per component) and files are opened relative to a dirfd,
so there is no lstat-then-open-by-path window; the fd's fstat must equal the directory-snapshot identity
(dev/ino/mode/uid/nlink/size/mtime/ctime) BEFORE any byte is read.  The store must NOT contain the candidate
source (--candidate-sha) nor any '*.partial' entry.  Shim and launcher modules are re-validated (identity+hash) at
the end.  Residual limit: no lock shared with the installer, so a root-level writer could still race between the
two snapshots; that is reported as STOP (state/store changed), never as PASS.
Exit code: 0 = PASS (evidence only), 1 = STOP, 2 = bad invocation.
"""
import argparse
import errno
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


DIR_FLAGS = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC


def identity(st):
    return (st.st_dev, st.st_ino, st.st_mode, st.st_uid, st.st_nlink, st.st_size, st.st_mtime_ns, st.st_ctime_ns)


def open_dir(path, trusted_uids):
    """Walk an absolute path component by component with O_NOFOLLOW; returns an open dirfd (caller closes).
    Every component (including '/') must be owned by a trusted uid and not group/other-writable unless sticky.
    Raises OSError: ELOOP/ENOTDIR (symlink or non-directory), EPERM (untrusted ancestor)."""
    if not os.path.isabs(path):
        raise OSError(errno.EINVAL, "NOT_ABSOLUTE")

    def trusted(fd):
        st = os.fstat(fd)
        if st.st_uid not in trusted_uids or ((st.st_mode & 0o022) and not (st.st_mode & stat.S_ISVTX)):
            raise OSError(errno.EPERM, "ANCESTOR_UNTRUSTED")

    fd = os.open("/", DIR_FLAGS)
    try:
        trusted(fd)
        for part in [p for p in os.path.abspath(path).split("/") if p]:
            nxt = os.open(part, DIR_FLAGS, dir_fd=fd)
            os.close(fd)
            fd = nxt
            trusted(fd)
        return fd
    except BaseException:
        os.close(fd)
        raise


def dir_problem(label, exc):
    if exc.errno in (errno.ELOOP, errno.ENOTDIR):
        return label + "_PATH_HAS_SYMLINK"
    if exc.errno == errno.EPERM:
        return label + "_ANCESTOR_UNTRUSTED"
    return label + "_UNREADABLE"


def read_at(dfd, name, want, limit=MAX_FILE):
    """Open `name` relative to dfd without blocking/following; the fd identity must equal `want` BEFORE reading.
    Returns (sha256, bytes, stat)."""
    fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC, dir_fd=dfd)
    try:
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode):
            raise ValueError("NOT_REGULAR")
        if want is not None and identity(st) != want:
            raise ValueError("IDENTITY_CHANGED")
        if st.st_size > limit:
            raise ValueError("TOO_LARGE")
        data = os.read(fd, limit + 1)
        if len(data) != st.st_size:
            raise ValueError("SHORT_READ")
        return hashlib.sha256(data).hexdigest(), data, st
    finally:
        os.close(fd)


def lstat_at(dfd, name):
    return os.stat(name, dir_fd=dfd, follow_symlinks=False)


def meta(st):
    return {"mode": "%04o" % stat.S_IMODE(st.st_mode), "uid": st.st_uid, "nlink": st.st_nlink}


def dir_ok(dfd, uid, exact=None):
    st = os.fstat(dfd)
    if st.st_uid != uid:
        return False
    return stat.S_IMODE(st.st_mode) == exact if exact is not None else (stat.S_IMODE(st.st_mode) & 0o022) == 0


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


def snapshot(dfd):
    rows = []
    for name in sorted(os.listdir(dfd)):
        rows.append((name, identity(lstat_at(dfd, name))))
    return rows


def read_state(state, expect_uid):
    rep = {"entries": 0, "kinds": {}, "pending": [], "unknown": [], "receipts": [], "problems": []}
    try:
        dfd = open_dir(state, {0, expect_uid})
    except OSError as exc:
        rep["problems"].append(dir_problem("STATE", exc))
        return rep, {}, None
    try:
        state_ok = dir_ok(dfd, expect_uid, 0o700)
        if not state_ok:
            rep["problems"].append("STATE_DIR_METADATA_VIOLATION")
        snap1 = snapshot(dfd)
        token = (identity(os.fstat(dfd)), state_ok, snap1)  # directory identity + metadata verdict + listing
        rep["entries"] = len(snap1)
        if len(snap1) > MAX_ENTRIES:
            rep["problems"].append("TOO_MANY_ENTRIES")
            return rep, {}, token
        edges, total = {}, 0
        backups, receipts = {}, {}
        for name, ident in snap1:
            mode, uid, nlink, size = ident[2], ident[3], ident[4], ident[5]
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
                return rep, {}, token
            if kind in ("receipt", "backup"):
                try:
                    digest, data, st = read_at(dfd, name, ident)
                except (OSError, ValueError) as exc:
                    code = str(exc) if isinstance(exc, ValueError) else "UNREADABLE"
                    rep["problems"].append("%s:%s" % ("IDENTITY_CHANGED" if code == "IDENTITY_CHANGED" else "UNREADABLE", name))
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
        if snapshot(dfd) != snap1 or identity(os.fstat(dfd)) != token[0] or dir_ok(dfd, expect_uid, 0o700) != state_ok:
            rep["problems"].append("STATE_CHANGED_DURING_READ")
        return rep, edges, token
    finally:
        os.close(dfd)


def check_store(store, source_sha, candidate_sha, expect_uid):
    """Store must exist as a trusted dir, hold NO candidate source and NO '*.partial' entry; stable snapshot."""
    rep = {"entries": 0, "candidate_present": None, "partials": [], "problems": []}
    token = None
    try:
        dfd = open_dir(store, {0, expect_uid})
    except OSError as exc:
        rep["problems"].append(dir_problem("STORE", exc))
        return rep, None
    try:
        store_ok = dir_ok(dfd, expect_uid)
        if not store_ok:
            rep["problems"].append("STORE_DIR_METADATA_VIOLATION")
        snap1 = snapshot(dfd)
        token = (identity(os.fstat(dfd)), store_ok, snap1)
        names = [n for n, _ in snap1]
        rep["entries"] = len(names)
        rep["candidate_present"] = candidate_sha in names
        rep["partials"] = [n for n in names if n.endswith(".partial")]
        if rep["candidate_present"]:
            rep["problems"].append("STORE_CANDIDATE_PRESENT")
        rep["problems"] += ["STORE_PARTIAL_PRESENT:" + n for n in rep["partials"]]
        if source_sha not in names:
            rep["problems"].append("STORE_SOURCE_MISSING")
        if snapshot(dfd) != snap1 or identity(os.fstat(dfd)) != token[0] or dir_ok(dfd, expect_uid) != store_ok:
            rep["problems"].append("STORE_CHANGED_DURING_READ")
    except OSError:
        rep["problems"].append("STORE_UNREADABLE")
    finally:
        os.close(dfd)
    return rep, token


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


def read_pinned(parent, name, uid, strict_mode, problems, label):
    """Identity-checked read of one file under `parent` (path).  Returns (dict, closer) or (None, None)."""
    try:
        dfd = open_dir(parent, {0, uid})
    except OSError as exc:
        problems.append(dir_problem(label, exc))
        return None
    try:
        if not dir_ok(dfd, uid):
            problems.append(label + "_DIR_METADATA_VIOLATION")
        ident = identity(lstat_at(dfd, name))
        digest, _, st = read_at(dfd, name, ident)
        rec = dict(meta(st), sha256=digest)
        bad = st.st_uid != uid or st.st_nlink != 1 or (stat.S_IMODE(st.st_mode) & 0o022) != 0
        if strict_mode is not None and stat.S_IMODE(st.st_mode) != strict_mode:
            bad = True
        if bad:
            problems.append(label + "_METADATA_VIOLATION")
        rec["_ident"], rec["_parent"], rec["_name"], rec["_uid"] = ident, parent, name, uid
        return rec
    except (OSError, ValueError):
        problems.append(label + "_UNREADABLE")
        return None
    finally:
        os.close(dfd)


def revalidate(rec, problems, label):
    """At closing time: same identity and same hash, else STOP."""
    try:
        dfd = open_dir(rec["_parent"], {0, rec["_uid"]})
    except OSError:
        problems.append(label + "_CHANGED_DURING_READ")
        return
    try:
        if identity(lstat_at(dfd, rec["_name"])) != rec["_ident"] \
                or read_at(dfd, rec["_name"], rec["_ident"])[0] != rec["sha256"]:
            problems.append(label + "_CHANGED_DURING_READ")
    except (OSError, ValueError):
        problems.append(label + "_CHANGED_DURING_READ")
    finally:
        os.close(dfd)


def readback(*, shim=SHIM, state=STATE, store=STORE, source_sha, candidate_sha, expect_shim, expect_original,
             expect_modules, expect_uid=0):
    """All pins are mandatory; a missing pin is a programming error, not a PASS."""
    if not (SHA40.fullmatch(source_sha or "") and SHA40.fullmatch(candidate_sha or "") and candidate_sha != source_sha
            and HEX64.fullmatch(expect_shim or "") and HEX64.fullmatch(expect_original or "")
            and set(expect_modules or {}) == set(MODULES) and all(HEX64.fullmatch(v) for v in expect_modules.values())):
        raise ValueError("B0_PINS_INCOMPLETE")
    out = {"status": None, "problems": [], "pins": {"source_sha": source_sha, "candidate_sha": candidate_sha,
                                                   "shim": expect_shim, "original": expect_original,
                                                   "modules": dict(expect_modules)}}
    probs = out["problems"]
    shim_rec = read_pinned(os.path.dirname(os.path.abspath(shim)), os.path.basename(shim), expect_uid, 0o700, probs, "SHIM")
    if shim_rec:
        out["shim"] = {k: v for k, v in shim_rec.items() if not k.startswith("_")}
    mods, recs = {}, {}
    base = os.path.join(store, source_sha, "scripts/qa")
    for mod in MODULES:
        rec = read_pinned(base, mod, expect_uid, None, probs, "MODULE:" + mod)
        if rec:
            recs[mod] = rec
            mods[mod] = {k: v for k, v in rec.items() if not k.startswith("_")}
        if mods.get(mod, {}).get("sha256") != expect_modules[mod]:
            probs.append("MODULE_PIN_MISMATCH:" + mod)
    out["modules"] = mods
    out["store"], store_snap = check_store(store, source_sha, candidate_sha, expect_uid)
    probs += out["store"]["problems"]
    try:
        rep, edges, state_snap = read_state(state, expect_uid)
    except OSError:
        probs.append("STATE_UNREADABLE")
        rep, edges, state_snap = {"pending": [], "unknown": [], "problems": []}, {}, None
    out["state"] = rep
    original, live, info = chain(edges)
    out["chain"] = {"original": original, "live": live, "length": info if original else 0,
                    "reason": None if original else info}
    probs += rep["problems"] + ["PENDING:" + n for n in rep["pending"]] + ["UNKNOWN_ENTRY:" + n for n in rep["unknown"]]
    if original is None:
        probs.append("CHAIN_" + str(info))
    if shim_rec:
        if live and live != shim_rec["sha256"]:
            probs.append("LIVE_NEQ_SHIM")
        if shim_rec["sha256"] != expect_shim:
            probs.append("SHIM_PIN_MISMATCH")
        revalidate(shim_rec, probs, "SHIM")
    for mod, rec in recs.items():
        revalidate(rec, probs, "MODULE:" + mod)
    for label, path, token, exact in (("STATE", state, state_snap, 0o700), ("STORE", store, store_snap, None)):
        if token is None:
            continue  # the open itself already produced a problem
        try:  # same O_NOFOLLOW walk; directory inode/metadata/verdict AND listing must all be unchanged
            fd = open_dir(path, {0, expect_uid})
            try:
                if (identity(os.fstat(fd)), dir_ok(fd, expect_uid, exact), snapshot(fd)) != token:
                    probs.append(label + "_CHANGED_AT_CLOSE")
            finally:
                os.close(fd)
        except OSError:
            probs.append(label + "_CHANGED_AT_CLOSE")
    out["limits"] = ["NO_SHARED_LOCK_WITH_INSTALLER", "EVIDENCE_ONLY_NOT_ADMISSION",
                     "OPERATIONAL_EXCLUSION_REQUIRED: no installer/launcher/G6/auto-web process or timer may run during B0"]
    if original != expect_original:
        probs.append("ORIGINAL_PIN_MISMATCH")
    out["status"] = "B0_READBACK_PASS_EVIDENCE_ONLY" if not probs else "B0_READBACK_STOP"
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--source-sha", required=True, help="40-hex release retained under the store (da274d65...)")
    ap.add_argument("--candidate-sha", required=True, help="40-hex candidate source that must be ABSENT (e5c40d3a...)")
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
        rep = readback(shim=a.shim, state=a.state, store=a.store, source_sha=a.source_sha, candidate_sha=a.candidate_sha,
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
