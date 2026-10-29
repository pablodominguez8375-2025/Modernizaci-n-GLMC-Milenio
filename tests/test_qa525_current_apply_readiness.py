"""Offline tests for supervised QA525 inspection (never apply/backup/restart)."""
import contextlib
import copy
import importlib.util
import io
import json
from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "scripts/qa/operators/qa525-current-apply-readiness.py"
spec = importlib.util.spec_from_file_location("qa525_apply_readiness_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
TARGET = "a" * 40
BASE = "b" * 40
WEB_BASE = "c" * 40
RELEASE = "sha256:" + "d" * 64
API = "sha256:" + "e" * 64
WEB = "sha256:" + "f" * 64
OLD = "sha256:" + "1" * 64


def fixture():
    baseline = {
        service: {"container_fingerprint": "2" * 64,
                  "image_id": OLD, "running": True}
        for service in ("api", "web", "keycloak", "postgres",
                        "minio", "gateway", "clamav")
    }
    return {
        "receipt": {
            "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
            "scope": "BUILD_ONLY_SIGNED_API_WEB_NO_CONTAINER_CHANGES_NO_APPLY",
            "source_sha": TARGET,
            "source_asset_sha256": RELEASE,
            "api_image_id": API,
            "web_image_id": WEB,
            "baseline": baseline,
            "images_built": True,
            "services_unchanged": True,
            "may_apply": False,
            "fresh_backup_created": False,
            "signed_stage_ephemeral": True,
            "actual_rollback_drill_completed": False,
        },
        "before": baseline,
        "after": copy.deepcopy(baseline),
        "host": {
            "status": "HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED",
            "source_sha": TARGET,
            "may_apply": False,
            "may_build": False,
            "fresh_backup_created": False,
        },
        "source": {
            "status": "READY_FOR_SIGNATURE_VERIFICATION_ONLY",
            "source_sha": TARGET,
            "release_tag": "qa-signed-" + TARGET,
            "release_asset_sha256": RELEASE,
        },
        "checked_images": (API, WEB),
    }


class ApplyReadinessTests(unittest.TestCase):
    def assert_blocked(self, data, code):
        with self.assertRaises(mod.Blocked) as ctx:
            mod.evaluate(**data)
        self.assertEqual(code, str(ctx.exception))

    def test_valid_only_reports_pending_backup_and_rollback(self):
        data = fixture()
        result = mod.evaluate(**data)
        self.assertEqual(
            "PREPARED_IMAGES_MATCH_LIVE_QA_BACKUP_AND_ROLLBACK_PENDING",
            result["status"])
        self.assertTrue(result["seven_services_unchanged"])
        for flag in ("may_apply", "signature_reverified_by_this_probe",
                     "backup_offhost_verified_by_this_probe",
                     "rollback_drill_verified_by_this_probe"):
            self.assertIs(result[flag], False)
        self.assertEqual(data, fixture())

    def test_prepared_sha_drift_blocks(self):
        data = fixture()
        data["receipt"]["source_sha"] = "0" * 40
        self.assert_blocked(data, "PREPARED_RECEIPT_NOT_ACCEPTABLE")

    def test_old_or_unsigned_release_metadata_blocks(self):
        data = fixture()
        data["source"]["status"] = "PUBLISH_PENDING"
        self.assert_blocked(data, "CURRENT_SIGNED_RELEASE_METADATA_INVALID")

    def test_wrong_release_asset_digest_blocks(self):
        data = fixture()
        data["receipt"]["source_asset_sha256"] = "sha256:" + "0" * 64
        self.assert_blocked(data, "PREPARED_RECEIPT_NOT_ACCEPTABLE")

    def test_sensitive_change_blocks_before_any_images(self):
        data = fixture()
        data["host"]["status"] = "BLOCKED_REVIEW_REQUIRED"
        self.assert_blocked(data, "HOST_PREFLIGHT_NOT_ELIGIBLE")

    def test_identity_infra_migration_controls_remain_readonly(self):
        data = fixture()
        for field in ("may_build", "may_apply", "fresh_backup_created"):
            row = copy.deepcopy(data)
            row["host"][field] = True
            self.assert_blocked(row, "HOST_PREFLIGHT_NOT_ELIGIBLE")

    def test_prior_receipt_cannot_authorize_deployment(self):
        data = fixture()
        for key in ("may_apply", "fresh_backup_created",
                    "actual_rollback_drill_completed"):
            row = copy.deepcopy(data)
            row["receipt"][key] = True
            self.assert_blocked(row, "PREPARED_RECEIPT_NOT_ACCEPTABLE")

    def test_physical_container_change_detected(self):
        data = fixture()
        data["after"]["api"]["container_fingerprint"] = "0" * 64
        self.assert_blocked(data, "PHYSICAL_SERVICES_CHANGED")

    def test_baseline_change_detected(self):
        data = fixture()
        data["receipt"]["baseline"]["web"]["container_fingerprint"] = "3" * 64
        self.assert_blocked(data, "PHYSICAL_SERVICES_CHANGED")

    def test_missing_auxiliary_service_blocks(self):
        data = fixture()
        data["after"].pop("keycloak")
        self.assert_blocked(data, "PHYSICAL_SERVICES_CHANGED")

    def test_candidate_images_must_differ_from_old(self):
        for key in ("api_image_id", "web_image_id"):
            data = fixture()
            data["receipt"][key] = OLD
            self.assert_blocked(data, "CANDIDATE_IMAGES_INVALID")

    def test_candidate_image_must_be_verified_not_only_receipted(self):
        data = fixture()
        data["checked_images"] = (API, "sha256:" + "0" * 64)
        self.assert_blocked(data, "PHYSICAL_IMAGE_VERIFICATION_MISSING")

    def test_malformed_candidate_image_id_blocks(self):
        data = fixture()
        data["receipt"]["api_image_id"] = "image:latest"
        self.assert_blocked(data, "CANDIDATE_IMAGES_INVALID")

    def test_image_identity_readonly_checks_image_id_and_revision(self):
        calls = []
        def run(args):
            calls.append(args)
            return API if "{{.Id}}" in args else TARGET
        mod.image_identity(run, API, TARGET)
        self.assertEqual(2, len(calls))
        self.assertTrue(all(args[:3] == ["image", "inspect", "--format"]
                            for args in calls))
        self.assertNotIn("run", json.dumps(calls))

    def test_image_identity_rejects_revision_mismatch(self):
        runner = Mock(side_effect=[API, "f" * 40])
        with self.assertRaisesRegex(mod.Blocked, "PREPARED_IMAGE_IDENTITY_CHANGED"):
            mod.image_identity(runner, API, TARGET)

    def test_no_sensitive_exception_text_in_public_output(self):
        out = io.StringIO()
        with patch.object(mod, "inspect",
                          side_effect=RuntimeError("CLIENT_SECRET_PRIVATE")), \
             contextlib.redirect_stdout(out):
            status = mod.main(["--inspect"])
        self.assertEqual(2, status)
        self.assertNotIn("CLIENT_SECRET_PRIVATE", out.getvalue())
        self.assertIn('"may_apply": false', out.getvalue())

    def test_no_apply_or_rebuild_cli(self):
        for argv in (["--apply"], ["--prepare"], []):
            with self.subTest(argv=argv), \
                 patch.object(mod, "inspect") as inspect, \
                 contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(2, mod.main(argv))
                inspect.assert_not_called()

    def test_host_required_before_trusted_code_imports(self):
        with patch.object(mod.os, "geteuid", return_value=1000), \
             patch.object(mod.socket, "gethostname", return_value="foreign"), \
             patch.object(mod, "load_after_trust") as loader:
            with self.assertRaisesRegex(mod.Blocked, "HOST_NOT_AUTHORIZED"):
                mod.inspect()
            loader.assert_not_called()

    def test_source_guards_run_before_import(self):
        called = []
        def trusted(path):
            called.append(Path(path))
            if path == mod.SOURCE_CHAIN[-1]:
                raise mod.Blocked("UNTRUSTED_SOURCE_OR_PARENT")
        with patch.object(mod, "trusted", side_effect=trusted), \
             patch.object(importlib.util, "spec_from_file_location") as import_source:
            with self.assertRaisesRegex(mod.Blocked, "UNTRUSTED_SOURCE_OR_PARENT"):
                mod.load_after_trust()
            import_source.assert_not_called()
        self.assertEqual(list(mod.SOURCE_CHAIN), called)

    def test_code_is_nonmutating_by_contract(self):
        src = SRC.read_text()
        for prohibited in ('["build"', '"--force-recreate"', '"pg_dump"',
                           '"systemctl"', '"docker", "compose", "up"',
                           'os.system(', 'subprocess.run(', 'write_text('):
            self.assertNotIn(prohibited, src)
        self.assertIn('"may_apply": False', src)


if __name__ == "__main__":
    unittest.main()
