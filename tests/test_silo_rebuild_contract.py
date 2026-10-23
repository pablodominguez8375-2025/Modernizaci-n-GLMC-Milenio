"""Negative controls for the review-only rebuild pipeline, not S3 substitutes."""
import importlib.util
from pathlib import Path
from unittest.mock import patch
import unittest
import struct
import zlib
import test_silo_select_record_limit as select_limit

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('rebuild', ROOT / 'scripts/qa/rebuild-silo-candidate.py')
rebuild = importlib.util.module_from_spec(spec)
spec.loader.exec_module(rebuild)


class SiloRebuildContractTests(unittest.TestCase):
    def setUp(self):
        self.addCleanup(patch.stopall)
        patch.object(rebuild, 'SERVER_BINARY', 'c' * 64).start()

    def test_unreviewed_server_hash_blocks_publication(self):
        with patch.object(rebuild, 'SERVER_BINARY', None), self.assertRaisesRegex(ValueError, 'review pending'):
            rebuild.fixed_component_vex({}, 'sha256:' + 'a' * 64)

    def test_scan_requires_known_main_module_and_correct_go_for_both_binaries(self):
        import copy
        inventory = {'Results': [
            {'Target': 'usr/bin/silo', 'Packages': [
                {'Name': 'stdlib', 'Version': 'v1.27.3'},
                {'Name': 'github.com/minio/minio', 'Version': rebuild.SERVER_PURL.rsplit('@', 1)[1]}]},
            {'Target': 'usr/bin/mcli', 'Packages': [{'Name': 'stdlib', 'Version': 'v1.27.3'}]},
        ]}
        rebuild.verify_go_inventory(inventory)
        for index, package in ((0, 0), (0, 1), (1, 0)):
            changed = copy.deepcopy(inventory)
            changed['Results'][index]['Packages'][package]['Version'] = '(devel)'
            with self.subTest(target=index, package=package), self.assertRaises(ValueError):
                rebuild.verify_go_inventory(changed)

    def test_failure_details_do_not_disclose_urls_or_headers(self):
        import urllib.error
        error = urllib.error.HTTPError('https://private.invalid/token', 403, 'secret', {'Authorization': 'secret'}, None)
        self.assertEqual({'type': 'HTTPError', 'http_status': 403}, rebuild.failure_details(error))

    def test_export_builds_only_immutable_git_archive(self):
        import io, tarfile, tempfile, hashlib
        data = io.BytesIO()
        with tarfile.open(fileobj=data, mode='w') as archive:
            item = tarfile.TarInfo('main.go')
            item.size = 6
            archive.addfile(item, io.BytesIO(b'pinned'))
        raw = data.getvalue()
        with tempfile.TemporaryDirectory() as tmp, patch.object(rebuild, 'check_source', return_value=hashlib.sha256(raw).hexdigest()), patch.object(rebuild.subprocess, 'check_output', return_value=raw):
            target = Path(tmp)/'source'
            rebuild.export_source(Path('/checkout'), 'a'*40, target)
            self.assertEqual(b'pinned', (target/'main.go').read_bytes())
            self.assertEqual(['main.go'], [p.name for p in target.iterdir()])

    def test_source_change_between_validation_and_export_blocks(self):
        import tempfile
        with tempfile.TemporaryDirectory() as tmp, patch.object(rebuild, 'check_source', return_value='a'*64), patch.object(rebuild.subprocess, 'check_output', return_value=b'changed'):
            with self.assertRaisesRegex(ValueError, 'archive changed'):
                rebuild.export_source(Path('/checkout'), 'a'*40, Path(tmp)/'source')

    def test_ignored_checkout_file_never_enters_build_context(self):
        import subprocess, tempfile
        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp)/'repo'
            source.mkdir()
            def git(*args):
                return subprocess.check_output(['git', '-C', str(source), *args], stderr=subprocess.DEVNULL)
            git('init')
            git('config', 'user.name', 'Synthetic test')
            git('config', 'user.email', 'test@example.invalid')
            (source/'.gitignore').write_text('local-only.txt\n')
            (source/'main.go').write_text('package main\n')
            git('add', '.')
            git('commit', '-m', 'synthetic pinned source')
            commit = git('rev-parse', 'HEAD').decode().strip()
            (source/'local-only.txt').write_text('synthetic local data')
            target = Path(tmp)/'export'
            rebuild.export_source(source, commit, target)
            self.assertTrue((target/'main.go').is_file())
            self.assertFalse((target/'local-only.txt').exists())
            self.assertFalse((target/'.git').exists())

    def test_wrong_or_dirty_client_source_is_rejected_before_build(self):
        with patch.object(rebuild.subprocess, 'check_output', return_value='bad-sha'):
            with self.assertRaises(ValueError):
                rebuild.check_source(Path('/synthetic'))
        with patch.object(rebuild.subprocess, 'check_output', side_effect=[rebuild.CLIENT_SOURCE, b' M go.mod']):
            with self.assertRaises(ValueError):
                rebuild.check_source(Path('/synthetic'))

    def test_scan_is_fail_closed_and_retains_high_without_fixed_version(self):
        for data in ({}, {'Results': []}, {'Results': None}):
            with self.assertRaises(ValueError):
                rebuild.blocking_findings(data)
        scan = {'Results': [{'Target': 'server', 'Vulnerabilities': [
            {'VulnerabilityID': 'CVE-synthetic', 'Severity': 'HIGH', 'FixedVersion': None},
            {'VulnerabilityID': 'CVE-low', 'Severity': 'LOW'}]}]}
        rows = rebuild.blocking_findings(scan)
        self.assertEqual(1, len(rows))
        self.assertIsNone(rows[0]['FixedVersion'])

    def test_select_event_parser_verifies_crc_and_error_headers(self):
        headers = bytearray()
        for key, value in ((':message-type', 'error'), (':error-code', 'OverMaxRecordSize')):
            name, data = key.encode(), value.encode()
            headers.extend(bytes([len(name)]) + name + bytes([7]) + struct.pack('!H', len(data)) + data)
        prelude = struct.pack('!II', 16 + len(headers), len(headers))
        frame = prelude + struct.pack('!I', zlib.crc32(prelude)) + headers
        frame += struct.pack('!I', zlib.crc32(frame))
        self.assertEqual('OverMaxRecordSize', select_limit.select_events(frame)[0][':error-code'])
        for invalid in (b'', frame[:-1], frame[:-4] + bytes(4)):
            with self.assertRaises(ValueError):
                select_limit.select_events(invalid)

    def test_fixed_vex_rejects_unbound_binary_image_source_or_missing_regression(self):
        image = 'sha256:' + 'a' * 64
        evidence = {'vulnerability': 'CVE-2026-39414', 'source_commit': rebuild.SERVER_SOURCE,
                    'fix_commit': rebuild.SERVER_FIX, 'fix_ancestry_verified': True,
                    'server_binary_sha256': rebuild.SERVER_BINARY, 'candidate_image_id': image,
                    'server_binary_preserved': False,
              'vendor_server_binary_sha256': rebuild.VENDOR_SERVER_BINARY,
              'server_build_toolchain': 'go1.27.3', 'server_source_rebuilt': True,
                    'binary_regression': 'CSV_AND_JSON_LINES_COUNT_OVERSIZED_RECORDS_REJECTED',
                    'source_files': {k: {'git_blob': v} for k, v in rebuild.SERVER_BLOBS.items()}}
        vex = rebuild.fixed_component_vex(evidence, image)
        self.assertEqual('fixed', vex['statements'][0]['status'])
        self.assertEqual(rebuild.SERVER_PURL, vex['statements'][0]['products'][0]['@id'])
        self.assertEqual(1, len(vex['statements']))
        for field in ('source_commit', 'fix_commit', 'fix_ancestry_verified', 'server_binary_sha256',
                      'candidate_image_id', 'server_binary_preserved', 'binary_regression', 'source_files',
                      'vendor_server_binary_sha256', 'server_build_toolchain', 'server_source_rebuilt'):
            changed = dict(evidence)
            changed.pop(field)
            with self.subTest(field=field), self.assertRaises(ValueError):
                rebuild.fixed_component_vex(changed, image)
        with self.assertRaises(ValueError):
            rebuild.fixed_component_vex(evidence, 'latest')

    def test_rebuild_does_not_publish_or_override_active_compose(self):
        dockerfile = (ROOT / 'infrastructure/silo/Dockerfile.qa-rebuild').read_text()
        workflow = (ROOT / '.github/workflows/qa-silo-rebuild.yml').read_text()
        script = (ROOT / 'scripts/qa/rebuild-silo-candidate.py').read_text()
        self.assertIn('go build -mod=readonly', dockerfile)
        self.assertIn('GOTOOLCHAIN=local', dockerfile)
        self.assertIn('go1.27.3', dockerfile)
        self.assertIn('COPY --from=silo-build /out/silo', dockerfile)
        self.assertIn('--server-source artifacts/silo-source', workflow)
        self.assertIn('upgrade pcre2 pcre2-syntax', dockerfile)
        self.assertIn('FROM scratch', dockerfile)
        self.assertNotIn('--push', script)
        self.assertNotIn('packages: write', workflow)
        self.assertNotIn('secrets.', workflow)
        self.assertNotIn('docker-compose.srv01', script)
        self.assertIn('--ignorefile', script)
        self.assertNotIn('--ignore-unfixed', script)
        self.assertIn('--vex', script)
        self.assertIn('unexpected VEX filtering', script)
        self.assertIn('test_silo_dotnet_sdk', script)


if __name__ == '__main__':
    unittest.main()
