#!/usr/bin/env python3
"""QA525 root LAUNCHER child: atomic, reversible swap of the root launcher. INACTIVE library; grants NO authority.

Loaded by the authenticated launcher (`qa525-vnext-root-launcher.py`) as the ONE child of a LAUNCHER_ONLY GO and
called as `act(fd)` INSIDE the plane (5 legacy locks + lease held, GO verified, nonce already spent). This
module installs nothing by being imported and holds no pins of its own: the old-launcher digest and the exact
new bytes come from the caller (Sponsor-signed config + authenticated release), never from disk or the sender.

`swap` order (every step a fixed STOP, nothing mutates the launcher before the durable intent AND a verified backup):
  1. state dir real, root-owned, 0700; launcher a root-owned 0700 single-link regular file;
  2. launcher bytes read without following links; digest must equal the pinned OLD digest;
  3. intent, backup, receipt and staged names must not exist (replay / partial state => STOP);
  4. durable intent -> backup of the old bytes (re-read and compared) -> staged new bytes next to the launcher
     (same filesystem, fsync, re-read and compared);
  5. launcher digest re-checked equal to OLD immediately before `os.replace` (atomic) + directory fsync;
  6. launcher re-read: must equal NEW. If not, the backup is restored by the same atomic replace and the
     swap is reported as reverted (or as failed if even that cannot be proven);
  7. receipt written; intent archived. An interruption between 4 and 7 leaves the intent: NO automatic retry.
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


class Stop(Exception):
    """Opaque STOP code."""


def _digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


def _dir_ok(path, uid):
    try:
        info = os.lstat(path)
    except OSError:
        raise Stop("CHILD_STATE_UNSAFE") from None
    if (not stat.S_ISDIR(info.st_mode) or info.st_uid != uid or stat.S_IMODE(info.st_mode) != 0o700):
        raise Stop("CHILD_STATE_UNSAFE")


def _read(path, uid, code):
    """Single-link regular file owned by `uid`, not group/other writable, read without following links."""
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK)
    except OSError:
        raise Stop(code) from None
    try:
        info = os.fstat(fd)
        if (not stat.S_ISREG(info.st_mode) or info.st_uid != uid or info.st_nlink != 1
                or info.st_mode & 0o022
                or not 0 < info.st_size <= MAX_LAUNCHER):
            raise Stop(code)
        data = os.read(fd, MAX_LAUNCHER + 1)
        if len(data) != info.st_size or os.read(fd, 1) != b"":
            raise Stop(code)
        return data
    finally:
        os.close(fd)


def _absent(path):
    try:
        os.lstat(path)
    except FileNotFoundError:
        return True
    except OSError:
        raise Stop("CHILD_STATE_UNSAFE") from None
    return False


def _sync_dir(path):
    fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_CLOEXEC)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def _write_once(path, data, mode):
    """Create exclusively (never replaces), fsync the file and its directory."""
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC, mode)
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
    _sync_dir(os.path.dirname(path))


def _json(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":")) + "\n").encode("ascii")


def _names(launcher, state, nonce):
    return {"intent": os.path.join(state, "intent-" + nonce + ".json"),
            "backup": os.path.join(state, "old-" + nonce + ".bin"),
            "receipt": os.path.join(state, "receipt-" + nonce + ".json"),
            "archived": os.path.join(state, "intent-" + nonce + ".superseded"),
            "staged": os.path.join(os.path.dirname(launcher), ".pmgm-qa-update-" + nonce + ".tmp"),
            "restore": os.path.join(os.path.dirname(launcher), ".pmgm-qa-update-" + nonce + ".restore")}


def _check_args(launcher, state, nonce, *digests, uid):
    if (type(launcher) is not str or not os.path.isabs(launcher) or launcher != os.path.normpath(launcher)
            or type(state) is not str or not os.path.isabs(state) or state != os.path.normpath(state)
            or type(nonce) is not str or NONCE.fullmatch(nonce) is None
            or any(type(d) is not str or SHA256.fullmatch(d) is None for d in digests)
            or type(uid) is not int or isinstance(uid, bool) or uid < 0):
        raise Stop("CHILD_ARGS_INVALID")


def swap(*, launcher, state, nonce, old_sha256, new_bytes, uid=0):
    """Replace `launcher` (currently the pinned OLD bytes) by `new_bytes`. Returns the receipt record."""
    if type(new_bytes) is not bytes or not 0 < len(new_bytes) <= MAX_LAUNCHER:
        raise Stop("CHILD_ARGS_INVALID")
    new_sha = _digest(new_bytes)
    _check_args(launcher, state, nonce, old_sha256, new_sha, uid=uid)
    if new_sha == old_sha256:
        raise Stop("CHILD_NEW_EQUALS_OLD")
    _dir_ok(state, uid)
    old_bytes = _read(launcher, uid, "CHILD_LAUNCHER_UNSAFE")
    if _digest(old_bytes) != old_sha256 or stat.S_IMODE(os.lstat(launcher).st_mode) != 0o700:
        raise Stop("CHILD_OLD_LAUNCHER_DRIFT")
    n = _names(launcher, state, nonce)
    if not all(_absent(n[k]) for k in ("intent", "backup", "receipt", "archived", "staged", "restore")):
        raise Stop("CHILD_REPLAY_OR_PARTIAL_STATE")
    record = {"status": "SWAP_INTENT_NO_DEPLOYMENT", "nonce": nonce,
              "old_launcher_sha256": old_sha256, "new_launcher_sha256": new_sha}
    _write_once(n["intent"], _json(record), 0o600)  # nothing touches the launcher before this exists
    _write_once(n["backup"], old_bytes, 0o700)
    if _digest(_read(n["backup"], uid, "CHILD_BACKUP_UNVERIFIED")) != old_sha256:
        raise Stop("CHILD_BACKUP_UNVERIFIED")
    _write_once(n["staged"], new_bytes, 0o700)
    if _digest(_read(n["staged"], uid, "CHILD_STAGED_CHANGED")) != new_sha:
        raise Stop("CHILD_STAGED_CHANGED")
    if _digest(_read(launcher, uid, "CHILD_LAUNCHER_UNSAFE")) != old_sha256:
        raise Stop("CHILD_LAUNCHER_DRIFT_BEFORE_SWAP")
    os.replace(n["staged"], launcher)
    _sync_dir(os.path.dirname(launcher))
    try:
        landed = _digest(_read(launcher, uid, "CHILD_LAUNCHER_UNSAFE"))
    except Stop:
        landed = None
    if landed != new_sha:
        _restore(launcher, n, old_sha256, uid)  # the only automatic reversal: the swap itself did not land
        raise Stop("CHILD_SWAP_REVERTED")
    receipt = dict(record, status="LAUNCHER_ATOMIC_SWAP_COMMITTED")
    _write_once(n["receipt"], _json(receipt), 0o600)
    os.rename(n["intent"], n["archived"])
    _sync_dir(state)
    return receipt


def _restore(launcher, n, old_sha256, uid):
    """Atomically put the backup bytes back; STOP (never silently) unless the result is proven."""
    try:
        data = _read(n["backup"], uid, "CHILD_BACKUP_UNVERIFIED")
        if _digest(data) != old_sha256:
            raise Stop("CHILD_BACKUP_UNVERIFIED")
        _write_once(n["restore"], data, 0o700)
        os.replace(n["restore"], launcher)
        _sync_dir(os.path.dirname(launcher))
        if _digest(_read(launcher, uid, "CHILD_LAUNCHER_UNSAFE")) != old_sha256:
            raise Stop("CHILD_RESTORE_UNPROVEN")
    except Stop:
        raise
    except OSError:
        raise Stop("CHILD_RESTORE_UNPROVEN") from None


def rollback(*, launcher, state, nonce, uid=0):
    """Separately authorized reversal of a COMMITTED swap. Never called by `act`."""
    _check_args(launcher, state, nonce, uid=uid)
    _dir_ok(state, uid)
    n = _names(launcher, state, nonce)
    try:
        receipt = json.loads(_read(n["receipt"], uid, "CHILD_RECEIPT_MISSING").decode("ascii"))
    except (ValueError, UnicodeDecodeError):
        raise Stop("CHILD_RECEIPT_INVALID") from None
    if (type(receipt) is not dict or receipt.get("status") != "LAUNCHER_ATOMIC_SWAP_COMMITTED"
            or receipt.get("nonce") != nonce
            or any(type(receipt.get(k)) is not str or SHA256.fullmatch(receipt[k]) is None
                   for k in ("old_launcher_sha256", "new_launcher_sha256"))):
        raise Stop("CHILD_RECEIPT_INVALID")
    if _digest(_read(launcher, uid, "CHILD_LAUNCHER_UNSAFE")) != receipt["new_launcher_sha256"]:
        raise Stop("CHILD_NEW_LAUNCHER_DRIFT")
    if not all(_absent(n[k]) for k in ("restore",)):
        raise Stop("CHILD_REPLAY_OR_PARTIAL_STATE")
    done = os.path.join(state, "rollback-" + nonce + ".json")
    if not _absent(done):
        raise Stop("CHILD_REPLAY_OR_PARTIAL_STATE")
    _restore(launcher, n, receipt["old_launcher_sha256"], uid)
    _write_once(done, _json(dict(receipt, status="LAUNCHER_ROLLED_BACK")), 0o600)
    return {"status": "LAUNCHER_ROLLED_BACK", "nonce": nonce}


def act(fd):  # noqa: ARG001 - closed stub until the verified-release wiring is reviewed
    """The launcher requires a callable `act`; this one fails closed and touches nothing."""
    raise Stop("CHILD_NOT_WIRED")
