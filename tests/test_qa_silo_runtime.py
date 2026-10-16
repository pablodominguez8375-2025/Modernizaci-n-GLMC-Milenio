"""Real approved QA Compose/provisioning/recovery in an isolated CI project."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import secrets
import subprocess
import tempfile
import unittest
import uuid
from test_silo_real_iam import s3_request

ROOT = Path(__file__).resolve().parents[1]


class QaSiloRuntimeTests(unittest.TestCase):
    @unittest.skipUnless(os.getenv('QA_SILO_RUNTIME_REQUIRED') == '1', 'Requires the isolated QA Silo validation runner')
    def test_real_compose_provisioning_iam_backup_restore(self):
        project = 'qa-silo-ci-' + uuid.uuid4().hex[:12]
        stage = 'configuration'
        with tempfile.TemporaryDirectory(prefix='qa-silo-runtime-') as temporary:
            temporary = Path(temporary)
            envfile = temporary / 'qa.env'
            values = {key: secrets.token_urlsafe(24) for key in (
                'PMGM_QA_DB_PASSWORD', 'PMGM_QA_STORAGE_ADMIN_PASSWORD', 'PMGM_QA_DOCUMENT_SECRET_KEY',
                'PMGM_QA_BACKUP_SECRET_KEY', 'PMGM_QA_KEYCLOAK_ADMIN_PASSWORD', 'PMGM_QA_USER_PASSWORD')}
            # Exercise actual scripts with option-looking synthetic secrets.
            for key in ('PMGM_QA_STORAGE_ADMIN_PASSWORD', 'PMGM_QA_DOCUMENT_SECRET_KEY',
                        'PMGM_QA_BACKUP_SECRET_KEY'):
                values[key] = '--' + values[key]
            values.update(PMGM_QA_STORAGE_ADMIN_USER='admin' + secrets.token_hex(6),
                          PMGM_QA_DOCUMENT_ACCESS_KEY='docs' + secrets.token_hex(6),
                          PMGM_QA_BACKUP_ACCESS_KEY='backup' + secrets.token_hex(6),
                          PMGM_QA_BIND_ADDRESS='127.0.0.1', PMGM_QA_WEB_PUBLIC_URL='http://127.0.0.1:18081',
                          PMGM_QA_OIDC_PUBLIC_URL='http://127.0.0.1:18180')
            envfile.write_text(''.join(k + '=' + v + '\n' for k, v in values.items()))
            envfile.chmod(0o600)
            environment = dict(os.environ, PMGM_QA_COMPOSE_PROJECT=project)
            compose_file = ROOT / 'infrastructure/docker-compose.srv01.yml'
            if os.getenv('PMGM_QA_SILO_BUNDLE_DIR'):
                spec = importlib.util.spec_from_file_location('qa_silo_archive', ROOT / 'scripts/qa/prepare-silo-archive.py')
                archive = importlib.util.module_from_spec(spec)
                spec.loader.exec_module(archive)
                source_sha = os.environ['QA_VALIDATED_SOURCE_SHA']
                compose_file = temporary / 'verified-compose.json'
                archive.prepare(os.environ['PMGM_QA_SILO_BUNDLE_DIR'], source_sha, compose_file)
                environment['PMGM_QA_SILO_EXPECTED_SOURCE_SHA'] = source_sha
            compose = ['docker', 'compose', '-p', project, '--env-file', str(envfile),
                       '-f', str(compose_file)]
            def run(args, *, check=True, input=None, timeout=180):
                try:
                    result = subprocess.run(args, capture_output=True, text=True, input=input,
                                            env=environment, cwd=ROOT, timeout=timeout)
                except (OSError, subprocess.TimeoutExpired):
                    self.fail('QA Silo real command unavailable/timeout: ' + stage)
                if check and result.returncode:
                    self.fail('QA Silo real step failed: ' + stage)
                return result
            def sql(database, query):
                return run(compose + ['exec', '-T', 'postgres', 'psql', '-U', 'pmgm_app', '-d', database,
                                      '-At', '-v', 'ON_ERROR_STOP=1'], input=query).stdout.strip()
            try:
                model = json.loads(run(compose + ['config', '--format', 'json']).stdout)
                guard = run(['python3', 'scripts/check-qa-storage-config.py', '--stdin'], input=json.dumps(model))
                self.assertEqual('CONFIG_CHECK_ONLY', json.loads(guard.stdout)['status'])
                stage = 'start_private_storage_and_postgres'
                run(compose + ['up', '-d', '--wait', '--wait-timeout', '120', 'postgres', 'minio'], timeout=240)
                stage = 'actual_provisioning_script'
                run(compose + ['run', '--rm', '-T', 'minio-init'], timeout=120)
                container = run(compose + ['ps', '-q', 'minio']).stdout.strip()
                metadata = json.loads(run(['docker', 'inspect', '--format', '{{json .NetworkSettings.Networks}}', container]).stdout)
                ip = next(iter(metadata.values()))['IPAddress']
                self.assertFalse(json.loads(run(['docker', 'inspect', '--format', '{{json .HostConfig.PortBindings}}', container]).stdout))
                admin = ['docker', 'exec', container, 'mcli']
                run(admin + ['alias', 'set', '--', 'qa-admin', 'http://127.0.0.1:9000',
                             values['PMGM_QA_STORAGE_ADMIN_USER'], values['PMGM_QA_STORAGE_ADMIN_PASSWORD']])
                run(admin + ['mb', 'qa-admin/qa-forbidden'])
                stage = 'iam_positive_negative'
                user, password = values['PMGM_QA_DOCUMENT_ACCESS_KEY'], values['PMGM_QA_DOCUMENT_SECRET_KEY']
                payload = b'synthetic-qa-profile-object\x00' * 1024
                expected_hash = hashlib.sha256(payload).hexdigest()
                for method, body in [('PUT', payload), ('HEAD', b''), ('GET', b'')]:
                    status, response = s3_request(ip, method, 'pmgm-documents', 'qa.bin', user, password, body)
                    self.assertEqual(200, status)
                    if method == 'GET':
                        self.assertEqual(expected_hash, hashlib.sha256(response).hexdigest())
                self.assertEqual(404, s3_request(ip, 'HEAD', 'pmgm-documents', 'absent', user, password)[0])
                for method in ('GET', 'PUT', 'DELETE'):
                    self.assertEqual(403, s3_request(ip, method, 'qa-forbidden', 'forbidden', user, password, b'x' if method == 'PUT' else b'')[0])
                run(admin + ['alias', 'set', '--', 'qa-docs', 'http://127.0.0.1:9000', user, password])
                self.assertNotEqual(0, run(admin + ['admin', 'info', 'qa-docs'], check=False).returncode)
                listed = run(admin + ['ls', 'qa-docs'], check=False)
                self.assertNotIn('qa-forbidden', listed.stdout)
                backup_user, backup_password = values['PMGM_QA_BACKUP_ACCESS_KEY'], values['PMGM_QA_BACKUP_SECRET_KEY']
                self.assertEqual(403, s3_request(ip, 'DELETE', 'pmgm-documents', 'qa.bin', backup_user, backup_password)[0])
                stage = 'seed_database_and_actual_backup'
                for database in ('pmgm', 'pmgm_keycloak'):
                    sql(database, 'CREATE TABLE qa_recovery_probe (id int PRIMARY KEY); INSERT INTO qa_recovery_probe VALUES (1);')
                backup = temporary / 'backup'
                run(['bash', 'scripts/backup-srv01-qa.sh', '--env', str(envfile), '--output', str(backup)])
                self.assertEqual(expected_hash, hashlib.sha256((backup / 'objects/pmgm-documents/qa.bin').read_bytes()).hexdigest())
                for database in ('pmgm', 'pmgm_keycloak'):
                    sql(database, 'INSERT INTO qa_recovery_probe VALUES (2);')
                stage = 'restore_refuses_nonempty_bucket_before_sql'
                denied = run(['bash', 'scripts/restore-srv01-qa.sh', '--env', str(envfile),
                              '--backup', str(backup), '--yes', '--data-only'], check=False)
                self.assertNotEqual(0, denied.returncode)
                self.assertEqual('2', sql('pmgm', 'SELECT count(*) FROM qa_recovery_probe;'))
                self.assertIn(s3_request(ip, 'DELETE', 'pmgm-documents', 'qa.bin', user, password)[0], (200, 204))
                stage = 'actual_restore_data_and_verify'
                restored = run(['bash', 'scripts/restore-srv01-qa.sh', '--env', str(envfile),
                                '--backup', str(backup), '--yes', '--data-only'])
                self.assertIn('smoke PENDIENTE', restored.stdout)
                for database in ('pmgm', 'pmgm_keycloak'):
                    self.assertEqual('1', sql(database, 'SELECT count(*) FROM qa_recovery_probe;'))
                status, restored_bytes = s3_request(ip, 'GET', 'pmgm-documents', 'qa.bin', user, password)
                self.assertEqual(200, status)
                self.assertEqual(expected_hash, hashlib.sha256(restored_bytes).hexdigest())
                evidence = Path(os.environ.get('QA_SILO_EVIDENCE_DIR', temporary / 'evidence'))
                evidence.mkdir(parents=True, exist_ok=True)
                (evidence / 'qa-runtime-report.json').write_text(json.dumps({
                    'status': 'PASS_SYNTHETIC_DATA_RECOVERY', 'source_sha': os.getenv('QA_VALIDATED_SOURCE_SHA'),
                    'docker': run(['docker', 'version', '--format', '{{.Server.Version}}']).stdout.strip(),
                    'compose': run(['docker', 'compose', 'version', '--short']).stdout.strip(),
                    'object_sha256': expected_hash, 'document_iam': 'PASS', 'backup_delete_denied': True,
                    'postgres_and_keycloak_data': 'PASS', 'application_oidc_smoke': 'NOT_RUN_BY_THIS_TEST',
                    'scope': 'NO_INSTITUTIONAL_DEPLOYMENT',
                    'storage_supply': 'SIGNED_ARCHIVE' if os.getenv('PMGM_QA_SILO_BUNDLE_DIR') else 'VENDOR_IMAGE'}, indent=2) + '\n')
            finally:
                # Only the UUID-named synthetic project created in this test.
                run(compose + ['down', '--volumes', '--remove-orphans'], check=False, timeout=90)


if __name__ == '__main__':
    unittest.main()
