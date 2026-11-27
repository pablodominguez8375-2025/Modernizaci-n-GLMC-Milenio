#!/usr/bin/env python3
"""QA525 gate G6-0: retire the 13-nov G6 tree so `init` can create a NEW repo/key.  CANDIDATE, NOT AUTHORISED.

Modes (closed, exactly one per process): `preflight` (read-only), `apply` (one reversible rename + mkdir), `rollback`.
Never deletes or overwrites: apply = os.rename(ROOT, RETIRED) + mkdir(ROOT, 0700); rollback = rmdir of the EMPTY new root +
rename back.  No subprocess/network/docker/systemctl.  Output: closed `G6_0_KEY=value` lines; any anomaly = one
`G6_0_STOP:<CODE>` line and exit 1.
"""
import io
import os
import stat
import sys

ROOT = "/var/lib/pmgm-qa525-g6-v2"
RETIRED = ROOT + ".retired-20261113"
EXPECTED = ("repo-v2", "key-v2", "state-v2")  # must exist in the old tree; all other names are allowed but not listed
MODES = ("preflight", "apply", "rollback")


class Stop(Exception):
    pass


def _lstat(path):
    try:
        return os.lstat(path)
    except FileNotFoundError:
        return None
    except OSError:
        raise Stop("IO") from None


def _real_dir(st, uid):
    return st is not None and stat.S_ISDIR(st.st_mode) and st.st_uid == uid and stat.S_IMODE(st.st_mode) == 0o700


def _names(path):
    try:
        return sorted(os.listdir(path))
    except OSError:
        raise Stop("LIST_FAILED") from None


def _fsync_dir(path):
    fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_CLOEXEC)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def preflight(root, retired, uid, out):
    st = _lstat(root)
    if not _real_dir(st, uid):
        raise Stop("ROOT_NOT_SAFE_DIR")
    present = _names(root)
    if any(n not in present for n in EXPECTED):
        raise Stop("ROOT_LAYOUT_UNEXPECTED")
    if os.path.lexists(retired):
        raise Stop("RETIRED_TARGET_EXISTS")
    parent = os.path.dirname(root)
    pst = _lstat(parent)
    if (pst is None or not stat.S_ISDIR(pst.st_mode) or pst.st_uid != uid or stat.S_IMODE(pst.st_mode) & 0o022
            or pst.st_dev != st.st_dev or os.path.dirname(retired) != parent):
        raise Stop("PARENT_NOT_SAFE_OR_CROSS_DEVICE")
    out.write("G6_0_ROOT=dir uid=%d mode=0700 entries=%d\nG6_0_EXPECTED_PRESENT=%s\nG6_0_RETIRED_TARGET=ABSENT\n"
              "G6_0_PREFLIGHT=PASS\n" % (st.st_uid, len(present), ",".join(EXPECTED)))
    return st


def apply(root, retired, uid, out):
    st = preflight(root, retired, uid, io.StringIO())  # apply output holds only its own result lines
    parent = os.path.dirname(root)
    os.rename(root, retired)  # atomic, same device, target verified absent (rename onto an existing dir would fail anyway)
    try:
        os.mkdir(root, 0o700)
        os.chmod(root, 0o700)
        _fsync_dir(parent)
    except OSError:
        try:
            if os.path.isdir(root) and not os.listdir(root):
                os.rmdir(root)
            os.rename(retired, root)
        except OSError:
            raise Stop("APPLY_FAILED_ROLLBACK_FAILED") from None
        raise Stop("APPLY_FAILED_ROLLED_BACK") from None
    old, new = _lstat(retired), _lstat(root)
    if not (_real_dir(old, uid) and (old.st_dev, old.st_ino) == (st.st_dev, st.st_ino) and _real_dir(new, uid)
            and _names(root) == []):
        raise Stop("POST_VERIFY_FAILED")
    out.write("G6_0_APPLIED=1\nG6_0_RETIRED=dir uid=%d mode=0700\nG6_0_NEW_ROOT=dir uid=%d mode=0700 entries=0\n" % (uid, uid))


def rollback(root, retired, uid, out):
    new, old = _lstat(root), _lstat(retired)
    if not _real_dir(new, uid) or _names(root) != []:
        raise Stop("NEW_ROOT_NOT_EMPTY_OR_UNSAFE")  # a root that already holds G6 state is never touched
    if not _real_dir(old, uid) or any(n not in _names(retired) for n in EXPECTED):
        raise Stop("RETIRED_NOT_RESTORABLE")
    os.rmdir(root)
    os.rename(retired, root)
    _fsync_dir(os.path.dirname(root))
    out.write("G6_0_ROLLED_BACK=1\n")


def run(mode, root=ROOT, retired=RETIRED, uid=None, out=sys.stdout):
    uid = os.geteuid() if uid is None else uid
    try:
        if mode not in MODES:
            raise Stop("MODE_INVALID")
        if mode != "preflight" and uid != 0 and root == ROOT:
            raise Stop("ROOT_REQUIRED")
        {"preflight": preflight, "apply": apply, "rollback": rollback}[mode](root, retired, uid, out)
        return 0
    except Stop as exc:
        out.write("G6_0_STOP:%s\n" % exc)
        return 1
    except OSError:
        out.write("G6_0_STOP:IO\n")
        return 1


if __name__ == "__main__":
    sys.exit(run(sys.argv[1]) if len(sys.argv) == 2 else (sys.stdout.write("G6_0_STOP:MODE_INVALID\n"), 1)[1])
