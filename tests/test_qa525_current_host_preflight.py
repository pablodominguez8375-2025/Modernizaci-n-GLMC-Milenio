"""Offline read-only host QA525 gate tests. No SSH, Docker or secrets."""
import contextlib
import hashlib
import importlib.util
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
PATH = ROOT / "scripts/qa/operators/qa525-current-host-preflight.py"
spec = importlib.util.spec_from_file_location("qa525_host_preflight_test", PATH)
host = importlib.util.module_from_spec(spec)
spec.loader.exec_module(host)
TARGET = "a" * 40
BASE_API = "b" * 40
BASE_WEB = "c" * 40
GOOD = {
    "status": "ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY",
    "analytical_baseline_sha": BASE_API,
    "source_sha": TARGET,
    "may_apply": False,
    "source_signed_staging_required": True,
    "host_baseline_and_backup_verified": False,
}


class Qa525HostPreflightTests(unittest.TestCase):
    def setup_candidate(self, first=GOOD, second=GOOD):
        candidate = Mock()
        candidate.evaluate.side_effect = [first, second]
        preflight = Mock()
        preflight.BASE_API = BASE_API
        preflight.BASE_WEB = BASE_WEB
        preflight.check.return_value = None
        return candidate, preflight

    def test_valid_dynamic_policy_then_host_preflight_then_recheck(self):
        candidate, preflight = self.setup_candidate()
        with patch.object(host.os, "geteuid", return_value=0), \
             patch.object(host.socket, "gethostname", return_value="glmdch"):
            result = host.check_host(Mock(), candidate, preflight)
        self.assertEqual("HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED", result["status"])
        self.assertFalse(result["may_build"])
        self.assertFalse(result["may_apply"])
        self.assertFalse(result["signed_stage_executed"])
        self.assertFalse(result["fresh_backup_created"])
        self.assertEqual(2, candidate.evaluate.call_count)
        preflight.check.assert_called_once_with()

    def test_blocked_sensitive_current_dev_skips_any_host_probes(self):
        candidate, preflight = self.setup_candidate({**GOOD, "status": "BLOCKED_REVIEW_REQUIRED"})
        with patch.object(host.os, "geteuid", return_value=0), \
             patch.object(host.socket, "gethostname", return_value="glmdch"):
            with self.assertRaisesRegex(host.Blocked, "SENSITIVE_CHANGES_REQUIRE_REVIEW"):
                host.check_host(Mock(), candidate, preflight)
        preflight.check.assert_not_called()
        self.assertEqual(1, candidate.evaluate.call_count)

    def test_missing_host_authorisation_fails_before_remote_and_docker(self):
        for uid, hostname in ((1000, "glmdch"), (0, "not-glmdch")):
            with self.subTest(uid=uid,hostname=hostname):
                candidate, preflight = self.setup_candidate()
                with patch.object(host.os, "geteuid", return_value=uid), \
                     patch.object(host.socket, "gethostname", return_value=hostname):
                    with self.assertRaisesRegex(host.Blocked, "HOST_NOT_AUTHORIZED"):
                        host.check_host(Mock(), candidate, preflight)
                candidate.evaluate.assert_not_called()
                preflight.check.assert_not_called()

    def test_bad_signed_policy_shape_fails_closed(self):
        for change in ({"may_apply": True}, {"host_baseline_and_backup_verified": True},
                       {"source_signed_staging_required": False},
                       {"analytical_baseline_sha": "d" * 40}):
            with self.subTest(change=change):
                candidate, preflight = self.setup_candidate({**GOOD, **change})
                with patch.object(host.os, "geteuid", return_value=0), \
                     patch.object(host.socket, "gethostname", return_value="glmdch"):
                    with self.assertRaisesRegex(host.Blocked, "CANDIDATE_POLICY_EVIDENCE_INVALID"):
                        host.check_host(Mock(), candidate, preflight)
                preflight.check.assert_not_called()

    def test_dev_changes_during_readonly_host_inspection(self):
        candidate, preflight = self.setup_candidate(GOOD, {**GOOD, "source_sha": "d" * 40})
        with patch.object(host.os, "geteuid", return_value=0), \
             patch.object(host.socket, "gethostname", return_value="glmdch"):
            with self.assertRaisesRegex(host.Blocked, "SIGNED_CANDIDATE_CHANGED"):
                host.check_host(Mock(), candidate, preflight)
        preflight.check.assert_called_once_with()

    def test_local_preflight_exception_blocks_instead_of_passing(self):
        candidate, preflight = self.setup_candidate()
        preflight.check.side_effect = RuntimeError("secret-in-command-output")
        with patch.object(host.os, "geteuid", return_value=0), \
             patch.object(host.socket, "gethostname", return_value="glmdch"):
            with self.assertRaises(RuntimeError):
                host.check_host(Mock(), candidate, preflight)
        self.assertEqual(1, candidate.evaluate.call_count)

    def test_pinned_old_preflight_bytes_are_as_reviewed(self):
        path = ROOT / "scripts/qa/operators/qa525-update-readiness-0bd927b.py"
        self.assertEqual(host.PREFLIGHT_SHA256,
                         hashlib.sha256(path.read_bytes()).hexdigest())

    def test_cli_does_not_log_sensitive_error(self):
        out=io.StringIO()
        with patch.object(host.os, "geteuid", return_value=0), \
             patch.object(host.socket, "gethostname", return_value="glmdch"), \
             patch.object(host, "trusted_source"), \
             patch.object(host, "digest", return_value=host.PREFLIGHT_SHA256), \
             patch.object(host, "module", side_effect=RuntimeError("PASSWORD=PRIVATE")), \
             patch.object(host.sys, "argv", [str(PATH)]), \
             contextlib.redirect_stdout(out):
            code=host.main()
        self.assertEqual(2, code)
        self.assertNotIn("PASSWORD", out.getvalue())
        self.assertIn("HOST_EVIDENCE_UNAVAILABLE", out.getvalue())

    def test_no_cli_arguments_are_supported(self):
        with patch.object(host.sys, "argv", [str(PATH), "--apply"]), \
             contextlib.redirect_stdout(io.StringIO()) as captured:
            code=host.main()
        self.assertEqual(2, code)
        self.assertIn("READ_ONLY_NO_OPTIONS", captured.getvalue())

    def test_host_scripts_are_readonly_and_no_mutating_operators_called(self):
        src = PATH.read_text()
        for forbidden in ("docker compose up", "os.system", "subprocess.run",
                          "systemctl stop", "pg_dump", "write_text(", "write_bytes("):
            self.assertNotIn(forbidden,src)


if __name__ == "__main__":
    unittest.main()
