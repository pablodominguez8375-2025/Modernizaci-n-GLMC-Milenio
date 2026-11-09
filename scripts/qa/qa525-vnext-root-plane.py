#!/usr/bin/env python3
"""QA525 launcher-only exclusion PLANE (decision A, #936): 5 legacy locks + a dedicated root lease.
INACTIVE library; no authority; no command line; nothing runs on import.

It reuses the lock helpers of qa525-vnext-root-locks.py (passed in already loaded from bootstrap-verified
bytes: same fixed paths, same order, same checks, same reverse release) and the very same RootPlane
liveness shape. It deliberately does NOT call the API_WEB flight journal and never builds an API_WEB
admission: the single at-most-once nonce marker for a launcher-only GO is #967's consume(), done by the
entry (#975) AFTER signature/scope/FD checks and BEFORE the child exists.

LIVENESS = KERNEL OWNERSHIP BY THE EXACT OPEN FILE DESCRIPTION. "Something still holds the lock" (a probe
refused with EWOULDBLOCK) proves nothing: our descriptor could have been unlocked and another process
(or another descriptor) could hold the file. Linux publishes, per descriptor, the locks owned by THAT open
file description in /proc/self/fdinfo/<fd> ("lock:" lines). Every liveness check requires, for the lease
AND for each of the five legacy locks, a `FLOCK ... WRITE` entry for the expected device:inode in the
fdinfo of OUR original descriptor. Unlocked-but-still-open descriptors, a lock taken over by another
holder, a replaced file or an unreadable/unsupported fdinfo are all "not held" (fail closed).

The lease file is PRE-PROVISIONED by the installer (root, 0600, inside a root-owned 0700 directory whose
ancestors are all root-owned and not group/other-writable); it is never created here, and it is opened
relative to a directory descriptor with O_NOFOLLOW, never by re-walking a path string.
"""
from contextlib import contextmanager
import fcntl
import os
import stat

LEASE_NAME = ".launcher-only.lease"


class Stop(Exception):
    """Fixed codes only."""


def _ident(st):
    return (st.st_dev, st.st_ino)


def _ofd_flocks(fd):
    """{(st_dev, st_ino)} of FLOCK/WRITE locks owned by THIS open file description (kernel-reported)."""
    try:
        with open("/proc/self/fdinfo/%d" % fd, "r", encoding="ascii") as info:
            lines = info.read(65536).splitlines()
    except (OSError, ValueError, UnicodeError):
        return set()
    out = set()
    for line in lines:
        parts = line.split()
        if len(parts) < 7 or parts[0] != "lock:" or parts[2] != "FLOCK" or parts[4] != "WRITE":
            continue  # includes "->" (blocked waiters) and POSIX/OFD locks
        try:
            major, minor, ino = parts[6].split(":")
            out.add((os.makedev(int(major, 16), int(minor, 16)), int(ino)))
        except (ValueError, OverflowError):
            continue
    return out


class _Lease:
    def __init__(self, fd, path, ident, locks, view):
        self.fd, self.path, self.ident, self.locks, self.view = fd, path, ident, locks, view

    def close(self):
        fd, self.fd = self.fd, None
        if fd is not None:
            os.close(fd)  # releases the flock


class _LeaseCheck:
    """Plays the `journal` role RootPlane expects: only lease_is_held() is ever called."""

    @staticmethod
    def lease_is_held(lease):
        if type(lease) is not _Lease or lease.fd is None:
            return False
        try:
            if _ident(os.lstat(lease.path)) != lease.ident or _ident(os.fstat(lease.fd)) != lease.ident:
                return False
            if lease.ident not in lease.view(lease.fd):
                return False  # not owned by OUR original descriptor (unlocked, or taken by someone else)
            for fd, ident in lease.locks:
                if ident not in lease.view(fd):
                    return False  # one of the five legacy locks is no longer held by our descriptor
        except Exception:  # noqa: BLE001 - uncertainty is "not held"
            return False
        return True


def _check_chain(path, uid, anchor):
    """Absolute canonical path; every component a real directory of `uid`, never group/other-writable."""
    if (type(path) is not str or not os.path.isabs(path) or os.path.normpath(path) != path
            or type(anchor) is not str or (path != anchor and not path.startswith(anchor.rstrip("/") + "/"))):
        raise Stop("PLANE_LEASE_DIR_UNSAFE")
    cur = anchor
    parts = [p for p in path[len(anchor):].split("/") if p]
    chain = [anchor] + [None] * len(parts)
    for i, part in enumerate(parts):
        cur = os.path.join(cur, part)
        chain[i + 1] = cur
    for node in chain:
        try:
            st = os.lstat(node)
        except OSError:
            raise Stop("PLANE_LEASE_DIR_UNSAFE") from None
        if (not stat.S_ISDIR(st.st_mode) or st.st_uid != uid or st.st_mode & 0o022
                or (node == path and st.st_mode & 0o077)):
            raise Stop("PLANE_LEASE_DIR_UNSAFE")  # a symlink or foreign/writable ancestor is refused


def _take_lease(directory, uid, anchor, locks, view):
    _check_chain(directory, uid, anchor)
    dfd = fd = None
    try:
        dfd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
        dst = os.fstat(dfd)
        if os.lstat(directory).st_ino != dst.st_ino or dst.st_uid != uid or dst.st_mode & 0o077:
            raise Stop("PLANE_LEASE_DIR_UNSAFE")
        try:
            fd = os.open(LEASE_NAME, os.O_RDWR | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=dfd)  # no O_CREAT
        except FileNotFoundError:
            raise Stop("PLANE_LEASE_MISSING") from None
        st = os.fstat(fd)
        if (not stat.S_ISREG(st.st_mode) or st.st_uid != uid or st.st_nlink != 1
                or st.st_mode & 0o077):
            raise Stop("PLANE_LEASE_UNTRUSTED")
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise Stop("PLANE_LEASE_BUSY") from None
        path = os.path.join(directory, LEASE_NAME)
        if _ident(os.lstat(path)) != _ident(st):
            raise Stop("PLANE_LEASE_CHANGED")
        return _Lease(fd, path, _ident(st), locks, view)
    except BaseException as exc:
        if fd is not None:
            os.close(fd)
        if isinstance(exc, OSError):
            raise Stop("PLANE_LEASE_UNAVAILABLE") from None
        raise
    finally:
        if dfd is not None:
            os.close(dfd)


@contextmanager
def launcher_flight(rl, port, lease_dir, *, uid=0, anchor="/", view=None):
    """Yield a `rl.RootPlane` (so `assert_held()` is the contract #976 already adapts) holding the five
    legacy locks in the fixed order and then the dedicated lease; release is lease first, locks reversed.
    Any failed release is a STOP: success is never reported over an unconfirmed release."""
    rl._check_port(port)
    view = view or _ofd_flocks
    held, lease, plane = [], None, None
    try:
        for path in rl.LEGACY_LOCKS:  # identical order to the legacy installer
            fd, initial = rl._lock_one(port, path)
            held.append((path, fd, initial))
        locks = tuple((fd, _ident(initial)) for _, fd, initial in held)
        lease = _take_lease(lease_dir, uid, anchor, locks, view)
        plane = rl.RootPlane(port, _LeaseCheck(), lease, tuple(held))
        plane.assert_held()
        yield plane
    finally:
        if plane is not None:
            plane._active = False
        lease_close_failed = False
        if lease is not None:
            try:
                lease.close()
            except Exception:  # noqa: BLE001
                lease_close_failed = True
        fd_close_failed = False
        for _, fd, _ in reversed(held):  # every fd is attempted, none skipped
            if not rl._close(port, fd):
                fd_close_failed = True
        if fd_close_failed:
            raise Stop("PLANE_LOCK_CLOSE_FAILED")
        if lease_close_failed:
            raise Stop("PLANE_LEASE_CLOSE_FAILED")


if __name__ == "__main__":
    raise SystemExit("QA525_ROOT_PLANE_LIBRARY_ONLY_NO_EXECUTION")
