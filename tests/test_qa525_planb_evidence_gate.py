"""Offline adversarial tests for Plan B evidence guard. No host effects."""
import importlib.util
from pathlib import Path
import unittest

PATH = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-planb-evidence-gate.py"
spec = importlib.util.spec_from_file_location("planb_gate", PATH)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
A, B = "a" * 40, "b" * 40
D = "sha256:" + "c" * 64


def valid():
    return dict(schema="qa525-planb-evidence-v1", source_sha=B, release_source_sha=B,
                release_digest=D, last_applied_sha=A, live_api_sha=A, live_web_sha=A,
                live_healthy=True, pending_flight=False, prepared_reconciled=True,
                diff_complete=True, diff_count=10, physical_preflight_baseline_sha=A,
                staged_api_digest=D, staged_web_digest=D, target_api_digest=D,
                target_web_digest=D, sender_frame="QA525-BND-V1:",
                receiver_frame="QA525-BND-V1:", receiver_real_test_pass=True,
                g6_valid_now=True, g6_restore_verified=True, rollback_verified=True,
                release_sigstore_verified=True, scope="API_WEB_QA_ONLY")


class PlanBGateTests(unittest.TestCase):
    def test_consistent_evidence_is_not_go(self):
        self.assertEqual(mod.inspect(valid()), [])

    def test_every_required_gate_fails_closed(self):
        for field in ("live_healthy", "prepared_reconciled", "diff_complete",
                      "receiver_real_test_pass", "g6_valid_now",
                      "g6_restore_verified", "rollback_verified",
                      "release_sigstore_verified"):
            with self.subTest(field=field):
                e = valid()
                e[field] = False
                self.assertTrue(mod.inspect(e))

    def test_stale_baseline(self):
        e = valid()
        e["physical_preflight_baseline_sha"] = B
        self.assertIn("STALE_PHYSICAL_PREFLIGHT", mod.inspect(e))

    def test_frame_mismatch(self):
        e = valid()
        e["sender_frame"] = "QA525-FD-V1:"
        self.assertIn("FD_FRAME_MISMATCH", mod.inspect(e))

    def test_truncated_diff(self):
        for n in (300, 420, -1, True):
            e = valid()
            e["diff_count"] = n
            self.assertIn("DIFF_TRUNCATED_OR_INVALID", mod.inspect(e))

    def test_changed_release_or_staging(self):
        e = valid()
        e["release_source_sha"] = A
        self.assertIn("TARGET_RELEASE_MISMATCH", mod.inspect(e))
        e = valid()
        e["staged_web_digest"] = "sha256:" + "d" * 64
        self.assertIn("STAGED_TARGET_MISMATCH:web", mod.inspect(e))

    def test_pending_or_unknown_flight(self):
        for pending in (True, None):
            e = valid()
            e["pending_flight"] = pending
            self.assertIn("FLIGHT_PENDING_OR_UNKNOWN", mod.inspect(e))

    def test_missing_or_extra_field(self):
        e = valid()
        del e["g6_valid_now"]
        self.assertEqual(mod.inspect(e), ["EVIDENCE_SCHEMA_INCOMPLETE_OR_EXTRA"])
        e = valid()
        e["extra"] = True
        self.assertEqual(mod.inspect(e), ["EVIDENCE_SCHEMA_INCOMPLETE_OR_EXTRA"])

    def test_reused_target_or_wrong_scope(self):
        e = valid()
        e["source_sha"] = A
        e["release_source_sha"] = A
        self.assertIn("TARGET_ALREADY_APPLIED", mod.inspect(e))
        e = valid()
        e["scope"] = "LAUNCHER_ONLY"
        self.assertIn("SCOPE_INVALID", mod.inspect(e))


if __name__ == "__main__":
    unittest.main()
