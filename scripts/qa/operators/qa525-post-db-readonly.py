#!/usr/bin/env python3
"""QA525 PR801: host-only, READ-ONLY post-DB inspection; NEVER authorizes deploy.

Run only after a separately approved DBA migration. Does not invoke migrator,
Docker Compose, SQL writes, backup, or the API+WEB apply entrypoint.
Outputs only synthetic-safe counts/status; no rows, credentials, or SQL payload.
"""
from __future__ import annotations

import importlib.util
import json
import os
from pathlib import Path
import socket
import sys

HERE = Path(__file__).resolve().parent
API_RECEIPT = Path("/opt/centenario/qa-api-refresh/installed.json")
PG_CONTAINER = "centenario-qa-lab2-postgres-1"
HISTORY_SQL = ('BEGIN READ ONLY; SELECT "MigrationId" FROM '
               'public."__EFMigrationsHistory" ORDER BY 1; COMMIT;')
TABLES_SQL = (
    "BEGIN READ ONLY; SELECT n.nspname || '.' || c.relname "
    "FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace "
    "WHERE n.nspname='core' AND c.relkind='r' "
    "AND c.relname IN ('member_history_proposals', "
    "'member_history_proposal_items') ORDER BY 1; COMMIT;")
INDEXES_SQL = (
    "BEGIN READ ONLY; SELECT indexname FROM pg_indexes "
    "WHERE schemaname='core' AND tablename IN "
    "('member_history_proposals', 'member_history_proposal_items') "
    "ORDER BY indexname; COMMIT;")


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    require(spec is not None and spec.loader is not None, "MODULE_NOT_FOUND")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def checked_query(docker, sql):
    """Fixed SQL in a READ ONLY transaction; no shell interpolation from DB."""
    require(type(sql) is str and sql in (HISTORY_SQL, TABLES_SQL, INDEXES_SQL)
            and sql.startswith("BEGIN READ ONLY;") and sql.endswith("COMMIT;"),
            "QUERY_NOT_READ_ONLY_ALLOWLISTED")
    args = ["exec", PG_CONTAINER, "sh", "-c",
            'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"',
            "qa525-readonly", sql]
    result = docker(args)
    require(type(result) is str and len(result) < 32768, "QUERY_RESULT_INVALID")
    return result.splitlines()


def inspect(receipt, docker, gate):
    """Validate exact 98->99 history and two known tables/three named indexes.

    A green report proves only these read-only assertions, NOT the DBA approval,
    schema's full column-by-column equivalence, data recoverability or release.
    """
    require(type(receipt) is dict and type(receipt.get("database_migrations")) is list,
            "INSTALLED_API_EF_RECEIPT_INVALID")
    baseline = receipt["database_migrations"]
    history = checked_query(docker, HISTORY_SQL)
    tables = checked_query(docker, TABLES_SQL)
    indexes = checked_query(docker, INDEXES_SQL)
    gate.exact_post_migration_history(baseline, history)
    gate.schema_objects(tables, indexes)
    return {"status": "POST_DB_99_MIGRATIONS_AND_OBJECTS_READ_ONLY_PASS",
            "migration_id": gate.MIGRATION_ID, "history_count": len(history),
            "expected_tables_present": len(gate.TABLES),
            "expected_indexes_present": len(gate.INDEXES),
            "may_apply": False, "may_migrate": False}


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    require(argv == ["--inspect-readonly"], "ONLY_EXPLICIT_READ_ONLY_PROBE")
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "ONLY_GLM DCH_ROOT_QA".replace(" ", "_"))
    sys.dont_write_bytecode = True
    gate_path = HERE / "qa525-member-history-schema-gate.py"
    adapter_path = HERE.parent / "qa-lite-compose-adapter.py"
    entry_path = HERE / "qa525-supervised-dual-entrypoint.py"
    adapter = load("qa525_post_db_adapter", adapter_path)
    for path in (Path(__file__), gate_path, adapter_path, entry_path, API_RECEIPT):
        adapter.trusted_path(path)
    gate = load("qa525_post_db_gate", gate_path)
    entry = load("qa525_post_db_receipt", entry_path)
    receipt = entry.private_json(API_RECEIPT)
    return inspect(receipt, adapter.docker, gate)


if __name__ == "__main__":
    try:
        print(json.dumps(main(), sort_keys=True))
    except Exception:
        # Never print SQL output, Docker stderr, environment, or secret-bearing errors.
        print(json.dumps({"status": "BLOCKED_QA525_POST_DB_READ_ONLY",
                          "may_apply": False, "may_migrate": False}, sort_keys=True))
        raise SystemExit(2)
