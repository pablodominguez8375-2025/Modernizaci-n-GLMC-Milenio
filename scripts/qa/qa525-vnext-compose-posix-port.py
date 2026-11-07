#!/usr/bin/env python3
"""QA525 B7 POSIX read-only port for pinned Compose files. INACTIVE.

A future authenticated root entrypoint must supply the exact allowed file
paths. This class is deliberately not a command, a discovery tool or an APPLY
capability. The only effects are read-only OS operations when an instance is
explicitly created and invoked by a trusted caller.
"""
import os

ROOT = "/opt/centenario/"
MAX_PATH = 512
MAX_READ = 65536
FLAGS = os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK


class Stop(Exception):
    """Fixed diagnostics only, never a path or system error string."""


def _validate_path(value):
    if (type(value) is not str or not 1 < len(value) <= MAX_PATH
            or not value.startswith(ROOT) or value.endswith("/")
            or "\0" in value or
            any(part in ("", ".", "..") for part in value.split("/")[1:])):
        raise Stop("COMPOSEPOSIX_FILE_INVALID")
    return value


def _ancestors(path):
    result, prefix = {"/"}, ""
    for component in path.split("/")[1:-1]:
        prefix += "/" + component
        result.add(prefix)
    return result


class PosixReadPort:
    """Read-only descriptor operations scoped to a fixed set of Compose paths."""

    def __init__(self, exact_files):
        if (type(exact_files) not in (list, tuple) or not exact_files
                or len(exact_files) > 128):
            raise Stop("COMPOSEPOSIX_FILES_INVALID")
        values = [_validate_path(p) for p in exact_files]
        if len(values) != len(set(values)):
            raise Stop("COMPOSEPOSIX_FILES_INVALID")
        self._files = frozenset(values)
        self._stats = frozenset().union(*(_ancestors(x) for x in values)) | self._files
        self._fds = set()

    def euid(self):
        return os.geteuid()

    def lstat(self, path):
        if type(path) is not str or path not in self._stats:
            raise Stop("COMPOSEPOSIX_STAT_REFUSED")
        try:
            return os.lstat(path)
        except OSError:
            raise Stop("COMPOSEPOSIX_STAT_FAILED") from None

    def open_ro(self, path):
        if type(path) is not str or path not in self._files:
            raise Stop("COMPOSEPOSIX_OPEN_REFUSED")
        try:
            fd = os.open(path, FLAGS)
        except OSError:
            raise Stop("COMPOSEPOSIX_OPEN_FAILED") from None
        if type(fd) is not int or fd < 0:
            raise Stop("COMPOSEPOSIX_FD_INVALID")
        self._fds.add(fd)
        return fd

    def _fd(self, fd):
        if type(fd) is not int or fd not in self._fds:
            raise Stop("COMPOSEPOSIX_FD_REFUSED")
        return fd

    def fstat(self, fd):
        self._fd(fd)
        try:
            return os.fstat(fd)
        except OSError:
            raise Stop("COMPOSEPOSIX_FSTAT_FAILED") from None

    def read(self, fd, amount):
        self._fd(fd)
        if type(amount) is not int or not 1 <= amount <= MAX_READ:
            raise Stop("COMPOSEPOSIX_READ_LIMIT")
        try:
            return os.read(fd, amount)
        except OSError:
            raise Stop("COMPOSEPOSIX_READ_FAILED") from None

    def close(self, fd):
        self._fd(fd)
        self._fds.remove(fd)
        try:
            os.close(fd)
        except OSError:
            raise Stop("COMPOSEPOSIX_CLOSE_FAILED") from None


if __name__ == "__main__":
    raise SystemExit("QA525_COMPOSE_POSIX_LIBRARY_ONLY_NO_HOST_OR_APPLY")
