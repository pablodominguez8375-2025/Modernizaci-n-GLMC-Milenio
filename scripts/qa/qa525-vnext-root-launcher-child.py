#!/usr/bin/env python3
"""QA525 root LAUNCHER child: atomic, reversible swap of the root launcher. INACTIVE library; grants NO authority.

Loaded by the authenticated launcher (`qa525-vnext-root-launcher.py`) as the ONE child of a LAUNCHER_ONLY GO and
called as `act(fd)` INSIDE the plane (5 legacy locks + lease held, GO verified, nonce already spent). This
module installs nothing by being imported and holds no pins of its own: the old-launcher digest and the exact
new bytes come from the caller (Sponsor-signed config + authenticated release), never from disk or the sender.

`swap` order (every step a fixed STOP, nothing mutates the launcher before the durable intent AND a verified backup):
  0. `expected_new_sha256` (the candidate digest authenticated by the signed GO / release pin) must equal the
     digest of the bytes handed in, BEFORE any file is created: a well-formed but different payload is refused;
  1. BOTH directories (state and the launcher's parent) are opened ONCE, after every ancestor from `anchor` was
     checked real / owned by `uid` / not group-or-other writable, and re-identified by (dev, ino); from then on
     every create, read, replace, rename and fsync goes through those directory FDs (openat semantics), so a
     path cannot be re-bound between the checks and the replace;
  1b. ONE global in-flight marker (`inflight.json`, created `O_EXCL` + fsync BEFORE the first record or mutation,
     archived only after the receipt is durable) and a scan of the state dir for any unarchived intent: a pending
     operation of ANY nonce (swap or rollback) stops every other nonce with `CHILD_RECOVERY_REQUIRED`;
  2. launcher bytes read without following links; digest must equal the pinned OLD digest;
  3. intent, backup, receipt and staged names must not exist (replay / partial state => STOP);
  4. durable intent -> backup of the old bytes (re-read and compared) -> staged new bytes next to the launcher
     (same filesystem, fsync, re-read and compared);
  5. launcher digest re-checked equal to OLD immediately before `os.replace` (atomic) + directory fsync;
  6. launcher re-read: must equal NEW. If not, the backup is restored by the same atomic replace and the
     swap is reported as reverted (or as failed if even that cannot be proven);
  7. receipt written; intent archived. An interruption (SIGKILL, power) between 4 and 7 leaves the intent: NO automatic retry or rollback;
     the next run STOPs on `CHILD_REPLAY_OR_PARTIAL_STATE` and recovery needs a separately authorized action.
`rollback` (a separate, separately authorized action; `act` never calls it) restores the backup only if the
launcher still holds NEW and the backup still holds OLD; it takes the same global marker and writes a durable
rollback intent BEFORE the first byte moves, so a crash mid-rollback also blocks every nonce until a supervised
reconciliation. Nothing here reconciles or retries automatically.
`act` here is a closed stub: wiring the pins, new bytes and paths from the verified release is a separate change.
"""
import ctypes
import errno
import hashlib
import io
import json
import os
import re
import stat
import zipfile

NONCE = re.compile(r"[0-9a-f]{64}\Z")
SHA256 = re.compile(r"sha256:[0-9a-f]{64}\Z")
MAX_LAUNCHER = 1_048_576
HEX64 = re.compile(r"[0-9a-f]{64}\Z")
SOURCE = re.compile(r"[0-9a-f]{40}\Z")
MANIFEST_LINE = re.compile(r"([0-9a-f]{64})  \./([^\n]+)\Z")
LAUNCHER = "/usr/local/sbin/pmgm-qa-update"  # fixed in code, never from config or the peer
STORE = "/opt/centenario/qa525-signed-stage"
STATE = "/opt/centenario/qa525-launcher-replacement"
OPERATOR_REL = "scripts/qa/operators/qa525-update.py"
MAX_ZIP = 512 * 1024 * 1024
MAX_MEMBERS = 5000
MAX_MEMBER = 1024 * 1024 * 1024       # same per-file bound as qa-signed-staging: the attested release carries
MAX_TOTAL = 2 * 1024 * 1024 * 1024    # storage-bundle/silo-qa-review-only.tar (~229 MiB in c952a4a0)
MAX_RATIO = 200
MAX_TEXT = 5_000_000
EVIDENCE_KEYS = frozenset({"app_archive_sha256", "app_source_sha", "candidate_operator_sha256", "shim_sha256"})
DIR_FLAGS = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC


class Stop(Exception):
    """Opaque STOP code."""


def _digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


def _abs(path):
    return type(path) is str and os.path.isabs(path) and path == os.path.normpath(path) and "\0" not in path


def _open_dir(path, uid, anchor, exact_mode=None):
    """Directory FD for `path` after the whole chain from `anchor` is trusted; identity re-checked on the FD."""
    if not _abs(path) or not _abs(anchor) or not (path == anchor or path.startswith(anchor.rstrip("/") + "/")):
        raise Stop("CHILD_STATE_UNSAFE")
    current = anchor
    parts = [] if path == anchor else path[len(anchor.rstrip("/")) + 1:].split("/")
    last = None
    for part in [None] + parts:
        if part is not None:
            current = os.path.join(current, part)
        try:
            last = os.lstat(current)
        except OSError:
            raise Stop("CHILD_STATE_UNSAFE") from None
        if not stat.S_ISDIR(last.st_mode) or last.st_uid != uid or last.st_mode & 0o022:
            raise Stop("CHILD_STATE_UNSAFE")
    try:
        fd = os.open(path, DIR_FLAGS)
    except OSError:
        raise Stop("CHILD_STATE_UNSAFE") from None
    info = os.fstat(fd)
    if ((info.st_dev, info.st_ino) != (last.st_dev, last.st_ino) or info.st_uid != uid
            or info.st_mode & 0o022 or (exact_mode is not None and stat.S_IMODE(info.st_mode) != exact_mode)):
        os.close(fd)
        raise Stop("CHILD_STATE_UNSAFE")
    return fd


def _read(dfd, name, uid, code, limit=MAX_LAUNCHER):
    """Single-link regular file owned by `uid`, not group/other writable, opened inside `dfd` without links."""
    try:
        fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK, dir_fd=dfd)
    except OSError:
        raise Stop(code) from None
    try:
        info = os.fstat(fd)
        if (not stat.S_ISREG(info.st_mode) or info.st_uid != uid or info.st_nlink != 1
                or info.st_mode & 0o022 or not 0 < info.st_size <= limit):
            raise Stop(code)
        data = os.read(fd, limit + 1)
        if len(data) != info.st_size or os.read(fd, 1) != b"":
            raise Stop(code)
        return data
    finally:
        os.close(fd)


def _mode(dfd, name):
    return stat.S_IMODE(os.stat(name, dir_fd=dfd, follow_symlinks=False).st_mode)


def _absent(dfd, name):
    try:
        os.stat(name, dir_fd=dfd, follow_symlinks=False)
    except FileNotFoundError:
        return True
    except OSError:
        raise Stop("CHILD_STATE_UNSAFE") from None
    return False


def _write_once(dfd, name, data, mode):
    """Create exclusively inside `dfd` (never replaces), fsync the file and the directory."""
    try:
        fd = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC, mode, dir_fd=dfd)
    except OSError:
        raise Stop("CHILD_STATE_UNSAFE") from None
    try:
        os.fchmod(fd, mode)
        view = memoryview(data)
        while view:
            view = view[os.write(fd, view):]
        os.fsync(fd)
    finally:
        os.close(fd)
    os.fsync(dfd)


def _claim(sfd, n, op, nonce, uid):
    """Global in-flight marker: any pending operation (any nonce, swap or rollback) blocks every other one."""
    try:
        names = os.listdir(sfd)
    except OSError:
        raise Stop("CHILD_STATE_UNSAFE") from None
    if any(x == "inflight.json" or (x.startswith("intent-") and x.endswith(".json"))
           or (x.startswith("rollback-intent-") and x.endswith(".json")) for x in names):
        raise Stop("CHILD_RECOVERY_REQUIRED")
    try:
        _write_once(sfd, n["marker"], _json({"op": op, "nonce": nonce, "status": "IN_FLIGHT"}), 0o600)
    except Stop:
        raise Stop("CHILD_RECOVERY_REQUIRED") from None


def _release(sfd, n, op):
    done = n["done_swap"] if op == "swap" else n["done_rollback"]
    if not _absent(sfd, done):
        raise Stop("CHILD_REPLAY_OR_PARTIAL_STATE")
    os.rename(n["marker"], done, src_dir_fd=sfd, dst_dir_fd=sfd)
    os.fsync(sfd)


def _json(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":")) + "\n").encode("ascii")


def _names(launcher_name, nonce):
    return {"intent": "intent-" + nonce + ".json", "backup": "old-" + nonce + ".bin",
            "receipt": "receipt-" + nonce + ".json", "archived": "intent-" + nonce + ".superseded",
            "rolled": "rollback-" + nonce + ".json", "marker": "inflight.json",
            "done_swap": "inflight-swap-" + nonce + ".done", "done_rollback": "inflight-rollback-" + nonce + ".done",
            "rb_intent": "rollback-intent-" + nonce + ".json", "rb_done": "rollback-intent-" + nonce + ".superseded",

            "staged": "." + launcher_name + "-" + nonce + ".tmp",
            "restore": "." + launcher_name + "-" + nonce + ".restore"}


def _check_args(launcher, state, anchor, nonce, *digests, uid):
    if (not _abs(launcher) or not _abs(state) or not _abs(anchor) or os.path.dirname(launcher) == "/"
            or type(nonce) is not str or NONCE.fullmatch(nonce) is None
            or any(type(d) is not str or SHA256.fullmatch(d) is None for d in digests)
            or type(uid) is not int or isinstance(uid, bool) or uid < 0):
        raise Stop("CHILD_ARGS_INVALID")


def swap(*, launcher, state, nonce, old_sha256, expected_new_sha256, new_bytes, uid=0, anchor="/", evidence=None):
    """Replace `launcher` (currently the pinned OLD bytes) by `new_bytes`. Returns the receipt record."""
    if type(new_bytes) is not bytes or not 0 < len(new_bytes) <= MAX_LAUNCHER:
        raise Stop("CHILD_ARGS_INVALID")
    _check_args(launcher, state, anchor, nonce, old_sha256, expected_new_sha256, uid=uid)
    new_sha = _digest(new_bytes)
    if new_sha != expected_new_sha256:  # the authenticated candidate digest, checked before ANY mutation
        raise Stop("CHILD_CANDIDATE_MISMATCH")
    if new_sha == old_sha256:
        raise Stop("CHILD_NEW_EQUALS_OLD")
    ldir, lname = os.path.split(launcher)
    sfd = _open_dir(state, uid, anchor, exact_mode=0o700)
    try:
        lfd = _open_dir(ldir, uid, anchor)
        try:
            return _swap(lfd, lname, sfd, nonce, old_sha256, new_bytes, new_sha, uid, evidence, claimed=False)
        finally:
            os.close(lfd)
    finally:
        os.close(sfd)


def _swap(lfd, lname, sfd, nonce, old_sha256, new_bytes, new_sha, uid, evidence, claimed):
    old_bytes = _read(lfd, lname, uid, "CHILD_LAUNCHER_UNSAFE")
    if _digest(old_bytes) != old_sha256 or _mode(lfd, lname) != 0o700:
        raise Stop("CHILD_OLD_LAUNCHER_DRIFT")
    n = _names(lname, nonce)
    if not (all(_absent(sfd, n[k]) for k in ("intent", "backup", "receipt", "archived", "rolled"))
            and all(_absent(lfd, n[k]) for k in ("staged", "restore"))):
        raise Stop("CHILD_REPLAY_OR_PARTIAL_STATE")
    if not claimed:
        _claim(sfd, n, "swap", nonce, uid)  # before the first record or mutation
    record = {"status": "SWAP_INTENT_NO_DEPLOYMENT", "nonce": nonce,
              "old_launcher_sha256": old_sha256, "new_launcher_sha256": new_sha}
    if evidence:
        if (type(evidence) is not dict or not set(evidence) <= EVIDENCE_KEYS
                or any(type(v) is not str or len(v) > 100 for v in evidence.values())):
            raise Stop("CHILD_ARGS_INVALID")
        record["evidence"] = dict(evidence)
    _write_once(sfd, n["intent"], _json(record), 0o600)  # nothing touches the launcher before this exists
    _write_once(sfd, n["backup"], old_bytes, 0o700)
    if _digest(_read(sfd, n["backup"], uid, "CHILD_BACKUP_UNVERIFIED")) != old_sha256:
        raise Stop("CHILD_BACKUP_UNVERIFIED")
    _write_once(lfd, n["staged"], new_bytes, 0o700)
    if _digest(_read(lfd, n["staged"], uid, "CHILD_STAGED_CHANGED")) != new_sha:
        raise Stop("CHILD_STAGED_CHANGED")
    if _digest(_read(lfd, lname, uid, "CHILD_LAUNCHER_UNSAFE")) != old_sha256:
        raise Stop("CHILD_LAUNCHER_DRIFT_BEFORE_SWAP")
    os.replace(n["staged"], lname, src_dir_fd=lfd, dst_dir_fd=lfd)
    os.fsync(lfd)
    try:
        landed = _digest(_read(lfd, lname, uid, "CHILD_LAUNCHER_UNSAFE"))
    except Stop:
        landed = None
    if landed != new_sha:
        _restore(lfd, lname, sfd, n, old_sha256, uid)  # the only automatic reversal: the swap did not land
        raise Stop("CHILD_SWAP_REVERTED")
    receipt = dict(record, status="LAUNCHER_ATOMIC_SWAP_COMMITTED")
    _write_once(sfd, n["receipt"], _json(receipt), 0o600)
    os.rename(n["intent"], n["archived"], src_dir_fd=sfd, dst_dir_fd=sfd)
    os.fsync(sfd)
    _release(sfd, n, "swap")
    return receipt


def _restore(lfd, lname, sfd, n, old_sha256, uid):
    """Atomically put the backup bytes back; STOP (never silently) unless the result is proven."""
    try:
        data = _read(sfd, n["backup"], uid, "CHILD_BACKUP_UNVERIFIED")
        if _digest(data) != old_sha256:
            raise Stop("CHILD_BACKUP_UNVERIFIED")
        _write_once(lfd, n["restore"], data, 0o700)
        os.replace(n["restore"], lname, src_dir_fd=lfd, dst_dir_fd=lfd)
        os.fsync(lfd)
        if _digest(_read(lfd, lname, uid, "CHILD_LAUNCHER_UNSAFE")) != old_sha256:
            raise Stop("CHILD_RESTORE_UNPROVEN")
    except Stop:
        raise
    except OSError:
        raise Stop("CHILD_RESTORE_UNPROVEN") from None


def rollback(*, launcher, state, nonce, uid=0, anchor="/"):
    """Separately authorized reversal of a COMMITTED swap. Never called by `act`."""
    _check_args(launcher, state, anchor, nonce, uid=uid)
    ldir, lname = os.path.split(launcher)
    sfd = _open_dir(state, uid, anchor, exact_mode=0o700)
    try:
        lfd = _open_dir(ldir, uid, anchor)
        try:
            n = _names(lname, nonce)
            try:
                receipt = json.loads(_read(sfd, n["receipt"], uid, "CHILD_RECEIPT_MISSING").decode("ascii"))
            except (ValueError, UnicodeDecodeError):
                raise Stop("CHILD_RECEIPT_INVALID") from None
            if (type(receipt) is not dict or receipt.get("status") != "LAUNCHER_ATOMIC_SWAP_COMMITTED"
                    or receipt.get("nonce") != nonce
                    or any(type(receipt.get(k)) is not str or SHA256.fullmatch(receipt[k]) is None
                           for k in ("old_launcher_sha256", "new_launcher_sha256"))):
                raise Stop("CHILD_RECEIPT_INVALID")
            if _digest(_read(lfd, lname, uid, "CHILD_LAUNCHER_UNSAFE")) != receipt["new_launcher_sha256"]:
                raise Stop("CHILD_NEW_LAUNCHER_DRIFT")
            if not (_absent(lfd, n["restore"]) and _absent(sfd, n["rolled"])):
                raise Stop("CHILD_REPLAY_OR_PARTIAL_STATE")
            _claim(sfd, n, "rollback", nonce, uid)  # durable intent BEFORE the first byte of the launcher moves
            _write_once(sfd, n["rb_intent"], _json(dict(receipt, status="ROLLBACK_INTENT")), 0o600)
            _restore(lfd, lname, sfd, n, receipt["old_launcher_sha256"], uid)
            _write_once(sfd, n["rolled"], _json(dict(receipt, status="LAUNCHER_ROLLED_BACK")), 0o600)
            os.rename(n["rb_intent"], n["rb_done"], src_dir_fd=sfd, dst_dir_fd=sfd)
            os.fsync(sfd)
            _release(sfd, n, "rollback")
        finally:
            os.close(lfd)
    finally:
        os.close(sfd)
    return {"status": "LAUNCHER_ROLLED_BACK", "nonce": nonce}


# ---------------------------------------------------------------------------------------------------------------
# Release retention + launcher replacement (`act_for`). The ZIP is the sealed FD whose digest the signed GO fixed
# (`app_archive_sha256`, Sigstore-attested by the authenticity port); the operator inside must equal the GO's
# `candidate_operator_sha256`; the new launcher is the deterministic shim for `app_source_sha`.
# ---------------------------------------------------------------------------------------------------------------

def render(source_sha, store=None):
    """Byte-identical to `operators/qa525-launcher-pinned-template.render` (a test pins the equality)."""
    if type(source_sha) is not str or SOURCE.fullmatch(source_sha) is None:
        raise Stop("CHILD_ARGS_INVALID")
    path = os.path.join(store or STORE, source_sha, OPERATOR_REL)
    return ('#!/usr/bin/python3\nimport os,sys\n'
            'os.execv("/usr/bin/python3", ["/usr/bin/python3", "-B", ' + repr(path) + ', *sys.argv[1:]])\n'
            ).encode("utf-8")


def _read_fd(fd):
    try:
        size = os.fstat(fd).st_size
        if not 0 < size <= MAX_ZIP:
            raise Stop("CHILD_ARCHIVE_INVALID")
        chunks, got = [], 0
        while got < size:
            piece = os.pread(fd, min(1 << 20, size - got), got)
            if not piece:
                raise Stop("CHILD_ARCHIVE_INVALID")
            chunks.append(piece)
            got += len(piece)
        if os.pread(fd, 1, size) != b"":
            raise Stop("CHILD_ARCHIVE_INVALID")
    except OSError:
        raise Stop("CHILD_ARCHIVE_INVALID") from None
    return b"".join(chunks)


def _member_parts(name):
    if (type(name) is not str or not name or "\0" in name or "\\" in name or name.startswith("/")
            or len(name) > 1024):
        raise Stop("CHILD_ARCHIVE_UNSAFE")
    parts = name.rstrip("/").split("/") if name.endswith("/") else name.split("/")
    if not parts or any(x in ("", ".", "..") for x in parts) or any(len(x) > 255 for x in parts):
        raise Stop("CHILD_ARCHIVE_UNSAFE")
    return parts


def _scan_archive(zf, source_sha):
    """Accept exactly the signed installable layout produced by qa-signed-staging:
    Proyecto-Centenario-QA-srv01-<source12>/..., then retain paths relative to that
    one authenticated root. Never silently flatten arbitrary/mixed ZIP directories."""
    if type(source_sha) is not str or SOURCE.fullmatch(source_sha) is None:
        raise Stop("CHILD_CLAIMS_INVALID")
    root = "Proyecto-Centenario-QA-srv01-" + source_sha[:12]
    infos = zf.infolist()
    if not 0 < len(infos) <= MAX_MEMBERS:
        raise Stop("CHILD_ARCHIVE_UNSAFE")
    files, dirs, total, root_seen = {}, set(), 0, False
    for info in infos:
        parts = _member_parts(info.filename)
        mode = info.external_attr >> 16
        if (info.flag_bits & 0x1 or info.compress_type not in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED)
                or stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR)):
            raise Stop("CHILD_ARCHIVE_UNSAFE")
        if parts[0] != root or info.filename.endswith("//"):
            raise Stop("CHILD_ARCHIVE_UNSAFE")
        if len(parts) == 1:
            if not info.is_dir() or root_seen:
                raise Stop("CHILD_ARCHIVE_UNSAFE")
            root_seen = True
            continue
        key = "/".join(parts[1:])
        if key in files or key in dirs:
            raise Stop("CHILD_ARCHIVE_UNSAFE")
        if info.is_dir():
            dirs.add(key)
            continue
        if (info.file_size > MAX_MEMBER or (info.compress_size and info.file_size / info.compress_size > MAX_RATIO)
                or (not info.compress_size and info.file_size)):
            raise Stop("CHILD_ARCHIVE_UNSAFE")
        total += info.file_size
        files[key] = info
    if total > MAX_TOTAL:
        raise Stop("CHILD_ARCHIVE_UNSAFE")
    parents = {"/".join(k.split("/")[:i]) for k in files for i in range(1, len(k.split("/")))}
    if parents & set(files) or dirs & set(files):
        raise Stop("CHILD_ARCHIVE_UNSAFE")
    return files

def _small(zf, info, code):
    if info.file_size > MAX_TEXT:
        raise Stop(code)
    with zf.open(info) as handle:
        data = handle.read(MAX_TEXT + 1)
    if len(data) != info.file_size:
        raise Stop(code)
    return data


def _manifest(zf, files, source_sha):
    """Canonical MANIFEST.sha256 (same grammar as qa-signed-staging) + BUILD-INFO bound to the signed source sha."""
    if "MANIFEST.sha256" not in files or "BUILD-INFO.txt" not in files:
        raise Stop("CHILD_MANIFEST_INVALID")
    info = _small(zf, files["BUILD-INFO.txt"], "CHILD_MANIFEST_INVALID").decode("utf-8", "replace").splitlines()
    if info.count("SOURCE_SHA=" + source_sha) != 1:
        raise Stop("CHILD_MANIFEST_INVALID")
    try:
        text = _small(zf, files["MANIFEST.sha256"], "CHILD_MANIFEST_INVALID").decode("utf-8")
    except UnicodeDecodeError:
        raise Stop("CHILD_MANIFEST_INVALID") from None
    expected = {}
    for line in text.splitlines():
        matched = MANIFEST_LINE.fullmatch(line)
        if matched is None:
            raise Stop("CHILD_MANIFEST_INVALID")
        digest, path = matched.groups()
        if path == "MANIFEST.sha256" or path in expected or any(x in ("", ".", "..") for x in path.split("/")):
            raise Stop("CHILD_MANIFEST_INVALID")
        expected[path] = digest
    if not expected or set(expected) != set(files) - {"MANIFEST.sha256"}:  # exact list, nothing extra, nothing missing
        raise Stop("CHILD_MANIFEST_INVALID")
    if OPERATOR_REL not in expected:
        raise Stop("CHILD_MANIFEST_INVALID")
    return expected


def _mkdirs(root_fd, parts, made):
    """Open (creating, never following links) the directory chain `parts` under `root_fd`; returns its FD."""
    cur = os.dup(root_fd)
    try:
        for i, part in enumerate(parts):
            try:
                os.mkdir(part, 0o700, dir_fd=cur)
                created = True
            except FileExistsError:
                created = False
            nxt = os.open(part, DIR_FLAGS, dir_fd=cur)
            os.close(cur)
            cur = nxt
            if created:
                os.fchmod(cur, 0o755)
                made.append("/".join(parts[:i + 1]))
        out, cur = cur, -1
        return out
    finally:
        if cur >= 0:
            os.close(cur)


def _extract(zf, files, expected, partial_fd, uid):
    made = []
    for name, info in sorted(files.items()):
        parts = name.split("/")
        dfd = _mkdirs(partial_fd, parts[:-1], made)
        try:
            fd = os.open(parts[-1], os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC, 0o644, dir_fd=dfd)
            try:
                os.fchmod(fd, 0o644)
                h, written = hashlib.sha256(), 0
                with zf.open(info) as handle:
                    while True:
                        block = handle.read(1 << 20)
                        if not block:
                            break
                        written += len(block)
                        if written > info.file_size or written > MAX_MEMBER:
                            raise Stop("CHILD_ARCHIVE_UNSAFE")  # lied about its size
                        h.update(block)
                        view = memoryview(block)
                        while view:
                            view = view[os.write(fd, view):]
                if written != info.file_size:
                    raise Stop("CHILD_ARCHIVE_UNSAFE")
                if name != "MANIFEST.sha256" and h.hexdigest() != expected[name]:
                    raise Stop("CHILD_MANIFEST_MISMATCH")
                os.fsync(fd)
            finally:
                os.close(fd)
        finally:
            os.close(dfd)
    for d in sorted(made, key=lambda x: -x.count("/")):  # durable directory entries, deepest first
        dfd = _mkdirs(partial_fd, d.split("/"), [])
        try:
            os.fsync(dfd)
        finally:
            os.close(dfd)
    os.fsync(partial_fd)


def _noclobber_rename(store_fd, src, dst):
    """RENAME_NOREPLACE: a pre-existing destination is an error, never replaced."""
    try:
        libc = ctypes.CDLL(None, use_errno=True)
        fn = libc.renameat2
    except (OSError, AttributeError):
        raise Stop("CHILD_NOCLOBBER_UNAVAILABLE") from None
    fn.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
    if fn(store_fd, src.encode(), store_fd, dst.encode(), 1) != 0:
        err = ctypes.get_errno()
        if err == errno.EEXIST:
            raise Stop("CHILD_STORE_EXISTS")
        if err in (errno.ENOSYS, errno.EINVAL):
            raise Stop("CHILD_NOCLOBBER_UNAVAILABLE")
        raise Stop("CHILD_STORE_RENAME_FAILED")
    os.fsync(store_fd)


def _verify_retained(store_fd, sha, operator_hex, manifest_sha, uid):
    """Re-open what was published, through directory FDs: owners, modes, links and the pinned digests."""
    cur = os.open(sha, DIR_FLAGS, dir_fd=store_fd)
    try:
        for part in OPERATOR_REL.split("/")[:-1]:
            nxt = os.open(part, DIR_FLAGS, dir_fd=cur)
            os.close(cur)
            cur = nxt
            info = os.fstat(cur)
            if info.st_uid != uid or info.st_mode & 0o022:
                raise Stop("CHILD_RETAINED_DRIFT")
        info = os.fstat(cur)
        if info.st_uid != uid or info.st_mode & 0o022:
            raise Stop("CHILD_RETAINED_DRIFT")
        operator = _read(cur, OPERATOR_REL.split("/")[-1], uid, "CHILD_RETAINED_DRIFT")
    finally:
        os.close(cur)
    top = os.open(sha, DIR_FLAGS, dir_fd=store_fd)
    try:
        topinfo = os.fstat(top)
        if topinfo.st_uid != uid or topinfo.st_mode & 0o022:
            raise Stop("CHILD_RETAINED_DRIFT")
        manifest = _read(top, "MANIFEST.sha256", uid, "CHILD_RETAINED_DRIFT", limit=MAX_TEXT)
    finally:
        os.close(top)
    if hashlib.sha256(operator).hexdigest() != operator_hex or hashlib.sha256(manifest).hexdigest() != manifest_sha:
        raise Stop("CHILD_RETAINED_DRIFT")


def _claims_ok(claims, pins):
    if (type(claims) is not dict or claims.get("purpose") != "LAUNCHER_ONLY"
            or type(claims.get("nonce")) is not str or HEX64.fullmatch(claims["nonce"]) is None
            or type(claims.get("app_source_sha")) is not str or SOURCE.fullmatch(claims["app_source_sha"]) is None
            or type(claims.get("app_archive_sha256")) is not str or SHA256.fullmatch(claims["app_archive_sha256"]) is None
            or type(pins) is not dict
            or set(pins) != {"release_asset_sha256", "old_launcher_sha256", "candidate_operator_sha256"}
            or any(type(pins[k]) is not str or HEX64.fullmatch(pins[k]) is None
                   for k in ("old_launcher_sha256", "candidate_operator_sha256"))):
        raise Stop("CHILD_CLAIMS_INVALID")


def install(*, fd, claims, launcher_pins, uid=0, anchor="/", launcher=None, store=None, state=None):
    """Retain the attested release, THEN replace the launcher shim. Runs inside the plane after the nonce is spent.

    `claims` come from the signed GO (`check_cut` result, authenticated by the port inside the plane) and
    `launcher_pins` from the Sponsor-signed config that `check_cut` already matched to the GO; nothing here is
    read from the peer. A crash anywhere after the marker leaves it: no silent retry, no automatic cleanup."""
    _claims_ok(claims, launcher_pins)
    launcher, store, state = launcher or LAUNCHER, store or STORE, state or STATE
    _check_args(launcher, state, anchor, claims["nonce"], uid=uid)
    if not _abs(store):
        raise Stop("CHILD_ARGS_INVALID")
    sha, nonce = claims["app_source_sha"], claims["nonce"]
    operator_hex, old_sha = launcher_pins["candidate_operator_sha256"], "sha256:" + launcher_pins["old_launcher_sha256"]
    data = _read_fd(fd)
    if _digest(data) != claims["app_archive_sha256"]:  # root re-hashes the sealed FD against the signed digest
        raise Stop("CHILD_ARCHIVE_DIGEST_MISMATCH")
    new_bytes = render(sha, store)
    new_sha = _digest(new_bytes)
    if new_sha == old_sha:
        raise Stop("CHILD_NEW_EQUALS_OLD")
    try:
        zf = zipfile.ZipFile(io.BytesIO(data))
    except (zipfile.BadZipFile, OSError, ValueError):
        raise Stop("CHILD_ARCHIVE_UNSAFE") from None
    with zf:
        files = _scan_archive(zf, sha)
        expected = _manifest(zf, files, sha)
        if expected[OPERATOR_REL] != operator_hex:  # the GO-signed operator digest, checked before anything is created
            raise Stop("CHILD_CANDIDATE_MISMATCH")
        manifest_sha = hashlib.sha256(_small(zf, files["MANIFEST.sha256"], "CHILD_MANIFEST_INVALID")).hexdigest()
        ldir, lname = os.path.split(launcher)
        xfd = _open_dir(store, uid, anchor, exact_mode=0o700)
        try:
            sfd = _open_dir(state, uid, anchor, exact_mode=0o700)
            try:
                lfd = _open_dir(ldir, uid, anchor)
                try:
                    return _install(zf, files, expected, xfd, sfd, lfd, lname, sha, nonce, operator_hex, manifest_sha,
                                    old_sha, new_bytes, new_sha, claims["app_archive_sha256"], uid)
                finally:
                    os.close(lfd)
            finally:
                os.close(sfd)
        finally:
            os.close(xfd)


def _install(zf, files, expected, xfd, sfd, lfd, lname, sha, nonce, operator_hex, manifest_sha, old_sha,
             new_bytes, new_sha, archive_sha, uid):
    partial = sha + ".partial"
    if not (_absent(xfd, sha) and _absent(xfd, partial)):
        raise Stop("CHILD_STORE_EXISTS")
    old_bytes = _read(lfd, lname, uid, "CHILD_LAUNCHER_UNSAFE")
    if _digest(old_bytes) != old_sha or _mode(lfd, lname) != 0o700:
        raise Stop("CHILD_OLD_LAUNCHER_DRIFT")
    n = _names(lname, nonce)
    if not (all(_absent(sfd, n[k]) for k in ("intent", "backup", "receipt", "archived", "rolled"))
            and all(_absent(lfd, n[k]) for k in ("staged", "restore"))):
        raise Stop("CHILD_REPLAY_OR_PARTIAL_STATE")
    _claim(sfd, n, "swap", nonce, uid)  # before the first byte reaches the store; a crash from here blocks every nonce
    os.mkdir(partial, 0o700, dir_fd=xfd)
    pfd = os.open(partial, DIR_FLAGS, dir_fd=xfd)
    try:
        os.fchmod(pfd, 0o755)
        _extract(zf, files, expected, pfd, uid)
    finally:
        os.close(pfd)
    _noclobber_rename(xfd, partial, sha)
    _verify_retained(xfd, sha, operator_hex, manifest_sha, uid)
    evidence = {"app_archive_sha256": archive_sha, "app_source_sha": sha,
                "candidate_operator_sha256": "sha256:" + operator_hex, "shim_sha256": new_sha}
    return _swap(lfd, lname, sfd, nonce, old_sha, new_bytes, new_sha, uid, evidence, claimed=True)


def act_for(*, claims, launcher_pins):
    """Called by the launcher inside the plane with the AUTHENTICATED claims; returns the `act(fd)` it will run."""
    _claims_ok(claims, launcher_pins)
    claims, launcher_pins = dict(claims), dict(launcher_pins)

    def run(fd):
        return install(fd=fd, claims=claims, launcher_pins=launcher_pins)
    return run


def act(fd):  # noqa: ARG001 - closed stub until the verified-release wiring is reviewed
    """The launcher requires a callable `act`; this one fails closed and touches nothing."""
    raise Stop("CHILD_NOT_WIRED")
