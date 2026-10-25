"""G8 private LAB2 audit: offline contract tests; no Docker, DB or HTTP."""
import ast
import base64
import importlib.util
import json
from pathlib import Path
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / 'scripts/qa/g8-lab2-identity-rbac-probe.py'
SPEC = importlib.util.spec_from_file_location('qa_g8_lab2_probe', SCRIPT)
PROBE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PROBE)

class QaG8Lab2ProbeTests(unittest.TestCase):
    def test_script_is_python_and_pinned_private(self):
        source = SCRIPT.read_text()
        ast.parse(source)
        self.assertEqual(PROBE.PROJECT, 'centenario-qa-lab2')
        self.assertEqual(PROBE.SHA, '4ef456fee3a578a9e6418f3f7072af5dd79eb045')
        self.assertIn("('127.0.0.1',18443)", source)
        self.assertIn('pmgm_keycloak', source)
        self.assertIn('core.member_identity_links', source)
        self.assertIn('core.office_assignments', source)
        self.assertIn('G8_FIXTURES_NOT_READY', source)

    def test_sensitive_or_mutating_paths_rejected_before_io(self):
        for method,path in (('DELETE','/api/calendar'),
                            ('POST','/api/calendar'),
                            ('GET','/identity/admin'),
                            ('GET','https://untrusted.example/')):
            with self.subTest(method=method,path=path):
                with self.assertRaises(PROBE.Blocked), patch.object(PROBE,'LoopbackTLS') as tls:
                    PROBE.request(method,path)
                tls.assert_not_called()

    def test_fake_jwt_decoder_and_structure(self):
        payload={'sub':'aa820001-0000-4000-8000-000000000011',
                 'pmgm_scope':'organization','iss':PROBE.ISSUER}
        value=base64.urlsafe_b64encode(json.dumps(payload).encode()).decode().rstrip('=')
        self.assertEqual(payload, PROBE.claims('a.'+value+'.sig'))
        with self.assertRaises(PROBE.Blocked):
            PROBE.claims('not-a-jwt')

    def test_expected_synthetic_roles_no_conflicts(self):
        self.assertGreaterEqual(len(PROBE.PROFILES), 8)
        for user,index,scope,allowed,denied in PROBE.PROFILES:
            with self.subTest(user=user):
                self.assertTrue(user.startswith('qa.'))
                self.assertFalse(set(allowed) & set(denied))
                self.assertIn(scope,('order','organization'))
                self.assertEqual(scope,'organization' if index<=14 else 'order')
        self.assertEqual(19, len(set(range(1,20))))

    def test_office_boolean_text_cast_regression(self):
        """psql SELECT (boolean)::text returns 'true'/'false', not 't'/'f'."""
        source = SCRIPT.read_text()
        self.assertIn("offices=='true'", source)
        self.assertNotIn("offices=='t'", source)
        canonical = (ROOT / 'scripts/qa/check-g8-identities.sh').read_text()
        self.assertIn('[ "$offices" = true ]', canonical)
        with patch.object(PROBE,'scalar',side_effect=['19','18','t']), \
             patch('builtins.print'):
            with self.assertRaisesRegex(PROBE.Blocked,'G8_FIXTURES_NOT_READY'):
                PROBE.check_institutional_data()

    def test_full_database_inventory_pass_and_fail(self):
        with patch.object(PROBE,'scalar',side_effect=['19','18','true']), \
             patch('builtins.print'):
            PROBE.check_institutional_data()
        with patch.object(PROBE,'scalar',side_effect=['19','0','true']), \
             patch('builtins.print'):
            with self.assertRaisesRegex(PROBE.Blocked,'G8_FIXTURES_NOT_READY'):
                PROBE.check_institutional_data()

if __name__ == '__main__':
    unittest.main()
