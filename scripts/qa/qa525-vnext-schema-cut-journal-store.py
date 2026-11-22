#!/usr/bin/env python3
"""QA525 durable phase journal port, library only and no GO authority.

A root caller must supply the already verified phase contract, nonce journal
filesystem helpers and held root plane. One fresh nonce directory per flight;
one O_EXCL event per phase. A partial event is NEVER removed or retried.
"""
import os
import re
import stat

NONCE = re.compile(r"[0-9a-f]{64}\Z")
OPEN_DIR = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC
OPEN_FILE = os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK
MAX_EVENTS = 11
MAX_EVENT = 1024


class Stop(Exception):
    """Fixed code only; never expose paths or record data."""


def _held(plane):
    try:
        plane.assert_held()
    except Exception:
        raise Stop("SCHEMA_STORE_PLANE_LOST") from None


def _directory(rootfd, nonce, uid, first):
    if type(nonce) is not str or NONCE.fullmatch(nonce) is None:
        raise Stop("SCHEMA_STORE_NONCE_INVALID")
    if first:
        try:
            os.mkdir(nonce, 0o700, dir_fd=rootfd)
            os.fsync(rootfd)
        except FileExistsError:
            raise Stop("SCHEMA_STORE_REPLAY") from None
        except OSError:
            raise Stop("SCHEMA_STORE_IO") from None
    try:
        dfd = os.open(nonce, OPEN_DIR, dir_fd=rootfd)
        info, linked = os.fstat(dfd), os.stat(nonce, dir_fd=rootfd, follow_symlinks=False)
        if (not stat.S_ISDIR(info.st_mode) or info.st_uid != uid
                or stat.S_IMODE(info.st_mode) != 0o700
                or (info.st_dev, info.st_ino) != (linked.st_dev, linked.st_ino)):
            raise Stop("SCHEMA_STORE_DIR_UNTRUSTED")
        return dfd
    except OSError:
        raise Stop("SCHEMA_STORE_DIR_UNTRUSTED") from None


def _events(dfd, uid):
    try:
        names = os.listdir(dfd)
    except OSError:
        raise Stop("SCHEMA_STORE_IO") from None
    if (len(names) > MAX_EVENTS or
            sorted(names) != [f"{n:08d}.event" for n in range(len(names))]):
        raise Stop("SCHEMA_STORE_CONTENT_INVALID")
    out = []
    for name in sorted(names):
        try:
            fd = os.open(name, OPEN_FILE, dir_fd=dfd)
            try:
                st = os.fstat(fd)
                if (not stat.S_ISREG(st.st_mode) or st.st_uid != uid or st.st_nlink != 1
                        or stat.S_IMODE(st.st_mode) != 0o600 or not 0 < st.st_size <= MAX_EVENT):
                    raise Stop("SCHEMA_STORE_EVENT_UNTRUSTED")
                raw = os.read(fd, MAX_EVENT + 1)
                if len(raw) != st.st_size or os.read(fd, 1):
                    raise Stop("SCHEMA_STORE_EVENT_UNTRUSTED")
                out.append(raw)
            finally:
                os.close(fd)
        except OSError:
            raise Stop("SCHEMA_STORE_EVENT_UNTRUSTED") from None
    return out


def append(root, binding, phase, *, uid, plane, contract, nonce_journal):
    """Persist one reviewed phase under the held plane; returns only after fsync/readback."""
    if (type(binding) is not dict or set(binding) != {"nonce", "go_sha256", "plan_sha256"}
            or type(uid) is not int or isinstance(uid, bool) or uid < 0
            or not callable(getattr(contract, "next_event", None))
            or not callable(getattr(nonce_journal, "_journal", None))
            or not callable(getattr(nonce_journal, "_write_all", None))):
        raise Stop("SCHEMA_STORE_INPUT_INVALID")
    _held(plane)
    rootfd = dfd = None
    try:
        rootfd = nonce_journal._journal(root, uid)
        dfd = _directory(rootfd, binding["nonce"], uid, phase == "RESERVED")
        current = _events(dfd, uid)
        decision = contract.next_event(current, binding, phase)
        _held(plane)
        name = f"{len(current):08d}.event"
        try:
            fd = os.open(name, os.O_RDWR | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC,
                         0o600, dir_fd=dfd)
        except FileExistsError:
            raise Stop("SCHEMA_STORE_REPLAY") from None
        try:
            raw = decision["event"]
            nonce_journal._write_all(fd, raw)
            os.fsync(fd)
            st = os.fstat(fd)
            if (not stat.S_ISREG(st.st_mode) or st.st_uid != uid or st.st_nlink != 1
                    or stat.S_IMODE(st.st_mode) != 0o600 or st.st_size != len(raw)
                    or os.pread(fd, len(raw) + 1, 0) != raw):
                raise Stop("SCHEMA_STORE_WRITE_INCOMPLETE")
        finally:
            os.close(fd)
        os.fsync(dfd)
        _held(plane)
        return {"phase": phase, "event_sha256": decision["event_sha256"],
                "failure_class_if_interrupted": decision["failure_class_if_interrupted"],
                "may_apply": False}
    except Stop:
        raise
    except OSError:
        raise Stop("SCHEMA_STORE_IO") from None
    finally:
        if dfd is not None:
            os.close(dfd)
        if rootfd is not None:
            os.close(rootfd)


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_STORE_LIBRARY_ONLY_NO_ROOT")
