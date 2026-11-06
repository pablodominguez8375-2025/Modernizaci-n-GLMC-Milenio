#!/usr/bin/env python3
"""QA525 synthetic six-lock plane. INACTIVE, cannot access physical locks.

The future trusted engine must validate the signed root bootstrap, owner of
every ancestor and admission before it can use a physical implementation.
This module intentionally exposes ONLY a synthetic fixture entrypoint, never
a live host option or a physical APPLY capability.
"""
from contextlib import contextmanager
import errno
import fcntl
import os
from pathlib import Path
import stat

LEGACY_LOCKS = (
    "/opt/centenario/qa-api-refresh/operation.lock",
    "/opt/centenario/qa-auto-lite/web-update.lock",
    "/run/lock/pmgm-master-qa-604.lock",
    "/run/lock/pmgm-qa754-web-hotfix.lock",
    "/run/lock/pmgm-qa754-api-activation.lock",
)
ROOT_LOCK = "/opt/centenario/qa525-signed-prepare/.qa525-transaction.lock"
SYNTHETIC_NAMES = tuple(Path(path).name for path in LEGACY_LOCKS)
STATUS = "QA525_SYNTHETIC_SIX_LOCKS_ONLY_NOT_HOST_AUTHORITY"


class Stop(Exception):
    """Only static fail-closed codes; do not include external content."""


def _private_dir(path):
    if not isinstance(path, Path) or not path.is_absolute() or ".." in path.parts:
        raise Stop("PLANE_SYNTHETIC_DIR_INVALID")
    try:
        info = path.lstat()
    except OSError:
        raise Stop("PLANE_SYNTHETIC_DIR_INVALID") from None
    if (not stat.S_ISDIR(info.st_mode) or stat.S_ISLNK(info.st_mode)
            or info.st_uid != os.geteuid() or info.st_gid != os.getegid()
            or stat.S_IMODE(info.st_mode) != 0o700):
        raise Stop("PLANE_SYNTHETIC_DIR_UNTRUSTED")


def _file_stat(path):
    try:
        st = path.lstat()
    except OSError:
        raise Stop("PLANE_LOCK_MISSING") from None
    if (not stat.S_ISREG(st.st_mode) or stat.S_ISLNK(st.st_mode)
            or st.st_nlink != 1 or st.st_uid != os.geteuid()
            or st.st_gid != os.getegid()
            or stat.S_IMODE(st.st_mode) != 0o600):
        raise Stop("PLANE_LOCK_UNTRUSTED")
    return st


def _matches(a, b):
    return (a.st_dev, a.st_ino, a.st_uid, a.st_gid, a.st_mode, a.st_nlink) == (
        b.st_dev, b.st_ino, b.st_uid, b.st_gid, b.st_mode, b.st_nlink)


def _lock_one(path):
    """Open O_RDONLY/O_NOFOLLOW, validate identity, lock exclusive NOWAIT."""
    before = _file_stat(path)
    fd = None
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        opened = os.fstat(fd)
        if not _matches(before, opened):
            raise Stop("PLANE_LOCK_CHANGED")
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except (BlockingIOError, OSError):
            raise Stop("PLANE_LOCK_BUSY") from None
        if not _matches(opened, os.fstat(fd)) or not _matches(opened, _file_stat(path)):
            raise Stop("PLANE_LOCK_CHANGED")
        return fd, opened
    except Stop:
        if fd is not None:
            os.close(fd)
        raise
    except OSError:
        if fd is not None:
            os.close(fd)
        raise Stop("PLANE_LOCK_UNAVAILABLE") from None


def _still_locked(path, fd, initial):
    """A second independent open must be unable to obtain the same flock."""
    try:
        if not _matches(initial, os.fstat(fd)) or not _matches(initial, _file_stat(path)):
            raise Stop("PLANE_LOCK_LOST")
        probe = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        try:
            if not _matches(initial, os.fstat(probe)):
                raise Stop("PLANE_LOCK_LOST")
            try:
                fcntl.flock(probe, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except (BlockingIOError, OSError) as error:
                if isinstance(error, BlockingIOError) or getattr(error, "errno", None) in (
                        errno.EACCES, errno.EAGAIN, errno.EWOULDBLOCK):
                    return
                raise Stop("PLANE_LOCK_CHECK_FAILED") from None
            else:
                fcntl.flock(probe, fcntl.LOCK_UN)
                raise Stop("PLANE_LOCK_LOST")
        finally:
            os.close(probe)
    except Stop:
        raise
    except OSError:
        raise Stop("PLANE_LOCK_LOST") from None


class SyntheticPlane:
    """Held legacy fds followed by EXACT journal module's root Lease."""

    def __init__(self, journal, lease, entries, directory):
        self.journal, self.lease = journal, lease
        self._entries, self._directory = entries, directory
        self._active = True

    def assert_held(self):
        if not self._active:
            raise Stop("PLANE_CLOSED")
        _private_dir(self._directory)
        for path, fd, initial in self._entries:
            _still_locked(path, fd, initial)
        try:
            if self.journal.lease_is_held(self.lease) is not True:
                raise Stop("PLANE_JOURNAL_LEASE_LOST")
        except Stop:
            raise
        except Exception:
            raise Stop("PLANE_JOURNAL_LEASE_LOST") from None
        return {
            "status": STATUS, "legacy_locks_held": len(self._entries),
            "root_lease_held_synthetically": True,
            "may_apply": False, "may_install": False,
            "signature_verified_here": False,
            "physical_runtime_verified": False,
            "legacy_entrypoint_disabled_verified": False,
        }


@contextmanager
def synthetic_flight(journal, root, admission, admission_sha256, now, *,
                     synthetic_lock_dir):
    """Hold five PREEXISTING private test locks then begin journal's root lease.

    No option to acquire the literal host lock paths. The signed host engine
    must implement a separately reviewed root-owned wrapper before any
    physical use. The journal may consume a nonce in an isolated fixture.
    """
    if synthetic_lock_dir is None or not isinstance(synthetic_lock_dir, (Path, str)):
        raise Stop("PLANE_SYNTHETIC_DIR_REQUIRED")
    directory = Path(synthetic_lock_dir)
    _private_dir(directory)
    if not (callable(getattr(journal, "begin", None))
            and callable(getattr(journal, "lease_is_held", None))):
        raise Stop("PLANE_JOURNAL_INVALID")
    paths = [directory / name for name in SYNTHETIC_NAMES]
    if len(set(paths)) != 5:
        raise Stop("PLANE_LOCK_ORDER_INVALID")
    held = []
    lease = None
    plane = None
    try:
        for path in paths:  # order is fixed: identical to legacy installer
            _private_dir(directory)
            fd, initial = _lock_one(path)
            held.append((path, fd, initial))
        try:
            lease = journal.begin(root, admission, admission_sha256, now)
        except Exception:
            raise Stop("PLANE_JOURNAL_BEGIN_DENIED") from None
        plane = SyntheticPlane(journal, lease, tuple(held), directory)
        plane.assert_held()
        yield plane
    finally:
        if plane is not None:
            plane._active = False
        if lease is not None:
            lease.close()
        for _, fd, _ in reversed(held):
            try:
                os.close(fd)
            except OSError:
                pass


if __name__ == "__main__":
    raise SystemExit("QA525_LOCK_PLANE_SYNTHETIC_ONLY_NO_EXECUTION")
