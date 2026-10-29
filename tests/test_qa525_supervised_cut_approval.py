"""Hermetic tests for local SHA-scoped QA525 supervision, NO apply."""
import copy
import importlib.util
import io
from pathlib import Path
import unittest
from unittest.mock import patch

SRC = (Path(__file__).resolve().parents[1] / "scripts/qa/operators" /
       "qa525-supervised-cut-approval.py")
spec = importlib.util.spec_from_file_location("qa525_supervised_approval_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
NOW = 1793268000
SHA = "a" * 40
DIGEST = "sha256:" + "b" * 64


def fixture():
    return {
        "approval": {
            "target_sha": SHA,
            "release_asset_sha256": DIGEST,
            "scope": mod.SCOPE,
            "environment": mod.ENVIRONMENT,
            "action": mod.ACTION,
            "nonce": "f" * 64,
            "issued_at": NOW - 300,
            "expires_at": NOW + 300,
            "operator_reviewed": True,
        },
        "source": {
            "source_sha": SHA,
            "release_tag": "qa-signed-" + SHA,
            "release_asset_sha256": DIGEST,
            "status": "READY_FOR_SIGNATURE_VERIFICATION_ONLY",
        },
        "prepared": {
            "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
            "source_sha": SHA,
            "source_asset_sha256": DIGEST,
            "images_built": True,
            "services_unchanged": True,
            "may_apply": False,
        },
    }


class SupervisedCutTests(unittest.TestCase):
    def validate(self, sample):
        return mod.validate(**sample, now=NOW)

    def assert_blocked(self, sample, code):
        with self.assertRaises(mod.Blocked) as ctx:
            self.validate(sample)
        self.assertEqual(code, str(ctx.exception))

    def test_exact_local_cut_only_matches_without_apply_authorization(self):
        data = fixture()
        original = copy.deepcopy(data)
        result = self.validate(data)
        self.assertEqual(
            "LOCAL_SUPERVISED_CUT_MATCH_NO_DEPLOYMENT_AUTHORITY", result["status"])
        for flag in ("may_apply", "signature_verified_by_this_tool",
                     "host_backup_verified_by_this_tool",
                     "single_use_consumed_by_this_tool",
                     "sensitive_change_exception_granted"):
            self.assertIs(result[flag], False)
        self.assertEqual(data, original)

    def test_old_sha_rejected_for_any_record(self):
        for table in ("approval", "source", "prepared"):
            sample = fixture()
            sample[table]["target_sha" if table == "approval" else "source_sha"] = "0" * 40
            expected = {"approval": "SUPERVISION_SCOPE_OR_CUT_CHANGED",
                        "source": "SOURCE_NOT_SIGNED_METADATA",
                        "prepared": "PREPARED_SHA_OR_DIGEST_MISMATCH"}[table]
            self.assert_blocked(sample, expected)

    def test_wrong_digest_rejected_in_each_record(self):
        for table in ("approval", "source", "prepared"):
            sample = fixture()
            sample[table]["release_asset_sha256" if table != "prepared"
                          else "source_asset_sha256"] = "sha256:" + "c" * 64
            expected = ("SUPERVISION_SCOPE_OR_CUT_CHANGED" if table == "approval"
                        else "PREPARED_SHA_OR_DIGEST_MISMATCH" if table == "source"
                        else "PREPARED_SHA_OR_DIGEST_MISMATCH")
            self.assert_blocked(sample, expected)

    def test_invalid_release_metadata_blocks(self):
        sample = fixture()
        sample["source"]["status"] = "PUBLISH_PENDING"
        self.assert_blocked(sample, "SOURCE_NOT_SIGNED_METADATA")

    def test_prepared_cannot_assert_apply_or_changed_services(self):
        for key, value in (("may_apply", True), ("services_unchanged", False),
                           ("images_built", False)):
            sample = fixture()
            sample["prepared"][key] = value
            self.assert_blocked(sample, "PREPARED_SHA_OR_DIGEST_MISMATCH")

    def test_expired_approval_blocks(self):
        sample = fixture()
        sample["approval"]["expires_at"] = NOW
        self.assert_blocked(sample, "SUPERVISION_EXPIRED_OR_INVALID")

    def test_approval_longer_than_30_minutes_blocks(self):
        sample = fixture()
        sample["approval"]["expires_at"] = NOW + 2000
        self.assert_blocked(sample, "SUPERVISION_EXPIRED_OR_INVALID")

    def test_approval_from_future_blocks(self):
        sample = fixture()
        sample["approval"]["issued_at"] = NOW + 2
        self.assert_blocked(sample, "SUPERVISION_EXPIRED_OR_INVALID")

    def test_old_approval_over_30_minutes_blocks(self):
        sample = fixture()
        sample["approval"]["issued_at"] = NOW - 1900
        self.assert_blocked(sample, "SUPERVISION_EXPIRED_OR_INVALID")

    def test_approval_from_prod_or_other_action_blocks(self):
        for key, value in (("environment", "PRODUCTION"),
                           ("action", "unattended-deployment"),
                           ("scope", "ALLOW_APPLY")):
            sample = fixture()
            sample["approval"][key] = value
            self.assert_blocked(sample, "SUPERVISION_SCOPE_OR_CUT_CHANGED")

    def test_authorization_requires_operator_review_not_string(self):
        for value in (False, "true", 1, None):
            sample = fixture()
            sample["approval"]["operator_reviewed"] = value
            self.assert_blocked(sample, "SUPERVISION_SCOPE_OR_CUT_CHANGED")

    def test_weak_or_invalid_nonce_rejected(self):
        for nonce in ("abcd", "G" * 64, "", None):
            sample = fixture()
            sample["approval"]["nonce"] = nonce
            self.assert_blocked(sample, "SUPERVISION_SCOPE_OR_CUT_CHANGED")

    def test_unexpected_input_keys_block(self):
        sample = fixture()
        sample["approval"]["may_apply"] = True
        self.assert_blocked(sample, "LOCAL_APPROVAL_FIELDS_INVALID")

    def test_nonint_or_bool_timestamp_blocks(self):
        for val in (True, str(NOW), float(NOW), None):
            sample = fixture()
            sample["approval"]["issued_at"] = val
            self.assert_blocked(sample, "SUPERVISION_EXPIRED_OR_INVALID")

    def test_no_cli_apply_even_with_readable_approval(self):
        for arg in (["--apply"], ["--prepare"], []):
            with patch.object(mod, "read_root_approval") as read, \
                 patch.object(mod.sys, "argv", ["tool", *arg]), \
                 patch("sys.stdout", io.StringIO()):
                self.assertEqual(2, mod.main())
                read.assert_not_called()

    def test_cli_presence_check_never_claims_to_match_source_sha(self):
        output = io.StringIO()
        with patch.object(mod, "read_root_approval", return_value=fixture()["approval"]), \
             patch("sys.stdout", output):
            self.assertEqual(0, mod.main(["--check"]))
        self.assertIn("LOCAL_APPROVAL_FILE_PRESENT_NOT_APPLY_AUTHORITY", output.getvalue())
        self.assertIn('"may_apply": false', output.getvalue())
        self.assertNotIn(SHA, output.getvalue())

    def test_cli_does_not_expose_secret_in_errors(self):
        output = io.StringIO()
        with patch.object(mod, "read_root_approval",
                          side_effect=RuntimeError("SECRET_VALUE_PRIVATE")), \
             patch("sys.stdout", output):
            self.assertEqual(2, mod.main(["--check"]))
        self.assertNotIn("SECRET_VALUE_PRIVATE", output.getvalue())

    def test_no_host_or_mutating_code(self):
        text = SRC.read_text()
        for forbidden in ("subprocess", "docker", "systemctl",
                          "pg_dump", "os.system(", "write_text(",
                          "unlink(", "os.replace("):
            self.assertNotIn(forbidden, text)
        self.assertIn('"may_apply": False', text)


if __name__ == "__main__":
    unittest.main()
