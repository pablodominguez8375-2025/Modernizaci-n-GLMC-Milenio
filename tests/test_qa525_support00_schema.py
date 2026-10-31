"""Adversarial contract/probe tests; Docker is a deterministic fake here."""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

OPS = Path(__file__).resolve().parents[1] / 'scripts/qa/operators'


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, OPS / filename)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


gate = load('support00_test_gate', 'qa525-support00-schema-gate.py')
probe = load('support00_test_probe', 'qa525-support00-post-db-readonly.py')
base = load('support00_test_base', 'qa525-post-db-readonly.py')
entry = load('support00_test_entry', 'qa525-supervised-dual-entrypoint.py')
OLD = [f'baseline{i:03}' for i in range(98)]
LIVE = OLD + [gate.member.MIGRATION_ID, gate.MIGRATION_ID]


class Support00Tests(unittest.TestCase):
    def fixture(self):
        files = [{'filename': p, 'status': 'added'} for p in gate.SCHEMA_PATHS]
        blobs = {gate.member.MIGRATION_PATH: gate.member.MIGRATION_BLOB,
                 gate.CONTEXT_PATH: gate.CONTEXT_BLOB, gate.MIGRATION_PATH: gate.MIGRATION_BLOB}
        return files, blobs

    def test_exact_three_reviewed_blobs(self):
        f, b = self.fixture()
        self.assertFalse(gate.reviewed_diff(f, b)['may_apply'])

    def test_postgresql_integration_exercises_the_identical_sql(self):
        source = (OPS.parents[2] / 'backend/tests/PMGM.Api.Tests/Integration/SupportViewAsPostgreSqlTests.cs').read_text()
        start = source.index('command.CommandText = """') + len('command.CommandText = """')
        sql = source[start:source.index('""";', start)]
        import textwrap
        self.assertEqual(probe.SUPPORT_SQL, textwrap.dedent(sql).strip())

    def test_unknown_schema_or_renamed_original_is_rejected(self):
        for filename in ('backend/X/Migrations/Other.cs', 'backend/OtherDbContext.cs'):
            f, b = self.fixture()
            with self.assertRaises(gate.Blocked):
                gate.reviewed_diff(f + [{'filename': filename}], b)
            f[0].update(status='renamed', previous_filename=filename)
            with self.assertRaises(gate.Blocked):
                gate.reviewed_diff(f, b)

    def test_any_changed_or_missing_blob_is_rejected(self):
        for p in gate.SCHEMA_PATHS:
            f, b = self.fixture()
            b[p] = 'f' * 40
            with self.assertRaises(gate.Blocked):
                gate.reviewed_diff(f, b)
            del b[p]
            with self.assertRaises(gate.Blocked):
                gate.reviewed_diff(f, b)

    def test_100_is_exact_union_not_just_count(self):
        self.assertEqual(100, gate.exact_post_migration_history(OLD, LIVE)['count'])
        for history in (LIVE[:-1], LIVE + ['extra'], LIVE[:-2] + ['a', gate.MIGRATION_ID],
                        LIVE[:-1] + [LIVE[0]], ['x'] + LIVE[1:]):
            with self.assertRaises(gate.Blocked):
                gate.exact_post_migration_history(OLD, history)

    def test_bad_original_receipt_never_accepted(self):
        for receipt in (OLD[:-1], OLD + ['extra'], [OLD[0]] * 98, None):
            with self.assertRaises(gate.Blocked):
                gate.exact_post_migration_history(receipt, LIVE)

    def test_support_seed_index_and_check_all_required(self):
        gate.support_objects(['1|1|1|0', '1', '1'])
        for rows in ([], ['1|1|1|0', '1', '0'], ['1|1|1|0', '0', '1'],
                     ['0|0|0|0', '1', '1'], ['2|2|1|0', '1', '1'],
                     ['1|1|0|0', '1', '1'], ['1|1|1|1', '1', '1']):
            with self.assertRaises(gate.Blocked):
                gate.support_objects(rows)

    def docker(self, *, history=None, support='1|1|1|0\n1\n1', columns=None):
        values = {base.HISTORY_SQL: LIVE if history is None else history,
                  base.TABLES_SQL: gate.member.TABLES,
                  base.INDEXES_SQL: gate.member.INDEX_DEFINITIONS,
                  base.COLUMNS_SQL: gate.member.EXPECTED_COLUMNS if columns is None else columns,
                  base.CONSTRAINTS_SQL: gate.member.EXPECTED_CONSTRAINTS}
        def run(args):
            self.assertEqual(['exec', 'centenario-qa-lab2-postgres-1', 'sh', '-c'], args[:4])
            self.assertIn('-U "$POSTGRES_USER" -d pmgm', args[4])
            sql = args[-1]
            self.assertTrue(sql.startswith('BEGIN READ ONLY;') and sql.endswith('COMMIT;'))
            if sql == probe.SUPPORT_SQL:
                return support
            self.assertIn(sql, values)
            return '\n'.join(values[sql])
        return run

    def test_live_probe_requires_original_schema_plus_support_catalog(self):
        result = probe.inspect({'database_migrations': OLD}, self.docker(), gate)
        self.assertEqual(probe.POST_DB_PASS_STATUS, result['status'])
        self.assertEqual(100, result['history_count'])
        self.assertFalse(result['may_apply'])
        self.assertFalse(result['may_migrate'])

    def test_probe_blocks_99_drift_and_invalid_constraints(self):
        for kwargs in ({'history': LIVE[:-1]}, {'support': '1|1|1|0\n0\n1'},
                       {'support': '1|1|1|0\n1\n0'}, {'columns': ()}):
            with self.assertRaises(Exception):
                probe.inspect({'database_migrations': OLD}, self.docker(**kwargs), gate)

    def test_probe_cli_cannot_apply_or_run_on_wrong_host(self):
        with self.assertRaises(Exception):
            probe.main(['--apply'])
        with patch.object(probe.socket, 'gethostname', return_value='other'):
            with self.assertRaises(Exception):
                probe.main(['--inspect-readonly'])

    def test_old_gate_still_rejects_100(self):
        with self.assertRaises(gate.member.Blocked):
            gate.member.exact_post_migration_history(OLD, LIVE)

    def test_entry_reads_all_three_blobs_and_rejects_wrong_one(self):
        f, b = self.fixture()
        calls = []
        def fetch(path):
            calls.append(path)
            filename = path.split('/contents/', 1)[1].split('?')[0]
            return {'sha': b[filename]}
        diff = {'status': 'ahead', 'files': f}
        self.assertEqual(gate.SCHEMA_REVIEW_STATE,
            entry.scan_migrations(None, fetch, 'a' * 40, gate, comparison=diff))
        self.assertEqual(3, len(calls))
        b[gate.MIGRATION_PATH] = 'f' * 40
        with self.assertRaises(entry.Blocked):
            entry.scan_migrations(None, fetch, 'a' * 40, gate, comparison=diff)

    def test_source_contract_rejects_any_unreviewed_text(self):
        with self.assertRaises(gate.Blocked):
            gate.reviewed_migration_source('migrationBuilder.Sql("INSERT")')

    def test_explicit_new_route_and_no_implicit_downgrade(self):
        for action in ('--prepare-reviewed', '--apply-supervised'):
            self.assertEqual((action, True, True), entry.schema_supervision_mode(
                [action, '--single-operator-qa', '--support00-postdb']))
            self.assertEqual((action, True, False), entry.schema_supervision_mode(
                [action, '--single-operator-qa']))
            for args in ([action, '--support00-postdb'],
                         [action, '--support00-postdb', '--single-operator-qa'],
                         [action, '--single-operator-qa', '--support00-postdb', '--apply']):
                with self.assertRaises(entry.Blocked):
                    entry.schema_supervision_mode(args)

    def test_prepared_receipt_cannot_cross_schema_routes(self):
        entry.validate_prepared_schema_mode({}, False)
        entry.validate_prepared_schema_mode({'schema_mode': 'support00_postdb'}, True)
        for prepared, mode in (({}, True), ({'schema_mode': 'support00_postdb'}, False),
                               ({'schema_mode': 'member_history_postdb'}, True)):
            with self.assertRaises(entry.Blocked):
                entry.validate_prepared_schema_mode(prepared, mode)

    def test_postdb100_acta_cannot_reuse_pr801_scope_or_fake_99(self):
        from test_qa525_supervised_dual_entrypoint import SingleOperatorPostDbTests, SRC
        fixture = SingleOperatorPostDbTests()
        fixture.setUp()
        record = dict(fixture.record, migration_id=gate.MIGRATION_ID,
                      migration_blob_sha=gate.MIGRATION_BLOB, dbcontext_blob_sha=gate.CONTEXT_BLOB,
                      scope=gate.SINGLE_OPERATOR_SCOPE)
        fake_probe = type('Probe', (), {'POST_DB_PASS_STATUS': probe.POST_DB_PASS_STATUS,
            'Blocked': probe.Blocked, 'inspect': staticmethod(lambda *args: {
                'status': probe.POST_DB_PASS_STATUS, 'history_count': 100, 'may_apply': False})})
        def private(path, maximum=65536):
            return record if path == entry.SINGLE_OPERATOR_REVIEW else fixture.receipt
        with patch.object(entry, 'private_json', side_effect=private), \
             patch.object(entry, 'validate_g6', return_value=True):
            self.assertTrue(entry.validate_post_db_dba(fixture.review, SRC, fixture.now,
                lambda _: '', gate, fake_probe, single_operator=True))
            record['scope'] = 'POST_PR801_API_WEB_ONLY_NO_MIGRATION'
            with self.assertRaises(entry.Blocked):
                entry.validate_post_db_dba(fixture.review, SRC, fixture.now,
                    lambda _: '', gate, fake_probe, single_operator=True)
            record['scope'] = gate.SINGLE_OPERATOR_SCOPE
            fake_probe.inspect = staticmethod(lambda *args: {
                'status': probe.POST_DB_PASS_STATUS, 'history_count': 99, 'may_apply': False})
            with self.assertRaises(entry.Blocked):
                entry.validate_post_db_dba(fixture.review, SRC, fixture.now,
                    lambda _: '', gate, fake_probe, single_operator=True)


if __name__ == '__main__':
    unittest.main()
