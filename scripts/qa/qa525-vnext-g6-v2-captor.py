#!/usr/bin/env python3
"""QA525 G6 v2 physical CAPTOR (INACTIVE library, never a GO, never run here).

Builds the ``captor(stage)`` that ``qa525-vnext-g6-v2-producer.py::backup`` calls. It
leaves in a fresh private stage exactly: ``pmgm.dump``, ``pmgm_keycloak.dump``,
``globals.sql`` and a non-empty ``silo/``. Every PostgreSQL command is a FIXED argv
built in this module (no shell, no caller-supplied command text); the runner only
executes an argv whose first element is the pinned docker binary. Each dump must
also pass ``pg_restore --list`` (right database, positive table-of-contents) before
the captor returns. The Silo copy is an injected port plus a link-free tree copier.

NOT authenticated here (belongs to the root bootstrap, #936/#822): the docker binary,
the host, the container identity beyond its compose labels, the Silo source path.
The two databases and the Silo are NOT captured at one atomic instant.
Returns only counts; every function is fail-closed with fixed public codes.
"""
from __future__ import annotations

import hashlib
import os
import re
import stat
import subprocess
import threading

DOCKER = "/usr/bin/docker"
PROJECT = "centenario-qa-lab2"
POSTGRES_SERVICE = "postgres"
POSTGRES_CONTAINER = PROJECT + "-postgres-1"
SOCKET = "unix:///var/run/docker.sock"
DBS = ("pmgm", "pmgm_keycloak")
MAX_DUMP_BYTES = 4 * 1024**3
MAX_LIST_BYTES = 8 * 1024**2
MIN_TOC_ENTRIES = 10
DUMP_TIMEOUT = 1800
SILO_MAX_FILES = 200000
SILO_MAX_BYTES = 8 * 1024**3
CHUNK = 1024 * 1024
DBNAME_LINE = re.compile(r"^;\s+dbname:\s+(\S+)\s*$", re.M)
TOC_LINE = re.compile(r"^;\s+TOC Entries:\s+([0-9]{1,9})\s*$", re.M)
FORMAT_LINE = re.compile(r"^;\s+Format:\s+CUSTOM\b", re.M)


class Stop(Exception):
    """Fixed public codes only; never echo paths, output or errors."""


def _docker(binary, *tail):
    return [binary, "--host", SOCKET, *tail]


def argv_inspect(binary, container):
    return _docker(binary, "inspect", "--format",
                   '{{index .Config.Labels "com.docker.compose.project"}}|'
                   '{{index .Config.Labels "com.docker.compose.service"}}|'
                   "{{.State.Running}}", container)


def argv_pg_dump(binary, container, db):
    return _docker(binary, "exec", "--user", "postgres", container, "pg_dump",
                   "--format=custom", "--no-owner", "--no-acl", "--dbname", db)


def argv_globals(binary, container):
    return _docker(binary, "exec", "--user", "postgres", container, "pg_dumpall",
                   "--globals-only", "--no-role-passwords")


def argv_restore_list(binary, container):
    return _docker(binary, "exec", "-i", "--user", "postgres", container,
                   "pg_restore", "--list")


def _kill_group(proc):
    """Kill the whole process group (docker client and any child), ignore if gone."""
    try:
        os.killpg(proc.pid, 9)
    except (ProcessLookupError, PermissionError):
        pass


class FixedRunner:
    """Executes only argv[0] == the pinned docker path; no shell, minimal env."""

    def __init__(self, docker=DOCKER, timeout=DUMP_TIMEOUT):
        if type(docker) is not str or not os.path.isabs(docker) or type(timeout) is not int \
                or not 0 < timeout <= 7200:
            raise Stop("G6CAP_RUNNER_INVALID")
        self.docker, self.timeout = docker, timeout

    def __call__(self, argv, *, stdin_path=None, stdout_fd=None, capture_limit=0):
        if (type(argv) is not list or not argv or argv[0] != self.docker
                or not all(type(a) is str and "\0" not in a for a in argv)
                or (stdout_fd is None) == (capture_limit <= 0)):
            raise Stop("G6CAP_RUNNER_REFUSED")
        stdin = subprocess.DEVNULL
        stdin_fd = None
        if stdin_path is not None:
            try:
                stdin_fd = os.open(stdin_path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
            except OSError:
                raise Stop("G6CAP_INPUT_UNAVAILABLE") from None
            stdin = stdin_fd
        limit = MAX_DUMP_BYTES if stdout_fd is not None else capture_limit
        proc = None
        timer = None
        try:
            try:
                proc = subprocess.Popen(
                    argv, stdin=stdin, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                    env={"PATH": "/usr/bin:/bin", "LANG": "C"}, shell=False,
                    close_fds=True, start_new_session=True)
            except OSError:
                raise Stop("G6CAP_EXEC_FAILED") from None
            timer = threading.Timer(self.timeout, _kill_group, (proc,))
            timer.start()
            total, parts = 0, []
            while True:
                block = proc.stdout.read(CHUNK)
                if not block:
                    break
                total += len(block)
                if total > limit:
                    _kill_group(proc)
                    raise Stop("G6CAP_OUTPUT_TOO_LARGE")
                if stdout_fd is not None:
                    view = memoryview(block)
                    while view:
                        view = view[os.write(stdout_fd, view):]
                else:
                    parts.append(block)
            code = proc.wait()
            if timer is not None and not timer.is_alive() and code < 0:
                raise Stop("G6CAP_TIMEOUT")
            return code, b"".join(parts)
        finally:
            if timer is not None:
                timer.cancel()
            if proc is not None:
                _kill_group(proc)
                proc.stdout.close()
                proc.wait()
            if stdin_fd is not None:
                os.close(stdin_fd)


def _check_stage(stage, uid):
    try:
        info = os.lstat(stage)
        entries = os.listdir(stage)
    except OSError:
        raise Stop("G6CAP_STAGE_INVALID") from None
    if (not stat.S_ISDIR(info.st_mode) or info.st_uid != uid
            or stat.S_IMODE(info.st_mode) != 0o700 or entries):
        raise Stop("G6CAP_STAGE_INVALID")


def _new_file(path):
    return os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC,
                   0o600)


def _positive_size(path):
    try:
        info = os.lstat(path)
    except OSError:
        raise Stop("G6CAP_DUMP_INVALID") from None
    if not stat.S_ISREG(info.st_mode) or not 0 < info.st_size <= MAX_DUMP_BYTES:
        raise Stop("G6CAP_DUMP_INVALID")


def parse_restore_list(text, db):
    """Return the TOC entry count of a pg_restore --list text for ``db`` or Stop."""
    if type(text) is not str or db not in DBS:
        raise Stop("G6CAP_LIST_INVALID")
    names, tocs = DBNAME_LINE.findall(text), TOC_LINE.findall(text)
    if len(names) != 1 or names[0] != db or len(tocs) != 1 or not FORMAT_LINE.search(text):
        raise Stop("G6CAP_LIST_INVALID")
    declared = int(tocs[0])
    listed = sum(1 for line in text.splitlines() if line and not line.startswith(";"))
    if declared < MIN_TOC_ENTRIES or not 0 < listed <= declared:
        raise Stop("G6CAP_LIST_INVALID")
    return declared


def _write_all(fd, data):
    view = memoryview(data)
    while view:
        written = os.write(fd, view)
        if written <= 0:
            raise Stop("G6CAP_SILO_COPY_MISMATCH")
        view = view[written:]


def _identity(st):
    return (st.st_dev, st.st_ino, st.st_size, st.st_mtime_ns, st.st_ctime_ns)


def _copy_object(src, dst, listed):
    """Copy one object: exact byte count, source stable before/after, destination re-hashed."""
    fd = os.open(src, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        first = os.fstat(fd)
        if (not stat.S_ISREG(first.st_mode) or first.st_nlink != 1
                or _identity(first) != _identity(listed)):
            raise Stop("G6CAP_SILO_CHANGED")
        out = _new_file(dst)
        h = hashlib.sha256()
        total = 0
        try:
            while True:
                block = os.read(fd, CHUNK)
                if not block:
                    break
                total += len(block)
                if total > first.st_size:
                    raise Stop("G6CAP_SILO_CHANGED")
                h.update(block)
                _write_all(out, block)
            os.fsync(out)
        finally:
            os.close(out)
        if total != first.st_size or _identity(os.fstat(fd)) != _identity(first):
            raise Stop("G6CAP_SILO_CHANGED")
        if _identity(os.lstat(src)) != _identity(first):
            raise Stop("G6CAP_SILO_CHANGED")
    finally:
        os.close(fd)
    check = os.open(dst, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        info = os.fstat(check)
        if not stat.S_ISREG(info.st_mode) or info.st_size != total:
            raise Stop("G6CAP_SILO_COPY_MISMATCH")
        again = hashlib.sha256()
        count = 0
        while True:
            block = os.read(check, CHUNK)
            if not block:
                break
            count += len(block)
            again.update(block)
        if count != total or again.digest() != h.digest():
            raise Stop("G6CAP_SILO_COPY_MISMATCH")
    finally:
        os.close(check)


def copy_tree_nofollow(source, destination, *, max_files=SILO_MAX_FILES,
                       max_bytes=SILO_MAX_BYTES):
    """Copy regular files only: no links, devices or sockets; private modes; capped."""
    if type(source) is not str or not os.path.isabs(source) or type(destination) is not str:
        raise Stop("G6CAP_SILO_INVALID")
    files = size = 0

    def walk(src, dst):
        nonlocal files, size
        for name in sorted(os.listdir(src)):
            if name in (".", "..") or "/" in name or "\0" in name:
                raise Stop("G6CAP_SILO_INVALID")
            s, d = os.path.join(src, name), os.path.join(dst, name)
            info = os.lstat(s)
            if stat.S_ISDIR(info.st_mode):
                os.mkdir(d, 0o700)
                walk(s, d)
            elif stat.S_ISREG(info.st_mode):
                files += 1
                size += info.st_size
                if files > max_files or size > max_bytes:
                    raise Stop("G6CAP_SILO_TOO_LARGE")
                _copy_object(s, d, info)
            else:
                raise Stop("G6CAP_SILO_UNSAFE_ENTRY")

    try:
        if not stat.S_ISDIR(os.lstat(source).st_mode):
            raise Stop("G6CAP_SILO_INVALID")
        os.mkdir(destination, 0o700)
        walk(source, destination)
    except OSError:
        raise Stop("G6CAP_SILO_COPY_FAILED") from None
    return files


def _has_regular_file(root):
    for base, _dirs, leaves in os.walk(root, followlinks=False):
        for leaf in leaves:
            info = os.lstat(os.path.join(base, leaf))
            if stat.S_ISREG(info.st_mode) and info.st_size > 0:
                return True
    return False


def build_captor(*, runner, copy_silo, silo_quiescent, uid, docker=DOCKER,
                 container=POSTGRES_CONTAINER):
    """Return ``captor(stage)`` for the G6 v2 producer.

    ``copy_silo(dest_dir)`` and ``silo_quiescent()`` are ports. The Silo copy is only accepted if
    ``silo_quiescent()`` is exactly True both before and after it: a live Silo is never an atomic
    snapshot, so the physical port must prove quiesce/immutability (not defined here).
    """
    if not callable(runner) or not callable(copy_silo) or not callable(silo_quiescent) \
            or type(uid) is not int \
            or container != POSTGRES_CONTAINER:
        raise Stop("G6CAP_CONFIG_INVALID")

    def check_identity():
        code, out = runner(argv_inspect(docker, container), capture_limit=4096)
        if code != 0 or out.decode("ascii", "replace").strip() != \
                "|".join((PROJECT, POSTGRES_SERVICE, "true")):
            raise Stop("G6CAP_CONTAINER_UNEXPECTED")

    def dump_to(path, argv):
        fd = _new_file(path)
        try:
            code, _ = runner(argv, stdout_fd=fd)
            os.fsync(fd)
        finally:
            os.close(fd)
        if code != 0:
            raise Stop("G6CAP_DUMP_FAILED")
        _positive_size(path)

    def captor(stage):
        _check_stage(stage, uid)
        check_identity()
        counts = {}
        for db in DBS:
            path = os.path.join(stage, db + ".dump")
            dump_to(path, argv_pg_dump(docker, container, db))
            code, out = runner(argv_restore_list(docker, container), stdin_path=path,
                               capture_limit=MAX_LIST_BYTES)
            if code != 0:
                raise Stop("G6CAP_LIST_INVALID")
            counts[db] = parse_restore_list(out.decode("utf-8", "replace"), db)
        dump_to(os.path.join(stage, "globals.sql"), argv_globals(docker, container))
        silo = os.path.join(stage, "silo")
        if silo_quiescent() is not True:
            raise Stop("G6CAP_SILO_NOT_QUIESCENT")
        copy_silo(silo)
        if silo_quiescent() is not True:
            raise Stop("G6CAP_SILO_NOT_QUIESCENT")
        try:
            ok = stat.S_ISDIR(os.lstat(silo).st_mode) and _has_regular_file(silo)
        except OSError:
            ok = False
        if not ok:
            raise Stop("G6CAP_SILO_EMPTY")
        return counts

    return captor


if __name__ == "__main__":
    raise SystemExit("QA525_G6_V2_CAPTOR_LIBRARY_ONLY_NO_RUN_NO_GO")
