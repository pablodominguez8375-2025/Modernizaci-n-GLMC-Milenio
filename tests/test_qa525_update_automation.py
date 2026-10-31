"""Repeated-update and explicit migration guards; no real host operations."""
import contextlib
import copy
import importlib.util
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import patch

OPS = Path(__file__).resolve().parents[1] / 'scripts/qa/operators'


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, OPS / filename)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


update = load('update_automation_test', 'qa525-update.py')
migration = load('migration_automation_test', 'qa525-support00-migration.py')
entry = load('entry_automation_test', 'qa525-supervised-dual-entrypoint.py')
core = load('core_automation_test', 'qa525-supervised-dual-transaction.py')
SHA = 'a' * 40
DIGEST = 'sha256:' + 'b' * 64
NOW = 1800000000


def packet():
    return {'schema_mode': 'support00_postdb',
            'review': {'source_sha': SHA, 'release_asset_sha256': DIGEST, 'reviewed_at': NOW - 5},
            'post_db': {'source_sha': SHA, 'release_asset_sha256': DIGEST, 'approved_at': NOW - 10}}


class ReviewPacketTests(unittest.TestCase):
    def test_cli_diagnostics_preserve_static_codes_but_never_arbitrary_details(self):
        import sys
        with patch.dict(sys.modules, {'qa525_update_entry': entry}):
            for module, fallback in ((update, 'UPDATE_NOT_COMPLETED'),
                    (migration, 'MIGRATION_NOT_CONFIRMED_RECONCILE_REQUIRED')):
                for code in ('GITHUB_API_RATE_LIMITED', 'GITHUB_API_RETRY_LATER',
                             'GITHUB_API_HTTP_ERROR', 'GITHUB_API_UNAVAILABLE'):
                    self.assertEqual(code, module.public_error_code(RuntimeError(code)))
                self.assertEqual('G6_CHECKPOINT_TOO_OLD',
                    module.public_error_code(entry.Blocked('G6_CHECKPOINT_TOO_OLD')))
                self.assertEqual('LOCAL_STATIC_BLOCK',
                    module.public_error_code(module.Blocked('LOCAL_STATIC_BLOCK')))
                for exc in (RuntimeError('password=DO_NOT_EMIT_TOKEN'),
                            entry.Blocked('not a static code: secret'),
                            module.Blocked('not a static code: secret')):
                    self.assertEqual(fallback, module.public_error_code(exc))
        with patch.dict(sys.modules, {'qa525_update_entry': None}):
            self.assertEqual('UPDATE_NOT_COMPLETED', update.public_error_code(RuntimeError('secret')))

    def test_github_review_must_bind_owner_exact_sha_digest_and_explicit_ai_kind(self):
        import json
        source = {'source_sha': SHA, 'release_asset_sha256': DIGEST}
        body = dict(schema='qa525-reviewed-cut-v1', **source,
                    review_kind='automated_ai', review_mode='single_operator_qa',
                    reviewed_sections=sorted(entry.REVIEWED), status='REVIEWED_FOR_QA_UPDATE')
        reference = 'https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-123'
        def fetch(path):
            self.assertEqual('/issues/comments/123', path)
            return {'user': {'login': 'pablodominguez8375-2025'}, 'body': json.dumps(body)}
        update.verify_technical_reference(fetch, reference, source, entry.REVIEWED)
        for key, value in (('source_sha', 'c' * 40), ('release_asset_sha256', 'sha256:' + 'f' * 64),
                           ('review_kind', 'independent_human_dba'), ('status', 'REVOKED')):
            previous = body[key]; body[key] = value
            with self.assertRaises(update.Blocked):
                update.verify_technical_reference(fetch, reference, source, entry.REVIEWED)
            body[key] = previous
        with self.assertRaises(update.Blocked):
            update.verify_technical_reference(lambda _: {'user': {'login': 'other'},
                'body': json.dumps(body)}, reference, source, entry.REVIEWED)
        with self.assertRaises(update.Blocked):
            update.verify_technical_reference(fetch, 'https://evil.invalid/', source, entry.REVIEWED)

    def test_single_operator_cannot_enable_account_flags_through_package_defaults(self):
        import json
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp) / 'backend/src/PMGM.Api'
            folder.mkdir(parents=True)
            file = folder / 'appsettings.json'
            file.write_text('{}')
            entry.validate_package_account_defaults(Path(tmp))
            for key in ('EnableLifecycleMutations', 'RequireLiveTokenIntrospection'):
                file.write_text(json.dumps({'UserAccounts': {'Identity': {key: True}}}))
                with self.assertRaises(entry.Blocked):
                    entry.validate_package_account_defaults(Path(tmp))

    def test_packet_expiry_is_not_renewed_by_running_again(self):
        p = packet()
        self.assertEqual(NOW + 1790, update.packet_contract(p, NOW))
        self.assertEqual(NOW + 1790, update.packet_contract(p, NOW + 100))
        with self.assertRaises(update.Blocked):
            update.packet_contract(p, NOW + 1791)

    def test_expired_future_mismatched_and_unknown_review_are_blocked(self):
        variants = []
        p = packet(); p['review']['reviewed_at'] = NOW - 1800; variants.append(p)
        p = packet(); p['post_db']['approved_at'] = NOW + 1; variants.append(p)
        p = packet(); p['post_db']['source_sha'] = 'c' * 40; variants.append(p)
        p = packet(); p['schema_mode'] = 'auto_migrate_anything'; variants.append(p)
        p = packet(); p['auto_approve'] = True; variants.append(p)
        for p in variants:
            with self.assertRaises(update.Blocked):
                update.packet_contract(p, NOW)

    def test_prior_success_requires_both_images_live(self):
        prepared = {'source_sha': SHA, 'status': 'PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT',
                    'api_image_id': DIGEST, 'web_image_id': 'sha256:' + 'c' * 64}
        journal = {'source_sha': SHA, 'status': 'APPLIED',
                   'new_images': {'api': prepared['api_image_id'], 'web': prepared['web_image_id']}}
        live = {n: ['id', prepared[n + '_image_id'], 'running'] for n in ('api', 'web')}
        update.previous_prepared_is_applied(prepared, journal, live)
        for status in ('PENDING', 'ROLLED_BACK', 'FAILED', None):
            with self.assertRaises(update.Blocked):
                update.previous_prepared_is_applied(prepared, dict(journal, status=status), live)
        live['web'][1] = 'sha256:' + 'd' * 64
        with self.assertRaises(update.Blocked):
            update.previous_prepared_is_applied(prepared, journal, live)

    def test_run_reviewed_orders_prepare_before_apply_and_stops_on_failure(self):
        fake = types.SimpleNamespace(STATE=Path('/unused'), MODULES=list(range(8)),
            trusted=lambda *a, **k: None, private_json=lambda *a, **k: packet())
        modules = [None, types.SimpleNamespace(exclusive=contextlib.nullcontext), None,
                   None, None, None, None, None]
        fake.load = lambda name, index: modules[index]
        with patch.object(update, 'load_entry', return_value=fake), \
             patch.object(update.stat, 'S_IMODE', return_value=0o700), \
             patch.object(Path, 'stat', return_value=types.SimpleNamespace(st_mode=0o700)), \
             patch.object(update.os, 'geteuid', return_value=0), \
             patch.object(update.socket, 'gethostname', return_value='glmdch'), \
             patch.object(update, 'install_review_packet', return_value=True), \
             patch.object(update, 'invoke', side_effect=[
                 {'status': 'PREPARED_REVIEWED_API_WEB_NO_DEPLOYMENT'},
                 {'status': 'DUAL_API_WEB_APPLIED'}]) as invoke:
            result = update.main(['--run-reviewed', '/root/review.json'])
            self.assertEqual('DUAL_API_WEB_APPLIED', result['status'])
            self.assertEqual(['--prepare-reviewed', '--apply-supervised'],
                             [call.args[1] for call in invoke.call_args_list])
            invoke.reset_mock()
            invoke.side_effect = update.Blocked('PREPARE_FAILED')
            with self.assertRaises(update.Blocked):
                update.main(['--run-reviewed', '/root/review.json'])
            self.assertEqual(1, invoke.call_count)



class ExpiredPreparedArchiveTests(unittest.TestCase):
    """No Docker/SSH/network: pure expired-prepared preconditions and archive."""
    def setUp(self):
        self.sha = SHA
        self.digest = DIGEST
        self.baseline = {'api': {'image_id': 'sha256:' + 'd' * 64},
                         'web': {'image_id': 'sha256:' + 'e' * 64}}
        self.prepared = dict(
            status='PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT',
            source_sha=SHA, source_asset_sha256=DIGEST,
            schema_mode='support00_postdb', review_mode='single_operator_qa',
            images_built=True, services_unchanged=True, may_apply=False,
            baseline=self.baseline, api_image_id='sha256:' + 'f' * 64,
            web_image_id='sha256:' + 'b' * 64)
        self.review = dict(source_sha=SHA, release_asset_sha256=DIGEST,
                           reviewed_at=NOW - 2000)
        self.post = dict(source_sha=SHA, release_asset_sha256=DIGEST,
                         approved_at=NOW - 2000, review_mode='single_operator_qa')
        self.cut = dict(target_sha=SHA, release_asset_sha256=DIGEST,
                        nonce='c' * 64, issued_at=NOW - 2000,
                        expires_at=NOW - 200, operator_reviewed=True,
                        scope='LOCAL_SUPERVISION_MATCH_ONLY_NO_APPLY',
                        action='first-qa-api-web-supervised',
                        environment='QA_SYNTHETIC')
        self.journals = [{'status': 'APPLIED', 'source_sha': 'd' * 40}]

    def contract(self, **changes):
        params = dict(sha=SHA, digest=DIGEST, baseline=self.baseline,
                      now=NOW, journals=self.journals, nonce_consumed=False)
        params.update(changes)
        return update.expired_prepared_archive_contract(
            self.prepared, self.review, self.post, self.cut, **params)

    def test_only_fully_expired_target_with_unconsumed_nonce_is_archivable(self):
        self.assertTrue(self.contract())
        self.cut['expires_at'] = NOW + 1
        with self.assertRaises(update.Blocked):
            self.contract()
        self.cut['expires_at'] = NOW - 200
        self.review['reviewed_at'] = NOW - 1799
        with self.assertRaises(update.Blocked):
            self.contract()
        self.review['reviewed_at'] = NOW - 2000
        with self.assertRaises(update.Blocked):
            self.contract(nonce_consumed=True)

    def test_foreign_sha_digest_unexpected_mode_and_baseline_are_blocked(self):
        for key, bad in [('source_sha', 'd' * 40),
                         ('source_asset_sha256', 'sha256:' + 'e' * 64),
                         ('schema_mode', 'member_history_postdb'),
                         ('review_mode', 'independent_dba'),
                         ('status', 'APPLIED'),
                         ('images_built', False),
                         ('services_unchanged', False),
                         ('may_apply', True),
                         ('baseline', {})]:
            original = self.prepared[key]
            self.prepared[key] = bad
            with self.subTest(key=key), self.assertRaises(update.Blocked):
                self.contract()
            self.prepared[key] = original
        for object_, key, bad in [
                (self.review, 'source_sha', 'd' * 40),
                (self.review, 'release_asset_sha256', 'sha256:' + 'e' * 64),
                (self.post, 'approved_at', NOW - 1),
                (self.post, 'review_mode', 'automatic'),
                (self.cut, 'nonce', 'unsafe'),
                (self.cut, 'operator_reviewed', False),
                (self.cut, 'action', 'apply_everything'),
                (self.cut, 'environment', 'PRODUCTION')]:
            previous = object_[key]
            object_[key] = bad
            with self.subTest(field=key), self.assertRaises(update.Blocked):
                self.contract()
            object_[key] = previous

    def test_pending_or_target_journal_is_never_treated_as_completed(self):
        for journal in ({'status': 'PENDING', 'source_sha': 'd' * 40},
                        {'status': 'ROLLED_BACK', 'source_sha': 'd' * 40},
                        {'status': 'APPLIED', 'source_sha': SHA}):
            with self.subTest(journal=journal), self.assertRaises(update.Blocked):
                self.contract(journals=[journal])

    def test_archive_preserves_bytes_and_never_runs_deployment(self):
        import json
        from unittest.mock import Mock
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            state = root / 'state'
            state.mkdir()
            sensitive = root / 'review.json'
            post = root / 'post.json'
            approval_path = root / 'approval.json'
            self.prepared['source_sha'] = SHA
            for path, value in ((state / 'prepared.json', self.prepared),
                                (sensitive, self.review), (post, self.post),
                                (approval_path, self.cut),
                                (state / ('dual-' + 'd' * 40 + '.json'), self.journals[0])):
                path.write_text(json.dumps(value, sort_keys=True))
            before = {p.name: p.read_bytes() for p in
                      (state / 'prepared.json', sensitive, post, approval_path)}
            live = {name: ['id', self.baseline[name]['image_id'], 'running',
                           None, None, None, None, 'base-compose.json']
                    for name in ('api', 'web')}
            def docker(cmd):
                if cmd[0:2] != ['image', 'inspect']:
                    raise AssertionError('unexpected docker command')
                if cmd[2:4] == ['--format', '{{.Id}}']:
                    return cmd[-1]
                if 'org.opencontainers.image.revision' in cmd[3]:
                    return SHA if cmd[-1] in (self.prepared['api_image_id'],
                                              self.prepared['web_image_id']) else 'd' * 40
                raise AssertionError('unknown inspect format')
            adapter = types.SimpleNamespace(docker=docker, metadata=lambda: live)
            build = types.SimpleNamespace(snapshot=lambda _: self.baseline)
            approval_mod = types.SimpleNamespace(APPROVAL=approval_path)
            core_mod = types.SimpleNamespace(TARGETS=['api', 'web'])
            e = types.SimpleNamespace(
                STATE=state, REVIEW=sensitive, SINGLE_OPERATOR_REVIEW=post,
                WORKDIR=root,
                single_operator_runtime=lambda _: None,
                reviewed_layers=lambda *args: None,
                private_json=lambda p: json.loads(p.read_text()),
                health=lambda source: source == 'd' * 40)
            args = types.SimpleNamespace(source=SHA, digest=DIGEST,
                                         operator_name='soporte@glmdch')
            modules = [core_mod, build, approval_mod, None, None, adapter, None, None]
            with patch.object(update, 'invoke', side_effect=AssertionError('must not apply')):
                result = update.archive_expired_prepared(e, args, modules)
            self.assertEqual('EXPIRED_PREPARED_ARCHIVED_NO_DEPLOYMENT', result['status'])
            archives = list(state.glob('review-archive-*'))
            self.assertEqual(len(archives), 1)
            self.assertEqual('EXPIRED_PREPARED_ARCHIVED_NO_DEPLOYMENT',
                json.loads((archives[0] / 'expired-prepared-archive.json').read_text())['status'])
            for name, bytes_before in before.items():
                self.assertEqual(bytes_before, (archives[0] / name).read_bytes())
            self.assertFalse((state / 'prepared.json').exists())
            self.assertTrue((state / ('dual-' + 'd' * 40 + '.json')).exists())


class RepeatedLayersTests(unittest.TestCase):
    def setUp(self):
        self.root = entry.WORKDIR
        self.layers = {'api': [str(self.root / 'compose.recovery.json'),
            '/opt/centenario/qa-api-refresh/api-' + 'f' * 40 + '.json',
            '/etc/pmgm/qa754-identity-overlay.stage.json'],
            'web': [str(self.root / 'compose.recovery.json'),
                    '/opt/centenario/qa-auto-lite/web-' + 'e' * 64 + '.json']}
        self.records = {}
        for sha, image in (('a' * 40, 'b' * 64), ('c' * 40, 'd' * 64)):
            images = {}
            for n in ('api', 'web'):
                path = entry.STATE / ('qa525-' + n + '-' + sha + '-' + image + '.json')
                self.layers[n].append(str(path))
                images[n] = 'sha256:' + image
                self.records[path] = {'services': {n: {'image': images[n]}}}
            self.records[entry.STATE / ('dual-' + sha + '.json')] = {
                'source_sha': sha, 'status': 'APPLIED', 'new_images': images}

    def test_two_successive_updates_preserve_all_identity_layers(self):
        with patch.object(entry, 'private_json', side_effect=lambda p: self.records[p]):
            answer = entry.reviewed_layers(core, self.layers, self.root)
        self.assertEqual(tuple(self.layers['api']), answer['api'])
        args = core.compose_args(str(self.root), answer['api'], '/tmp/new-image.json', 'api')
        self.assertIn('/etc/pmgm/qa754-identity-overlay.stage.json', args)
        self.assertEqual(len(self.layers['api']) + 1, args.count('--file'))

    def test_extra_non_image_config_and_incomplete_journal_are_rejected(self):
        p = Path(self.layers['api'][-1])
        self.records[p]['services']['api']['environment'] = {'unsafe': 'true'}
        with patch.object(entry, 'private_json', side_effect=lambda p: self.records[p]):
            with self.assertRaises(entry.Blocked):
                entry.reviewed_layers(core, self.layers, self.root)
        del self.records[p]['services']['api']['environment']
        self.records[entry.STATE / ('dual-' + 'c' * 40 + '.json')]['status'] = 'PENDING'
        with patch.object(entry, 'private_json', side_effect=lambda p: self.records[p]):
            with self.assertRaises(entry.Blocked):
                entry.reviewed_layers(core, self.layers, self.root)

    def test_unknown_overlay_or_removed_identity_is_rejected(self):
        for changed in ({**self.layers, 'api': self.layers['api'] + ['/tmp/evil.json']},
                        {**self.layers, 'api': self.layers['api'][:2] + self.layers['api'][3:]}):
            with patch.object(entry, 'private_json', side_effect=lambda p: self.records[p]):
                with self.assertRaises(Exception):
                    entry.reviewed_layers(core, changed, self.root)


class MigrationTests(unittest.TestCase):
    def record(self):
        return dict(source_sha=SHA, release_asset_sha256=DIGEST, reviewed_at=NOW - 10,
            expires_at=NOW + 1000, nonce='c' * 64, operator_reviewed=True,
            review_mode='single_operator_qa', technical_review_reference=migration.SPONSOR_REFERENCE,
            sponsor_approval_reference=migration.SPONSOR_REFERENCE,
            scope=migration.SCOPE, g6={'g6_offhost_sha256_verified': True})

    def test_only_exact_cut_scope_fresh_authority_and_recovery_are_accepted(self):
        source = {'source_sha': SHA, 'release_asset_sha256': DIGEST}
        migration.approval_contract(self.record(), source, NOW)
        for changes in ({'source_sha': 'd' * 40}, {'scope': 'all_migrations'},
                        {'review_mode': 'automatic'}, {'operator_reviewed': False},
                        {'expires_at': NOW - 1}, {'reviewed_at': NOW + 1},
                        {'nonce': 'not-a-nonce'}, {'g6': {'g6_offhost_sha256_verified': False}}):
            record = dict(self.record(), **changes)
            with self.assertRaises(migration.Blocked):
                migration.approval_contract(record, source, NOW)

    def test_container_has_no_ports_mounts_privileges_or_secrets_in_argv(self):
        args = migration.run_arguments(DIGEST, 'c' * 64, '/root/private/migration.env', 'd' * 64)
        self.assertIn('--read-only', args)
        self.assertIn('no-new-privileges', args)
        self.assertIn('--rm', args)
        self.assertNotIn('--privileged', args)
        self.assertNotIn('-p', args)
        self.assertNotIn('--volume', args)
        self.assertNotIn('--env', args)
        self.assertEqual(DIGEST, args[-1])

    def test_journal_is_private_exclusive_and_does_not_silently_overwrite(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'journal.json'
            migration.write_new(p, {'status': 'PENDING'})
            self.assertEqual(0o600, p.stat().st_mode & 0o777)
            with self.assertRaises(FileExistsError):
                migration.write_new(p, {'status': 'APPLIED'})
            self.assertIn('PENDING', p.read_text())

    def test_wrong_host_never_imports_transport_or_starts_migration(self):
        with patch.object(migration.socket, 'gethostname', return_value='other'):
            with self.assertRaises(migration.Blocked):
                migration.main(['--migrate-reviewed'])


if __name__ == '__main__':
    unittest.main()
