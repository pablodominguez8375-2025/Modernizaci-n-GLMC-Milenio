#!/usr/bin/env python3
"""Root-only ephemeral Docker env-file scope; library, no CLI or authority.

Caller MUST hold the authenticated RootPlane, and invoke this only within
SCHEMA migration phase. A new directory in /run (tmpfs) is created mode 0700,
files mode 0600. The source is opened nofollow, owner root, single-link, 0600.
Cleanup is attempted on every exit, including errors; failure fails closed.
"""
from contextlib import contextmanager
import os
import stat
import tempfile

SOURCE = "/etc/pmgm/srv01.env"
TMP_ROOT = "/run"
MAX_SOURCE = 65536

class Stop(Exception):
    """Fixed diagnostics only; never expose credentials."""

def _read_source(path=SOURCE):
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK)
        try:
            st = os.fstat(fd)
            if (not stat.S_ISREG(st.st_mode) or st.st_uid != 0 or st.st_nlink != 1
                    or st.st_mode & 0o177 or not 0 < st.st_size <= MAX_SOURCE):
                raise Stop("SCHEMA_ENV_SOURCE_UNSAFE")
            data = os.read(fd, MAX_SOURCE + 1)
            if len(data) != st.st_size or os.read(fd, 1):
                raise Stop("SCHEMA_ENV_SOURCE_UNSAFE")
            return data
        finally:
            os.close(fd)
    except OSError:
        raise Stop("SCHEMA_ENV_SOURCE_UNSAFE") from None

def _write(directory, name, data):
    fd = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC,
                 0o600, dir_fd=directory)
    try:
        os.fchmod(fd, 0o600)
        offset = 0
        while offset < len(data):
            written = os.write(fd, data[offset:])
            if written <= 0:
                raise Stop("SCHEMA_ENV_WRITE_FAILED")
            offset += written
        os.fsync(fd)
    finally:
        os.close(fd)

@contextmanager
def open_env(plane, renderer, history):
    if not callable(getattr(plane, "assert_held", None)) or not callable(getattr(renderer, "render", None)):
        raise Stop("SCHEMA_ENV_BINDING_INVALID")
    plane.assert_held()
    source = _read_source()
    migrator, grants = renderer.render(source, history)
    if type(migrator) is not bytes or type(grants) is not bytes:
        raise Stop("SCHEMA_ENV_BINDING_INVALID")
    directory = None
    dfd = None
    try:
        root = os.stat(TMP_ROOT, follow_symlinks=False)
        if not stat.S_ISDIR(root.st_mode) or root.st_uid != 0 or root.st_mode & 0o022:
            raise Stop("SCHEMA_ENV_TMPFS_UNSAFE")
        # /run must be a tmpfs; do not fall back to disk if host differs.
        with open("/proc/mounts", "r", encoding="ascii") as mounts:
            if not any(line.split()[:3] == ["tmpfs", "/run", "tmpfs"] for line in mounts):
                raise Stop("SCHEMA_ENV_TMPFS_UNSAFE")
        directory = tempfile.mkdtemp(prefix="qa525-schema-", dir=TMP_ROOT)
        os.chmod(directory, 0o700)
        dfd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
        plane.assert_held()
        _write(dfd, "migrator.env", migrator)
        _write(dfd, "grants.env", grants)
        plane.assert_held()
        yield directory + "/migrator.env", directory + "/grants.env"
    finally:
        cleanup_failed = False
        if dfd is not None:
            for name in ("migrator.env", "grants.env"):
                try:
                    os.unlink(name, dir_fd=dfd)
                except FileNotFoundError:
                    pass
                except OSError:
                    cleanup_failed = True
            os.close(dfd)
        if directory is not None:
            try:
                os.rmdir(directory)
            except OSError:
                cleanup_failed = True
        if cleanup_failed:
            raise Stop("SCHEMA_ENV_CLEANUP_FAILED")

if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_ENV_FILES_LIBRARY_ONLY_NO_ROOT")
