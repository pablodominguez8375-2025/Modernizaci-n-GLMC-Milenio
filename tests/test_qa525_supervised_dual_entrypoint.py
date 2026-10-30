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

class SingleOperatorPostDbTests(unittest.TestCase):
    def setUp(self):
        self.now = 1793299200
        self.review = review(self.now)
        self.record = {
            "source_sha": TARGET, "release_asset_sha256": SRC["release_asset_sha256"],
            "baseline_sha": host.BASELINE,
            "migration_id": "20261029120000_AddMemberHistoryProposals",
            "migration_blob_sha": "a" * 40, "dbcontext_blob_sha": "b" * 40,
            "migration_completed_at": 1793294400, "approved_at": self.now - 100,
            "additive_up_only": True, "previous_api_forward_compatible": True,
            "post_db_g6_archive_name": self.review["g6_archive_name"],
            "post_db_g6_archive_sha256": self.review["g6_archive_sha256"],
            "post_db_g6_restore_report_name": self.review["g6_restore_report_name"],
            "review_mode": "single_operator_qa", "environment": "QA_SYNTHETIC",
            "host": "glmdch", "scope": "POST_PR801_API_WEB_ONLY_NO_MIGRATION",
            "sponsor_approval_reference": host.SINGLE_OPERATOR_DECISION,
            "sponsor_accepts_single_operator_risk": True,
            "independent_human_review": False, "technical_review_kind": "automated_ai",
            "technical_review_reference": host.SINGLE_OPERATOR_DECISION,
            "operator_name": "Synthetic QA operator",
        }
        self.receipt = {"database_migrations": ["baseline"]}
        self.private_paths = []
        outer = self
        class Gate:
            MIGRATION_ID = outer.record["migration_id"]
            MIGRATION_BLOB = "a" * 40
            CONTEXT_BLOB = "b" * 40
            class Blocked(Exception):
                pass
        class Probe:
            POST_DB_PASS_STATUS = "POST_DB_99_MIGRATIONS_AND_EXACT_SCHEMA_READ_ONLY_PASS"
            class Blocked(Exception):
                pass
            def inspect(self, receipt, docker, gate):
                outer.assertIs(receipt, outer.receipt)
                outer.assertIs(gate, Gate)
                return {"status": self.POST_DB_PASS_STATUS, "history_count": 99,
                        "may_apply": False}
        self.gate, self.probe = Gate, Probe()

    def check(self, *, single=True):
        def private(path, maximum=65536):
            self.private_paths.append(path)
            if path in (host.SINGLE_OPERATOR_REVIEW, host.DBA_REVIEW):
                return self.record
            return self.receipt
        with (patch.object(host, "private_json", side_effect=private),
              patch.object(host, "validate_g6", return_value=True) as backup):
            answer = host.validate_post_db_dba(
                self.review, SRC, self.now, lambda _: "", self.gate, self.probe,
                single_operator=single)
            backup.assert_called_once_with(self.review, self.now)
            return answer

    def test_explicit_mode_accepts_realistic_claims_and_reads_separate_file(self):
        self.assertTrue(self.check())
        self.assertEqual(self.private_paths[0], host.SINGLE_OPERATOR_REVIEW)
        self.assertNotIn(host.DBA_REVIEW, self.private_paths)

    def test_default_dba_route_does_not_accept_single_operator_record(self):
        with self.assertRaises(host.Blocked):
            self.check(single=False)
        self.assertEqual(self.private_paths, [host.DBA_REVIEW])

    def test_missing_dba_acta_never_falls_back(self):
        with patch.object(host, "private_json", side_effect=host.Blocked("MISSING")) as read:
            with self.assertRaisesRegex(host.Blocked, "MISSING"):
                host.validate_post_db_dba(self.review, SRC, self.now,
                                         lambda _: "", self.gate, self.probe)
            read.assert_called_once_with(host.DBA_REVIEW, maximum=8192)

    def test_unknown_fields_and_claim_of_independent_dba_are_rejected(self):
        self.record["dba_independently_reviewed"] = True
        with self.assertRaisesRegex(host.Blocked, "FIELDS_INVALID"):
            self.check()

    def test_explicit_approval_attribution_scope_and_evidence_required(self):
        invalid = {
            "review_mode": "automatic", "environment": "PRODUCTION", "host": "srv01",
            "scope": "MIGRATE", "sponsor_approval_reference": "https://example.invalid/",
            "sponsor_accepts_single_operator_risk": False, "independent_human_review": True,
            "technical_review_kind": "human_dba", "technical_review_reference": "file:///secret",
            "operator_name": "\ninvalid",
        }
        for key, value in invalid.items():
            with self.subTest(key=key):
                original = self.record[key]
                self.record[key] = value
                with self.assertRaises(host.Blocked):
                    self.check()
                self.record[key] = original
        del self.record["sponsor_accepts_single_operator_risk"]
        with self.assertRaises(host.Blocked):
            self.check()

    def test_source_schema_backup_and_age_guards_still_enforced(self):
        for key, value in (
                ("source_sha", "e" * 40), ("release_asset_sha256", "sha256:" + "e" * 64),
                ("migration_blob_sha", "e" * 40), ("dbcontext_blob_sha", "e" * 40),
                ("previous_api_forward_compatible", False), ("additive_up_only", False),
                ("approved_at", self.now - 1801), ("approved_at", self.now + 1),
                ("migration_completed_at", 1793298000),
                ("post_db_g6_archive_sha256", "e" * 64)):
            with self.subTest(key=key, value=value):
                original = self.record[key]
                self.record[key] = value
                with self.assertRaises(host.Blocked):
                    self.check()
                self.record[key] = original

    def test_live_probe_failure_still_blocks(self):
        with patch.object(self.probe, "inspect", side_effect=self.probe.Blocked("DRIFT")):
            with self.assertRaisesRegex(host.Blocked, "SCHEMA_LIVE"):
                self.check()

    def test_backup_failure_still_blocks(self):
        with (patch.object(host, "private_json", return_value=self.record),
              patch.object(host, "validate_g6", side_effect=host.Blocked("G6_INVALID"))):
            with self.assertRaisesRegex(host.Blocked, "G6_INVALID"):
                host.validate_post_db_dba(self.review, SRC, self.now, lambda _: "",
                                         self.gate, self.probe, single_operator=True)

    def test_cli_requires_explicit_opt_in_and_keeps_legacy_modes(self):
        for action in ("--prepare-reviewed", "--apply-supervised"):
            self.assertEqual(host.supervision_mode([action]), (action, False))
            self.assertEqual(host.supervision_mode([action, "--single-operator-qa"]),
                             (action, True))
        for args in ([], ["--auto"], ["--single-operator-qa"],
                     ["--apply-supervised", "--single-operator-qa", "--force"]):
            with self.assertRaises(host.Blocked):
                host.supervision_mode(args)

    def test_single_operator_environment_disallows_migrations_and_account_activation(self):
        import json
        baseline = ["Database__ApplyMigrationsOnStartup=false",
                    "Database__RunMigrationsAndExit=false", "PRIVATE_VALUE=never_print"]
        self.assertEqual(len(host.single_operator_runtime(lambda _: json.dumps(baseline))), 64)
        for values in (baseline[1:], [baseline[0]],
                       [baseline[0].replace("false", "true"), *baseline[1:]],
                       [*baseline, "UserAccounts__Identity__EnableLifecycleMutations=true"],
                       [*baseline, "UserAccounts__Identity__RequireLiveTokenIntrospection=true"],
                       [*baseline, "database__applymigrationsonstartup=true"]):
            with self.subTest(values=values):
                with self.assertRaises(host.Blocked):
                    host.single_operator_runtime(lambda _: json.dumps(values))

    def test_prepared_images_cannot_cross_review_modes(self):
        host.validate_prepared_review_mode({}, False)  # historical DBA receipt
        host.validate_prepared_review_mode({"review_mode": "independent_dba"}, False)
        host.validate_prepared_review_mode({"review_mode": "single_operator_qa"}, True)
        for record, single in (({}, True), ({"review_mode": "independent_dba"}, True),
                               ({"review_mode": "single_operator_qa"}, False),
                               ({"review_mode": "unknown"}, True)):
            with self.assertRaisesRegex(host.Blocked, "PREPARED_REVIEW_MODE_MISMATCH"):
                host.validate_prepared_review_mode(record, single)

    def test_prepare_checks_disabled_flags_before_any_build(self):
        import json
        from types import SimpleNamespace
        docker = lambda _: json.dumps(["Database__ApplyMigrationsOnStartup=true"])
        with self.assertRaisesRegex(host.Blocked, "MIGRATION_FLAGS_NOT_DISABLED"):
            host.prepare_reviewed(None, None, None, SimpleNamespace(docker=docker),
                                  SRC, None, None, single_operator=True)


class PublicApiBudgetTests(unittest.TestCase):
    """Real metadata/staging orchestration with fake HTTP, signer and Docker."""

    def setUp(self):
        import copy
        from types import SimpleNamespace
        from test_qa525_current_release_gate import fixture, SHA
        self.sha = SHA
        self.db, _, _ = fixture()
        self.calls = []
        self.changed = False
        self.branch_drift = False
        self.manifest_bad = False
        self.copy = copy.deepcopy
        filename = FILE.parents[1] / "qa525-stage-current-release.py"
        spec = importlib.util.spec_from_file_location("qa525_budget_signed_stage", filename)
        self.signed = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.signed)
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.now = 1793299200
        self.adapter = SimpleNamespace(docker=lambda _: "not used")
        self.approval = SimpleNamespace(SCOPE="local", ACTION="reviewed", ENVIRONMENT="QA_SYNTHETIC",
                                        validate=lambda *args: {})
        self.build = SimpleNamespace(SCOPE="test", snapshot=lambda _: {"api": {"running": True}},
                                    builder=SimpleNamespace(prepare_web=None), build_images=self.images)

    def fetch(self, path):
        self.calls.append(path)
        return self.copy(self.db[path])

    def download(self, selection, archive):
        archive.write_bytes(b"synthetic signed envelope")
        return {"source_sha": self.sha, "sha256": selection["artifact_sha256"]}

    def sign(self, archive, digest, sha, output, *, tokenless):
        self.assertTrue(tokenless)
        output.mkdir()
        return {"status": "SIGNED_CODE_STAGED_NOT_INSTALLED", "source_sha": sha}

    def images(self, package, sha, verify, run, web):
        # Existing build_images + API and WEB builders have six source callbacks.
        if self.branch_drift:
            self.db["/branches/dev"] = {"commit": {"sha": "c" * 40}}
        for _ in range(6):
            host.require(verify(package, sha), "TEST_BUILD_GUARD_REJECTED")
        if self.changed:
            key = next(p for p in self.db if p.startswith("/actions/runs?"))
            self.db[key]["workflow_runs"][0]["conclusion"] = "failure"
        return {"api_image_id": "sha256:" + "1" * 64,
                "web_image_id": "sha256:" + "2" * 64}

    def prepare(self):
        gate = self.signed.gate
        with (patch.object(gate.readiness, "fetch_public", side_effect=self.fetch),
              patch.object(self.signed.download, "download", side_effect=self.download),
              patch.object(self.signed.staging, "stage", side_effect=self.sign),
              patch.object(self.signed.staging, "verify_manifest",
                           side_effect=self.signed.staging.Blocked("MANIFEST_CHANGED")
                           if self.manifest_bad else None),
              patch.object(host, "STATE", self.root),
              patch.object(host, "validate_g6", return_value=True),
              patch.object(host.time, "time", return_value=self.now)):
            source = gate.evaluate(gate.readiness.fetch_public, expected_sha=self.sha)
            approve = dict(target_sha=self.sha, release_asset_sha256=source["release_asset_sha256"],
                           scope=self.approval.SCOPE, action=self.approval.ACTION,
                           environment=self.approval.ENVIRONMENT, operator_reviewed=True,
                           issued_at=self.now - 1, expires_at=self.now + 100)
            host.prepare_reviewed(self.build, self.approval, self.signed, self.adapter,
                                  source, approve, {})
            return source

    def test_preparation_callbacks_fit_public_quota_without_reusing_receipts(self):
        import json
        self.prepare()
        self.assertEqual(len(self.calls), 18)  # +3 live diff/blob reads in main =21
        inventories = [p for p in self.calls if p.startswith("/actions/runs?")]
        self.assertEqual(len(inventories), 3)  # start, signature end, build end
        self.assertTrue(json.loads((self.root / "prepared.json").read_text())["images_built"])

    def test_ci_revoked_during_build_never_produces_prepared_receipt(self):
        self.changed = True
        with self.assertRaises(self.signed.gate.readiness.GateFailure):
            self.prepare()
        self.assertFalse((self.root / "prepared.json").exists())

    def test_head_drift_and_manifest_tampering_block_build_callbacks(self):
        self.branch_drift = True
        with self.assertRaisesRegex(host.Blocked, "TEST_BUILD_GUARD_REJECTED"):
            self.prepare()
        self.assertFalse((self.root / "prepared.json").exists())
        self.db["/branches/dev"] = {"commit": {"sha": self.sha}}
        self.branch_drift = False
        self.manifest_bad = True
        with self.assertRaisesRegex(host.Blocked, "TEST_BUILD_GUARD_REJECTED"):
            self.prepare()
        self.assertFalse((self.root / "prepared.json").exists())

    def test_same_comparison_is_classified_and_schema_checked_with_three_reads(self):
        from types import SimpleNamespace
        class Gate:
            MIGRATION_PATH = "backend/src/PMGM.Api/Migrations/Known.cs"
            CONTEXT_PATH = "backend/src/PMGM.Api/Data/PmgmDbContext.cs"
            MIGRATION_BLOB = "a" * 40
            CONTEXT_BLOB = "b" * 40
            class Blocked(Exception):
                pass
            @classmethod
            def reviewed_diff(cls, files, blobs):
                if blobs != {cls.MIGRATION_PATH: cls.MIGRATION_BLOB,
                             cls.CONTEXT_PATH: cls.CONTEXT_BLOB}:
                    raise cls.Blocked("BLOB")
        diff = {"status": "ahead", "files": [
            {"filename": Gate.MIGRATION_PATH, "status": "added"},
            {"filename": Gate.CONTEXT_PATH, "status": "modified"}]}
        self.db["/compare/" + host.BASELINE + "..." + self.sha] = diff
        self.db["/contents/" + Gate.MIGRATION_PATH + "?ref=" + self.sha] = {"sha": Gate.MIGRATION_BLOB}
        self.db["/contents/" + Gate.CONTEXT_PATH + "?ref=" + self.sha] = {"sha": Gate.CONTEXT_BLOB}
        classified = []
        def classify(baseline, source, comparison):
            self.assertEqual(baseline, host.BASELINE)
            self.assertEqual(comparison, diff)
            classified.append(True)
            return {"status": "BLOCKED_REVIEW_REQUIRED"}
        with patch.object(self.signed.gate.readiness, "fetch_public", side_effect=self.fetch):
            host.current_change_review(SimpleNamespace(classify=classify), self.signed,
                                       {"source_sha": self.sha}, Gate)
        self.assertEqual(len(self.calls), 3)
        self.assertEqual(classified, [True])

    def test_diagnostic_exposes_only_allowlisted_public_api_codes(self):
        self.assertEqual(host.public_failure_code(ValueError("PRIVATE_SECRET_EXAMPLE")),
                         "SUPERVISED_APPLY_NOT_COMPLETED")
        self.assertEqual(host.public_failure_code(self.signed.gate.readiness.GateFailure(
            "GITHUB_API_RATE_LIMITED")), "GITHUB_API_RATE_LIMITED")

    def test_recreation_and_post_phases_always_fetch_fresh_full_gate(self):
        with patch.object(self.signed.gate.readiness, "fetch_public", side_effect=self.fetch):
            source = self.signed.gate.evaluate(self.fetch, expected_sha=self.sha)
            self.calls.clear()
            self.assertEqual(host.phase_source(self.signed, source, "pre"), source)
            self.assertEqual(self.calls, [])  # no recreation before next phase
            self.assertEqual(host.phase_source(self.signed, source, "just-before-apply"), source)
            self.assertEqual(len(self.calls), 4)
            key = next(p for p in self.db if p.startswith("/actions/runs?"))
            self.db[key]["workflow_runs"][0]["conclusion"] = "failure"
            with self.assertRaises(self.signed.gate.readiness.GateFailure):
                host.phase_source(self.signed, source, "post")
            with self.assertRaises(host.Blocked):
                host.phase_source(self.signed, source, "skip")


if __name__ == "__main__":
    unittest.main()
