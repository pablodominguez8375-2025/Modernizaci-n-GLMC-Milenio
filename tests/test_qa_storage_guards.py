"""No server, S3 mock, image, volume or live credential is used by these guards."""
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('qa_storage_guard', ROOT / 'scripts/check-qa-storage-config.py')
guard = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(guard)
PIN = 'registry.example.invalid/storage@sha256:' + 'a' * 64


def fixture():
    return {'services': {
        'api': {'environment': {'DocumentStorage__ServiceUrl': 'http://storage:9000',
                               'DocumentStorage__Bucket': 'pmgm-documents',
                               'DocumentStorage__Region': 'us-east-1',
                               'DocumentStorage__ForcePathStyle': 'true',
                               'DocumentStorage__AccessKey': 'synthetic-document-user',
                               'DocumentStorage__SecretKey': 'synthetic-document-secret'}},
        'storage': {'image': PIN, 'environment': {'MINIO_ROOT_USER': 'synthetic-admin',
                                                'MINIO_ROOT_PASSWORD': 'synthetic-admin-secret'}}}}


class QaStorageConfigTests(unittest.TestCase):
    def test_separate_identity_pinned_internal_service_is_config_only(self):
        self.assertEqual([], guard.evaluate(fixture()))

    def test_external_real_s3_endpoint_needs_no_local_storage_image(self):
        config = fixture()
        del config['services']['storage']
        config['services']['api']['environment']['DocumentStorage__ServiceUrl'] = 'https://s3.example.invalid'
        self.assertEqual([], guard.evaluate(config))

    def test_blocks_reuse_of_either_admin_component(self):
        for field, value in [('AccessKey', 'synthetic-admin'), ('SecretKey', 'synthetic-admin-secret')]:
            config = fixture()
            config['services']['api']['environment']['DocumentStorage__' + field] = value
            self.assertIn('ADMIN_CREDENTIAL_REUSED_BY_API', guard.evaluate(config))

    def test_blocks_mutable_and_historical_server_images(self):
        for image in ['registry.example.invalid/storage:stable', 'registry.example.invalid/storage:latest',
                      'minio/minio@sha256:' + 'b' * 64, 'quay.io/minio/minio:RELEASE.2025-09-07T16-13-09Z']:
            config = fixture()
            config['services']['storage']['image'] = image
            self.assertTrue(guard.evaluate(config))

    def test_blocks_wrong_bucket_admin_env_and_external_cleartext(self):
        for key, value, error in [
            ('DocumentStorage__Bucket', 'another-bucket', 'DOCUMENT_BUCKET_REQUIRED'),
            ('MINIO_ROOT_PASSWORD', 'synthetic', 'ADMIN_CREDENTIAL_IN_API'),
            ('DocumentStorage__ServiceUrl', 'http://external.invalid', 'EXTERNAL_S3_REQUIRES_TLS'),
            ('DocumentStorage__ServiceUrl', 'https://user:secret@external.invalid', 'INVALID_S3_ENDPOINT'),
            ('DocumentStorage__AccessKey', '', 'DOCUMENT_CREDENTIALS_MISSING')]:
            config = fixture()
            config['services']['api']['environment'][key] = value
            self.assertIn(error, guard.evaluate(config))

    def test_cli_never_prints_expanded_credentials_or_invalid_input(self):
        for config in [fixture(), {'services': 'synthetic-secret-malformed'}]:
            result = subprocess.run([sys.executable, str(ROOT / 'scripts/check-qa-storage-config.py'), '--stdin'],
                                    input=json.dumps(config), text=True, capture_output=True, check=False)
            self.assertNotIn('synthetic-', result.stdout + result.stderr)
            self.assertEqual('NO_IAM_OR_DEPLOYMENT_CERTIFICATION', json.loads(result.stdout)['scope'])

    def test_restore_stops_before_any_runtime_or_database_mutation(self):
        with tempfile.TemporaryDirectory() as directory:
            temp = Path(directory)
            backup = temp / 'backup'
            backup.mkdir()
            QaBackupIntegrityTests().create_backup(backup)
            envfile = temp / 'qa.env'
            envfile.write_text('# No live credentials\n')
            binary = temp / 'bin'
            binary.mkdir()
            config = fixture()
            config['services']['api']['environment']['DocumentStorage__AccessKey'] = 'synthetic-admin'
            fake = binary / 'docker'
            fake.write_text('#!/usr/bin/env python3\nimport sys\nfrom pathlib import Path\n'
                            'if "config" in sys.argv:\n print(' + repr(json.dumps(config)) + ')\n'
                            'else:\n Path(' + repr(str(temp / 'mutation')) + ').touch()\n sys.exit(99)\n')
            fake.chmod(0o755)
            result = subprocess.run(['bash', str(ROOT / 'scripts/restore-srv01-qa.sh'),
                                     '--env', str(envfile), '--backup', str(backup), '--yes'],
                                    env=dict(os.environ, PATH=str(binary) + ':' + os.environ['PATH']),
                                    text=True, capture_output=True, check=False, timeout=30)
            self.assertNotEqual(0, result.returncode)
            self.assertIn('ADMIN_CREDENTIAL_REUSED_BY_API', result.stdout)
            self.assertFalse((temp / 'mutation').exists())

    def test_installer_stops_before_pull_build_or_up_with_unsafe_config(self):
        with tempfile.TemporaryDirectory() as directory:
            temp = Path(directory)
            binary = temp / 'bin'
            binary.mkdir()
            config = fixture()
            config['services']['storage']['image'] = 'registry.example.invalid/storage:latest'
            fake = binary / 'docker'
            fake.write_text('#!/usr/bin/env python3\nimport sys,json\nfrom pathlib import Path\n'
                            'args=sys.argv[1:]\n'
                            'if "config" in args:\n print(' + repr(json.dumps(config)) + ')\n'
                            'elif "pull" in args or "build" in args or "up" in args:\n'
                            ' Path(' + repr(str(temp / 'mutation')) + ').touch()\n sys.exit(99)\n')
            fake.chmod(0o755)
            envfile = temp / 'qa.env'
            values = dict(PMGM_QA_WEB_PUBLIC_URL='https://web.example.invalid',
                          PMGM_QA_OIDC_PUBLIC_URL='https://identity.example.invalid',
                          PMGM_QA_DB_PASSWORD='synthetic-db-password-0001',
                          PMGM_QA_MINIO_USER='synthetic-admin',
                          PMGM_QA_MINIO_PASSWORD='synthetic-storage-password-0002',
                          PMGM_QA_KEYCLOAK_ADMIN_PASSWORD='synthetic-oidc-password-0003',
                          PMGM_QA_USER_PASSWORD='synthetic-user-password-0004')
            envfile.write_text(''.join(k + '=' + v + '\n' for k, v in values.items()))
            envfile.chmod(0o600)
            result = subprocess.run(['bash', str(ROOT / 'scripts/install-srv01-qa.sh'),
                                     '--env', str(envfile), '--skip-smoke'],
                                    env=dict(os.environ, PATH=str(binary) + ':' + os.environ['PATH']),
                                    text=True, capture_output=True, check=False, timeout=30)
            self.assertNotEqual(0, result.returncode)
            self.assertIn('STORAGE_IMAGE_DIGEST_REQUIRED', result.stdout)
            self.assertFalse((temp / 'mutation').exists())
            self.assertNotIn('synthetic-storage-password', result.stdout + result.stderr)


class QaBackupIntegrityTests(unittest.TestCase):
    def create_backup(self, root):
        entries = []
        for name in ['database/pmgm.dump', 'database/pmgm_keycloak.dump', 'objects/pmgm-documents/object.bin']:
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b'synthetic-backup-bytes')
            entries.append(dict(path=name, sizeBytes=path.stat().st_size,
                                sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        self.write_manifest(root, entries)
        return entries

    def write_manifest(self, root, entries):
        (root / 'manifest.json').write_text(json.dumps(dict(formatVersion=1, project='PMGM', stack='qa-srv01',
            createdAtUtc='2026-10-16T03:00:00Z', sourceSha='a' * 40, files=entries,
            fileCount=len(entries), totalBytes=sum(e['sizeBytes'] for e in entries))))

    def verify(self, root):
        return subprocess.run(['bash', str(ROOT / 'scripts/verify-srv01-backup.sh'), str(root)],
                              capture_output=True, text=True, check=False, timeout=10)

    def test_accepts_existing_manifest_contract(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.create_backup(root)
            self.assertEqual(0, self.verify(root).returncode)

    def test_rejects_changed_bytes_missing_dump_and_extra_file(self):
        for mutation in ('bytes', 'missing', 'extra'):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                self.create_backup(root)
                if mutation == 'bytes':
                    (root / 'database/pmgm.dump').write_bytes(b'altered')
                elif mutation == 'missing':
                    (root / 'database/pmgm.dump').unlink()
                else:
                    (root / 'unexpected').touch()
                self.assertNotEqual(0, self.verify(root).returncode)

    def test_rejects_symlink_in_parent_even_if_target_hash_matches(self):
        with tempfile.TemporaryDirectory() as temp, tempfile.TemporaryDirectory() as outside:
            root, target = Path(temp), Path(outside)
            self.create_backup(root)
            (target / 'pmgm-documents').mkdir()
            obj = root / 'objects/pmgm-documents/object.bin'
            (target / 'pmgm-documents/object.bin').write_bytes(obj.read_bytes())
            obj.unlink()
            obj.parent.rmdir()
            (root / 'objects').rmdir()
            (root / 'objects').symlink_to(target, target_is_directory=True)
            self.assertNotEqual(0, self.verify(root).returncode)

    def test_rejects_duplicate_traversal_and_noncanonical_paths(self):
        for relative in ['../outside', '/tmp/outside', './database/pmgm.dump', 'database//pmgm.dump']:
            with self.subTest(path=relative), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                entries = self.create_backup(root)
                entries[0]['path'] = relative
                self.write_manifest(root, entries)
                self.assertNotEqual(0, self.verify(root).returncode)
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            entries = self.create_backup(root)
            self.write_manifest(root, entries + [copy.deepcopy(entries[0])])
            self.assertNotEqual(0, self.verify(root).returncode)

    def test_rejects_hidden_nested_manifest_and_manifest_symlink(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.create_backup(root)
            (root / 'database/manifest.json').write_text('unlisted')
            self.assertNotEqual(0, self.verify(root).returncode)
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.create_backup(root)
            (root / 'manifest.json').rename(root / 'original')
            (root / 'manifest.json').symlink_to(root / 'original')
            self.assertNotEqual(0, self.verify(root).returncode)


if __name__ == '__main__':
    unittest.main()
