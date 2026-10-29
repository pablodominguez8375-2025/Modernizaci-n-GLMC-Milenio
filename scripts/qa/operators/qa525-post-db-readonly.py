#!/usr/bin/env python3
"""Host-only, fixed-query read-only post-DB inspection for QA525 PR801."""
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
    "FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace "
    "WHERE n.nspname='core' AND c.relkind='r' AND c.relname IN "
    "('member_history_proposal_items','member_history_proposals') ORDER BY 1; COMMIT;")
INDEXES_SQL = (
    "BEGIN READ ONLY; SELECT t.relname || '|' || i.relname || '|' || "
    "x.indisunique || '|' || string_agg(a.attname, ',' ORDER BY k.ord) "
    "FROM pg_index x JOIN pg_class i ON i.oid=x.indexrelid "
    "JOIN pg_class t ON t.oid=x.indrelid JOIN pg_namespace n ON n.oid=t.relnamespace "
    "JOIN LATERAL unnest(x.indkey) WITH ORDINALITY k(attnum,ord) ON true "
    "JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=k.attnum "
    "WHERE n.nspname='core' AND i.relname IN "
    "('IX_member_history_proposals_MemberId_Status',"
    "'IX_member_history_proposals_OrganizationId_Status_CreatedAtUtc',"
    "'IX_member_history_proposal_items_ProposalId') "
    "GROUP BY t.relname,i.relname,x.indisunique ORDER BY 1; COMMIT;")
COLUMNS_SQL = (
    "BEGIN READ ONLY; SELECT c.relname || '|' || a.attnum || '|' || a.attname || "
    "'|' || pg_catalog.format_type(a.atttypid,a.atttypmod) || '|' || a.attnotnull "
    "FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid "
    "JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='core' "
    "AND c.relname IN ('member_history_proposal_items','member_history_proposals') "
    "AND a.attnum>0 AND NOT a.attisdropped ORDER BY c.relname,a.attnum; COMMIT;")
CONSTRAINTS_SQL = (
    "BEGIN READ ONLY; WITH keys AS (SELECT con.oid, string_agg(a.attname, ',' "
    "ORDER BY k.ord) cols FROM pg_constraint con JOIN LATERAL "
    "unnest(con.conkey) WITH ORDINALITY k(attnum,ord) ON true "
    "JOIN pg_attribute a ON a.attrelid=con.conrelid AND a.attnum=k.attnum "
    "GROUP BY con.oid), refs AS (SELECT con.oid, string_agg(a.attname, ',' "
    "ORDER BY k.ord) cols FROM pg_constraint con JOIN LATERAL "
    "unnest(con.confkey) WITH ORDINALITY k(attnum,ord) ON true "
    "JOIN pg_attribute a ON a.attrelid=con.confrelid AND a.attnum=k.attnum "
    "WHERE con.contype='f' GROUP BY con.oid) SELECT c.relname || '|' || con.contype || "
    "'|' || con.conname || '|' || keys.cols || '|' || COALESCE(rn.nspname || '.' || "
    "rc.relname,'') || '|' || COALESCE(refs.cols,'') || '|' || "
    "CASE WHEN con.contype='f' THEN con.confdeltype::text ELSE '' END "
    "FROM pg_constraint con JOIN pg_class c ON c.oid=con.conrelid "
    "JOIN pg_namespace n ON n.oid=c.relnamespace JOIN keys ON keys.oid=con.oid "
    "LEFT JOIN pg_class rc ON rc.oid=con.confrelid LEFT JOIN pg_namespace rn ON rn.oid=rc.relnamespace "
    "LEFT JOIN refs ON refs.oid=con.oid WHERE n.nspname='core' "
    "AND c.relname IN ('member_history_proposal_items','member_history_proposals') "
    "AND con.contype IN ('p','f') ORDER BY 1; COMMIT;")
ALLOWED_SQL = (HISTORY_SQL, TABLES_SQL, INDEXES_SQL, COLUMNS_SQL, CONSTRAINTS_SQL)


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
    require(type(sql) is str and sql in ALLOWED_SQL and sql.startswith("BEGIN READ ONLY;")
            and sql.endswith("COMMIT;"), "QUERY_NOT_READ_ONLY_ALLOWLISTED")
    args = ["exec", PG_CONTAINER, "sh", "-c",
            'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"',
            "qa525-readonly", sql]
    result = docker(args)
    require(type(result) is str and len(result) < 32768, "QUERY_RESULT_INVALID")
    return result.splitlines()


def inspect(receipt, docker, gate):
    require(type(receipt) is dict and type(receipt.get("database_migrations")) is list,
            "INSTALLED_API_EF_RECEIPT_INVALID")
    history = checked_query(docker, HISTORY_SQL)
    tables = checked_query(docker, TABLES_SQL)
    indexes = checked_query(docker, INDEXES_SQL)
    columns = checked_query(docker, COLUMNS_SQL)
    constraints = checked_query(docker, CONSTRAINTS_SQL)
    gate.exact_post_migration_history(receipt["database_migrations"], history)
    gate.schema_objects(tables, indexes, columns, constraints)
    return {"status": "POST_DB_99_MIGRATIONS_AND_EXACT_SCHEMA_READ_ONLY_PASS",
            "migration_id": gate.MIGRATION_ID, "history_count": len(history),
            "expected_tables_present": len(tables), "expected_indexes_present": len(indexes),
            "expected_columns_present": len(columns),
            "expected_constraints_present": len(constraints),
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
    return inspect(entry.private_json(API_RECEIPT), adapter.docker, gate)


if __name__ == "__main__":
    try:
        print(json.dumps(main(), sort_keys=True))
    except Exception:
        print(json.dumps({"status": "BLOCKED_QA525_POST_DB_READ_ONLY",
                         "may_apply": False, "may_migrate": False}, sort_keys=True))
        raise SystemExit(2)
