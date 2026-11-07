#!/usr/bin/env python3
"""QA525 POSIX lock port for the root lock wrapper. INACTIVE library.

Implements the injected port of qa525-vnext-root-locks.py with real POSIX calls,
but ONLY for the five fixed legacy lock files and their ancestor directories.
Any other path is refused. File descriptors are tracked: only descriptors this
port opened can be inspected, locked or closed. Nothing runs on import, there
is no command line and no default instance; a future reviewed entry point must
build it explicitly and run as root on the single physical host.
"""
import errno
import fcntl
import os

LEGACY_LOCKS = (
    "/opt/centenario/qa-api-refresh/operation.lock",
    "/opt/centenario/qa-auto-lite/web-update.lock",
    "/run/lock/pmgm-master-qa-604.lock",
    "/run/lock/pmgm-qa754-web-hotfix.lock",
    "/run/lock/pmgm-qa754-api-activation.lock",
)


class Stop(Exception):
    """Only static fail-closed codes; never external content."""


def _ancestors(path):
    parts = path.split("/")[1:]
    out, cur = ["/"], ""
    for part in parts[:-1]:
        cur += "/" + part
        out.append(cur)
    return out


class PosixPort:
    _allowed = frozenset(LEGACY_LOCKS) | frozenset(
        d for p in LEGACY_LOCKS for d in _ancestors(p))
    _locks = frozenset(LEGACY_LOCKS)

    def __init__(self):
        self._open = {}

    def _path(self, path, *, lock=False):
        allowed = self._locks if lock else self._allowed
        if type(path) is not str or "\0" in path or path not in allowed:
            raise Stop("POSIXPORT_PATH_NOT_ALLOWED")
        return path

    def _fd(self, fd):
        if type(fd) is not int or fd not in self._open:
            raise Stop("POSIXPORT_FD_UNKNOWN")

    def euid(self):
        return os.geteuid()

    def lstat(self, path):
        try:
            return os.lstat(self._path(path))
        except OSError:
            raise Stop("POSIXPORT_LSTAT_FAILED") from None

    def open_ro(self, path):
        path = self._path(path, lock=True)
        try:
            fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        except OSError:
            raise Stop("POSIXPORT_OPEN_FAILED") from None
        self._open[fd] = path
        return fd

    def fstat(self, fd):
        self._fd(fd)
        try:
            return os.fstat(fd)
        except OSError:
            raise Stop("POSIXPORT_FSTAT_FAILED") from None

    def flock_nb(self, fd):
        self._fd(fd)
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError as error:
            if error.errno in (errno.EAGAIN, errno.EWOULDBLOCK, errno.EACCES):
                return False
            raise Stop("POSIXPORT_FLOCK_FAILED") from None
        return True

    def unlock(self, fd):
        self._fd(fd)
        try:
            fcntl.flock(fd, fcntl.LOCK_UN)
        except OSError:
            raise Stop("POSIXPORT_UNLOCK_FAILED") from None

    def close(self, fd):
        self._fd(fd)
        del self._open[fd]
        try:
            os.close(fd)
        except OSError:
            raise Stop("POSIXPORT_CLOSE_FAILED") from None


if __name__ == "__main__":
    raise SystemExit("QA525_POSIX_PORT_LIBRARY_ONLY_NO_EXECUTION")
