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
                    'server_binary_preserved': True,
                    'binary_regression': 'CSV_AND_JSON_LINES_COUNT_OVERSIZED_RECORDS_REJECTED',
                    'source_files': {k: {'git_blob': v} for k, v in rebuild.SERVER_BLOBS.items()}}
        vex = rebuild.fixed_component_vex(evidence, image)
        self.assertEqual('fixed', vex['statements'][0]['status'])
        self.assertEqual(rebuild.SERVER_PURL, vex['statements'][0]['products'][0]['@id'])
        self.assertEqual(1, len(vex['statements']))
        for field in ('source_commit', 'fix_commit', 'fix_ancestry_verified', 'server_binary_sha256',
                      'candidate_image_id', 'server_binary_preserved', 'binary_regression', 'source_files'):
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
