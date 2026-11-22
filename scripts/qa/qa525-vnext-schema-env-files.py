#!/usr/bin/env python3
"""QA525 schema cut: ephemeral root-only env-file lifecycle for the migrator and grants. LIBRARY ONLY, no CLI, no GO.

Wraps the pure `schema-env-render` output. Under the caller's held root plane it creates a NEW 0700 directory on a verified
tmpfs, writes both files with O_EXCL|O_NOFOLLOW at 0600 (no secret in argv/log/diagnostics), yields their paths and ALWAYS
removes them (zero-truncate, unlink, rmdir) even on failure. A pre-existing directory is refused and never deleted (stale
state is an operator decision). The caller injects the root-owned source reader, the renderer and the tmpfs verifier.
"""
import contextlib
import os
import stat

DIR_MODE, FILE_MODE = 0o700, 0o600
NAMES = ("migrator.env", "grants.env")


class Stop(Exception):
    """Fixed codes only; never secret content."""


def _check_dir(path, uid):
    st = os.lstat(path)
    if not stat.S_ISDIR(st.st_mode) or st.st_uid != uid or stat.S_IMODE(st.st_mode) != DIR_MODE:
        raise Stop("SCHEMA_ENVFILES_DIR_INVALID")


def _write(dir_fd, name, data, uid):
    fd = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC, FILE_MODE, dir_fd=dir_fd)
    try:
        os.fchmod(fd, FILE_MODE)
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode) or st.st_uid != uid or stat.S_IMODE(st.st_mode) != FILE_MODE or st.st_nlink != 1:
            raise Stop("SCHEMA_ENVFILES_FILE_INVALID")
        view = memoryview(data)
        while view:
            n = os.write(fd, view)
            if n <= 0:
                raise Stop("SCHEMA_ENVFILES_WRITE_FAILED")
            view = view[n:]
        os.fsync(fd)
    finally:
        os.close(fd)


def _cleanup(dir_path, dir_fd):
    failed = False
    for name in NAMES:
        try:
            fd = os.open(name, os.O_WRONLY | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=dir_fd)
        except FileNotFoundError:
            continue
        except OSError:
            failed = True
            continue
        try:
            os.ftruncate(fd, 0)
        except OSError:
            failed = True
        finally:
            os.close(fd)
        try:
            os.unlink(name, dir_fd=dir_fd)
        except OSError:
            failed = True
    try:
        os.rmdir(dir_path)
    except OSError:
        failed = True
    return not failed


@contextlib.contextmanager
def ephemeral_env_files(*, base_dir, source_reader, history, render, verify_tmpfs, uid=0):
    """Yield (migrator_env_path, grants_env_path). `base_dir` is the NEW directory (must not exist) under a tmpfs parent."""
    if (type(base_dir) is not str or not base_dir.startswith("/") or base_dir.endswith("/") or "\0" in base_dir
            or any(p in ("", ".", "..") for p in base_dir[1:].split("/")) or not callable(source_reader)
            or not callable(render) or not callable(verify_tmpfs) or type(uid) is not int or isinstance(uid, bool)):
        raise Stop("SCHEMA_ENVFILES_INPUT_INVALID")
    parent = os.path.dirname(base_dir)
    try:
        if verify_tmpfs(parent) is not True:
            raise Stop("SCHEMA_ENVFILES_NOT_TMPFS")
    except Stop:
        raise
    except Exception:
        raise Stop("SCHEMA_ENVFILES_NOT_TMPFS") from None
    try:
        source = source_reader()
        migrator, grants = render(source, history)
    except Exception:
        raise Stop("SCHEMA_ENVFILES_RENDER_FAILED") from None  # nothing created yet
    if type(migrator) is not bytes or type(grants) is not bytes:
        raise Stop("SCHEMA_ENVFILES_RENDER_FAILED")
    try:
        os.mkdir(base_dir, DIR_MODE)  # refuses an existing path (FileExistsError): stale dirs are never reused or deleted
    except FileExistsError:
        raise Stop("SCHEMA_ENVFILES_DIR_EXISTS") from None
    except OSError:
        raise Stop("SCHEMA_ENVFILES_DIR_INVALID") from None
    dir_fd = None
    try:
        os.chmod(base_dir, DIR_MODE)
        _check_dir(base_dir, uid)
        dir_fd = os.open(base_dir, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
        _write(dir_fd, NAMES[0], migrator, uid)
        _write(dir_fd, NAMES[1], grants, uid)
    except Stop:
        _finish(base_dir, dir_fd, quiet=True)
        raise
    except Exception:
        _finish(base_dir, dir_fd, quiet=True)
        raise Stop("SCHEMA_ENVFILES_WRITE_FAILED") from None
    try:
        yield base_dir + "/" + NAMES[0], base_dir + "/" + NAMES[1]
    finally:
        _finish(base_dir, dir_fd, quiet=False)


def _finish(base_dir, dir_fd, quiet):
    ok = True
    try:
        if dir_fd is not None:
            ok = _cleanup(base_dir, dir_fd)
        else:
            try:
                os.rmdir(base_dir)
            except OSError:
                ok = False
    finally:
        if dir_fd is not None:
            try:
                os.close(dir_fd)
            except OSError:
                ok = False
    if not ok and not quiet:
        raise Stop("SCHEMA_ENVFILES_CLEANUP_FAILED")


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_ENVFILES_LIBRARY_ONLY_NO_ROOT")
