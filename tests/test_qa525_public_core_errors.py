"""Hermetic public-error boundary regressions; no host or network access."""
import importlib.util
from pathlib import Path
import sys
import types
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
ENTRY = ROOT / "scripts/qa/operators/qa525-supervised-dual-entrypoint.py"
if not ENTRY.exists():
    ENTRY = ROOT / "qa525-supervised-dual-entrypoint.py"
spec = importlib.util.spec_from_file_location("entry_error_test", ENTRY)
entry = importlib.util.module_from_spec(spec)
spec.loader.exec_module(entry)


class CoreBlocked(Exception):
    pass


class PublicCoreErrors(unittest.TestCase):
    def test_known_core_codes_survive(self):
        with patch.dict(sys.modules, {"qa525_supervised_dual_core":
                        types.SimpleNamespace(Blocked=CoreBlocked)}):
            for code in entry.PUBLIC_CORE_FAILURES:
                with self.subTest(code=code):
                    self.assertEqual(entry.public_failure_code(CoreBlocked(code)), code)

    def test_unknown_core_text_is_redacted(self):
        with patch.dict(sys.modules, {"qa525_supervised_dual_core":
                        types.SimpleNamespace(Blocked=CoreBlocked)}):
            self.assertEqual(entry.public_failure_code(CoreBlocked("secret=synthetic")),
                             "SUPERVISED_APPLY_NOT_COMPLETED")

    def test_untyped_exception_cannot_spoof_core_code(self):
        self.assertEqual(entry.public_failure_code(
            RuntimeError("BASELINE_CHANGED_AT_ACTIVATION")),
            "SUPERVISED_APPLY_NOT_COMPLETED")

    def test_existing_entry_and_github_codes_preserved(self):
        self.assertEqual(entry.public_failure_code(entry.Blocked("PRIVATE_EVIDENCE_UNTRUSTED")),
                         "PRIVATE_EVIDENCE_UNTRUSTED")
        self.assertEqual(entry.public_failure_code(RuntimeError("GITHUB_API_RATE_LIMITED")),
                         "GITHUB_API_RATE_LIMITED")


if __name__ == "__main__":
    unittest.main()
