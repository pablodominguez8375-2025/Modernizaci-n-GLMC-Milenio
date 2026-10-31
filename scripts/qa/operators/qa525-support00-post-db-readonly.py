#!/usr/bin/env python3
"""Live exact EF100, PR801 schema and support00 probe; read-only SQL only."""
import importlib.util
import json
import os
from pathlib import Path
import socket
import sys

HERE = Path(__file__).resolve().parent
POST_DB_PASS_STATUS = 'POST_DB_100_AND_SUPPORT00_EXACT_SCHEMA_READ_ONLY_PASS'


class Blocked(Exception):
    pass


SUPPORT_SQL = '''BEGIN READ ONLY;
SELECT count(*) FILTER (WHERE lower("Type")='support') || '|' ||
       count(*) FILTER (WHERE lower("Type")='support' AND "Number"='00') || '|' ||
       count(*) FILTER (WHERE lower("Type")='support' AND "Number"='00'
         AND "Id"='b12c0000-0000-4000-8000-000000000000'::uuid) || '|' ||
       count(*) FILTER (WHERE "Number"='00' AND lower("Type")<>'support')
FROM core.organizations;
SELECT count(*) FROM pg_index i
JOIN pg_class c ON c.oid=i.indexrelid
WHERE i.indrelid='core.organizations'::regclass
  AND c.relname='UX_organizations_single_support'
  AND i.indisunique AND i.indisvalid AND i.indisready
  AND i.indnatts=1 AND i.indnkeyatts=1
  AND regexp_replace(pg_get_expr(i.indexprs,i.indrelid), '\\s', '', 'g')='lower(("Type")::text)'
  AND regexp_replace(pg_get_expr(i.indpred,i.indrelid), '\\s', '', 'g')='(lower(("Type")::text)=''support''::text)';
SELECT count(*) FROM pg_constraint
WHERE conrelid='core.organizations'::regclass
  AND conname='CK_organizations_support_number_00' AND contype='c' AND convalidated
  AND regexp_replace(pg_get_constraintdef(oid), '\\s', '', 'g')=
    'CHECK((((lower(("Type")::text)=''support''::text)AND(("Number")::text=''00''::text))OR((lower(("Type")::text)<>''support''::text)AND(("Number")::textISDISTINCTFROM''00''::text))))';
COMMIT;'''


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    sys.modules[name] = result
    spec.loader.exec_module(result)
    return result


def inspect(receipt, docker, gate):
    base = load('qa525_support00_base_probe', HERE / 'qa525-post-db-readonly.py')
    # The old probe validates the original 22 columns, all indexes/constraints,
    # while the supplied explicit contract requires original98+PR801+PR813.
    result = base.inspect(receipt, docker, gate)
    base.require(result['history_count'] == 100, 'SUPPORT00_EF100_REQUIRED')
    rows = docker(['exec', base.PG_CONTAINER, 'sh', '-c',
        'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"',
        'qa525-support00-readonly', SUPPORT_SQL])
    base.require(type(rows) is str and len(rows) < 256, 'SUPPORT00_RESULT_INVALID')
    gate.support_objects(rows.splitlines())
    return {**result, 'status': POST_DB_PASS_STATUS, 'support_organization_count': 1,
            'support_number_00_count': 1, 'may_apply': False, 'may_migrate': False}


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    if argv != ['--inspect-readonly'] or os.geteuid() != 0 or socket.gethostname() != 'glmdch':
        raise ValueError('ONLY_GLM DCH_ROOT_READONLY'.replace(' ', '_'))
    sys.dont_write_bytecode = True
    entry_path = HERE / 'qa525-supervised-dual-entrypoint.py'
    # Bootstrap trust verifier has no top-level runtime mutation.
    entry = load('qa525_support00_entry', entry_path)
    files = (Path(__file__), entry_path, HERE / 'qa525-support00-schema-gate.py',
             HERE / 'qa525-member-history-schema-gate.py', HERE / 'qa525-post-db-readonly.py',
             HERE.parent / 'qa-lite-compose-adapter.py')
    for p in files:
        entry.trusted(p)
    gate = load('qa525_support00_gate', files[2])
    adapter = load('qa525_support00_transport', files[-1])
    return inspect(entry.private_json(Path('/opt/centenario/qa-api-refresh/installed.json')),
                   adapter.docker, gate)


if __name__ == '__main__':
    try:
        print(json.dumps(main(), sort_keys=True))
    except Exception:
        print(json.dumps({'status': 'BLOCKED_QA525_SUPPORT00_READ_ONLY',
                          'may_apply': False, 'may_migrate': False}, sort_keys=True))
        raise SystemExit(2)
