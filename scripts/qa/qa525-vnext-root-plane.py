#!/usr/bin/env python3
"""QA525 launcher-only exclusion PLANE (decision A, #936): 5 legacy locks + a dedicated root lease.
INACTIVE library; no authority; no command line; nothing runs on import.

It reuses the lock helpers of qa525-vnext-root-locks.py (passed in already loaded from bootstrap-verified
bytes: same fixed paths, same order, same checks, same reverse release) and the very same RootPlane
liveness check. It deliberately does NOT call qa525-vnext-journal.begin and never builds an API_WEB
admission: the single at-most-once nonce marker for a launcher-only GO is #967's consume(), done by the
entry (#975) AFTER signature/scope/FD checks and BEFORE the child exists.

The lease is a flock on a private file inside the root-owned nonce journal directory. "Held" is not
"fd still open": every liveness check opens a NEW file description and requires the kernel to refuse
LOCK_EX|LOCK_NB, and re-checks that the path still names the same inode.
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


class _Lease:
    def __init__(self, fd, path, ident):
        self.fd, self.path, self.ident = fd, path, ident

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
            probe = os.open(lease.path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        except OSError:
            return False
        try:
            try:
                fcntl.flock(probe, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                return True  # another description (ours) still holds it: really held
            fcntl.flock(probe, fcntl.LOCK_UN)
            return False
        except OSError:
            return False
        finally:
            os.close(probe)


def _take_lease(directory, uid):
    try:
        dst = os.lstat(directory)
    except OSError:
        raise Stop("PLANE_LEASE_DIR_UNSAFE") from None
    if not stat.S_ISDIR(dst.st_mode) or dst.st_uid != uid or dst.st_mode & 0o077:
        raise Stop("PLANE_LEASE_DIR_UNSAFE")  # a private (0700) directory of the root owner
    path = os.path.join(directory, LEASE_NAME)
    fd = None
    try:
        fd = os.open(path, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW | os.O_CLOEXEC, 0o600)
        st = os.fstat(fd)
        if (not stat.S_ISREG(st.st_mode) or st.st_uid != uid or st.st_nlink != 1
                or st.st_mode & 0o077):
            raise Stop("PLANE_LEASE_UNTRUSTED")
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise Stop("PLANE_LEASE_BUSY") from None
        if _ident(os.lstat(path)) != _ident(st):
            raise Stop("PLANE_LEASE_CHANGED")
        return _Lease(fd, path, _ident(st))
    except BaseException as exc:
        if fd is not None:
            os.close(fd)
        if isinstance(exc, OSError):
            raise Stop("PLANE_LEASE_UNAVAILABLE") from None
        raise


@contextmanager
def launcher_flight(rl, port, lease_dir, *, uid=0):
    """Yield a `rl.RootPlane` (so `assert_held()` is the contract #976 already adapts) holding the five
    legacy locks in the fixed order and then the dedicated lease; release is lease first, locks reversed.
    Any failed release is a STOP: success is never reported over an unconfirmed release."""
    rl._check_port(port)
    held, lease, plane = [], None, None
    try:
        for path in rl.LEGACY_LOCKS:  # identical order to the legacy installer
            fd, initial = rl._lock_one(port, path)
            held.append((path, fd, initial))
        lease = _take_lease(lease_dir, uid)
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
