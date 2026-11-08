#!/usr/bin/env python3
"""QA525 G6 v2 ISOLATED RESTORE VERIFIER (INACTIVE library, never a GO, never run here).

Builds the ``verifier(data_dir)`` the G6 v2 producer's RESTORE calls on the extracted data
(``pmgm.dump``, ``pmgm_keycloak.dump``, ``globals.sql``, ``silo/``, ``manifest.json``). It
restores both dumps into a THROWAWAY PostgreSQL container (created with ``--pull=never``) that has no network, a read-only
root, no capabilities and no host mount, counts the restored user
tables of each database and the regular Silo objects, then ALWAYS removes the container.
Returns ``({"pmgm": n, "pmgm_keycloak": m}, silo_objects)``; the producer rejects zero.

Every docker command is a fixed argv built here (no shell, no caller text); the runner only
executes argv[0] == the pinned docker binary (see the captor's FixedRunner). Each dump is
streamed to ``pg_restore`` on stdin, so NOTHING from the host is mounted into the container.
Never touches the production containers or volumes. The image must be a Debian-based postgres
(uid 999) pinned by digest; the tmpfs ownership and ``--user 999:999`` assume it. NOT proven here: that the data are production's, that roles
and passwords can be recreated (``globals.sql`` is not applied), ACL/owner correctness after
``--no-owner --no-acl`` (a ``db-grants`` run is a separate test), or MinIO reading the Silo.
The image reference must be pinned by digest by the caller. Fixed public codes only.
"""
from __future__ import annotations

import os
import re
import stat
import time

DOCKER = "/usr/bin/docker"
DBS = ("pmgm", "pmgm_keycloak")
IMAGE = re.compile(r"[a-z0-9][a-z0-9._/-]{0,127}@sha256:[0-9a-f]{64}\Z")
STAMP = re.compile(r"[0-9]{8}T[0-9]{6}Z\Z")
MIN_SERVER_VERSION_NUM = 170000
MAX_SERVER_VERSION_NUM = 179999
MAX_DUMP_TOTAL_BYTES = 1024**3
HEX64 = re.compile(r"[0-9a-f]{64}\Z")
READY_ATTEMPTS = 60
SOCKET = "unix:///var/run/docker.sock"
OUT_LIMIT = 65536
TABLES_SQL = ("select count(*) from pg_class c join pg_namespace n on n.oid = c.relnamespace "
              "where c.relkind in ('r','p') and n.nspname not in ('pg_catalog',"
              "'information_schema') and n.nspname not like 'pg_toast%'")


class Stop(Exception):
    """Fixed public codes only; never echo output, paths or errors."""


def container_name(stamp):
    if type(stamp) is not str or STAMP.fullmatch(stamp) is None:
        raise Stop("G6VER_CONFIG_INVALID")
    return "pmgm-g6v2-verify-" + stamp.lower()


def _d(binary, *tail):
    return [binary, "--host", SOCKET, *tail]


def argv_create(binary, name, image):
    return _d(binary, "create", "--pull=never", "--name", name, "--network", "none",
              "--read-only", "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
              "--pids-limit", "256", "--memory", "2g", "--user", "999:999",
              "--label", "org.pmgm.qa525.g6v2-verifier=1",
              "--tmpfs", "/var/lib/postgresql/data:rw,size=4g,uid=999,gid=999,mode=0700",
              "--tmpfs", "/run/postgresql:rw,size=16m,uid=999,gid=999,mode=0755",
              "--tmpfs", "/tmp:rw,size=256m",
              "-e", "POSTGRES_HOST_AUTH_METHOD=trust", image)


def argv_start(binary, cid):
    return _d(binary, "start", cid)


def argv_ready(binary, cid):
    return _d(binary, "exec", cid, "pg_isready", "-U", "postgres", "-h", "/run/postgresql")


def argv_version(binary, cid):
    return _d(binary, "exec", cid, "psql", "-U", "postgres", "-At", "-c",
              "show server_version_num")


def argv_createdb(binary, cid, db):
    return _d(binary, "exec", cid, "createdb", "-U", "postgres", db)


def argv_restore(binary, cid, db):
    # The dump is streamed on stdin: nothing from the host is mounted into the container.
    return _d(binary, "exec", "-i", cid, "pg_restore", "--exit-on-error", "--no-owner",
              "--no-acl", "-U", "postgres", "--dbname", db)


def argv_count(binary, cid, db):
    return _d(binary, "exec", cid, "psql", "-U", "postgres", "-d", db, "-At", "-c", TABLES_SQL)


def argv_remove(binary, cid):
    """Only ever called with the ID this invocation created, never with a name."""
    return _d(binary, "rm", "-f", "-v", cid)


def argv_list_name(binary, name):
    return _d(binary, "container", "ls", "-a", "--no-trunc", "--filter", "name=^/" + name + "$",
              "--format", "{{.ID}}")


def argv_list_id(binary, cid):
    return _d(binary, "container", "ls", "-a", "--no-trunc", "--filter", "id=" + cid,
              "--format", "{{.ID}}")


def count_silo_objects(data_dir):
    """Count non-empty regular files under data_dir/silo; links and specials are refused."""
    root = os.path.join(data_dir, "silo")
    try:
        if not stat.S_ISDIR(os.lstat(root).st_mode):
            raise Stop("G6VER_SILO_INVALID")
        total = 0
        for base, dirs, leaves in os.walk(root, followlinks=False):
            for name in dirs + leaves:
                info = os.lstat(os.path.join(base, name))
                if stat.S_ISDIR(info.st_mode):
                    continue
                if not stat.S_ISREG(info.st_mode):
                    raise Stop("G6VER_SILO_UNSAFE_ENTRY")
            for name in leaves:
                if os.lstat(os.path.join(base, name)).st_size > 0:
                    total += 1
    except OSError:
        raise Stop("G6VER_SILO_INVALID") from None
    if total <= 0:
        raise Stop("G6VER_SILO_INVALID")
    return total


def _number(out, minimum):
    text = out.decode("ascii", "replace").strip()
    if re.fullmatch(r"[0-9]{1,12}", text) is None or int(text) < minimum:
        raise Stop("G6VER_RESULT_INVALID")
    return int(text)


def build_verifier(*, runner, image, stamp, docker=DOCKER, sleep=time.sleep):
    """Return ``verifier(data_dir)``; ``runner(argv, capture_limit=n)`` -> (rc, bytes)."""
    if not callable(runner) or not callable(sleep) or type(image) is not str \
            or IMAGE.fullmatch(image) is None:
        raise Stop("G6VER_CONFIG_INVALID")
    name = container_name(stamp)

    def run(argv, stdin_path=None):
        kw = {"capture_limit": OUT_LIMIT}
        if stdin_path is not None:
            kw["stdin_path"] = stdin_path
        code, out = runner(argv, **kw)
        if code != 0:
            raise Stop("G6VER_COMMAND_FAILED")
        return out

    def dumps_size(data_dir):
        total = 0
        for db in DBS:
            try:
                info = os.lstat(os.path.join(data_dir, db + ".dump"))
            except OSError:
                raise Stop("G6VER_DATA_DIR_INVALID") from None
            if not stat.S_ISREG(info.st_mode) or info.st_size <= 0:
                raise Stop("G6VER_DATA_DIR_INVALID")
            total += info.st_size
        if total > MAX_DUMP_TOTAL_BYTES:
            raise Stop("G6VER_TOO_LARGE")

    def verifier(data_dir):
        if type(data_dir) is not str or not os.path.isabs(data_dir) \
                or "\0" in data_dir:
            raise Stop("G6VER_DATA_DIR_INVALID")
        silo = count_silo_objects(data_dir)
        dumps_size(data_dir)
        # A failed or uncertain look-up stops here: absence must be positively shown.
        if run(argv_list_name(docker, name)).strip():
            raise Stop("G6VER_NAME_IN_USE")
        cid = None
        try:
            # `create` first: if it fails nothing was created by us, so nothing is removed.
            cid = run(argv_create(docker, name, image)).decode("ascii", "replace").strip()
            if HEX64.fullmatch(cid) is None:
                cid = None
                raise Stop("G6VER_CLEANUP_FAILED")  # ownership of what exists is unknown
            run(argv_start(docker, cid))
            for _ in range(READY_ATTEMPTS):
                code, _ = runner(argv_ready(docker, cid), capture_limit=OUT_LIMIT)
                if code == 0:
                    break
                sleep(1)
            else:
                raise Stop("G6VER_NOT_READY")
            version = _number(run(argv_version(docker, cid)), MIN_SERVER_VERSION_NUM)
            if version > MAX_SERVER_VERSION_NUM:
                raise Stop("G6VER_RESULT_INVALID")
            tables = {}
            for db in DBS:
                run(argv_createdb(docker, cid, db))
                run(argv_restore(docker, cid, db),
                    stdin_path=os.path.join(data_dir, db + ".dump"))
                tables[db] = _number(run(argv_count(docker, cid, db)), 1)
            return tables, silo
        finally:
            if cid is not None:
                removed, _ = runner(argv_remove(docker, cid), capture_limit=OUT_LIMIT)
                listed, left = runner(argv_list_id(docker, cid), capture_limit=OUT_LIMIT)
                # PASS only if the removal succeeded AND absence is positively confirmed.
                if removed != 0 or listed != 0 or left.strip():
                    raise Stop("G6VER_CLEANUP_FAILED")

    return verifier


if __name__ == "__main__":
    raise SystemExit("QA525_G6_V2_VERIFIER_LIBRARY_ONLY_NO_RUN_NO_GO")
