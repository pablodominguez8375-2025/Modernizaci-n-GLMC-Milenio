"""Hermetic public-error boundary regressions; no host or network access."""
import importlib.util
from pathlib import Path
import sys
import types
import json
import tempfile
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
    def test_real_transaction_failure_before_apply_keeps_pending_without_run(self):
        self.transaction_failure(before_apply=True)

    def test_real_transaction_failure_after_start_still_rolls_both_targets_back(self):
        self.transaction_failure(before_apply=False)

    def transaction_failure(self, before_apply):
        core_path = ENTRY.with_name("qa525-supervised-dual-transaction.py")
        spec = importlib.util.spec_from_file_location("error_transaction_test", core_path)
        core = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(core)
        before = {name: {"image": "old-" + name, "status": "running"} for name in
                  core.TARGETS + core.DEPENDENCIES + core.ONESHOTS}
        new = {"api": "new-api", "web": "new-web"}
        calls = []
        def fail():
            raise RuntimeError("synthetic-private-detail")
        def evidence(phase, *args):
            if phase == "just-before-apply" and before_apply:
                entry.checked_evidence_step(phase, "SOURCE", fail)
            return True
        def run(args):
            calls.append(args[-1])
            if len(calls) == 1 and not before_apply:
                raise RuntimeError("synthetic-docker-error")
        with tempfile.TemporaryDirectory() as d:
            with patch.object(core, "root_path", side_effect=lambda p, **k: Path(p)), \
                 patch.object(core, "baseline_contract"), \
                 patch.object(core, "make_override", return_value="override.json"):
                with self.assertRaises(Exception):
                    core.transact(before=before, new=new, source_sha="a" * 40,
                        layers={"api": ("base", "api"), "web": ("base", "web")},
                        workdir=d, state_dir=d, run=run,
                        live_snapshot=lambda: before, evidence=evidence,
                        health=lambda: True, audit=lambda *a: None)
            result = json.loads((Path(d) / ("dual-" + "a" * 40 + ".json")).read_text())
            self.assertEqual(result["status"], "PENDING" if before_apply else "ROLLED_BACK")
            self.assertEqual(calls, [] if before_apply else ["api", "web", "api"])

    def test_evidence_stage_and_redaction(self):
        for phase in ("pre", "just-before-apply", "post"):
            for step in ("SOURCE", "APPROVAL", "REVIEW", "DATABASE", "G6"):
                self.assertTrue(entry.checked_evidence_step(phase, step, lambda: True))
                with self.assertRaises(entry.Blocked):
                    entry.checked_evidence_step(phase, step, lambda: False)
                def secret_failure():
                    raise RuntimeError("password=synthetic")
                with self.assertRaises(entry.Blocked) as caught:
                    entry.checked_evidence_step(phase, step, secret_failure)
                self.assertNotIn("password", str(caught.exception))
                self.assertIn(step, str(caught.exception))

    def test_evidence_network_code(self):
        def limited():
            raise RuntimeError("GITHUB_API_RATE_LIMITED")
        with self.assertRaises(entry.Blocked) as caught:
            entry.checked_evidence_step("just-before-apply", "SOURCE", limited)
        self.assertEqual(str(caught.exception),
            "EVIDENCE_ACTIVATION_SOURCE_FAILED_GITHUB_API_RATE_LIMITED")

    def test_invalid_context_and_truthy_value_rejected(self):
        with self.assertRaises(entry.Blocked):
            entry.checked_evidence_step("arbitrary", "secret", lambda: True)
        with self.assertRaises(entry.Blocked):
            entry.checked_evidence_step("pre", "G6", lambda: 1)

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
