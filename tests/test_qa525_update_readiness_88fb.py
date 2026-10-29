import base64
import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

SOURCE = Path(__file__).with_name('qa525-update-readiness-88fb.py')
if not SOURCE.exists():
    SOURCE = Path(__file__).resolve().parents[1] / 'scripts/qa/operators/qa525-update-readiness-88fb.py'
spec = importlib.util.spec_from_file_location('readiness', SOURCE)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


class PreservationTests(unittest.TestCase):
    def setUp(self):
        self.identity = {
            'UserAccounts__Identity__ServerUrl': 'http://keycloak:8080/identity',
            'UserAccounts__Identity__Issuer': m.ORIGIN + '/identity/realms/pmgm',
            'UserAccounts__Identity__ClientId': 'pmgm-account-admin',
            'UserAccounts__Mail__LoginUrl': m.ORIGIN + '/',
            'UserAccounts__Identity__ClientSecret': 'synthetic-service-secret',
        }
        self.master = {'InitialLoad__MasterSubject': 'synthetic-master-subject',
                       'InitialLoad__MasterIssuer': m.ORIGIN + '/identity/realms/pmgm',
                       'Authentication__Authority': 'http://keycloak:8080/identity/realms/pmgm'}
        self.smtp = base64.b64encode(bytes(32)).decode()
        self.env = {**self.identity, **self.master, 'SystemMail__EncryptionKey': self.smtp,
                    'Database__ApplyMigrationsOnStartup': 'false',
                    'Database__RunMigrationsAndExit': 'false'}

    def verify(self):
        m.validate_identity(self.env, self.identity, self.master,
                            'synthetic-service-secret', self.smtp)

    def test_active_identity_is_accepted(self):
        self.verify()

    def test_lost_third_layer_secret_blocks(self):
        self.env['UserAccounts__Identity__ClientSecret'] = ''
        with self.assertRaisesRegex(m.Blocked, 'IDENTITY_RUNTIME_DRIFT'):
            self.verify()

    def test_wrong_internal_path_blocks(self):
        self.identity['UserAccounts__Identity__ServerUrl'] = 'http://keycloak:8080'
        with self.assertRaisesRegex(m.Blocked, 'IDENTITY_LAYER_DRIFT'):
            self.verify()

    def test_identity_overlay_cannot_modify_unrelated_setting(self):
        self.identity['Database__ApplyMigrationsOnStartup'] = 'true'
        with self.assertRaisesRegex(m.Blocked, 'IDENTITY_LAYER_DRIFT'):
            self.verify()

    def test_master_subject_drift_blocks(self):
        self.env['InitialLoad__MasterSubject'] = 'different'
        with self.assertRaisesRegex(m.Blocked, 'MASTER_RUNTIME_DRIFT'):
            self.verify()

    def test_master_issuer_cannot_be_internal(self):
        self.master['InitialLoad__MasterIssuer'] = 'http://keycloak:8080/identity/realms/pmgm'
        with self.assertRaisesRegex(m.Blocked, 'MASTER_IDENTITY_INVALID'):
            self.verify()

    def test_mail_key_replacement_blocks(self):
        self.env['SystemMail__EncryptionKey'] = base64.b64encode(bytes([1]) * 32).decode()
        with self.assertRaisesRegex(m.Blocked, 'SMTP_KEY_DRIFT'):
            self.verify()

    def test_api_cannot_run_migrator_on_restart(self):
        for k in ('Database__ApplyMigrationsOnStartup', 'Database__RunMigrationsAndExit'):
            with self.subTest(k=k):
                self.env[k] = 'true'
                with self.assertRaisesRegex(m.Blocked, 'MIGRATIONS_ENABLED'):
                    self.verify()
                self.env[k] = 'false'

    def test_extra_runtime_setting_is_detected(self):
        with self.assertRaisesRegex(m.Blocked, 'ENV_EFFECTIVE_DRIFT'):
            m.validate_env({'A': 'configured'}, {'A': 'configured', 'EXTRA': 'drift'}, {})

    def test_image_defaults_and_compose_override_merge(self):
        m.validate_env({'A': 'configured'}, {'A': 'configured', 'B': 'default'},
                       {'A': 'default', 'B': 'default'})

    def test_duplicate_environment_is_rejected(self):
        with self.assertRaisesRegex(m.Blocked, 'ENV_DUPLICATE'):
            m.environment(['SECRET=first', 'SECRET=second'])

    def test_reference_backup_does_not_expire_before_fresh_apply_dumps(self):
        import hashlib
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'g6'
            path.write_bytes(b'x' * 1027549)
            expected = hashlib.sha256(path.read_bytes()).hexdigest()
            with patch.object(m, 'BACKUP', path), patch.object(m, 'BACKUP_SHA', expected):
                m.backup_check(m.BACKUP_TIME + 30 * 24 * 3600)
        with self.assertRaisesRegex(m.Blocked, 'BACKUP_CLOCK_INVALID'):
            m.backup_check(m.BACKUP_TIME - 1)

    def test_backup_symlink_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            link = Path(directory) / 'link'
            link.symlink_to(Path(directory) / 'target')
            with patch.object(m, 'BACKUP', link), self.assertRaises(OSError):
                m.backup_check(m.BACKUP_TIME + 1)

    def test_private_file_permissions_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory) / 'public'
            p.write_text('synthetic')
            p.chmod(0o644)
            with self.assertRaises(m.Blocked):
                m.private(p)


if __name__ == '__main__':
    unittest.main()
