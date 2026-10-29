"""Hermetic negative tests for the QA525 reversible-transaction contract.

Only synthetic in-memory JSON. There is no SSH, Docker, credential, GitHub,
backup access or deployment. A PASS never grants permission to apply.
"""
import copy
import importlib.util
import io
import json
from pathlib import Path
import unittest
from unittest.mock import patch

SRC = (Path(__file__).resolve().parents[1] / "scripts/qa/operators" /
       "qa525-reversible-transaction-contract.py")
spec = importlib.util.spec_from_file_location("qa525_reversible_contract", SRC)
contract = importlib.util.module_from_spec(spec)
spec.loader.exec_module(contract)

TARGET = "a" * 40
BASE = "b" * 40
WEB = "c" * 40
ASSET = "sha256:" + "d" * 64
API_IMAGE = "sha256:" + "e" * 64
WEB_IMAGE = "sha256:" + "f" * 64


def fixture():
    return {
        "source": {
            "status": "READY_FOR_SIGNATURE_VERIFICATION_ONLY",
            "source_sha": TARGET, "release_tag": "qa-signed-" + TARGET,
            "release_asset_sha256": ASSET,
        },
        "policy": {
            "status": "ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY",
            "source_sha": TARGET, "analytical_baseline_sha": BASE,
            "reasons": [], "may_apply": False,
        },
        "preflight": {
            "status": "HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED",
            "source_sha": TARGET, "baseline_api_sha": BASE,
            "baseline_web_sha": WEB,
            "may_build": False, "may_apply": False,
            "fresh_backup_created": False,
        },
        "staging": {
            "status": "SIGNED_SOURCE_AND_HOST_RECHECKED_NOT_PREPARED",
            "source_sha": TARGET, "release_asset_sha256": ASSET,
            "signed_stage_executed": True, "host_checked_twice": True,
            "may_apply": False, "fresh_backup_created": False,
        },
        "prepared": {
            "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
            "source_sha": TARGET, "source_asset_sha256": ASSET,
            "images_built": True, "services_unchanged": True,
            "may_apply": False, "api_image_id": API_IMAGE,
            "web_image_id": WEB_IMAGE,
        },
        "baseline": {
            name: {"container_fingerprint": "7" * 64,
                   "image_id": "sha256:" + "8" * 64, "running": True}
            for name in contract.SERVICES
        },
        "backups": {
            name: {"sha256": "sha256:" + "9" * 64, "size": 101,
                   "pg_restore_list_verified": True,
                   "offhost_custody_verified": True,
                   "restore_drill_verified": True}
            for name in contract.DATABASES
        },
        "rollback": {
            key: True for key in (
                "api_restored_in_drill", "web_restored_in_drill",
                "identity_preserved_in_drill", "migrations_unchanged_in_drill",
                "auxiliary_services_unchanged_in_drill",
                "old_image_ids_retained"
            )
        },
    }


class ReversibleTransactionContractTests(unittest.TestCase):
    def assert_blocked(self, modified, code):
        with self.assertRaises(contract.Blocked) as ctx:
            contract.evaluate(modified)
        self.assertEqual(code, str(ctx.exception))

    def test_complete_synthetic_receipt_only_passes_structural_gate(self):
        result = contract.evaluate(fixture())
        self.assertEqual("STRUCTURAL_CONTRACT_PASS_PHYSICAL_REVIEW_REQUIRED",
                         result["status"])
        self.assertEqual(7, result["service_receipt_count"])
        self.assertEqual(2, result["database_receipt_count"])
        for key in ("may_apply", "may_build", "host_attested",
                    "backup_verified_by_this_tool",
                    "rollback_executed_by_this_tool"):
            self.assertIs(result[key], False)

    def test_all_sensitive_changes_block_without_safety_bypass(self):
        for reason in ("ACCOUNT_OR_IDENTITY_CHANGE", "SCHEMA_OR_MODEL_CHANGE",
                       "API_BOOTSTRAP_CHANGE", "INFRASTRUCTURE_CHANGE"):
            sample = fixture()
            sample["policy"]["status"] = "BLOCKED_REVIEW_REQUIRED"
            sample["policy"]["reasons"] = [reason]
            self.assert_blocked(sample, "SENSITIVE_OR_UNREVIEWED_CHANGE")

    def test_mismatched_sha_in_each_receipt_is_blocked(self):
        keys = (("source", "source_sha", "SOURCE_SHA_INVALID"),
                ("policy", "source_sha", "SENSITIVE_OR_UNREVIEWED_CHANGE"),
                ("preflight", "source_sha", "HOST_PREFLIGHT_RECEIPT_INVALID"),
                ("staging", "source_sha", "SIGNED_STAGING_RECEIPT_INVALID"),
                ("prepared", "source_sha", "PREPARED_RECEIPT_INVALID"))
        for section, attr, code in keys:
            with self.subTest(section=section):
                sample = fixture()
                sample[section][attr] = "0" * 40
                self.assert_blocked(sample, code)

    def test_policy_baseline_conflict_blocks(self):
        sample = fixture()
        sample["policy"]["analytical_baseline_sha"] = TARGET
        self.assert_blocked(sample, "SENSITIVE_OR_UNREVIEWED_CHANGE")

    def test_signed_asset_mismatch_blocks(self):
        for section, field, code in (
            ("source", "release_asset_sha256", "SOURCE_RELEASE_NOT_VERIFIED"),
            ("staging", "release_asset_sha256", "SIGNED_STAGING_RECEIPT_INVALID"),
            ("prepared", "source_asset_sha256", "PREPARED_RECEIPT_INVALID"),
        ):
            with self.subTest(section=section):
                sample = fixture()
                sample[section][field] = "sha256:" + "1" * 64
                self.assert_blocked(sample, code)

    def test_broken_signature_or_double_host_check_blocks(self):
        for key in ("signed_stage_executed", "host_checked_twice"):
            sample = fixture()
            sample["staging"][key] = False
            self.assert_blocked(sample, "SIGNED_STAGING_RECEIPT_INVALID")

    def test_old_images_not_retained_blocks(self):
        for name, field in (("api", "api_image_id"), ("web", "web_image_id")):
            sample = fixture()
            sample["baseline"][name]["image_id"] = sample["prepared"][field]
            self.assert_blocked(sample, "BASELINE_REPLACED_BEFORE_TRANSACTION")

    def test_missing_seven_service_baseline_blocks(self):
        sample = fixture()
        del sample["baseline"]["keycloak"]
        self.assert_blocked(sample, "SEVEN_SERVICE_BASELINE_INCOMPLETE")

    def test_invalid_docker_image_or_container_id_blocks(self):
        for field, value in (("container_fingerprint", "b" * 40),
                             ("image_id", "b" * 64)):
            sample = fixture()
            sample["baseline"]["api"][field] = value
            self.assert_blocked(sample, "SERVICE_RECEIPT_INVALID")

    def test_service_stopped_blocks(self):
        sample = fixture()
        sample["baseline"]["minio"]["running"] = False
        self.assert_blocked(sample, "SERVICE_RECEIPT_INVALID")

    def test_both_database_backups_mandatory(self):
        for name in contract.DATABASES:
            sample = fixture()
            del sample["backups"][name]
            self.assert_blocked(sample, "BOTH_DATABASES_NOT_BACKED_UP")

    def test_independent_backup_and_restore_evidence_mandatory(self):
        for name in contract.DATABASES:
            for flag in ("pg_restore_list_verified", "offhost_custody_verified",
                         "restore_drill_verified"):
                with self.subTest(name=name, flag=flag):
                    sample = fixture()
                    sample["backups"][name][flag] = False
                    self.assert_blocked(sample, "BACKUP_OR_RESTORE_EVIDENCE_MISSING")

    def test_backup_digest_size_and_boolean_not_coerced(self):
        for field, value in (("sha256", "sha256:xyz"),
                             ("size", 100), ("size", True)):
            sample = fixture()
            sample["backups"]["pmgm"][field] = value
            self.assert_blocked(sample, "BACKUP_OR_RESTORE_EVIDENCE_MISSING")

    def test_each_rollback_assertion_mandatory(self):
        for flag in fixture()["rollback"]:
            with self.subTest(flag=flag):
                sample = fixture()
                sample["rollback"][flag] = False
                self.assert_blocked(sample, "ROLLBACK_DRILL_NOT_COMPLETE")

    def test_missing_rollback_field_blocks(self):
        sample = fixture()
        sample["rollback"].pop("web_restored_in_drill")
        self.assert_blocked(sample, "ROLLBACK_EVIDENCE_INCOMPLETE")

    def test_no_unexpected_input_section(self):
        sample = fixture()
        sample["apply_approved"] = True
        self.assert_blocked(sample, "DOCUMENT_FIELDS_INVALID")

    def test_no_io_in_evaluator(self):
        data = fixture()
        original = copy.deepcopy(data)
        result = contract.evaluate(data)
        self.assertEqual(data, original)
        self.assertFalse(result["may_apply"])

    def test_cli_errors_are_redacted_and_never_authorize(self):
        bad_json = b'{"SMTP_SECRET": "dont leak", "broken": true}'
        output = io.StringIO()
        mock_stdin = unittest.mock.Mock()
        mock_stdin.buffer.read.return_value = bad_json
        with patch.object(contract.sys, "stdin", mock_stdin), \
             patch.object(contract.sys, "argv", ["contract"]), \
             patch("sys.stdout", output):
            code = contract.main()
        self.assertEqual(2, code)
        self.assertIn("BLOCKED_QA525_TRANSACTION_CONTRACT", output.getvalue())
        self.assertNotIn("dont leak", output.getvalue())


if __name__ == "__main__":
    unittest.main()
