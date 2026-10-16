"""Adversarial archive/policy controls; real signatures/Compose run in GitHub."""
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('archive', ROOT / 'scripts/qa/prepare-silo-archive.py')
archive = importlib.util.module_from_spec(spec)
spec.loader.exec_module(archive)
spec = importlib.util.spec_from_file_location('guard', ROOT / 'scripts/check-qa-storage-config.py')
guard = importlib.util.module_from_spec(spec)
spec.loader.exec_module(guard)
SOURCE = 'a' * 40
WORKFLOW_SHA = 'b' * 40


def fixture(directory):
    rebuild = archive.module()
    config = json.dumps({'os': 'linux', 'architecture': 'amd64'}).encode()
    image = 'sha256:' + hashlib.sha256(config).hexdigest()
    with tarfile.open(directory / 'silo-qa-review-only.tar', 'w') as tar:
        for name, data in [('config.json', config), ('manifest.json', b'[{"Config":"config.json","Layers":[]}]')]:
            member = tarfile.TarInfo(name)
            member.size = len(data)
            tar.addfile(member, io.BytesIO(data))
    triage = {'vulnerability': 'CVE-2026-39414', 'source_commit': rebuild.SERVER_SOURCE,
              'fix_commit': rebuild.SERVER_FIX, 'fix_ancestry_verified': True,
              'server_binary_sha256': rebuild.SERVER_BINARY, 'candidate_image_id': image,
              'server_binary_preserved': True,
              'binary_regression': 'CSV_AND_JSON_LINES_COUNT_OVERSIZED_RECORDS_REJECTED',
              'source_files': {k: {'git_blob': v} for k, v in rebuild.SERVER_BLOBS.items()}}
    objects = {'server-cve-triage.json': triage,
               'server-fixed.openvex.json': rebuild.fixed_component_vex(triage, image),
               'trivy-raw.json': {'Results': [{'Target': 'synthetic', 'Vulnerabilities': []}]},
               'trivy-reviewed.json': {'Results': [{'Target': 'synthetic', 'Vulnerabilities': []}]},
               'sbom.cdx.json': {'synthetic': True}, 'runtime-binaries-and-rpms.txt': 'synthetic',
               'project-provenance.sigstore.json': {'test-only': True}}
    for name, data in objects.items():
        (directory / name).write_text(json.dumps(data))
    report = {'source_sha': SOURCE, 'workflow_sha': WORKFLOW_SHA,
              'status': 'CANDIDATE_VALIDATED_NOT_DEPLOYED', 'scope': 'SYNTHETIC_QA_RUNNER_ONLY',
              'failures': {}, 'blocking_findings': [], 'effective_blocking_findings': [],
              'real_iam_recovery_sdk': {'status': 'PASS'}, 'vendor_signature_verified': True,
              'local_image_id': image, 'server_cve_triage': triage,
              'archive_sha256': hashlib.sha256((directory / 'silo-qa-review-only.tar').read_bytes()).hexdigest(),
              'evidence_sha256': {n: hashlib.sha256((directory / n).read_bytes()).hexdigest() for n in (
                  'trivy-raw.json', 'trivy-reviewed.json', 'sbom.cdx.json', 'runtime-binaries-and-rpms.txt')}}
    (directory / 'rebuild-report.json').write_text(json.dumps(report))
    return report


class SiloArchiveContractTests(unittest.TestCase):
    def test_verifier_policy_binds_workflow_merge_and_signed_checkout_separately(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(archive, 'run') as verify:
            directory = Path(tmp)
            report = fixture(directory)
            self.assertEqual(report, archive.verify_bundle(directory, SOURCE))
            self.assertEqual(4, verify.call_count)
            for call in verify.call_args_list:
                command = call.args[0]
                self.assertEqual('gh', command[0])
                self.assertIn('--deny-self-hosted-runners', command)
                self.assertEqual(WORKFLOW_SHA, command[command.index('--source-digest') + 1])
                self.assertEqual(archive.WORKFLOW, command[command.index('--signer-workflow') + 1])

    def test_bad_signature_blocks_before_docker(self):
        with tempfile.TemporaryDirectory() as tmp:
            directory = Path(tmp)
            fixture(directory)
            with patch.object(archive, 'run', side_effect=subprocess.CalledProcessError(1, 'gh')) as verify:
                with self.assertRaises(subprocess.CalledProcessError):
                    archive.prepare(directory, SOURCE, directory / 'compose.json')
                self.assertEqual(1, verify.call_count)
                self.assertFalse((directory / 'compose.json').exists())

    def test_source_gate_evidence_tar_and_binary_mismatches_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(archive, 'run'):
            directory = Path(tmp)
            original = fixture(directory)
            for field, invalid in [('source_sha', 'c' * 40), ('workflow_sha', ''), ('status', 'BLOCKED'),
                                   ('archive_sha256', '0' * 64), ('local_image_id', 'latest'),
                                   ('effective_blocking_findings', [{}]), ('evidence_sha256', {})]:
                report = copy.deepcopy(original)
                report[field] = invalid
                (directory / 'rebuild-report.json').write_text(json.dumps(report))
                with self.subTest(field=field), self.assertRaises(ValueError):
                    archive.verify_bundle(directory, SOURCE)
            fixture(directory)
            (directory / 'silo-qa-review-only.tar').write_bytes(b'changed')
            with self.assertRaises(ValueError):
                archive.verify_bundle(directory, SOURCE)
            fixture(directory)
            vex = json.loads((directory / 'server-fixed.openvex.json').read_text())
            vex['statements'][0]['products'][0]['hashes']['sha-256'] = '0' * 64
            (directory / 'server-fixed.openvex.json').write_text(json.dumps(vex))
            with self.assertRaises(ValueError):
                archive.verify_bundle(directory, SOURCE)

    def test_unsigned_inventory_and_symlink_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(archive, 'run'):
            directory = Path(tmp)
            fixture(directory)
            (directory / 'trivy-reviewed.json').write_text('{"Results":[]}')
            with self.assertRaises(ValueError):
                archive.verify_bundle(directory, SOURCE)
            fixture(directory)
            target = directory / 'trivy-raw.json'
            target.unlink()
            target.symlink_to(directory / 'trivy-reviewed.json')
            with self.assertRaises(ValueError):
                archive.verify_bundle(directory, SOURCE)

    def test_config_id_requires_verified_binding_all_clients_and_no_pull(self):
        image = 'sha256:' + 'a' * 64
        model = {'services': {
            'api': {'environment': {'DocumentStorage__AccessKey': 'synthetic-docs',
                    'DocumentStorage__SecretKey': 'synthetic-secret', 'DocumentStorage__Bucket': 'pmgm-documents',
                    'DocumentStorage__Region': 'us-east-1', 'DocumentStorage__ForcePathStyle': 'true',
                    'DocumentStorage__ServiceUrl': 'http://minio:9000'}},
            **{n: {'image': image, 'pull_policy': 'never'} for n in archive.SERVICES}}}
        self.assertTrue(guard.evaluate(model))
        self.assertEqual([], guard.evaluate(model, image))
        for name in archive.SERVICES:
            bad = copy.deepcopy(model)
            bad['services'][name]['image'] = 'sha256:' + 'b' * 64
            self.assertTrue(guard.evaluate(bad, image))
            bad = copy.deepcopy(model)
            bad['services'][name]['pull_policy'] = 'always'
            self.assertTrue(guard.evaluate(bad, image))


if __name__ == '__main__':
    unittest.main()
