"""QA525 historical status guard: adversarial, stdlib-only and host-inert."""
import copy
from datetime import date
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "scripts/qa/qa525-status-snapshot-check.py"
spec = importlib.util.spec_from_file_location("qa525_status_snapshot_check", SOURCE)
m = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = m
spec.loader.exec_module(m)


class QA525StatusSnapshotTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.snapshot = m.read_snapshot(ROOT / "docs/qa/status-current.json")

    def valid(self):
        return copy.deepcopy(self.snapshot)

    def assert_blocked(self, data, error):
        with self.assertRaisesRegex(m.Blocked, error):
            m.verify(data, today=date(2026, 11, 2))

    def test_valid_historical_snapshot_is_never_host_authorization(self):
        v = m.verify(self.valid(), today=date(2026, 11, 2))
        self.assertEqual(v["status"], "HISTORICAL_SNAPSHOT_SHAPE_VALID_NOT_LIVE_QA")
        self.assertTrue(v["same_calendar_day"])
        for flag in ("current_git_and_drive_rechecked",
                     "current_physical_host_verified",
                     "may_deploy", "may_merge", "may_authorize_root"):
            self.assertIs(v[flag], False)

    def test_historical_snapshot_does_not_expire_test_suite(self):
        v = m.verify(self.valid(), today=date(2027, 1, 1))
        self.assertFalse(v["same_calendar_day"])
        self.assertGreater(v["age_days"], 0)
        self.assertFalse(v["may_deploy"])

    def test_future_dated_snapshot_blocks(self):
        self.assert_blocked({**self.valid(), "observed_date": "2027-01-01"},
                            "FUTURE_SNAPSHOT")

    def test_wrong_schema_and_extra_field_blocks(self):
        value = self.valid()
        value["schema_version"] = "pmgm.qa525.snapshot.v2"
        self.assert_blocked(value, "SNAPSHOT_MUST_REMAIN_HISTORICAL")
        value = self.valid()
        value["root_token"] = "DO_NOT_STORE_SECRETS"
        self.assert_blocked(value, "SNAPSHOT_SCHEMA_DRIFT")

    def test_bad_sha_blocks_and_release_sha_must_match_dev(self):
        value = self.valid()
        value["github"]["observed_dev_sha"] = "not-a-sha"
        self.assert_blocked(value, "GITHUB_REVISION_INVALID")
        value = self.valid()
        value["github"]["original_signed_release"]["source_sha"] = "a" * 40
        self.assert_blocked(value, "SIGNED_RELEASE_CANNOT_CLAIM_PR_838")

    def test_fake_pr_approval_and_unsigned_release_claim_block(self):
        value = self.valid()
        value["github"]["launcher_recovery_pr"]["independent_approved_reviews"] = 1
        self.assert_blocked(value, "ROOT_REVIEW_MUST_REMAIN_NON_AUTHORIZING")
        value = self.valid()
        value["github"]["original_signed_release"]["contains_pr_838_changes"] = True
        self.assert_blocked(value, "SIGNED_RELEASE_CANNOT_CLAIM_PR_838")

    def test_forged_host_readback_and_support_vm_block(self):
        value = self.valid()
        value["environment"]["physical_host_readback_this_snapshot"] = True
        self.assert_blocked(value, "UNSUPPORTED_QA_CLAIM")
        value = self.valid()
        value["environment"]["allowed_host_names"].append("srv01")
        self.assert_blocked(value, "UNSUPPORTED_QA_CLAIM")

    def test_false_host_deployment_and_uat_claims_block(self):
        value = self.valid()
        value["physical_qa"]["any_fresh_application"] = "APPLIED"
        self.assert_blocked(value, "PHYSICAL_HOST_NOT_PROVEN")
        value = self.valid()
        value["physical_qa"]["uat"] = "PASS"
        self.assert_blocked(value, "PHYSICAL_HOST_NOT_PROVEN")

    def test_spoofed_last_observed_launcher_or_api_web_blocks(self):
        value = self.valid()
        value["physical_qa"]["launcher"] = "INSTALLED_WITHOUT_PROOF"
        self.assert_blocked(value, "PHYSICAL_HOST_NOT_PROVEN")
        value = self.valid()
        value["physical_qa"]["api_web"] = "RUNNING_CURRENT_VERSION"
        self.assert_blocked(value, "PHYSICAL_HOST_NOT_PROVEN")
        value = self.valid()
        value["physical_qa"]["pending_prepared"] = "APPLIED"
        self.assert_blocked(value, "PHYSICAL_HOST_NOT_PROVEN")

    def test_forged_g6_offhost_or_same_host_credential_claim_blocks(self):
        value = self.valid()
        value["backup_g6"]["encrypted_offhost_copy"] = "RESTORE_ALL_VERIFIED"
        self.assert_blocked(value, "G6_OVERCLAIM_OR_INVALID_HASH")
        value = self.valid()
        value["backup_g6"]["same_host_isolated_restore"] = "IAM_AND_ALL_DB_PASS"
        self.assert_blocked(value, "G6_OVERCLAIM_OR_INVALID_HASH")

    def test_false_disaster_recovery_or_iam_claims_block(self):
        value = self.valid()
        value["backup_g6"]["disaster_restore_other_host"] = "PASS"
        self.assert_blocked(value, "G6_OVERCLAIM_OR_INVALID_HASH")
        value = self.valid()
        value["backup_g6"]["iam_restore"] = "PASS"
        self.assert_blocked(value, "G6_OVERCLAIM_OR_INVALID_HASH")

    def test_unapproved_launcher_or_api_web_go_blocks(self):
        value = self.valid()
        value["decision"]["launcher_physical_cut"] = "GO"
        self.assert_blocked(value, "UNAUTHORIZED_GO_CLAIM")
        value = self.valid()
        value["decision"]["api_web_physical_update"] = "GO"
        self.assert_blocked(value, "UNAUTHORIZED_GO_CLAIM")

    def test_untrusted_links_and_secret_field_block(self):
        value = self.valid()
        value["source_links"]["primary_qa_issue"] = "https://evil.invalid/525"
        self.assert_blocked(value, "UNTRUSTED_SOURCE_LINK")
        value = self.valid()
        value["decision"]["password"] = "DO_NOT_STORE"
        self.assert_blocked(value, "DECISION_SCHEMA_DRIFT")

    def test_duplicate_key_and_nonfinite_json_rejected(self):
        with tempfile.TemporaryDirectory(prefix="qa525-status-test-") as tmp:
            p = Path(tmp) / "snapshot.json"
            p.write_text('{"schema_version":"x","schema_version":"y"}')
            with self.assertRaisesRegex(m.Blocked, "DUPLICATE_JSON_KEY"):
                m.read_snapshot(p)
            p.write_text('{"a":NaN}')
            with self.assertRaisesRegex(m.Blocked, "NONFINITE_JSON_NUMBER"):
                m.read_snapshot(p)

    def test_symlink_and_directory_rejected_without_writes(self):
        with tempfile.TemporaryDirectory(prefix="qa525-status-test-") as tmp:
            p = Path(tmp) / "snapshot.json"
            p.write_text(json.dumps(self.valid()))
            symlink = Path(tmp) / "shortcut.json"
            symlink.symlink_to(p)
            with self.assertRaisesRegex(m.Blocked, "NOT_A_REGULAR_SMALL_SNAPSHOT"):
                m.read_snapshot(symlink)
            with self.assertRaisesRegex(m.Blocked, "NOT_A_REGULAR_SMALL_SNAPSHOT"):
                m.read_snapshot(Path(tmp))

    def test_descriptor_bound_read_avoids_second_path_dereference(self):
        with tempfile.TemporaryDirectory(prefix="qa525-status-test-") as tmp:
            path = Path(tmp) / "snapshot.json"
            path.write_text(json.dumps(self.valid()))
            with patch.object(Path, "read_bytes",
                              side_effect=AssertionError("reopen not allowed")):
                result = m.read_snapshot(path)
            self.assertEqual(result["schema_version"],
                             "pmgm.qa525.readonly.snapshot.v1")

    def test_cli_refuses_garbage_non_authorizing(self):
        with tempfile.TemporaryDirectory(prefix="qa525-status-test-") as tmp:
            p = Path(tmp) / "bad.json"
            p.write_text('[]')
            self.assertEqual(m.main([str(p)]), 2)


if __name__ == "__main__":
    unittest.main()
