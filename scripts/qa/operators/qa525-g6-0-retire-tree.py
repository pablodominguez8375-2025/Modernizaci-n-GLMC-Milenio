#!/usr/bin/env python3
"""QA525 gate G6-0: retire the 13-nov G6 tree so `init` can create a NEW repo/key.  CANDIDATE, NOT AUTHORISED.

CLI (exactly one mode per process):  preflight | apply <dev>:<ino> | rollback <dev>:<ino>
`<dev>:<ino>` is a just-in-time physical identity of the ROOT printed by a prior `preflight` (G6_0_ROOT_BIND); it is NOT
derived from the A0 inventory (which prints no dev/ino). apply and rollback refuse any other tree: any change = STOP.  Never deletes or overwrites.  Every operation is relative to a parent-directory FD opened with
O_NOFOLLOW (no path is re-resolved after validation); the rename uses renameat2(RENAME_NOREPLACE) so a retired target created
during the race is never replaced.  No subprocess/network/docker/systemctl.  Closed `G6_0_KEY=value` lines; one
`G6_0_STOP:<CODE>` line and exit 1 on any anomaly.
"""
import ctypes
import errno
import io
import os
import re
import stat
import sys

ROOT = "/var/lib/pmgm-qa525-g6-v2"
RETIRED = ROOT + ".retired-20261113"
EXPECTED = ("repo-v2", "key-v2", "state-v2", "exports-v2", "restores-v2", "downloads-v2", "stages-v2")  # exact set (inventory v3)
MODES = ("preflight", "apply", "rollback")
BIND_RE = re.compile(r"\A([0-9]{1,20}):([0-9]{1,20})\Z")
DIR_FLAGS = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC
RENAME_NOREPLACE = 1


class Stop(Exception):
    pass


def _hook(stage):  # test seam: deterministic race injection; no-op in production
    return None


def _load_renameat2():
    """Exact signature: int renameat2(int, const char*, int, const char*, unsigned int); absent symbol -> closed STOP."""
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


def _is_tree(st, uid):
    return st is not None and stat.S_ISDIR(st.st_mode) and st.st_uid == uid and stat.S_IMODE(st.st_mode) == 0o700


def _same(a, b):
    return (a.st_dev, a.st_ino) == (b.st_dev, b.st_ino)


def _open_parent(path, uid):
    parent = os.path.dirname(path)
    if os.path.realpath(parent) != parent:
        raise Stop("PARENT_NOT_CANONICAL")
    try:
        pfd = os.open(parent, DIR_FLAGS)
        pst = os.fstat(pfd)
    except OSError:
        raise Stop("PARENT_OPEN_REFUSED") from None
    if not stat.S_ISDIR(pst.st_mode) or pst.st_uid != uid or stat.S_IMODE(pst.st_mode) & 0o022:
        os.close(pfd)
        raise Stop("PARENT_NOT_SAFE")
    return pfd, pst


def _inspect_tree(pfd, name, uid, bind):
    """Validate the root by FD; returns (rfd, fstat). Caller closes rfd."""
    lst = _st(pfd, name)
    if not _is_tree(lst, uid):
        raise Stop("ROOT_NOT_SAFE_DIR")
    rfd = _open_dir(pfd, name)
    try:
        fst = os.fstat(rfd)
        if not _same(lst, fst) or not _is_tree(fst, uid):
            raise Stop("ROOT_CHANGED_DURING_CHECK")
        present = os.listdir(rfd)
        if sorted(present) != sorted(EXPECTED):
            raise Stop("ROOT_LAYOUT_UNEXPECTED")
        key = os.stat("key-v2", dir_fd=rfd, follow_symlinks=False)
        if not (stat.S_ISREG(key.st_mode) and key.st_uid == uid and key.st_nlink == 1 and stat.S_IMODE(key.st_mode) == 0o600):
            raise Stop("KEY_METADATA_UNEXPECTED")
        for sub in EXPECTED:
            if sub == "key-v2":
                continue
            if not _is_tree(os.stat(sub, dir_fd=rfd, follow_symlinks=False), uid):
                raise Stop("SUBDIR_METADATA_UNEXPECTED")
        if bind is not None and (fst.st_dev, fst.st_ino) != bind:
            raise Stop("TREE_NOT_THE_INVENTORIED_ONE")
        return rfd, fst, len(present)
    except BaseException:
        os.close(rfd)
        raise


def preflight(root, retired, uid, out, bind=None, pfd=None):
    own = pfd is None
    if own:
        pfd, _ = _open_parent(root, uid)
    try:
        rfd, fst, n = _inspect_tree(pfd, os.path.basename(root), uid, bind)
        os.close(rfd)
        if os.path.dirname(retired) != os.path.dirname(root) or _st(pfd, os.path.basename(retired)) is not None:
            raise Stop("RETIRED_TARGET_EXISTS")
        if os.fstat(pfd).st_dev != fst.st_dev:
            raise Stop("CROSS_DEVICE")
        out.write("G6_0_ROOT_BIND=%d:%d\nG6_0_ROOT=dir uid=%d mode=0700 entries=%d\nG6_0_KEY_V2=file nlink=1 mode=0600\n"
                  "G6_0_RETIRED_TARGET=ABSENT\nG6_0_PREFLIGHT=PASS\n" % (fst.st_dev, fst.st_ino, uid, n))
        return fst
    finally:
        if own:
            os.close(pfd)


def apply(root, retired, uid, out, bind):
    if bind is None:
        raise Stop("BIND_REQUIRED")
    pfd, _ = _open_parent(root, uid)
    try:
        fst = preflight(root, retired, uid, io.StringIO(), bind, pfd)
        rn, tn = os.path.basename(root), os.path.basename(retired)
        _hook("after_preflight")
        _noreplace_rename(pfd, rn, tn)
        moved = _st(pfd, tn)  # identity of whatever the name resolved to at rename time
        try:
            if moved is None or not _same(moved, fst):
                raise Stop("MOVED_TREE_NOT_BOUND")
            _hook("after_rename")
            os.mkdir(rn, 0o700, dir_fd=pfd)
            nfd = _open_dir(pfd, rn)
            try:
                os.fchmod(nfd, 0o700)
                nst = os.fstat(nfd)
                if not _is_tree(nst, uid) or os.listdir(nfd) != [] or _same(nst, fst):
                    raise Stop("POST_VERIFY_FAILED")
            finally:
                os.close(nfd)
            os.fsync(pfd)
        except (OSError, Stop) as exc:
            code = str(exc) if isinstance(exc, Stop) else "APPLY_FAILED"
            try:
                cur = _st(pfd, rn)
                if cur is not None:
                    cfd = _open_dir(pfd, rn)
                    try:
                        empty = os.listdir(cfd) == [] and not _same(os.fstat(cfd), moved)
                    finally:
                        os.close(cfd)
                    if not empty:
                        raise Stop("ROLLBACK_REFUSED_NEW_ROOT_NOT_EMPTY")
                    os.rmdir(rn, dir_fd=pfd)
                _noreplace_rename(pfd, tn, rn)
                back = _st(pfd, rn)
                if back is None or not _same(back, moved):
                    raise Stop("ROLLBACK_VERIFY_FAILED")
            except (OSError, Stop):
                raise Stop(code + "_ROLLBACK_FAILED") from None
            raise Stop(code + "_ROLLED_BACK") from None
        old, new = _st(pfd, tn), _st(pfd, rn)
        if old is None or new is None or not _same(old, fst) or _same(new, fst):
            raise Stop("POST_VERIFY_FAILED")
        out.write("G6_0_APPLIED=1\nG6_0_RETIRED_BIND=%d:%d\nG6_0_NEW_ROOT=dir uid=%d mode=0700 entries=0\n"
                  % (old.st_dev, old.st_ino, uid))
    finally:
        os.close(pfd)


def rollback(root, retired, uid, out, bind):
    if bind is None:
        raise Stop("BIND_REQUIRED")
    pfd, _ = _open_parent(root, uid)
    try:
        rn, tn = os.path.basename(root), os.path.basename(retired)
        old = _st(pfd, tn)
        if not _is_tree(old, uid) or (old.st_dev, old.st_ino) != bind:
            raise Stop("RETIRED_NOT_THE_BOUND_TREE")
        rfd = _open_dir(pfd, tn)
        try:
            if sorted(os.listdir(rfd)) != sorted(EXPECTED):
                raise Stop("RETIRED_NOT_RESTORABLE")
        finally:
            os.close(rfd)
        nfd_st = _st(pfd, rn)
        if not _is_tree(nfd_st, uid):
            raise Stop("NEW_ROOT_NOT_EMPTY_OR_UNSAFE")
        nfd = _open_dir(pfd, rn)
        try:
            if os.listdir(nfd) != [] or not _same(os.fstat(nfd), nfd_st):
                raise Stop("NEW_ROOT_NOT_EMPTY_OR_UNSAFE")
        finally:
            os.close(nfd)
        _hook("after_rollback_check")
        try:
            os.rmdir(rn, dir_fd=pfd)  # fails if anything appeared meanwhile
        except OSError:
            raise Stop("NEW_ROOT_NOT_EMPTY_OR_UNSAFE") from None
        _hook("after_rmdir")
        _noreplace_rename(pfd, tn, rn)
        back, gone = _st(pfd, rn), _st(pfd, tn)
        if back is None or gone is not None or (back.st_dev, back.st_ino) != bind:
            raise Stop("ROLLBACK_VERIFY_FAILED")
        os.fsync(pfd)
        out.write("G6_0_ROLLED_BACK=1\nG6_0_ROOT_BIND=%d:%d\n" % bind)
    finally:
        os.close(pfd)


def run(mode, root=ROOT, retired=RETIRED, uid=None, out=sys.stdout, bind=None):
    uid = os.geteuid() if uid is None else uid
    try:
        if mode not in MODES:
            raise Stop("MODE_INVALID")
        if mode != "preflight" and uid != 0 and root == ROOT:
            raise Stop("ROOT_REQUIRED")
        if mode == "preflight":
            preflight(root, retired, uid, out, bind)
        else:
            {"apply": apply, "rollback": rollback}[mode](root, retired, uid, out, bind)
        return 0
    except Stop as exc:
        out.write("G6_0_STOP:%s\n" % exc)
        return 1
    except OSError:
        out.write("G6_0_STOP:IO\n")
        return 1


def main(argv):
    try:
        mode = argv[1]
        bind = None
        if mode == "preflight" and len(argv) == 2:
            pass
        elif mode in ("apply", "rollback") and len(argv) == 3 and BIND_RE.match(argv[2]):
            bind = tuple(int(x) for x in argv[2].split(":"))
        else:
            raise IndexError
    except IndexError:
        sys.stdout.write("G6_0_STOP:MODE_INVALID\n")
        return 1
    return run(mode, bind=bind)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
