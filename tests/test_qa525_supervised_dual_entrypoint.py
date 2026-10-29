"""Offline adversarial QA525 supervised host entrypoint tests."""
import importlib.util
from pathlib import Path
import time
import unittest
from unittest.mock import patch

FILE = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-supervised-dual-entrypoint.py"
spec = importlib.util.spec_from_file_location("qa525_supervised_host_test", FILE)
host = importlib.util.module_from_spec(spec)
spec.loader.exec_module(host)
TARGET = "0bd14cb81a1c0e607e04283083f77a5ddf36b339"
SRC = {"source_sha": TARGET, "release_asset_sha256": "sha256:" + "a" * 64}
POLICY = {"status": "BLOCKED_REVIEW_REQUIRED",
          "reasons": ["ACCOUNT_OR_IDENTITY_CHANGE", "API_BOOTSTRAP_CHANGE",
                      "OTHER_BACKEND_RUNTIME_CHANGE"]}


def review(now):
    return {
        "source_sha": TARGET,
        "release_asset_sha256": SRC["release_asset_sha256"],
        "baseline_sha": host.BASELINE,
        "reviewed_sections": sorted(host.REVIEWED),
        "sensitive_reasons": POLICY["reasons"],
        "no_new_ef_migrations": True,
        "runtime_migration_flags_disabled": True,
        "legacy_master_and_smtp_preserved": True,
        "reviewed_at": now - 10,
        "g6_archive_name": "centenario-qa-g6-CIFRADO-20261029T172434Z.tar.gz",
        "g6_archive_sha256": "b" * 64,
        "g6_drive_file_id": "1TErsKdCZd7LzJD8aST6NV0iiqlUeNYT9",
        "g6_offhost_sha256_verified": True,
        "g6_restore_report_name": "isolated-restore-20261029T172434Z.report.json",
    }


class SupervisorTests(unittest.TestCase):
    def setUp(self):
        self.now = 1793299200

    def test_review_exact_scope_is_accepted_only_as_claim(self):
        host.approved_review(review(self.now), SRC, POLICY, self.now)

    def test_reject_wrong_source(self):
        v = review(self.now)
        v["source_sha"] = "c" * 40
        with self.assertRaises(host.Blocked):
            host.approved_review(v, SRC, POLICY, self.now)

    def test_reject_unreviewed_keycloak_and_smtp(self):
        v = review(self.now)
        v["reviewed_sections"].remove("Keycloak/SMTP")
        with self.assertRaises(host.Blocked):
            host.approved_review(v, SRC, POLICY, self.now)

    def test_reject_schema_claim_not_approved(self):
        v = review(self.now)
        v["no_new_ef_migrations"] = False
        with self.assertRaises(host.Blocked):
            host.approved_review(v, SRC, POLICY, self.now)

    def test_reject_revocation_and_master_not_reviewed(self):
        v = review(self.now)
        v["legacy_master_and_smtp_preserved"] = False
        with self.assertRaises(host.Blocked):
            host.approved_review(v, SRC, POLICY, self.now)

    def test_reject_incomplete_sensitive_reason_list(self):
        v = review(self.now)
        v["sensitive_reasons"] = ["API_BOOTSTRAP_CHANGE"]
        with self.assertRaises(host.Blocked):
            host.approved_review(v, SRC, POLICY, self.now)

    def test_reject_stale_review(self):
        v = review(self.now)
        v["reviewed_at"] = self.now - 1801
        with self.assertRaises(host.Blocked):
            host.approved_review(v, SRC, POLICY, self.now)

    def test_reject_missing_external_verification(self):
        v = review(self.now)
        v["g6_offhost_sha256_verified"] = False
        with self.assertRaises(host.Blocked):
            host.approved_review(v, SRC, POLICY, self.now)

    def test_migration_change_is_blocked_for_separate_approval(self):
        def fetch(_):
            return {"status": "ahead", "files": [
                {"filename": "backend/src/PMGM.Api/Persistence/Migrations/New.cs", "status": "added"}]}
        with self.assertRaisesRegex(host.Blocked, "EF_MIGRATION"):
            host.scan_migrations(POLICY, fetch, TARGET)

    def test_renamed_dbcontext_is_blocked(self):
        def fetch(_):
            return {"status": "ahead", "files": [
                {"filename": "backend/NewName.cs", "previous_filename": "backend/Data/PmgmDbContext.cs",
                 "status": "renamed"}]}
        with self.assertRaisesRegex(host.Blocked, "EF_MIGRATION"):
            host.scan_migrations(POLICY, fetch)

    def test_simple_runtime_change_can_pass_schema_scan(self):
        host.scan_migrations(POLICY, lambda _: {
            "status": "ahead", "files": [{"filename": "frontend/src/App.tsx", "status": "modified"}]}, TARGET)

    def test_tampered_g6_archive_ref_is_blocked(self):
        v = review(self.now)
        v["g6_archive_name"] = "../../secret.env"
        with self.assertRaises(host.Blocked):
            host.approved_review(v, SRC, POLICY, self.now)

    def test_no_normal_cli_or_remote_agent(self):
        with patch.object(host.sys, "argv", ["qa525-supervised-dual-entrypoint.py", "--auto"]):
            with self.assertRaisesRegex(host.Blocked, "ONLY_EXPLICIT_SUPERVISION"):
                host.main()


if __name__ == "__main__":
    unittest.main()
