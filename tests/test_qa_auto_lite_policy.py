"""Offline adversarial policy checks; nothing invokes Docker or accesses network."""
from datetime import datetime, timezone
import importlib.util
from pathlib import Path
import unittest

MODULE = Path(__file__).resolve().parents[1] / "scripts/qa/qa-auto-lite-policy.py"
SPEC = importlib.util.spec_from_file_location("qa_auto_lite_policy", MODULE)
qa = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(qa)

OLD = "a" * 40
NEW = "b" * 40
NOW = datetime(2026, 10, 19, 19, tzinfo=timezone.utc)
POLICY = {
    "mode": "QA-AUTO-LIGERO", "approved": True, "host": "glmdch",
    "project": "centenario-qa-lab2", "source_branch": "dev", "qa_only": True,
    "services": ["web", "api"], "allow_destructive_migrations": False,
    "allow_public_network_changes": False,
}
STAGED = {"status": "SIGNED_QA_CANDIDATE_STAGED_NO_DEPLOYMENT",
          "scope": "NO_DOCKER_NO_INSTALLATION_NO_QA_ACCEPTANCE",
          "source_sha": NEW, "artifact_id": 123456}
ANCESTRY = {"status": "FAST_FORWARD_CANDIDATE_ONLY",
            "previous_deployed_sha": OLD, "candidate_sha": NEW}
BACKUP = {"checkpoint_at_utc": "2026-10-19T17:52:18Z",
          "offhost_verified_at_utc": "2026-10-19T18:20:00Z",
          "sha256": "c" * 64, "postgres_pmgm": True,
          "postgres_keycloak": True, "postgres_globals": True,
          "silo_current_objects": True, "restic_check": True,
          "recovery_procedure_tested": True, "encrypted_only": True,
          "offhost_location_verified": True}
RUNTIME = {"hostname": "glmdch", "project": "centenario-qa-lab2",
           "qa_only": True, "current_image_sha_verified": True,
           "volumes_preserved": True, "rollback_images_pinned": True,
           "migrations_disabled": True, "schema_compatible": True,
           "network_unchanged": True, "public_ports_unchanged": True,
           "pending_password_change_after_backup": False}


class AutoLitePolicyTests(unittest.TestCase):
    def call(self, policy=None, installed=OLD, staged=None, ancestry=None, backup=None, runtime=None, now=NOW):
        return qa.evaluate(POLICY if policy is None else policy,
                           installed, STAGED if staged is None else staged,
                           ANCESTRY if ancestry is None else ancestry,
                           BACKUP if backup is None else backup,
                           RUNTIME if runtime is None else runtime, now)

    def block(self, expected, **kw):
        with self.assertRaisesRegex(qa.Blocked, expected):
            self.call(**kw)

    def test_review_only_success(self):
        report = self.call()
        self.assertEqual("ELIGIBLE_FOR_RESTRICTED_QA_EXECUTOR_REVIEW_ONLY", report["status"])
        self.assertIn("NO_DOCKER", report["scope"])
        self.assertEqual(NEW, report["source_sha"])
        self.assertNotIn("credentials", report)

    def test_no_supervision_bypass(self):
        self.block("POLICY_SCOPE_FORBIDDEN", policy={**POLICY, "approved": False})
        self.block("POLICY_SCOPE_FORBIDDEN", policy={**POLICY, "project": "srv01"})
        self.block("POLICY_SCOPE_FORBIDDEN", policy={**POLICY, "source_branch": "main"})
        self.block("POLICY_SCOPE_FORBIDDEN", policy={**POLICY, "qa_only": False})
        self.block("POLICY_SCOPE_FORBIDDEN", policy={**POLICY, "services": ["postgres", "api"]})
        self.block("POLICY_SCOPE_FORBIDDEN", policy={**POLICY, "allow_destructive_migrations": True})
        self.block("POLICY_SCHEMA_INVALID", policy={**POLICY, "shell": "sudo bash"})

    def test_no_first_boot_or_stale_candidate(self):
        self.block("TRUSTED_BASELINE_MISSING", installed=None)
        self.block("ALREADY_DEPLOYED", installed=NEW)
        self.block("SIGNED_STAGING_UNVERIFIED", staged={**STAGED, "status": "UNSIGNED"})
        self.block("SIGNED_STAGING_UNVERIFIED", staged={**STAGED, "artifact_id": True})
        self.block("FAST_FORWARD_NOT_VERIFIED", ancestry={**ANCESTRY, "status": "diverged"})
        self.block("FAST_FORWARD_NOT_VERIFIED", ancestry={**ANCESTRY, "candidate_sha": "d" * 40})

    def test_backup_must_be_offhost_and_fresh(self):
        self.block("BACKUP_RECEIPT_MISSING", backup={})
        self.block("BACKUP_NOT_RECENT_OR_OFFHOST", backup={**BACKUP, "checkpoint_at_utc": "2026-10-15T01:00:00Z"})
        self.block("BACKUP_NOT_RECENT_OR_OFFHOST", backup={**BACKUP, "offhost_verified_at_utc": "2026-10-20T01:00:00Z"})
        self.block("BACKUP_HASH_INVALID", backup={**BACKUP, "sha256": "bad"})
        self.block("BACKUP_INCOMPLETE", backup={**BACKUP, "offhost_location_verified": False})
        self.block("BACKUP_INCOMPLETE", backup={**BACKUP, "postgres_keycloak": False})
        self.block("BACKUP_INCOMPLETE", backup={**BACKUP, "silo_current_objects": False})
        self.block("BACKUP_INCOMPLETE", backup={**BACKUP, "recovery_procedure_tested": False})

    def test_runtime_protection(self):
        self.block("RUNTIME_PREFLIGHT_INCOMPLETE", runtime={})
        self.block("HOST_IDENTITY_UNVERIFIED", runtime={**RUNTIME, "hostname": "other"})
        self.block("HOST_IDENTITY_UNVERIFIED", runtime={**RUNTIME, "current_image_sha_verified": False})
        self.block("ROLLBACK_OR_SCHEMA_NOT_SAFE", runtime={**RUNTIME, "rollback_images_pinned": False})
        self.block("ROLLBACK_OR_SCHEMA_NOT_SAFE", runtime={**RUNTIME, "migrations_disabled": False})
        self.block("ROLLBACK_OR_SCHEMA_NOT_SAFE", runtime={**RUNTIME, "volumes_preserved": False})
        self.block("NETWORK_OR_BACKUP_STATE_CHANGED", runtime={**RUNTIME, "network_unchanged": False})
        self.block("NETWORK_OR_BACKUP_STATE_CHANGED", runtime={**RUNTIME, "pending_password_change_after_backup": True})

    def test_timestamps_not_synthetic_shortcut(self):
        self.block("INVALID_BACKUP_TIMESTAMP", backup={**BACKUP, "checkpoint_at_utc": "yesterday"})
        self.block("INVALID_CHECK_TIME", now=NOW.replace(tzinfo=None))


if __name__ == "__main__":
    unittest.main()
