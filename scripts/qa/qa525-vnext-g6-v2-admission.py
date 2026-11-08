#!/usr/bin/env python3
"""QA525 G6 v2 admission adapter. INACTIVE library, NEVER a physical GO.

Reads the three v2 evidence files and the two archive copies of ONE cut from an explicit v2
root through directory file descriptors (no path walk after the root is opened), hashes the
archive copies here, and hands everything to ``qa525-vnext-g6-v2-evidence.py::inspect``.

It never reads or writes the legacy G6 receipt, the legacy home-directory archive or the
legacy validator inputs, accepts no v1 name or schema (the evidence module's exact v2
schemas and name patterns refuse them), and always reports
``may_apply=False``. ``uid`` is the expected owner and is ALSO an assertion of the caller (a physical bootstrap
pins it, e.g. 0). Pins, repo id, key epoch and clocks are ASSERTIONS of the caller: they must
come from the authenticated pre-root bootstrap (#936/#822). This module authenticates nothing
about Drive, Restic, the host, Sponsor or signatures.
"""
from __future__ import annotations

import hashlib
import importlib.util
import os
from pathlib import Path
import re
import stat

HERE = Path(__file__).resolve().parent
STATUS = "G6_V2_ADMISSION_COHERENT_UNAUTHENTICATED_NO_PHYSICAL_GO"
STAMP = re.compile(r"\d{8}T\d{6}Z\Z")
STATE_DIR, EXPORTS_DIR, DOWNLOADS_DIR = "state-v2", "exports-v2", "downloads-v2"
MAX_EVIDENCE = 65536
MAX_ARCHIVE = 8 * 1024 ** 3
CHUNK = 1 << 20
OPEN = os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK  # a FIFO must not block


class Stop(Exception):
    """Fixed public codes only; never echo paths, bytes or errors."""


def _load_evidence():
    spec = importlib.util.spec_from_file_location(
        "qa525_g6_v2_admission_evidence", HERE / "qa525-vnext-g6-v2-evidence.py")
    if spec is None or spec.loader is None:
        raise Stop("G6ADM_EVIDENCE_UNAVAILABLE")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


evidence = _load_evidence()


def _names(stamp):
    if type(stamp) is not str or STAMP.fullmatch(stamp) is None:
        raise Stop("G6ADM_NAME_INVALID")
    return {"review": "review-%s.json" % stamp, "receipt": "receipt-%s.json" % stamp,
            "restore": "isolated-restore-v2-%s.report.json" % stamp,
            "archive": "centenario-qa-g6-v2-CIFRADO-%s.tar.gz" % stamp}


def _identity(st):
    return (st.st_dev, st.st_ino, st.st_size, st.st_mtime_ns, st.st_ctime_ns)


def _check_ancestors(root, uid):
    """Every ancestor must be a real directory owned by root or ``uid`` that others can not
    rename into: not group/other-writable, unless root-owned with the sticky bit (/tmp)."""
    parts = Path(root).parents
    for ancestor in parts:
        try:
            st = os.lstat(str(ancestor))
        except OSError:
            raise Stop("G6ADM_ROOT_UNTRUSTED") from None
        writable = st.st_mode & 0o022
        sticky_root = st.st_uid == 0 and st.st_mode & stat.S_ISVTX
        if (not stat.S_ISDIR(st.st_mode) or st.st_uid not in (0, uid)
                or (writable and not sticky_root)):
            raise Stop("G6ADM_ROOT_UNTRUSTED")


def _open_root(root, uid):
    if (type(root) is not str or not os.path.isabs(root)
            or os.path.realpath(root) != root):
        raise Stop("G6ADM_ROOT_UNTRUSTED")
    _check_ancestors(root, uid)
    try:
        fd = os.open(root, OPEN | os.O_DIRECTORY)
    except OSError:
        raise Stop("G6ADM_ROOT_UNTRUSTED") from None
    try:
        st = os.fstat(fd)
        # The ROOT ITSELF, judged on the open descriptor: a directory of the expected owner
        # that nobody else can enter or change; and the descriptor must still be the path
        # that was vetted (closes the window between realpath and open).
        if (not stat.S_ISDIR(st.st_mode) or st.st_uid != uid or st.st_mode & 0o077
                or os.readlink("/proc/self/fd/%d" % fd) != root):
            raise Stop("G6ADM_ROOT_UNTRUSTED")
        again = os.lstat(root)
        if (again.st_dev, again.st_ino) != (st.st_dev, st.st_ino):
            raise Stop("G6ADM_ROOT_UNTRUSTED")
    except OSError:
        os.close(fd)
        raise Stop("G6ADM_ROOT_UNTRUSTED") from None
    except Stop:
        os.close(fd)
        raise
    return fd


def _open_subdir(parent_fd, name, uid):
    try:
        fd = os.open(name, OPEN | os.O_DIRECTORY, dir_fd=parent_fd)
        st = os.fstat(fd)
    except OSError:
        raise Stop("G6ADM_DIR_UNTRUSTED") from None
    if not stat.S_ISDIR(st.st_mode) or st.st_uid != uid or st.st_mode & 0o077:
        os.close(fd)
        raise Stop("G6ADM_DIR_UNTRUSTED")
    return fd


def _open_file(dir_fd, name, uid, limit):
    try:
        fd = os.open(name, OPEN, dir_fd=dir_fd)
        st = os.fstat(fd)
    except OSError:
        raise Stop("G6ADM_FILE_UNTRUSTED") from None
    if (not stat.S_ISREG(st.st_mode) or st.st_uid != uid or st.st_nlink != 1
            or st.st_mode & 0o177 or not 0 < st.st_size <= limit):
        os.close(fd)
        raise Stop("G6ADM_FILE_UNTRUSTED")
    return fd, st


def _read_exact(dir_fd, name, uid):
    fd, before = _open_file(dir_fd, name, uid, MAX_EVIDENCE)
    try:
        data = b""
        while len(data) <= MAX_EVIDENCE:
            block = os.read(fd, MAX_EVIDENCE + 1 - len(data))
            if not block:
                break
            data += block
        after = os.fstat(fd)
    except OSError:
        raise Stop("G6ADM_FILE_UNTRUSTED") from None
    finally:
        os.close(fd)
    if _identity(before) != _identity(after) or len(data) != before.st_size:
        raise Stop("G6ADM_FILE_CHANGED")
    return data


def _hash_file(dir_fd, name, uid):
    fd, before = _open_file(dir_fd, name, uid, MAX_ARCHIVE)
    try:
        digest, size = hashlib.sha256(), 0
        while True:
            block = os.read(fd, CHUNK)
            if not block:
                break
            size += len(block)
            digest.update(block)
        after = os.fstat(fd)
    except OSError:
        raise Stop("G6ADM_FILE_UNTRUSTED") from None
    finally:
        os.close(fd)
    if _identity(before) != _identity(after) or size != before.st_size:
        raise Stop("G6ADM_FILE_CHANGED")
    return "sha256:" + digest.hexdigest()


def admit(root, *, stamp, pins, expected_restic_repo_id, expected_key_epoch, uid, now,
          admission_expires_at):
    """Coherence of ONE v2 cut read from ``root``; never authority, never writes."""
    if type(uid) is not int:
        raise Stop("G6ADM_UID_INVALID")
    names = _names(stamp)
    root_fd = _open_root(root, uid)
    opened = [root_fd]
    try:
        state = _open_subdir(root_fd, STATE_DIR, uid)
        opened.append(state)
        exports = _open_subdir(root_fd, EXPORTS_DIR, uid)
        opened.append(exports)
        downloads = _open_subdir(root_fd, DOWNLOADS_DIR, uid)
        opened.append(downloads)
        review = _read_exact(state, names["review"], uid)
        receipt = _read_exact(state, names["receipt"], uid)
        restore = _read_exact(state, names["restore"], uid)
        local = _hash_file(exports, names["archive"], uid)
        offhost = _hash_file(downloads, names["archive"], uid)
    finally:
        for fd in opened:
            os.close(fd)
    try:
        result = evidence.inspect(
            review_bytes=review, receipt_bytes=receipt, restore_bytes=restore, pins=pins,
            observed_archive_sha256=local, downloaded_offhost_sha256=offhost,
            expected_restic_repo_id=expected_restic_repo_id,
            expected_key_epoch=expected_key_epoch, now=now,
            admission_expires_at=admission_expires_at)
    except evidence.Stop as error:
        raise Stop(str(error)) from None
    if result["archive_sha256"] != local or local != offhost:
        raise Stop("G6ADM_ARCHIVE_COPIES_DIFFER")
    return {**result, "admission": STATUS, "stamp": stamp, "may_apply": False,
            "may_install": False}


if __name__ == "__main__":
    raise SystemExit("QA525_G6_V2_ADMISSION_LIBRARY_ONLY_NO_EXECUTION")
