"""Safety contract for one-account-only QA administrator onboarding."""
from pathlib import Path
import ast
import unittest

ROOT = Path(__file__).resolve().parents[1]
ADMIN = (ROOT / "scripts/qa/reset-sponsor-admin.py").read_text()
OTHER = (ROOT / "scripts/qa/reset-sponsor-taller23.py").read_text()


class QAAdminAccessContract(unittest.TestCase):
    def test_python_syntax(self):
        ast.parse(ADMIN)

    def test_identity_is_exact_synthetic_admin(self):
        self.assertIn('TARGET_USERNAME = "qa.admin"', ADMIN)
        self.assertIn('TARGET_ID = "aa820001-0000-4000-8000-000000000001"', ADMIN)
        self.assertIn("users/aa820001-0000-4000-8000-000000000001/reset-password", ADMIN)
        self.assertIn("PRECHECK_ADMIN_ROLE", ADMIN)
        self.assertIn('platform_superadmin', ADMIN)
        self.assertIn('socket.gethostname().split(".")[0] != "glmdch"', ADMIN)
        self.assertIn('com.docker.compose.service=', ADMIN)
        self.assertIn('qa-keycloak-admin', ADMIN)

    def test_no_logs_or_password_args(self):
        self.assertIn("getpass.getpass(", ADMIN)
        self.assertIn("input=payload", ADMIN)
        self.assertIn("capture_output=True", ADMIN)
        self.assertNotIn("print(payload)", ADMIN)
        self.assertNotIn("print(pw)", ADMIN)
        self.assertNotIn("PMGM_QA_USER_PASSWORD", ADMIN)
        self.assertIn("KC_CLI_PASSWORD", ADMIN)
        self.assertIn("trap 'rm -f", ADMIN)
        self.assertIn("--config", ADMIN)

    def test_does_not_expand_access_to_admin_console(self):
        self.assertIn("http://127.0.0.1:8080/identity", ADMIN)
        self.assertNotIn("ufw allow", ADMIN)
        self.assertNotIn("0.0.0.0", ADMIN)
        self.assertNotIn("docker restart", ADMIN)
        self.assertNotIn("docker compose down", ADMIN)

    def test_existing_taller_script_is_unchanged(self):
        self.assertIn('TARGET_USERNAME = "qa.taller23"', OTHER)
        self.assertIn("aa820001-0000-4000-8000-000000000002", OTHER)


if __name__ == "__main__":
    unittest.main()
