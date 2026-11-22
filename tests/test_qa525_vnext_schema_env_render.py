"""Offline negative tests: no root IO, no secrets in failures."""
import importlib.util
from pathlib import Path
import unittest

path = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-schema-env-render.py"
spec = importlib.util.spec_from_file_location("qa525_schema_env_render", path)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

HISTORY = [f"20250101{i:06d}_Migration{i:03d}" for i in range(100)]
SOURCE = b"PMGM_QA_MIGRATOR_DB_PASSWORD=synthetic-secret-123\nPMGM_QA_DB_PASSWORD=ignored\n"

class SchemaEnvRenderTests(unittest.TestCase):
    def test_exact_docker_env_keys(self):
        migrator, grants = mod.render(SOURCE, HISTORY)
        self.assertEqual([x.split(b"=",1)[0] for x in migrator.splitlines()],
                         [b"QA525_DDL_CONNECTION", b"QA525_EXPECTED_EF_HISTORY"])
        self.assertEqual(grants, b"PGPASSWORD=synthetic-secret-123\n")
        self.assertIn(b"Username=pmgm_migrator;Password=synthetic-secret-123", migrator)
        self.assertIn(b'["20250101', migrator)

    def test_equals_and_dollar_are_literal(self):
        source = b"PMGM_QA_MIGRATOR_DB_PASSWORD=syn=$the$tic==\n"
        migrator, grants = mod.render(source, HISTORY)
        self.assertIn(b"Password=syn=$the$tic==\n", migrator)
        self.assertEqual(grants, b"PGPASSWORD=syn=$the$tic==\n")

    def test_password_missing_duplicate_or_unsafe(self):
        for raw in (b"OTHER=value\n", SOURCE + SOURCE, b"PMGM_QA_MIGRATOR_DB_PASSWORD=\n",
                    b"PMGM_QA_MIGRATOR_DB_PASSWORD=bad;evil\n",
                    b"PMGM_QA_MIGRATOR_DB_PASSWORD=bad space\n",
                    b"PMGM_QA_MIGRATOR_DB_PASSWORD=bad\\slash\n"):
            with self.subTest(raw=raw[:20]), self.assertRaises(mod.Stop) as error:
                mod.render(raw, HISTORY)
            self.assertNotIn("synthetic-secret", str(error.exception))

    def test_history_must_be_exact_100(self):
        for history in (HISTORY[:99], HISTORY + HISTORY[:1], list(reversed(HISTORY)), ["bad"] * 100):
            with self.assertRaisesRegex(mod.Stop, "SCHEMA_ENV_HISTORY_INVALID"):
                mod.render(SOURCE, history)

    def test_no_secret_in_diagnostics(self):
        with self.assertRaises(mod.Stop) as error:
            mod.render(b"PMGM_QA_MIGRATOR_DB_PASSWORD=synthetic-secret-123;DROP\n", HISTORY)
        self.assertNotIn("synthetic-secret", str(error.exception))

if __name__ == "__main__":
    unittest.main()
