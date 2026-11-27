#!/usr/bin/env python3
"""QA525 gate DRIVER-INSTALL (route C): install the FINAL manual-cut driver as ONE file.  CANDIDATE, NOT AUTHORISED.
Generated from the template by qa525-driver-install-build.py: the driver bytes are EMBEDDED, so one file = one transfer = one hash.
Modes (exactly one per process): preflight | apply <bind> | rollback <bind>, bind = "dev:ino" of the base dir
/opt/centenario/qa525-manual-cut-e5c40d3a printed by `preflight` (just-in-time; any change = STOP).
Installs exactly: /opt/centenario/qa525-manual-cut-e5c40d3a/qa525-manual-cut-e5c40d3a.py (root, 0644, nlink 1, the embedded bytes).
Preflight/apply require HOST-PREP to be in place: base dir root 0755 holding exactly `lib`; `lib` holding exactly the 11 pinned
libraries (hash, root, 0644, nlink 1); journal root root 0700 and EMPTY; target ABSENT.  Never overwrites and never deletes:
rollback / failed apply RENAME the file to a fixed `.rolledback-/.apply-failed-` name (renameat2 NOREPLACE).  Every operation is
relative to O_NOFOLLOW parent FDs.  It spawns no processes, opens no sockets, never imports/executes the driver and runs no G6.
"""
import base64
import ctypes
import errno
import hashlib
import os
import re
import stat
import sys

OPT_PARENT = "/opt/centenario"
VAR_PARENT = "/var/lib"
BASE = "qa525-manual-cut-e5c40d3a"
LIB = "lib"
DRIVER_NAME = "qa525-manual-cut-e5c40d3a.py"
JBASE = "pmgm-qa525-manual-cut-e5c40d3a"
JOURNAL = "journal"
SUFFIX_RB = ".rolledback-20261127"
SUFFIX_FAIL = ".apply-failed-20261127"
MODES = ("preflight", "apply", "rollback")
BIND_RE = re.compile(r"\A([0-9]{1,20}):([0-9]{1,20})\Z")
DIR_FLAGS = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC
RENAME_NOREPLACE = 1
# GENERATED, do not edit: the driver and the 11 library pins it loads (the set HOST-PREP installed).
DRIVER = ("", 0, ())  # @@DRIVER@@
LIBS = {}  # @@LIBS@@


class Stop(Exception):
    pass


def _hook(stage):  # test seam for deterministic fault injection; no-op in production
    return None


def _load_renameat2():
    try:
        fn = ctypes.CDLL(None, use_errno=True).renameat2
    except (OSError, AttributeError):
        raise Stop("RENAMEAT2_UNAVAILABLE") from None
    fn.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
    fn.restype = ctypes.c_int
    return fn


def _noreplace_rename(pfd, src, dst, loader=None):
    fn = (loader or _load_renameat2)()
    rc = fn(pfd, os.fsencode(src), pfd, os.fsencode(dst), RENAME_NOREPLACE)
    if rc != 0:
        err = ctypes.get_errno()
        raise Stop("RENAME_REFUSED_ERRNO_%s" % (errno.errorcode.get(err, str(err)) if rc < 0 else "RC"))


def driver_bytes():
    want, size, lines = DRIVER
    if not re.fullmatch(r"[0-9a-f]{64}", want) or size <= 0 or not lines:
        raise Stop("DRIVER_NOT_EMBEDDED")
    try:
        data = base64.b64decode("".join(lines), validate=True)
    except ValueError:
        raise Stop("DRIVER_DECODE_FAILED") from None
    if len(data) != size or hashlib.sha256(data).hexdigest() != want:
        raise Stop("DRIVER_HASH_MISMATCH")
    return data


def _open_parent(path, uid):
    if os.path.realpath(path) != path:
        raise Stop("PARENT_NOT_CANONICAL")
    try:
        fd = os.open(path, DIR_FLAGS)
        st = os.fstat(fd)
    except OSError:
        raise Stop("PARENT_OPEN_REFUSED") from None
    if not stat.S_ISDIR(st.st_mode) or st.st_uid != uid or stat.S_IMODE(st.st_mode) & 0o022:
        os.close(fd)
        raise Stop("PARENT_NOT_SAFE")
    return fd, st


def _st(pfd, name):
    try:
        return os.stat(name, dir_fd=pfd, follow_symlinks=False)
    except FileNotFoundError:
        return None
    except OSError:
        raise Stop("IO") from None


def _open_dir(pfd, name):
    try:
        return os.open(name, DIR_FLAGS, dir_fd=pfd)
    except OSError:
        raise Stop("OPEN_REFUSED") from None


def _dir_exact(pfd, name, mode, uid, code):
    st = _st(pfd, name)
    if st is None or not stat.S_ISDIR(st.st_mode) or st.st_uid != uid or stat.S_IMODE(st.st_mode) != mode:
        raise Stop(code)
    fd = _open_dir(pfd, name)
    if os.fstat(fd).st_ino != st.st_ino:
        os.close(fd)
        raise Stop(code)
    return fd


def _read_regular(dfd, name, uid, mode, data_len=None):
    st = os.stat(name, dir_fd=dfd, follow_symlinks=False)
    if (not stat.S_ISREG(st.st_mode) or st.st_uid != uid or st.st_nlink != 1 or stat.S_IMODE(st.st_mode) != mode
            or st.st_size > (1 << 22) or (data_len is not None and st.st_size != data_len)):
        raise Stop("INSTALLED_STATE_UNEXPECTED")
    fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=dfd)
    try:
        got = os.read(fd, st.st_size + 1)
    finally:
        os.close(fd)
    if len(got) != st.st_size:
        raise Stop("INSTALLED_STATE_UNEXPECTED")
    return got


def _check_bind(bind, bst):
    if bind is not None and bind != (bst.st_dev, bst.st_ino):
        raise Stop("BASE_NOT_THE_BOUND_ONE")


def _check_host_prep(ofd, vfd, uid):
    """HOST-PREP must be exactly in place (same shape its own gate verified)."""
    bfd = _dir_exact(ofd, BASE, 0o755, uid, "BASE_STATE_UNEXPECTED")
    try:
        names = sorted(os.listdir(bfd))
        if LIB not in names or any(n not in (LIB, DRIVER_NAME, DRIVER_NAME + SUFFIX_RB, DRIVER_NAME + SUFFIX_FAIL) for n in names):
            raise Stop("BASE_STATE_UNEXPECTED")
        lfd = _dir_exact(bfd, LIB, 0o755, uid, "LIB_STATE_UNEXPECTED")
        try:
            if sorted(os.listdir(lfd)) != sorted(LIBS):
                raise Stop("LIB_STATE_UNEXPECTED")
            for name, want in LIBS.items():
                got = _read_regular(lfd, name, uid, 0o644)
                if hashlib.sha256(got).hexdigest() != want:
                    raise Stop("LIB_HASH_MISMATCH")
        finally:
            os.close(lfd)
    finally:
        os.close(bfd)
    jfd = _dir_exact(vfd, JBASE, 0o700, uid, "JOURNAL_STATE_UNEXPECTED")
    try:
        if os.listdir(jfd) != [JOURNAL]:
            raise Stop("JOURNAL_STATE_UNEXPECTED")
        yfd = _dir_exact(jfd, JOURNAL, 0o700, uid, "JOURNAL_STATE_UNEXPECTED")
        try:
            if os.listdir(yfd) != []:
                raise Stop("JOURNAL_NOT_EMPTY")
        finally:
            os.close(yfd)
    finally:
        os.close(jfd)


def _open_base(ofd, uid):
    bfd = _dir_exact(ofd, BASE, 0o755, uid, "BASE_STATE_UNEXPECTED")
    return bfd, os.fstat(bfd)


def preflight(ofd, vfd, uid, out, bind):
    data = driver_bytes()
    bfd, bst = _open_base(ofd, uid)
    try:
        _check_bind(bind, bst)
        _check_host_prep(ofd, vfd, uid)
        for n in (DRIVER_NAME, DRIVER_NAME + SUFFIX_RB, DRIVER_NAME + SUFFIX_FAIL):
            if _st(bfd, n) is not None:
                raise Stop("TARGET_EXISTS")
    finally:
        os.close(bfd)
    out.write("G6DI_BIND=%d:%d\nG6DI_PARENTS=uid=%d no-group-world-write\nG6DI_HOSTPREP=base=0755 lib=files=%d hash=OK journal=0700 entries=0\n"
              "G6DI_DRIVER=sha256=%s size=%d\nG6DI_TARGET=ABSENT\nG6DI_PREFLIGHT=PASS\n"
              % (bst.st_dev, bst.st_ino, uid, len(LIBS), hashlib.sha256(data).hexdigest(), len(data)))


def _verify_installed(bfd, uid, data):
    names = sorted(os.listdir(bfd))
    if names != sorted([LIB, DRIVER_NAME]):
        raise Stop("INSTALLED_STATE_UNEXPECTED")
    if _read_regular(bfd, DRIVER_NAME, uid, 0o644, len(data)) != data:
        raise Stop("INSTALLED_STATE_UNEXPECTED")


def apply(ofd, vfd, uid, out, bind):
    if bind is None:
        raise Stop("BIND_REQUIRED")
    import io
    preflight(ofd, vfd, uid, io.StringIO(), bind)
    data = driver_bytes()
    bfd, _ = _open_base(ofd, uid)
    created = False
    try:
        try:
            _hook("before")
            fd = os.open(DRIVER_NAME, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC, 0o600, dir_fd=bfd)
            created = True
            try:
                view = memoryview(data)
                while view:
                    view = view[os.write(fd, view):]
                _hook("written")
                os.fchmod(fd, 0o644)
                os.fsync(fd)
            finally:
                os.close(fd)
            os.fsync(bfd)
            _hook("verify")
            _verify_installed(bfd, uid, data)
        except (OSError, Stop) as exc:
            code = str(exc) if isinstance(exc, Stop) else "APPLY_FAILED"
            if created:
                try:
                    _noreplace_rename(bfd, DRIVER_NAME, DRIVER_NAME + SUFFIX_FAIL)
                except (OSError, Stop):
                    raise Stop(code + "_ROLLBACK_FAILED") from None
            raise Stop(code + ("_ROLLED_BACK" if created else "")) from None
    finally:
        os.close(bfd)
    out.write("G6DI_APPLIED=1\nG6DI_DRIVER_FILE=regular uid=%d mode=0644 nlink=1 size=%d\nG6DI_DRIVER_SHA256=%s\n"
              % (uid, len(data), hashlib.sha256(data).hexdigest()))


def rollback(ofd, vfd, uid, out, bind):
    if bind is None:
        raise Stop("BIND_REQUIRED")
    data = driver_bytes()
    bfd, bst = _open_base(ofd, uid)
    try:
        _check_bind(bind, bst)
        if _st(bfd, DRIVER_NAME + SUFFIX_RB) is not None:
            raise Stop("TARGET_EXISTS")
        _verify_installed(bfd, uid, data)  # exact bytes installed by this gate, else STOP
        _noreplace_rename(bfd, DRIVER_NAME, DRIVER_NAME + SUFFIX_RB)
        if _st(bfd, DRIVER_NAME) is not None:
            raise Stop("ROLLBACK_VERIFY_FAILED")
    finally:
        os.close(bfd)
    out.write("G6DI_ROLLED_BACK=1\n")


def run(mode, opt=OPT_PARENT, var=VAR_PARENT, uid=None, out=sys.stdout, bind=None):
    uid = os.geteuid() if uid is None else uid
    ofd = vfd = None
    try:
        if mode not in MODES:
            raise Stop("MODE_INVALID")
        if mode != "preflight" and uid != 0 and opt == OPT_PARENT:
            raise Stop("ROOT_REQUIRED")
        ofd, _ = _open_parent(opt, uid)
        vfd, _ = _open_parent(var, uid)
        {"preflight": preflight, "apply": apply, "rollback": rollback}[mode](ofd, vfd, uid, out, bind)
        return 0
    except Stop as exc:
        out.write("G6DI_STOP:%s\n" % exc)
        return 1
    except OSError:
        out.write("G6DI_STOP:IO\n")
        return 1
    finally:
        for fd in (ofd, vfd):
            if fd is not None:
                os.close(fd)


def main(argv):
    try:
        mode, bind = argv[1], None
        if mode == "preflight" and len(argv) == 2:
            pass
        elif mode in ("apply", "rollback") and len(argv) == 3 and BIND_RE.match(argv[2]):
            m = BIND_RE.match(argv[2])
            bind = (int(m[1]), int(m[2]))
        else:
            raise IndexError
    except IndexError:
        sys.stdout.write("G6DI_STOP:MODE_INVALID\n")
        return 1
    return run(mode, bind=bind)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
