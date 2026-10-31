#!/usr/bin/env python3
"""Exact reviewed PR801+PR813 contract. No migration/deployment authority."""
import hashlib
import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('qa525_support00_member_gate',
    HERE / 'qa525-member-history-schema-gate.py')
member = importlib.util.module_from_spec(spec)
spec.loader.exec_module(member)
Blocked = member.Blocked
require = member.require
MIGRATION_ID = '20261030223000_SeedSupportWorkshop00'
MIGRATION_PATH = 'backend/src/PMGM.Api/Migrations/' + MIGRATION_ID + '.cs'
MIGRATION_BLOB = '8eb86e632009acd0bf252e8ea8abee1eabee6c6b'
CONTEXT_PATH = member.CONTEXT_PATH
CONTEXT_BLOB = member.CONTEXT_BLOB
SCHEMA_PATHS = (member.MIGRATION_PATH, CONTEXT_PATH, MIGRATION_PATH)
SINGLE_OPERATOR_SCOPE = 'POST_PR813_API_WEB_ONLY_NO_MIGRATION'
SCHEMA_REVIEW_STATE = 'PR813_REVIEWED_SUPPORT00_NEEDS_POST_DB'
EXPECTED_HISTORY_COUNT = 100


def reviewed_diff(files, blobs):
    require(type(files) is list and 0 < len(files) < 300
            and all(type(f) is dict and type(f.get('filename')) is str for f in files),
            'SUPPORT00_DIFF_INVALID')
    names = set()
    for row in files:
        names.add(row['filename'])
        if row.get('status') == 'renamed':
            require(type(row.get('previous_filename')) is str, 'SUPPORT00_RENAME_UNKNOWN')
            names.add(row['previous_filename'])
    sensitive = {p for p in names if '/migrations/' in p.lower() or 'dbcontext' in p.lower()}
    require(sensitive == set(SCHEMA_PATHS), 'SUPPORT00_UNKNOWN_SCHEMA_CHANGE')
    require(blobs == {member.MIGRATION_PATH: member.MIGRATION_BLOB,
                     CONTEXT_PATH: CONTEXT_BLOB, MIGRATION_PATH: MIGRATION_BLOB},
            'SUPPORT00_UNREVIEWED_BLOB')
    return {'status': 'PR813_SCHEMA_REVIEWED_NOT_MIGRATION_AUTHORITY', 'may_apply': False}


def reviewed_migration_source(content):
    require(type(content) is str, 'SUPPORT00_SOURCE_INVALID')
    data = content.encode('utf-8')
    require(hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
            == MIGRATION_BLOB, 'SUPPORT00_SOURCE_CHANGED')
    return True


def exact_post_migration_history(receipt, live):
    require(type(live) is list and len(live) == 100 and len(set(live)) == 100
            and MIGRATION_ID in live, 'SUPPORT00_EF100_REQUIRED')
    member.exact_post_migration_history(receipt, [x for x in live if x != MIGRATION_ID])
    return {'status': 'EF_HISTORY_100_READ_ONLY_VERIFIED', 'count': 100, 'may_apply': False}


def schema_objects(tables, indexes, columns, constraints):
    return member.schema_objects(tables, indexes, columns, constraints)


def support_objects(rows):
    # Three fixed catalog/result rows: canonical seed, unique expression index,
    # validated CHECK definition. No row contents or personal data are returned.
    require(rows == ['1|1|1|0', '1', '1'], 'SUPPORT00_EXACT_SCHEMA_REQUIRED')
    return True


if __name__ == '__main__':
    raise SystemExit('NO_CLI_NOT_MIGRATION_AUTHORITY')
