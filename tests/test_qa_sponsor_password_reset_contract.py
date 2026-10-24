"""QA Sponsor login self-service: no secret leakage and fixed target."""
import ast
from pathlib import Path
import unittest

SOURCE = Path(__file__).resolve().parents[1] / "scripts/qa/reset-sponsor-taller23.py"
TEXT = SOURCE.read_text()


class SponsorQaResetContract(unittest.TestCase):
    def test_syntax(self):
        ast.parse(TEXT)

    def test_only_synthetic_account_and_exact_uuid(self):
        self.assertIn('TARGET_USERNAME = "qa.taller23"', TEXT)
        self.assertIn('TARGET_ID = "aa820001-0000-4000-8000-000000000002"', TEXT)
        self.assertIn('users/aa820001-0000-4000-8000-000000000002/reset-password', TEXT)
        self.assertIn('PRECHECK_WRONG_USER', TEXT)

    def test_password_hidden_and_only_stdin(self):
        self.assertIn('getpass.getpass(', TEXT)
        self.assertIn('input=payload', TEXT)
        self.assertIn('capture_output=True', TEXT)
        self.assertNotIn('print(pw)', TEXT)
        self.assertNotIn('print(payload)', TEXT)
        self.assertNotIn('PMGM_QA_USER_PASSWORD', TEXT)
        self.assertIn('No pegues ni compartas esa clave', TEXT)

    def test_admin_endpoint_internal_only(self):
        self.assertIn('http://127.0.0.1:8080/identity', TEXT)
        self.assertNotIn('https://sistema.granlogiamixta.cl/identity/admin', TEXT)
        self.assertIn('com.docker.compose.service=', TEXT)
        self.assertIn('PRECHECK_ADMIN_MISSING', TEXT)
        self.assertIn("trap 'rm -f", TEXT)

    def test_policy_not_shared_across_users(self):
        self.assertIn('temporary": False', TEXT)
        self.assertIn('len(pw) < 20', TEXT)
        self.assertNotIn('docker compose down', TEXT)
        self.assertNotIn('docker restart', TEXT)


if __name__ == "__main__":
    unittest.main()
