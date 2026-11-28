#!/usr/bin/env python3
"""QA525 gate HOST-PREP (route C): create the driver's lib dir and journal root and install the 11 pinned libraries.
CANDIDATE, NOT AUTHORISED.  Generated from the template by qa525-host-prep-build.py: the bundle is EMBEDDED, so one file = one
transfer = one hash.  Modes (exactly one per process): preflight | apply <bind> | rollback <bind>, bind = "dev:ino,dev:ino"
(the identities of /opt/centenario and /var/lib printed by `preflight`; just-in-time, any change = STOP).
Creates: /opt/centenario/qa525-manual-cut-e5c40d3a/lib (root 0755, 11 files 0644) and
/var/lib/pmgm-qa525-manual-cut-e5c40d3a/journal (root 0700, empty).  Never overwrites and never deletes: every target must be
absent; rollback/failed-apply RENAME the created tops to a fixed `.rolledback-/.apply-failed-` name (renameat2 NOREPLACE).
All operations are relative to O_NOFOLLOW parent FDs.  It spawns no processes, opens no sockets and does not run G6.
"""
import base64
import ctypes
import errno
import hashlib
import io
import os
import re
import stat
import sys

OPT_PARENT = "/opt/centenario"
VAR_PARENT = "/var/lib"
BASE = "qa525-manual-cut-e5c40d3a"
LIB = "lib"
JBASE = "pmgm-qa525-manual-cut-e5c40d3a"
JOURNAL = "journal"
SUFFIX_RB = ".rolledback-20261127"
SUFFIX_FAIL = ".apply-failed-20261127"
MODES = ("preflight", "apply", "rollback")
BIND_RE = re.compile(r"\A([0-9]{1,20}):([0-9]{1,20}),([0-9]{1,20}):([0-9]{1,20})\Z")
DIR_FLAGS = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC
RENAME_NOREPLACE = 1
# name -> (sha256, size, base64 lines); GENERATED, do not edit.
BUNDLE = {}  # @@BUNDLE@@


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


def bundle_files():
    """Decode and verify the embedded bundle; any anomaly = STOP before anything is touched."""
    if len(BUNDLE) != 11:
        raise Stop("BUNDLE_SET_INVALID")
    out = {}
    for name in sorted(BUNDLE):
        want, size, lines = BUNDLE[name]
        if not re.fullmatch(r"qa525-vnext-[a-z0-9-]+\.py", name):
            raise Stop("BUNDLE_NAME_INVALID")
        try:
            data = base64.b64decode("".join(lines), validate=True)
        except ValueError:
            raise Stop("BUNDLE_DECODE_FAILED") from None
        if len(data) != size or hashlib.sha256(data).hexdigest() != want:
            raise Stop("BUNDLE_HASH_MISMATCH")
        out[name] = data
    return out


def manifest_sha256():
    text = "".join("%s %s %d\n" % (n, BUNDLE[n][0], BUNDLE[n][1]) for n in sorted(BUNDLE))
    return hashlib.sha256(text.encode()).hexdigest()


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


def _check_bind(bind, ost, vst):
    if bind is not None and bind != ((ost.st_dev, ost.st_ino), (vst.st_dev, vst.st_ino)):
        raise Stop("PARENT_NOT_THE_BOUND_ONE")


def _mkdir(pfd, name, mode, uid):
    os.mkdir(name, 0o700, dir_fd=pfd)
    fd = _open_dir(pfd, name)
    os.fchmod(fd, mode)
    st = os.fstat(fd)
    if st.st_uid != uid or stat.S_IMODE(st.st_mode) != mode:
        os.close(fd)
        raise Stop("MKDIR_METADATA_UNEXPECTED")
    return fd


def _verify(ofd, vfd, uid, files):
    """The installed state must be EXACTLY the manifest; returns the lib/journal fds' metadata."""
    def tree(pfd, name, mode):
        st = _st(pfd, name)
        if st is None or not stat.S_ISDIR(st.st_mode) or st.st_uid != uid or stat.S_IMODE(st.st_mode) != mode:
            raise Stop("INSTALLED_STATE_UNEXPECTED")
        fd = _open_dir(pfd, name)
        if os.fstat(fd).st_ino != st.st_ino:
            os.close(fd)
            raise Stop("INSTALLED_STATE_UNEXPECTED")
        return fd
    bfd = tree(ofd, BASE, 0o755)
    try:
        if os.listdir(bfd) != [LIB]:
            raise Stop("INSTALLED_STATE_UNEXPECTED")
        lfd = tree(bfd, LIB, 0o755)
        try:
            if sorted(os.listdir(lfd)) != sorted(files):
                raise Stop("INSTALLED_STATE_UNEXPECTED")
            for name, data in files.items():
                st = os.stat(name, dir_fd=lfd, follow_symlinks=False)
                if (not stat.S_ISREG(st.st_mode) or st.st_uid != uid or st.st_nlink != 1 or stat.S_IMODE(st.st_mode) != 0o644
                        or st.st_size != len(data)):
                    raise Stop("INSTALLED_STATE_UNEXPECTED")
                fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=lfd)
                try:
                    got = os.read(fd, len(data) + 1)
                finally:
                    os.close(fd)
                if got != data:
                    raise Stop("INSTALLED_STATE_UNEXPECTED")
        finally:
            os.close(lfd)
    finally:
        os.close(bfd)
    jfd = tree(vfd, JBASE, 0o700)
    try:
        if os.listdir(jfd) != [JOURNAL]:
            raise Stop("INSTALLED_STATE_UNEXPECTED")
        yfd = tree(jfd, JOURNAL, 0o700)
        try:
            if os.listdir(yfd) != []:
                raise Stop("JOURNAL_NOT_EMPTY")
        finally:
            os.close(yfd)
    finally:
        os.close(jfd)


def preflight(ofd, vfd, ost, vst, uid, out, bind):
    _check_bind(bind, ost, vst)
    bundle_files()
    for pfd, name in ((ofd, BASE), (vfd, JBASE)):
        for n in (name, name + SUFFIX_RB, name + SUFFIX_FAIL):
            if _st(pfd, n) is not None:
                raise Stop("TARGET_EXISTS")
    out.write("G6HP_BIND=%d:%d,%d:%d\nG6HP_PARENTS=uid=%d no-group-world-write\nG6HP_BUNDLE=files=11 hash=OK manifest_sha256=%s\n"
              "G6HP_TARGETS=ABSENT\nG6HP_PREFLIGHT=PASS\n"
              % (ost.st_dev, ost.st_ino, vst.st_dev, vst.st_ino, uid, manifest_sha256()))


def apply(ofd, vfd, ost, vst, uid, out, bind):
    if bind is None:
        raise Stop("BIND_REQUIRED")
    preflight(ofd, vfd, ost, vst, uid, io.StringIO(), bind)
    files = bundle_files()
    created = []
    fds = []
    try:
        _hook("before")
        bfd = _mkdir(ofd, BASE, 0o755, uid); fds.append(bfd); created.append((ofd, BASE))
        lfd = _mkdir(bfd, LIB, 0o755, uid); fds.append(lfd)
        for i, (name, data) in enumerate(sorted(files.items())):
            _hook("file:%d" % i)
            fd = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC, 0o600, dir_fd=lfd)
            try:
                view = memoryview(data)
                while view:
                    view = view[os.write(fd, view):]
                os.fchmod(fd, 0o644)
                os.fsync(fd)
            finally:
                os.close(fd)
        os.fsync(lfd); os.fsync(bfd); os.fsync(ofd)
        _hook("journal")
        jfd = _mkdir(vfd, JBASE, 0o700, uid); fds.append(jfd); created.append((vfd, JBASE))
        yfd = _mkdir(jfd, JOURNAL, 0o700, uid); fds.append(yfd)
        os.fsync(yfd); os.fsync(jfd); os.fsync(vfd)
        _hook("verify")
        _verify(ofd, vfd, uid, files)
    except (OSError, Stop) as exc:
        code = str(exc) if isinstance(exc, Stop) else "APPLY_FAILED"
        try:
            for pfd, name in reversed(created):
                _noreplace_rename(pfd, name, name + SUFFIX_FAIL)
        except (OSError, Stop):
            raise Stop(code + "_ROLLBACK_FAILED") from None
        raise Stop(code + "_ROLLED_BACK") from None
    finally:
        for fd in fds:
            try:
                os.close(fd)
            except OSError:
                pass
    out.write("G6HP_APPLIED=1\nG6HP_LIB_DIR=dir uid=%d mode=0755 files=11\nG6HP_JOURNAL_ROOT=dir uid=%d mode=0700 entries=0\n"
              "G6HP_MANIFEST_SHA256=%s\n" % (uid, uid, manifest_sha256()))


def rollback(ofd, vfd, ost, vst, uid, out, bind):
    if bind is None:
        raise Stop("BIND_REQUIRED")
    _check_bind(bind, ost, vst)
    files = bundle_files()
    for pfd, name in ((ofd, BASE), (vfd, JBASE)):
        for n in (name + SUFFIX_RB,):
            if _st(pfd, n) is not None:
                raise Stop("TARGET_EXISTS")
    _verify(ofd, vfd, uid, files)  # exact installed set + EMPTY journal, else STOP (never touches a used journal)
    _noreplace_rename(ofd, BASE, BASE + SUFFIX_RB)
    try:
        _noreplace_rename(vfd, JBASE, JBASE + SUFFIX_RB)
    except Stop:
        try:
            _noreplace_rename(ofd, BASE + SUFFIX_RB, BASE)
        except Stop:
            raise Stop("ROLLBACK_PARTIAL") from None
        raise
    if _st(ofd, BASE) is not None or _st(vfd, JBASE) is not None:
        raise Stop("ROLLBACK_VERIFY_FAILED")
    out.write("G6HP_ROLLED_BACK=1\n")


def run(mode, opt=OPT_PARENT, var=VAR_PARENT, uid=None, out=sys.stdout, bind=None):
    uid = os.geteuid() if uid is None else uid
    ofd = vfd = None
    try:
        if mode not in MODES:
            raise Stop("MODE_INVALID")
        if mode != "preflight" and uid != 0 and opt == OPT_PARENT:
            raise Stop("ROOT_REQUIRED")
        ofd, ost = _open_parent(opt, uid)
        vfd, vst = _open_parent(var, uid)
        {"preflight": preflight, "apply": apply, "rollback": rollback}[mode](ofd, vfd, ost, vst, uid, out, bind)
        return 0
    except Stop as exc:
        out.write("G6HP_STOP:%s\n" % exc)
        return 1
    except OSError:
        out.write("G6HP_STOP:IO\n")
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
            bind = ((int(m[1]), int(m[2])), (int(m[3]), int(m[4])))
        else:
            raise IndexError
    except IndexError:
        sys.stdout.write("G6HP_STOP:MODE_INVALID\n")
        return 1
    return run(mode, bind=bind)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
