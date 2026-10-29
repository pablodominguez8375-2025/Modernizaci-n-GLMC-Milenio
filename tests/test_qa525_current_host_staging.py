"""Hermetic staging workflow tests; no network, Docker, host or real secret."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-current-host-staging.py"
spec = importlib.util.spec_from_file_location("qa525_current_host_staging_test", SOURCE)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

SHA = "a" * 40
FIRST = {
    "status": "HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED",
    "source_sha": SHA,
    "may_build": False,
    "may_apply": False,
}
STAGED = {
    "status": "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED",
    "scope": mod.signed.SCOPE,
    "source_sha": SHA,
    "release_asset_sha256": "sha256:" + "b" * 64,
}


class CurrentHostStagingTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="qa525-stage-host-")
        self.addCleanup(self.tmp.cleanup)
        self.parent = Path(self.tmp.name) / "private"
        self.parent.mkdir(mode=0o700)
        self.output = self.parent / "signed-package"
        self.events = []
        # The production destination checker enforces UID root. GitHub Actions
        # fixtures belong to its unprivileged runner; retain the same private
        # path contract here and leave production's UID check unchanged.
        self.destination_patch = patch.object(
            mod.signed, "trusted_destination", side_effect=self.fixture_destination)
        self.destination_patch.start()
        self.addCleanup(self.destination_patch.stop)

    def fixture_destination(self, output):
        output = Path(output)
        if not output.is_absolute() or output.exists() or output.is_symlink():
            raise mod.signed.Blocked("DESTINATION_ALREADY_EXISTS")
        if output.parent.is_symlink() or not output.parent.is_dir() or (output.parent.stat().st_mode & 0o777) != 0o700:
            raise mod.signed.Blocked("PRIVATE_STAGING_PARENT_UNTRUSTED")
        return output

    def good_preflight(self):
        self.events.append("host")
        return dict(FIRST)

    def good_stage(self, target):
        self.events.append("signed")
        target.mkdir()
        (target / "MANIFEST.sha256").write_text("synthetic mocked signature")
        return dict(STAGED)

    def run_good(self):
        with patch.object(mod.os, "geteuid", return_value=0), \
             patch.object(mod.socket, "gethostname", return_value="glmdch"), \
             patch.object(mod, "validated_host_check", side_effect=self.good_preflight), \
             patch.object(mod.signed, "verify_current", side_effect=self.good_stage):
            return mod.stage_current(self.output)

    def test_signed_stage_is_between_two_host_checks(self):
        result = self.run_good()
        self.assertEqual(["host", "signed", "host"], self.events)
        self.assertTrue((self.output / "MANIFEST.sha256").exists())
        self.assertEqual("SIGNED_SOURCE_AND_HOST_RECHECKED_NOT_PREPARED", result["status"])
        self.assertTrue(result["host_checked_twice"])
        self.assertFalse(result["api_web_images_built"])
        self.assertFalse(result["fresh_backup_created"])
        self.assertFalse(result["may_apply"])
        self.assertEqual(["signed-package"], [p.name for p in self.parent.iterdir()])

    def test_sensitive_change_blocks_before_download_or_staging_directory(self):
        with patch.object(mod.os, "geteuid", return_value=0), \
             patch.object(mod.socket, "gethostname", return_value="glmdch"), \
             patch.object(mod, "validated_host_check", return_value={
                 **FIRST, "status": "BLOCKED_REVIEW_REQUIRED"}), \
             patch.object(mod.signed, "verify_current") as stage:
            with self.assertRaisesRegex(mod.Blocked, "HOST_PREFLIGHT_NOT_ELIGIBLE"):
                mod.stage_current(self.output)
            stage.assert_not_called()
        self.assertFalse(self.output.exists())
        self.assertEqual([], list(self.parent.iterdir()))

    def test_stage_receipt_source_mismatch_blocks_and_removes_temporary_files(self):
        def wrong(target):
            self.good_stage(target)
            return {**STAGED, "source_sha": "c" * 40}
        with patch.object(mod.os, "geteuid", return_value=0), \
             patch.object(mod.socket, "gethostname", return_value="glmdch"), \
             patch.object(mod, "validated_host_check", side_effect=self.good_preflight), \
             patch.object(mod.signed, "verify_current", side_effect=wrong):
            with self.assertRaisesRegex(mod.Blocked, "SIGNED_PACKAGE_SOURCE_OR_RECEIPT_MISMATCH"):
                mod.stage_current(self.output)
        self.assertFalse(self.output.exists())
        self.assertEqual(["host", "signed"], self.events)
        self.assertEqual([], list(self.parent.iterdir()))

    def test_dev_moves_after_signature_blocks_and_discards_candidate(self):
        answers = [dict(FIRST), {**FIRST, "source_sha": "c" * 40}]
        with patch.object(mod.os, "geteuid", return_value=0), \
             patch.object(mod.socket, "gethostname", return_value="glmdch"), \
             patch.object(mod, "validated_host_check", side_effect=answers), \
             patch.object(mod.signed, "verify_current", side_effect=self.good_stage):
            with self.assertRaisesRegex(mod.Blocked, "HOST_OR_SIGNED_DEV_CHANGED"):
                mod.stage_current(self.output)
        self.assertFalse(self.output.exists())
        self.assertEqual([], list(self.parent.iterdir()))

    def test_identity_risk_discovered_on_second_check_discards_package(self):
        answers = [dict(FIRST), {**FIRST, "status": "BLOCKED_REVIEW_REQUIRED"}]
        with patch.object(mod.os, "geteuid", return_value=0), \
             patch.object(mod.socket, "gethostname", return_value="glmdch"), \
             patch.object(mod, "validated_host_check", side_effect=answers), \
             patch.object(mod.signed, "verify_current", side_effect=self.good_stage):
            with self.assertRaisesRegex(mod.Blocked, "HOST_OR_SIGNED_DEV_CHANGED"):
                mod.stage_current(self.output)
        self.assertEqual([], list(self.parent.iterdir()))

    def test_existing_output_not_overwritten(self):
        self.output.mkdir()
        (self.output / "sentinel").write_text("keep")
        with patch.object(mod.os, "geteuid", return_value=0), \
             patch.object(mod.socket, "gethostname", return_value="glmdch"), \
             patch.object(mod, "validated_host_check") as host:
            with self.assertRaises(mod.signed.Blocked):
                mod.stage_current(self.output)
            host.assert_not_called()
        self.assertEqual("keep", (self.output / "sentinel").read_text())

    def test_untrusted_parent_never_contacts_remote(self):
        self.parent.chmod(0o755)
        with patch.object(mod.os, "geteuid", return_value=0), \
             patch.object(mod.socket, "gethostname", return_value="glmdch"), \
             patch.object(mod, "validated_host_check") as host:
            with self.assertRaises(mod.signed.Blocked):
                mod.stage_current(self.output)
            host.assert_not_called()

    def test_host_authorization_precedes_all_operations(self):
        for uid, hostname in [(1000, "glmdch"), (0, "other")]:
            with self.subTest(uid=uid, hostname=hostname), \
                 patch.object(mod.os, "geteuid", return_value=uid), \
                 patch.object(mod.socket, "gethostname", return_value=hostname), \
                 patch.object(mod, "validated_host_check") as host:
                with self.assertRaisesRegex(mod.Blocked, "HOST_NOT_AUTHORIZED"):
                    mod.stage_current(self.output)
                host.assert_not_called()

    def test_signature_failure_discards_temp_without_host_recheck(self):
        def fail(target):
            target.mkdir()
            (target / "partial").write_text("no signature")
            raise RuntimeError("invalid signer")
        with patch.object(mod.os, "geteuid", return_value=0), \
             patch.object(mod.socket, "gethostname", return_value="glmdch"), \
             patch.object(mod, "validated_host_check", side_effect=self.good_preflight), \
             patch.object(mod.signed, "verify_current", side_effect=fail):
            with self.assertRaisesRegex(RuntimeError, "invalid signer"):
                mod.stage_current(self.output)
        self.assertEqual(["host"], self.events)
        self.assertEqual([], list(self.parent.iterdir()))

    def test_host_report_must_explicitly_deny_image_build_and_apply(self):
        for key in ("may_build", "may_apply"):
            with self.subTest(key=key):
                with patch.object(mod.os, "geteuid", return_value=0), \
                     patch.object(mod.socket, "gethostname", return_value="glmdch"), \
                     patch.object(mod, "validated_host_check", return_value={**FIRST,key:True}), \
                     patch.object(mod.signed, "verify_current") as stage:
                    with self.assertRaisesRegex(mod.Blocked, "HOST_PREFLIGHT_NOT_ELIGIBLE"):
                        mod.stage_current(self.output)
                    stage.assert_not_called()

    def test_cli_does_not_print_sensible_errors(self):
        output = io.StringIO()
        with patch.object(mod, "stage_current", side_effect=RuntimeError("SMTP_KEY=PRIVATE")), \
             contextlib.redirect_stdout(output):
            code = mod.main(["--stage", "--output", "/tmp/foo"])
        self.assertEqual(2, code)
        self.assertNotIn("SMTP_KEY", output.getvalue())
        self.assertEqual("BLOCKED_QA525_HOST_STAGING", json.loads(output.getvalue())["status"])

    def test_no_runtime_apply_build_calls_exposed(self):
        source = SOURCE.read_text()
        for forbidden in ("subprocess", "systemctl", "compose up", "docker build",
                          "pg_dump", "write_bytes(", "write_text(", "exec("):
            self.assertNotIn(forbidden, source)


if __name__ == "__main__":
    unittest.main()
