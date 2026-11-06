#!/usr/bin/env python3
"""QA525 root lock-wrapper CONTRACT. INACTIVE library, no host access.

It fixes the exact five legacy lock paths and their order, the ownership rules
for each lock and ancestor, and the release order (reverse; root lease first).
Every filesystem effect arrives through an injected port, so importing or
calling this module can never touch a real lock. There is no default port and
no command line. A physical port needs its own review and a Sponsor GO.
"""
from contextlib import contextmanager
import stat

LEGACY_LOCKS = (
    "/opt/centenario/qa-api-refresh/operation.lock",
    "/opt/centenario/qa-auto-lite/web-update.lock",
    "/run/lock/pmgm-master-qa-604.lock",
    "/run/lock/pmgm-qa754-web-hotfix.lock",
    "/run/lock/pmgm-qa754-api-activation.lock",
)
RUN_LOCK_DIR = "/run/lock"
STATUS = "QA525_ROOT_LOCK_WRAPPER_CONTRACT_NOT_HOST_AUTHORITY"
PORT_METHODS = ("euid", "lstat", "open_ro", "fstat", "flock_nb", "unlock", "close")


class Stop(Exception):
    """Only static fail-closed codes; never external content."""


def _ident(st):
    return (st.st_dev, st.st_ino, st.st_uid, st.st_gid, st.st_mode, st.st_nlink)


def _chain(path):
    parts = path.split("/")[1:]
    out, cur = ["/"], ""
    for part in parts[:-1]:
        cur += "/" + part
        out.append(cur)
    return out


def _check_dir(port, path):
    try:
        st = port.lstat(path)
    except Exception:
        raise Stop("ROOTLOCK_DIR_MISSING") from None
    mode = stat.S_IMODE(st.st_mode)
    sticky_ok = path == RUN_LOCK_DIR and mode in (0o700, 0o755, 0o1777)
    if (not stat.S_ISDIR(st.st_mode) or stat.S_ISLNK(st.st_mode)
            or st.st_uid != 0 or (mode & 0o022 and not sticky_ok)):
        raise Stop("ROOTLOCK_DIR_UNTRUSTED")


def _check_file(port, path):
    try:
        st = port.lstat(path)
    except Exception:
        raise Stop("ROOTLOCK_FILE_MISSING") from None
    if (not stat.S_ISREG(st.st_mode) or stat.S_ISLNK(st.st_mode)
            or st.st_uid != 0 or st.st_nlink != 1
            or stat.S_IMODE(st.st_mode) != 0o600):
        raise Stop("ROOTLOCK_FILE_UNTRUSTED")
    return st


def _check_port(port):
    if any(not callable(getattr(port, name, None)) for name in PORT_METHODS):
        raise Stop("ROOTLOCK_PORT_INVALID")
    if port.euid() != 0:
        raise Stop("ROOTLOCK_NOT_ROOT")


def _lock_one(port, path):
    for directory in _chain(path):
        _check_dir(port, directory)
    before = _check_file(port, path)
    fd = None
    try:
        fd = port.open_ro(path)
        if _ident(port.fstat(fd)) != _ident(before):
            raise Stop("ROOTLOCK_FILE_CHANGED")
        if port.flock_nb(fd) is not True:
            raise Stop("ROOTLOCK_BUSY")
        if _ident(port.fstat(fd)) != _ident(before) or _ident(_check_file(port, path)) != _ident(before):
            raise Stop("ROOTLOCK_FILE_CHANGED")
        return fd, before
    except Stop:
        if fd is not None:
            port.close(fd)
        raise
    except Exception:
        if fd is not None:
            try:
                port.close(fd)
            except Exception:
                pass
        raise Stop("ROOTLOCK_UNAVAILABLE") from None


class RootPlane:
    """Five held legacy locks followed by the journal's root lease."""

    def __init__(self, port, journal, lease, entries):
        self.port, self.journal, self.lease = port, journal, lease
        self._entries, self._active = entries, True

    def assert_held(self):
        if not self._active:
            raise Stop("ROOTLOCK_PLANE_CLOSED")
        for path, fd, initial in self._entries:
            try:
                same = (_ident(self.port.fstat(fd)) == _ident(initial)
                        and _ident(_check_file(self.port, path)) == _ident(initial))
            except Stop:
                raise Stop("ROOTLOCK_LOST") from None
            except Exception:
                raise Stop("ROOTLOCK_LOST") from None
            if not same:
                raise Stop("ROOTLOCK_LOST")
        try:
            if self.journal.lease_is_held(self.lease) is not True:
                raise Stop("ROOTLOCK_LEASE_LOST")
        except Stop:
            raise
        except Exception:
            raise Stop("ROOTLOCK_LEASE_LOST") from None
        return {
            "status": STATUS, "legacy_locks_held": len(self._entries),
            "root_lease_held": True, "may_apply": False, "may_install": False,
            "signature_verified_here": False, "physical_runtime_verified": False,
            "legacy_entrypoint_disabled_verified": False,
        }


@contextmanager
def root_flight(port, journal, root, admission, admission_sha256, now):
    _check_port(port)
    if not (callable(getattr(journal, "begin", None))
            and callable(getattr(journal, "lease_is_held", None))):
        raise Stop("ROOTLOCK_JOURNAL_INVALID")
    held, lease, plane = [], None, None
    try:
        for path in LEGACY_LOCKS:  # order identical to the legacy installer
            fd, initial = _lock_one(port, path)
            held.append((path, fd, initial))
        try:
            lease = journal.begin(root, admission, admission_sha256, now)
        except Exception:
            raise Stop("ROOTLOCK_JOURNAL_BEGIN_DENIED") from None
        plane = RootPlane(port, journal, lease, tuple(held))
        plane.assert_held()
        yield plane
    finally:
        if plane is not None:
            plane._active = False
        close_failed = False
        if lease is not None:
            try:
                lease.close()
            except Exception:
                close_failed = True
        for _, fd, _ in reversed(held):
            try:
                port.close(fd)
            except Exception:
                pass
        if close_failed:
            raise Stop("ROOTLOCK_JOURNAL_CLOSE_FAILED")


if __name__ == "__main__":
    raise SystemExit("QA525_ROOT_LOCKS_CONTRACT_ONLY_NO_EXECUTION")
