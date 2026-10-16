"""Negative controls for the review-only rebuild pipeline, not S3 substitutes."""
import importlib.util
from pathlib import Path
from unittest.mock import patch
import unittest

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
        self.assertNotIn('--vex', script)
        self.assertIn('test_silo_dotnet_sdk', script)


if __name__ == '__main__':
    unittest.main()
