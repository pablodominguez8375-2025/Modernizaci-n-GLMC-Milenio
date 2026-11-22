#!/usr/bin/env python3
"""QA525 schema-cut adapter for phases 4-6 (EF 100->104, grants, post-migration gate).

Pure library; no signature, root, lock or host authority. It performs NO I/O of its own: every
effect goes through an injected `runner(argv, timeout, stdin) -> (rc, stdout)` and every durable
journal write through an injected `journal(phase)`. The root caller must already hold the lock plane,
have spent the nonce, and have journalled WRITES_QUIESCED. This module only sequences, builds closed
argv lists, verifies pinned bytes/IDs and classifies the outcome. Secrets never appear in argv: the
migrator DDL connection and the grants password live in root-owned 0600 env files passed by path.

Journal discipline: MIGRATION_STARTED is persisted BEFORE the first effect of the DDL phase; any failure
after that persists the terminal PARTIAL_SCHEMA_STOP (EF applies each migration in its own transaction, so
an interrupted run is a partial schema that is only recoverable through the supervised G6 restore; there is
no automatic Down and no retry).
"""
import hashlib
import json
import re

DOCKER = "/usr/bin/docker"
SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")
NAME = re.compile(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,62}\Z")
PATH = re.compile(r"/[A-Za-z0-9_./-]{1,200}\Z")
MIGRATION = re.compile(r"20[0-9]{12}_[A-Za-z0-9_]+\Z")
REQUIRED = (
    "20261118150000_AddWorkshopRegistryState",
    "20261118170000_AddWorkshopRetiredGuard",
    "20261119010000_AddWorkshopStatusTransitions",
    "20261119010100_AddWorkshopStatusTransitionsAppendOnly",
)
MIGRATED = b'{"status":"WORKSHOPS104_EF104_MIGRATED"}\n'
GRANTS_MARKER = "Permisos BD2 aplicados: runtime DML sin DDL, auditoría append-only, informes sin tablas."
GATE_PASS = "QA525_WORKSHOP_POSTMIGRATION_READ_ONLY_PASS"
ACL_OK = b"true|true|false|false|false\n"
MIGRATE_TIMEOUT = 300
SQL_TIMEOUT = 120
MAX_OUT = 65536
PARAMS = frozenset({
    "network", "pg_container", "migrator_env_file", "grants_env_file",
    "migrator_image_id", "baseline_history_ids", "grant_script", "gate_sql",
    "grant_script_sha256", "postgate_sha256",
})


class Stop(Exception):
    """Fixed diagnostic only; never output, argv, paths or secrets."""


def _digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


def _check_params(p):
    if type(p) is not dict or set(p) != PARAMS:
        raise Stop("SCHEMA_ADAPTER_INPUT_INVALID")
    for key in ("network", "pg_container"):
        if type(p[key]) is not str or NAME.fullmatch(p[key]) is None:
            raise Stop("SCHEMA_ADAPTER_INPUT_INVALID")
    for key in ("migrator_env_file", "grants_env_file"):
        if (type(p[key]) is not str or PATH.fullmatch(p[key]) is None
                or ".." in p[key].split("/") or "//" in p[key]):
            raise Stop("SCHEMA_ADAPTER_INPUT_INVALID")
    for key in ("migrator_image_id", "grant_script_sha256", "postgate_sha256"):
        if type(p[key]) is not str or SHA.fullmatch(p[key]) is None:
            raise Stop("SCHEMA_ADAPTER_INPUT_INVALID")
    for key in ("grant_script", "gate_sql"):
        if type(p[key]) is not bytes or not 0 < len(p[key]) <= MAX_OUT:
            raise Stop("SCHEMA_ADAPTER_INPUT_INVALID")
    base = p["baseline_history_ids"]
    if (type(base) is not list or len(base) != 100 or len(set(base)) != 100 or base != sorted(base)
            or any(type(x) is not str or MIGRATION.fullmatch(x) is None for x in base)
            or any(x in base for x in REQUIRED)):
        raise Stop("SCHEMA_ADAPTER_INPUT_INVALID")
    if _digest(p["grant_script"]) != p["grant_script_sha256"] or _digest(p["gate_sql"]) != p["postgate_sha256"]:
        raise Stop("SCHEMA_ADAPTER_SQL_PIN_MISMATCH")


def expected_history_after(baseline):
    """The 104 ordered IDs the database must show after the four reviewed migrations."""
    return sorted(list(baseline) + list(REQUIRED))


def plan_commands(p):
    """Closed argv for each effect. Nothing is shell-interpreted and no secret is in argv."""
    _check_params(p)
    psql = (DOCKER, "exec", "-i", p["pg_container"], "psql", "-U", "pmgm_migrator", "-d", "pmgm", "-X", "-A", "-t",
            "-v", "ON_ERROR_STOP=1")
    return {
        "image_id": (DOCKER, "image", "inspect", "--format", "{{.Id}}", p["migrator_image_id"]),
        "migrate": (DOCKER, "run", "--rm", "--network", p["network"], "--read-only", "--tmpfs", "/tmp",
                    "--env-file", p["migrator_env_file"], p["migrator_image_id"]),
        "history": psql + ("-c",
                           'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId" COLLATE "C"'),
        "grants": (DOCKER, "exec", "-i", "--env-file", p["grants_env_file"], p["pg_container"], "bash", "-s"),
        "acl": psql + ("-c",
                       "SELECT has_table_privilege('pmgm_runtime','core.workshop_status_transitions','SELECT')||'|'"
                       "||has_table_privilege('pmgm_runtime','core.workshop_status_transitions','INSERT')||'|'"
                       "||has_table_privilege('pmgm_runtime','core.workshop_status_transitions','UPDATE')||'|'"
                       "||has_table_privilege('pmgm_runtime','core.workshop_status_transitions','DELETE')||'|'"
                       "||has_table_privilege('pmgm_runtime','core.workshop_status_transitions','TRUNCATE')"),
        "gate": psql,
    }


def _run(runner, argv, timeout, stdin=None):
    try:
        rc, out = runner(argv, timeout, stdin)
    except Exception:  # timeout, OS error, anything: fixed code, nothing echoed
        raise Stop("SCHEMA_ADAPTER_EFFECT_FAILED") from None
    if type(rc) is not int or isinstance(rc, bool) or type(out) is not bytes or len(out) > MAX_OUT:
        raise Stop("SCHEMA_ADAPTER_EFFECT_FAILED")
    return rc, out


def _history_ok(out, expected):
    try:
        lines = out.decode("ascii").split("\n")
    except UnicodeError:
        return False
    if lines and lines[-1] == "":
        lines.pop()
    return lines == expected


def run_phases(journal, runner, params):
    """Run phases 4-6. `journal(phase)` must persist (fsync) and return before the effect proceeds.

    Returns a bounded, non-authoritative summary. Any failure after MIGRATION_STARTED journals the terminal
    PARTIAL_SCHEMA_STOP once and raises Stop; nothing is retried or reverted.
    """
    if not callable(journal) or not callable(runner):
        raise Stop("SCHEMA_ADAPTER_INPUT_INVALID")
    cmds = plan_commands(params)
    expected = expected_history_after(params["baseline_history_ids"])
    started = False
    # Read-only precheck BEFORE the DDL boundary: a wrong/missing image is a PRE-DDL stop (nothing journalled here;
    # the caller's last journalled phase, WRITES_QUIESCED, already classifies it as PRE_DDL_STOP).
    rc, out = _run(runner, cmds["image_id"], SQL_TIMEOUT)
    if rc != 0 or out != (params["migrator_image_id"] + "\n").encode("ascii"):
        raise Stop("SCHEMA_ADAPTER_IMAGE_ID_MISMATCH")
    try:
        journal("MIGRATION_STARTED")  # durable BEFORE the first effect of the DDL phase
        started = True
        rc, out = _run(runner, cmds["migrate"], MIGRATE_TIMEOUT)
        if rc != 0 or out != MIGRATED:
            raise Stop("SCHEMA_ADAPTER_MIGRATION_FAILED")
        rc, out = _run(runner, cmds["history"], SQL_TIMEOUT)
        if rc != 0 or not _history_ok(out, expected):
            raise Stop("SCHEMA_ADAPTER_HISTORY_MISMATCH")
        journal("SCHEMA_104")
        rc, out = _run(runner, cmds["grants"], SQL_TIMEOUT, params["grant_script"])
        if rc != 0 or GRANTS_MARKER not in out.decode("utf-8", "replace").split("\n"):
            raise Stop("SCHEMA_ADAPTER_GRANTS_FAILED")
        rc, out = _run(runner, cmds["acl"], SQL_TIMEOUT)
        if rc != 0 or out != ACL_OK:
            raise Stop("SCHEMA_ADAPTER_ACL_INVALID")
        journal("GRANTS_VERIFIED")
        rc, out = _run(runner, cmds["gate"], SQL_TIMEOUT, params["gate_sql"])
        if rc != 0 or GATE_PASS not in out.decode("utf-8", "replace").split("\n"):
            raise Stop("SCHEMA_ADAPTER_GATE_FAILED")
        journal("POSTGATE_PASS")
    except Stop:
        if started:
            try:
                journal("PARTIAL_SCHEMA_STOP")
            except Exception:
                pass  # the original opaque code is what the caller must see; the journal already holds the start event
        raise
    except Exception:
        if started:
            try:
                journal("PARTIAL_SCHEMA_STOP")
            except Exception:
                pass
        raise Stop("SCHEMA_ADAPTER_UNEXPECTED") from None
    return {
        "status": "QA525_SCHEMA_PHASES_4_6_COMPLETE_NO_AUTHORITY",
        "migration_count_after": 104,
        "history_sha256_after": _digest("\n".join(expected).encode("ascii")),
        "may_apply": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_ADAPTER_LIBRARY_ONLY_NO_GO")
