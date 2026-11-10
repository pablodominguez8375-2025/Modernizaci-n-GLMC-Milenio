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
launcher still holds NEW and the backup still holds OLD.
`act` here is a closed stub: wiring the pins, new bytes and paths from the verified release is a separate change.
"""
import hashlib
import json
import os
import re
import stat

NONCE = re.compile(r"[0-9a-f]{64}\Z")
SHA256 = re.compile(r"sha256:[0-9a-f]{64}\Z")
MAX_LAUNCHER = 1_048_576
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


def _read(dfd, name, uid, code):
    """Single-link regular file owned by `uid`, not group/other writable, opened inside `dfd` without links."""
    try:
        fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK, dir_fd=dfd)
    except OSError:
        raise Stop(code) from None
    try:
        info = os.fstat(fd)
        if (not stat.S_ISREG(info.st_mode) or info.st_uid != uid or info.st_nlink != 1
                or info.st_mode & 0o022 or not 0 < info.st_size <= MAX_LAUNCHER):
            raise Stop(code)
        data = os.read(fd, MAX_LAUNCHER + 1)
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


def _json(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":")) + "\n").encode("ascii")


def _names(launcher_name, nonce):
    return {"intent": "intent-" + nonce + ".json", "backup": "old-" + nonce + ".bin",
            "receipt": "receipt-" + nonce + ".json", "archived": "intent-" + nonce + ".superseded",
            "rolled": "rollback-" + nonce + ".json",
            "staged": "." + launcher_name + "-" + nonce + ".tmp",
            "restore": "." + launcher_name + "-" + nonce + ".restore"}


def _check_args(launcher, state, anchor, nonce, *digests, uid):
    if (not _abs(launcher) or not _abs(state) or not _abs(anchor) or os.path.dirname(launcher) == "/"
            or type(nonce) is not str or NONCE.fullmatch(nonce) is None
            or any(type(d) is not str or SHA256.fullmatch(d) is None for d in digests)
            or type(uid) is not int or isinstance(uid, bool) or uid < 0):
        raise Stop("CHILD_ARGS_INVALID")


def swap(*, launcher, state, nonce, old_sha256, expected_new_sha256, new_bytes, uid=0, anchor="/"):
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
            return _swap(lfd, lname, sfd, state, nonce, old_sha256, new_bytes, new_sha, uid)
        finally:
            os.close(lfd)
    finally:
        os.close(sfd)


def _swap(lfd, lname, sfd, state, nonce, old_sha256, new_bytes, new_sha, uid):
    old_bytes = _read(lfd, lname, uid, "CHILD_LAUNCHER_UNSAFE")
    if _digest(old_bytes) != old_sha256 or _mode(lfd, lname) != 0o700:
        raise Stop("CHILD_OLD_LAUNCHER_DRIFT")
    n = _names(lname, nonce)
    if not (all(_absent(sfd, n[k]) for k in ("intent", "backup", "receipt", "archived", "rolled"))
            and all(_absent(lfd, n[k]) for k in ("staged", "restore"))):
        raise Stop("CHILD_REPLAY_OR_PARTIAL_STATE")
    record = {"status": "SWAP_INTENT_NO_DEPLOYMENT", "nonce": nonce,
              "old_launcher_sha256": old_sha256, "new_launcher_sha256": new_sha}
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
            _restore(lfd, lname, sfd, n, receipt["old_launcher_sha256"], uid)
            _write_once(sfd, n["rolled"], _json(dict(receipt, status="LAUNCHER_ROLLED_BACK")), 0o600)
        finally:
            os.close(lfd)
    finally:
        os.close(sfd)
    return {"status": "LAUNCHER_ROLLED_BACK", "nonce": nonce}


def act(fd):  # noqa: ARG001 - closed stub until the verified-release wiring is reviewed
    """The launcher requires a callable `act`; this one fails closed and touches nothing."""
    raise Stop("CHILD_NOT_WIRED")
