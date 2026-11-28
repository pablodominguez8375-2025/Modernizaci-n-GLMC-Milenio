#!/usr/bin/env python3
"""QA525 G6 GO-INSTALL (route C): place ONE signed GO and its signature in the root-only directory the driver reads, with
EXCLUSIVE creation.  CANDIDATE, NOT AUTHORISED.  Runs as root (sudo) on glmdch; spawns nothing, opens no sockets, never imports or
executes the driver.  Usage:  sudo python3 -I qa525-go-install.py install GO_SRC GO_SHA256 SIG_SRC SIG_SHA256
Sources: absolute paths, regular files directly in a real directory (no symlink), nlink 1, size-capped, opened O_NOFOLLOW from the
parent FD; bytes are read ONCE, hashed, and the very same bytes are written.  Destination: /var/lib/pmgm-qa525-manual-cut-e5c40d3a
(root, mode exactly 0700), names = source basenames (the signature must be `<go name>.sig`), created O_CREAT|O_EXCL|O_NOFOLLOW
relative to the directory FD with mode 0600 (no check-then-act: an existing name is STOP, never overwritten, never deleted).
Output: G6GI_GO=..., G6GI_SIG=..., G6GI_INSTALLED=2.  Any problem: one line G6GI_STOP:<CODE>, nothing overwritten or deleted.
"""
import hashlib
import os
import re
import stat
import sys

DEST_DIR = "/var/lib/pmgm-qa525-manual-cut-e5c40d3a"
NAME_RE = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]{0,79}\Z")
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
GO_MAX, SIG_MAX = 2048, 4096
DIR_FLAGS = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC


class Stop(Exception):
    pass


def _hook(stage):  # test seam for fault injection; no-op in production
    return None


def _read_source(path, want_sha, limit):
    if type(path) is not str or not os.path.isabs(path) or os.path.normpath(path) != path:
        raise Stop("SOURCE_PATH_INVALID")
    parent, name = os.path.split(path)
    if NAME_RE.fullmatch(name) is None or os.path.realpath(parent) != parent:
        raise Stop("SOURCE_PATH_INVALID")
    try:
        pfd = os.open(parent, DIR_FLAGS)
    except OSError:
        raise Stop("SOURCE_PARENT_REFUSED") from None
    try:
        pst = os.fstat(pfd)
        if pst.st_mode & 0o022:
            raise Stop("SOURCE_PARENT_UNSAFE")
        try:
            fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK, dir_fd=pfd)
        except OSError:
            raise Stop("SOURCE_OPEN_REFUSED") from None
        try:
            st = os.fstat(fd)
            if (not stat.S_ISREG(st.st_mode) or st.st_nlink != 1 or st.st_uid != pst.st_uid or st.st_size <= 0
                    or st.st_size > limit):
                raise Stop("SOURCE_NOT_ACCEPTABLE")
            data = b""
            while len(data) < st.st_size:
                chunk = os.read(fd, st.st_size - len(data))
                if not chunk:
                    raise Stop("SOURCE_TRUNCATED")
                data += chunk
            if os.read(fd, 1):
                raise Stop("SOURCE_CHANGED")
        finally:
            os.close(fd)
    finally:
        os.close(pfd)
    if hashlib.sha256(data).hexdigest() != want_sha:
        raise Stop("SOURCE_HASH_MISMATCH")
    return name, data


def _create_exclusive(dfd, name, data, uid):
    try:
        fd = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC, 0o600, dir_fd=dfd)
    except FileExistsError:
        raise Stop("DESTINATION_EXISTS") from None
    except OSError:
        raise Stop("DESTINATION_CREATE_FAILED") from None
    try:
        view = memoryview(data)
        while view:
            view = view[os.write(fd, view):]
        os.fchmod(fd, 0o600)
        os.fsync(fd)
    finally:
        os.close(fd)
    st = os.stat(name, dir_fd=dfd, follow_symlinks=False)
    got = _read_back(dfd, name)
    if (not stat.S_ISREG(st.st_mode) or st.st_uid != uid or st.st_nlink != 1 or stat.S_IMODE(st.st_mode) != 0o600
            or got != data):
        raise Stop("DESTINATION_VERIFY_FAILED")
    return st


def _read_back(dfd, name):
    fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=dfd)
    try:
        return os.read(fd, 1 << 16)
    finally:
        os.close(fd)


def install(go_src, go_sha, sig_src, sig_sha, dest=DEST_DIR, uid=None, out=sys.stdout):
    uid = os.geteuid() if uid is None else uid
    if HEX64.fullmatch(go_sha or "") is None or HEX64.fullmatch(sig_sha or "") is None:
        raise Stop("HASH_ARGUMENT_INVALID")
    if uid != 0 and dest == DEST_DIR:
        raise Stop("ROOT_REQUIRED")
    go_name, go = _read_source(go_src, go_sha, GO_MAX)
    sig_name, sig = _read_source(sig_src, sig_sha, SIG_MAX)
    if sig_name != go_name + ".sig":
        raise Stop("SIGNATURE_NAME_MISMATCH")
    if os.path.realpath(dest) != dest:
        raise Stop("DEST_NOT_CANONICAL")
    try:
        dfd = os.open(dest, DIR_FLAGS)
    except OSError:
        raise Stop("DEST_OPEN_REFUSED") from None
    try:
        dst = os.fstat(dfd)
        if not stat.S_ISDIR(dst.st_mode) or dst.st_uid != uid or stat.S_IMODE(dst.st_mode) != 0o700:
            raise Stop("DEST_NOT_ROOT_0700")
        for name in (go_name, sig_name):
            if _lexists(dfd, name):
                raise Stop("DESTINATION_EXISTS")
        _hook("before")
        gst = _create_exclusive(dfd, go_name, go, uid)
        try:
            _hook("between")
            sst = _create_exclusive(dfd, sig_name, sig, uid)
            os.fsync(dfd)
        except (Stop, OSError) as exc:
            code = str(exc) if isinstance(exc, Stop) else "IO"
            raise Stop(code + "_GO_CREATED_SIGNATURE_NOT") from None
    finally:
        os.close(dfd)
    out.write("G6GI_GO=%s sha256=%s size=%d mode=0600 uid=%d nlink=1\n" % (go_name, go_sha, len(go), gst.st_uid))
    out.write("G6GI_SIG=%s sha256=%s size=%d mode=0600 uid=%d nlink=1\n" % (sig_name, sig_sha, len(sig), sst.st_uid))
    out.write("G6GI_INSTALLED=2\n")


def _lexists(dfd, name):
    try:
        os.stat(name, dir_fd=dfd, follow_symlinks=False)
        return True
    except FileNotFoundError:
        return False
    except OSError:
        raise Stop("IO") from None


def main(argv, out=sys.stdout):
    if len(argv) != 6 or argv[1] != "install":
        out.write("G6GI_STOP:USAGE\n")
        return 1
    try:
        install(argv[2], argv[3], argv[4], argv[5], out=out)
        return 0
    except Stop as exc:
        out.write("G6GI_STOP:%s\n" % exc)
        return 1
    except OSError:
        out.write("G6GI_STOP:IO\n")
        return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
