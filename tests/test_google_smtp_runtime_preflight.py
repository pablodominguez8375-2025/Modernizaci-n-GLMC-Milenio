"""Synthetic QA tests: no real credentials, Docker, SSH, SMTP or network."""
import base64
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/qa/check-google-smtp-runtime.py"
MAPPING = "SystemMail__EncryptionKey: " + "$" + "{PMGM_SMTP_ENCRYPTION_KEY:-}"
KEY = base64.b64encode(bytes(range(32))).decode("ascii")


class SmtpPreflightTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.folder = Path(self.tmp.name)
        self.env = self.folder / "srv01.env"
        self.compose = self.folder / "qa.yml"
        self.write_env("PMGM_SMTP_ENCRYPTION_KEY=" + KEY)
        self.compose.write_text("services:\n  api:\n    environment:\n      " +
                                MAPPING + "\n  keycloak:\n    environment:\n      SAFE: value\n")

    def write_env(self, text, mode=0o600):
        self.env.write_text(text + "\n")
        self.env.chmod(mode)

    def run_probe(self):
        return subprocess.run(
            [sys.executable, str(SCRIPT), "--env-file", str(self.env),
             "--compose-file", str(self.compose)],
            text=True, capture_output=True, check=False, timeout=10)

    def test_ready_never_reveals_key(self):
        result = self.run_probe()
        self.assertEqual(0, result.returncode, result.stdout)
        self.assertIn("READY_STATIC_ONLY", result.stdout)
        self.assertNotIn(KEY, result.stdout + result.stderr)

    def test_missing_duplicate_invalid_and_all_zero_rejected(self):
        bad = ("OTHER_VALUE=x",
               "PMGM_SMTP_ENCRYPTION_KEY=" + KEY + "\nPMGM_SMTP_ENCRYPTION_KEY=" + KEY,
               "PMGM_SMTP_ENCRYPTION_KEY=placeholder",
               "PMGM_SMTP_ENCRYPTION_KEY=" + base64.b64encode(bytes(32)).decode(),
               "PMGM_SMTP_ENCRYPTION_KEY=" + "$" + "{HOST_SECRET}")
        for value in bad:
            with self.subTest(length=len(value)):
                self.write_env(value)
                result = self.run_probe()
                self.assertEqual(1, result.returncode)
                self.assertNotIn(KEY, result.stdout + result.stderr)

    def test_private_permissions_and_regular_file(self):
        self.write_env("PMGM_SMTP_ENCRYPTION_KEY=" + KEY, 0o644)
        self.assertIn("ENV_PERMISSIONS_UNSAFE", self.run_probe().stdout)
        self.env.unlink()
        target = self.folder / "target"
        target.write_text("PMGM_SMTP_ENCRYPTION_KEY=" + KEY)
        self.env.symlink_to(target)
        self.assertIn("ENV_SYMLINK_FORBIDDEN", self.run_probe().stdout)

    def test_compose_mapping_cannot_expose_keycloak(self):
        self.compose.write_text("services:\n  api:\n    environment:\n      " +
                                MAPPING + "\n  keycloak:\n    environment:\n      " + MAPPING + "\n")
        self.assertIn("SMTP_KEY_MAPPING_NOT_API_ONLY", self.run_probe().stdout)

    def test_live_repository_compose_structure(self):
        self.compose.write_text((ROOT / "infrastructure/docker-compose.srv01.yml").read_text())
        self.assertEqual(0, self.run_probe().returncode)


if __name__ == "__main__":
    unittest.main()
