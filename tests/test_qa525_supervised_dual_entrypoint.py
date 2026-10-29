"""Offline adversarial QA525 supervised host entrypoint tests."""
import importlib.util
from pathlib import Path
import tempfile
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
            host.scan_migrations(POLICY, fetch, TARGET)

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

    def test_new_signed_head_can_be_reviewed_without_code_pin(self):
        new_source = dict(SRC, source_sha="e" * 40)
        v = review(self.now)
        v["source_sha"] = new_source["source_sha"]
        host.approved_review(v, new_source, POLICY, self.now)

    def test_old_review_cannot_approve_new_signed_head(self):
        new_source = dict(SRC, source_sha="e" * 40)
        with self.assertRaises(host.Blocked):
            host.approved_review(review(self.now), new_source, POLICY, self.now)

    def test_compose_untouched_container_retains_identity_stack(self):
        original = ("/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json",
                    "/etc/pmgm/qa754-identity-overlay.stage.json")
        self.assertEqual(host.expected_compose_chain(
            name="api", target=TARGET, original_files=original,
            previous_id="1" * 64, previous_image="sha256:" + "2" * 64,
            current_id="1" * 64, current_image="sha256:" + "2" * 64,
            candidate_image="sha256:" + "3" * 64), list(original))

    def test_compose_recreated_api_appends_only_image_overlay(self):
        original = ("/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json",
                    "/opt/centenario/qa-api-refresh/api-" + "b"*40 + ".json",
                    "/etc/pmgm/qa754-identity-overlay.stage.json")
        result = host.expected_compose_chain(
            name="api", target=TARGET, original_files=original,
            previous_id="1" * 64, previous_image="sha256:" + "2" * 64,
            current_id="4" * 64, current_image="sha256:" + "3" * 64,
            candidate_image="sha256:" + "3" * 64)
        self.assertEqual(result[:3], list(original))
        self.assertEqual(result[-1], "/opt/centenario/qa525-signed-prepare/"
                         "qa525-api-" + TARGET + "-" + "3"*64 + ".json")

    def test_compose_unknown_image_is_rejected(self):
        with self.assertRaisesRegex(host.Blocked, "COMPOSE_UNEXPECTED_IMAGE"):
            host.expected_compose_chain(
                name="web", target=TARGET, original_files=("/etc/root-compose.json",),
                previous_id="1"*64, previous_image="sha256:" + "2"*64,
                current_id="4"*64, current_image="sha256:" + "9"*64,
                candidate_image="sha256:" + "3"*64)

    def test_compose_same_id_with_new_image_is_rejected(self):
        # Docker container identity must change on recreation.
        with self.assertRaises(host.Blocked):
            host.expected_compose_chain(
                name="web", target=TARGET, original_files=("/etc/root-compose.json",),
                previous_id="1"*64, previous_image="sha256:" + "2"*64,
                current_id="1"*64, current_image="sha256:" + "3"*64,
                candidate_image="sha256:" + "3"*64)

    def test_rollback_retains_both_verified_old_images(self):
        old_api, old_web = "sha256:" + "a"*64, "sha256:" + "b"*64
        existing = {old_api: old_api, old_web: old_web}
        actions = []
        def docker(cmd):
            if cmd[:2] == ["image", "inspect"]:
                if cmd[4] not in existing:
                    raise RuntimeError("missing tag")
                return existing[cmd[4]]
            if cmd[:2] == ["image", "tag"]:
                existing[cmd[3]] = cmd[2]
                actions.append(tuple(cmd))
                return ""
            raise AssertionError(cmd)
        result = host.retain_rollback_images(
            {"api": {"image": old_api}, "web": {"image": old_web}},
            TARGET, docker)
        self.assertEqual(set(result), {"api", "web"})
        self.assertEqual(len(actions), 2)
        for name, tag in result.items():
            self.assertEqual(existing[tag], old_api if name == "api" else old_web)

    def test_rollback_tag_reassignment_is_denied(self):
        old_api, old_web = "sha256:" + "a"*64, "sha256:" + "b"*64
        first_tag = "pmgm-qa525-rollback:api-" + TARGET[:12] + "-" + "a"*12
        def docker(cmd):
            if cmd[0:2] == ["image", "inspect"]:
                return "sha256:" + "f"*64 if cmd[4] == first_tag else cmd[4]
            raise AssertionError("must not overwrite a collision")
        with self.assertRaisesRegex(host.Blocked, "ROLLBACK_TAG_COLLISION"):
            host.retain_rollback_images(
                {"api": {"image": old_api}, "web": {"image": old_web}},
                TARGET, docker)

    def test_compose_empty_env_must_accept_physically_verified_zero_bytes(self):
        # Actual glmdch QA baseline: SHA256(empty.env) == SHA256(b"").
        # It is intentionally a 0-byte file, not a missing configuration.
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "empty.env"
            path.write_bytes(b"")
            path.chmod(0o600)
            with patch.object(host, "trusted", return_value=path):
                self.assertEqual(
                    host.checked_compose_env_file(path),
                    "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")

                # Allow comments only; block any effective assignment.
                path.write_text("# QA empty environment\n")
                self.assertNotEqual(host.checked_compose_env_file(path), "")
                path.write_text("KEYCLOAK_SECRET=unexpected\n")
                with self.assertRaisesRegex(host.Blocked, "COMPOSE_ENV_FILE_NOT_EMPTY"):
                    host.checked_compose_env_file(path)

                # Changing permissions must still fail closed.
                path.write_bytes(b"")
                path.chmod(0o644)
                with self.assertRaisesRegex(host.Blocked, "COMPOSE_ENV_FILE_UNTRUSTED"):
                    host.checked_compose_env_file(path)

    def test_live_ef_history_matches_installed_receipt_readonly(self):
        rows = ["2026_A_%03d" % i for i in range(98)]
        def docker(args):
            self.assertEqual(args[0:2], ["exec", "centenario-qa-lab2-postgres-1"])
            self.assertIn("BEGIN READ ONLY", args[-1])
            return "\n".join(rows)
        with patch.object(host, "private_json", return_value={"database_migrations": rows}):
            self.assertTrue(host.validate_live_ef_history(docker))

    def test_live_ef_migration_drift_blocks_apply(self):
        rows = ["2026_A_%03d" % i for i in range(98)]
        with patch.object(host, "private_json", return_value={"database_migrations": rows}):
            with self.assertRaisesRegex(host.Blocked, "EF_MIGRATION_HISTORY_DRIFT"):
                host.validate_live_ef_history(lambda _: "\n".join(rows[:-1]))

    def test_live_ef_receipt_missing_history_blocks_apply(self):
        with patch.object(host, "private_json", return_value={"database_migrations": []}):
            with self.assertRaisesRegex(host.Blocked, "EF_BASELINE_MIGRATIONS_INVALID"):
                host.validate_live_ef_history(lambda _: "not called")


    def test_public_version_health_passes_only_matching_signed_sha(self):
        class Response:
            status = 200
            def __init__(self, url, payload):
                self.url, self.payload = url, payload
            def __enter__(self):
                return self
            def __exit__(self, *_):
                return False
            def geturl(self):
                return self.url
            def read(self, _):
                return self.payload
        class Opener:
            def __init__(self, published):
                self.published = published
            def open(self, req, timeout=12):
                from json import dumps
                payload = (dumps({"environment": "QA_SYNTHETIC",
                                  "source_sha": self.published}).encode()
                           if req.full_url.endswith("/qa-version.json") else b"ok")
                return Response(req.full_url, payload)
        with patch.object(host, "disable_redirects", return_value=Opener(TARGET)):
            self.assertTrue(host.health(TARGET))
        with patch.object(host, "disable_redirects", return_value=Opener("e"*40)):
            self.assertFalse(host.health(TARGET))

    def test_public_version_health_fails_on_invalid_sha(self):
        self.assertFalse(host.health("old"))
        self.assertFalse(host.health(None))

    def test_public_version_ignores_large_but_healthy_homepage(self):
        class Response:
            status = 200
            def __init__(self, url):
                self.url = url
            def __enter__(self):
                return self
            def __exit__(self, *_):
                return False
            def geturl(self):
                return self.url
            def read(self, _):
                import json
                if self.url.endswith("/qa-version.json"):
                    return json.dumps({"environment": "QA_SYNTHETIC",
                                       "source_sha": TARGET}).encode()
                return b"X" * 4096
        class Opener:
            def open(self, req, timeout=12):
                return Response(req.full_url)
        with patch.object(host, "disable_redirects", return_value=Opener()):
            self.assertTrue(host.health(TARGET))



class DBAReviewedMigrationTests(unittest.TestCase):
    """A known schema change is not equivalent to deployment authorization."""

    def test_exact_pr801_blobs_are_only_supported_schema_exception(self):
        class Gate:
            MIGRATION_PATH = "backend/src/PMGM.Api/Migrations/20261029120000_AddMemberHistoryProposals.cs"
            CONTEXT_PATH = "backend/src/PMGM.Api/Data/PmgmDbContext.cs"
            MIGRATION_BLOB = "a" * 40
            CONTEXT_BLOB = "b" * 40
            class Blocked(Exception):
                pass
            @classmethod
            def reviewed_diff(cls, files, blobs):
                if blobs != {cls.MIGRATION_PATH: cls.MIGRATION_BLOB,
                             cls.CONTEXT_PATH: cls.CONTEXT_BLOB}:
                    raise cls.Blocked("BLOB_MISMATCH")
                if {r["filename"] for r in files if "DbContext" in r["filename"]
                    or "/Migrations/" in r["filename"]} != {
                        cls.MIGRATION_PATH, cls.CONTEXT_PATH}:
                    raise cls.Blocked("UNREVIEWED_MIGRATION")
        def fetch(path):
            if path.startswith("/compare/"):
                return {"status": "ahead", "files": [
                    {"filename": Gate.MIGRATION_PATH, "status": "added"},
                    {"filename": Gate.CONTEXT_PATH, "status": "modified"},
                    {"filename": "frontend/src/App.tsx", "status": "modified"}]}
            if path.endswith(Gate.MIGRATION_PATH + "?ref=" + TARGET):
                return {"sha": Gate.MIGRATION_BLOB}
            if path.endswith(Gate.CONTEXT_PATH + "?ref=" + TARGET):
                return {"sha": Gate.CONTEXT_BLOB}
            raise AssertionError(path)
        self.assertEqual(host.scan_migrations(POLICY, fetch, TARGET, Gate),
                         "PR801_REVIEWED_ADDITIVE_NEEDS_DBA")
        self.assertEqual(host.scan_migrations(POLICY, lambda _: {
            "status": "ahead", "files": [
                {"filename": "frontend/src/App.tsx", "status": "modified"}]},
            TARGET, Gate), "NO_SCHEMA_CHANGE")
        with self.assertRaisesRegex(host.Blocked, "SEPARATE_PLAN"):
            host.scan_migrations(POLICY, fetch, TARGET)
        def tampered(path):
            obj = fetch(path)
            return {"sha": "0" * 40} if path.endswith(Gate.CONTEXT_PATH + "?ref=" + TARGET) else obj
        with self.assertRaisesRegex(host.Blocked, "UNKNOWN_OR_UNREVIEWED_EF_CHANGE"):
            host.scan_migrations(POLICY, tampered, TARGET, Gate)

    def test_schema_review_requires_flag_false_only_after_verification(self):
        now = 1793299200
        newer = review(now)
        newer["no_new_ef_migrations"] = False
        with self.assertRaises(host.Blocked):
            host.approved_review(newer, SRC, POLICY, now)
        host.approved_review(newer, SRC, POLICY, now, schema_approved=True)
        with self.assertRaises(host.Blocked):
            host.approved_review(review(now), SRC, POLICY, now, schema_approved=True)

    def test_dba_approval_and_live_99_require_post_db_g6(self):
        now = 1793299200  # 2026-10-29 18:40 UTC
        g6 = review(now)
        dba = {
            "source_sha": TARGET,
            "release_asset_sha256": SRC["release_asset_sha256"],
            "baseline_sha": host.BASELINE,
            "migration_id": "20261029120000_AddMemberHistoryProposals",
            "migration_blob_sha": "a" * 40,
            "dbcontext_blob_sha": "b" * 40,
            "migration_completed_at": 1793294400,  # before 17:24 G6
            "approved_at": now - 100,
            "dba_independently_reviewed": True,
            "additive_up_only": True,
            "previous_api_forward_compatible": True,
            "post_db_g6_archive_name": g6["g6_archive_name"],
            "post_db_g6_archive_sha256": g6["g6_archive_sha256"],
            "post_db_g6_restore_report_name": g6["g6_restore_report_name"],
        }
        class Gate:
            MIGRATION_ID = dba["migration_id"]
            MIGRATION_BLOB = "a" * 40
            CONTEXT_BLOB = "b" * 40
        class Probe:
            POST_DB_PASS_STATUS = "POST_DB_99_MIGRATIONS_AND_EXACT_SCHEMA_READ_ONLY_PASS"
            class Blocked(Exception):
                pass
            @staticmethod
            def inspect(receipt, docker, gate):
                return {"status": Probe.POST_DB_PASS_STATUS,
                        "history_count": 99, "may_apply": False}
        private = lambda path, maximum=65536: (dba if path == host.DBA_REVIEW
                                                 else {"database_migrations": ["x"] * 98})
        with (patch.object(host, "private_json", side_effect=private),
              patch.object(host, "validate_g6", return_value=True)):
            self.assertTrue(host.validate_post_db_dba(g6, SRC, now,
                                                       lambda _: "", Gate, Probe))
            for key, value in (("previous_api_forward_compatible", False),
                               ("dba_independently_reviewed", False),
                               ("source_sha", "e" * 40),
                               ("migration_completed_at", 1793298000)):
                altered = dict(dba, **{key: value})
                with patch.object(host, "private_json", side_effect=lambda path, maximum=65536:
                                  altered if path == host.DBA_REVIEW else
                                  {"database_migrations": ["x"] * 98}):
                    with self.assertRaises(host.Blocked, msg=key):
                        host.validate_post_db_dba(g6, SRC, now, lambda _: "", Gate, Probe)

if __name__ == "__main__":
    unittest.main()
