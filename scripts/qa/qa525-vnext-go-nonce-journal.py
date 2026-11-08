#!/usr/bin/env python3
"""QA525 durable single-use GO-nonce journal. Library only; grants NO authority.

consume() atomically marks a URL-safe signed-GO nonce as spent, bound to the
APP/OPS/policy/plan/admission digests the caller supplies. The mark is the
EXISTENCE of a file created O_CREAT|O_EXCL|O_NOFOLLOW under a validated 0700
journal directory (by dir_fd, no path re-walk), fsynced with its directory
BEFORE success is returned. Fail-closed: a crash at any point after creation
leaves the nonce spent (never re-usable). A non-blocking flock lease serialises
concurrent consumers; losers get an opaque STOP. Verifying the GO signature,
APP provenance or Sponsor consent is NOT done here (bootstrap_authenticated=False).
No sudo, docker, network, or path outside the journal root is touched.
"""
import fcntl
import hashlib
import json
import os
import re
import stat

NONCE = re.compile(r"[A-Za-z0-9_-]{24,96}\Z")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")
FIELDS = ("app", "ops", "policy", "plan", "admission")
OPEN_DIR = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK
LEASE = ".lease"
MAX_RECORD = 4096


class Stop(Exception):
    """Opaque STOP code; never includes the nonce or wire content."""


def _journal(root, uid):
    if type(root) is not str or not os.path.isabs(root) or os.path.normpath(root) != root:
        raise Stop("NONCE_ROOT_INVALID")
    if type(uid) is not int or isinstance(uid, bool) or uid < 0:
        raise Stop("NONCE_UID_INVALID")
    # every ancestor must be a real directory owned by root/uid, not group/other writable
    cur = "/"
    for part in [""] + [p for p in root.split("/") if p]:
        cur = os.path.join(cur, part) if part else "/"
        try:
            info = os.lstat(cur)
        except OSError:
            raise Stop("NONCE_ROOT_INVALID") from None
        if (not stat.S_ISDIR(info.st_mode) or info.st_uid not in (0, uid)
                or (info.st_mode & 0o022 and not (info.st_uid == 0 and info.st_mode & stat.S_ISVTX))):
            raise Stop("NONCE_ROOT_UNTRUSTED")
    try:
        fd = os.open(root, OPEN_DIR)
    except OSError:
        raise Stop("NONCE_ROOT_INVALID") from None
    info = os.fstat(fd)
    if (not stat.S_ISDIR(info.st_mode) or info.st_uid != uid or info.st_mode & 0o077
            or os.readlink("/proc/self/fd/%d" % fd) != root):
        os.close(fd)
        raise Stop("NONCE_ROOT_UNTRUSTED")
    return fd


def _bind(binding):
    if (type(binding) is not dict or set(binding) != set(FIELDS)
            or any(type(binding[k]) is not str or DIGEST.fullmatch(binding[k]) is None
                   for k in FIELDS)):
        raise Stop("NONCE_BINDING_INVALID")
    return {k: binding[k] for k in FIELDS}


def consume(root, nonce, binding, *, uid, now, expires_at):
    """Spend `nonce` once. Returns {'consumed': True, ...} or raises Stop (replay etc.)."""
    if type(nonce) is not str or NONCE.fullmatch(nonce) is None:
        raise Stop("NONCE_INVALID")
    bound = _bind(binding)
    if (type(now) is not int or type(expires_at) is not int or isinstance(now, bool)
            or isinstance(expires_at, bool)):
        raise Stop("NONCE_CLOCK_INVALID")
    if not now < expires_at:
        raise Stop("NONCE_EXPIRED")
    dirfd = _journal(root, uid)
    lease = None
    try:
        try:
            lease = os.open(LEASE, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW | os.O_CLOEXEC,
                            0o600, dir_fd=dirfd)
            info = os.fstat(lease)
            if not stat.S_ISREG(info.st_mode) or info.st_uid != uid or info.st_nlink != 1:
                raise Stop("NONCE_LEASE_UNSAFE")
            fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise Stop("NONCE_LEASE_BUSY") from None
        name = hashlib.sha256(nonce.encode("ascii")).hexdigest() + ".spent"
        try:
            fd = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC,
                         0o600, dir_fd=dirfd)
        except FileExistsError:
            raise Stop("NONCE_REPLAY") from None
        # From here the nonce is SPENT even if we crash: existence is the record.
        try:
            record = json.dumps({"binding": bound, "spent_at": now, "expires_at": expires_at},
                                sort_keys=True, separators=(",", ":")).encode()
            if len(record) > MAX_RECORD:
                raise Stop("NONCE_BINDING_INVALID")
            os.write(fd, record)
            os.fsync(fd)
        finally:
            os.close(fd)
        os.fsync(dirfd)
        return {"consumed": True, "record_sha256": "sha256:" + hashlib.sha256(record).hexdigest(),
                "bootstrap_authenticated_here": False, "sponsor_authorized_here": False,
                "may_install": False, "may_apply": False}
    except Stop:
        raise
    except OSError:
        raise Stop("NONCE_JOURNAL_IO") from None
    finally:
        if lease is not None:
            os.close(lease)
        os.close(dirfd)


if __name__ == "__main__":
    raise SystemExit("QA525_NONCE_JOURNAL_LIBRARY_ONLY_NO_EXECUTION")
