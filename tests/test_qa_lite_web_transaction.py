"""QA #525: adversarial, fully offline transaction and rollback tests."""
import importlib.util
from datetime import datetime, timezone, timedelta
from pathlib import Path
import unittest

path = Path(__file__).resolve().parents[1] / "scripts/qa/qa-lite-web-transaction.py"
spec = importlib.util.spec_from_file_location("qa_lite_web_transaction", path)
import sys
mod = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = mod
spec.loader.exec_module(mod)

OLD = "a" * 40
NEW = "b" * 40
IMAGE_OLD = "sha256:" + "c" * 64
IMAGE_NEW = "sha256:" + "d" * 64
NOW = datetime(2026, 10, 19, 20, 0, tzinfo=timezone.utc)
POLICY = {
    "mode": "QA-AUTO-LIGERO", "approved": True, "host": "glmdch",
    "project": "centenario-qa-lab2", "branch": "dev", "qa_only": True,
    "allowed_services": ["web"], "allow_db_migrations": False,
    "allow_storage_changes": False,
}
INSTALLED = {"source_sha": OLD, "web_image_id": IMAGE_OLD}
CANDIDATE = {
    "state": "VERIFIED_SIGNED_RELEASE_FROM_DEV",
    "complete_release_gate": True, "fast_forward_from_sha": OLD,
    "source_sha": NEW, "web_image_id": IMAGE_NEW,
    "image_attestation_verified": True,
}
BACKUP = {
    "checkpoint_at": NOW - timedelta(minutes=30),
    "offhost_ciphertext_sha256_matches": True,
    "pg_both_and_globals": True, "silo_objects": True,
    "restic_integrity": True, "isolated_restore_procedure_tested": True,
    "sha256": "e" * 64,
}
RUNTIME = {
    "hostname": "glmdch", "compose_project": "centenario-qa-lab2",
    "running_web_image_id": IMAGE_OLD,
    "postgres_volume_preserved": True, "silo_volume_preserved": True,
    "keycloak_database_preserved": True, "rollback_image_pinned": True,
    "migration_on_startup_disabled": True, "network_unchanged": True,
}


class FakeAdapter:
    def __init__(self, fail=None):
        self.fail = fail
        self.log = []

    def reverify(self, plan):
        self.log.append("reverify")
        return self.fail != "reverify"

    def activate_web(self, plan):
        self.log.append("activate")
        if self.fail == "activate":
            raise RuntimeError("simulated interrupted change")

    def verify_running_web(self, image):
        self.log.append("inspect:" + ("new" if image == IMAGE_NEW else "old"))
        if image == IMAGE_NEW:
            return self.fail != "new_image"
        return self.fail != "rollback_image"

    def verify_untouched_dependencies(self):
        self.log.append("dependencies")
        return self.fail != "dependencies"

    def verify_public_health(self):
        self.log.append("health")
        if self.fail == "health":
            return False
        return self.fail != "rollback_health"

    def restore_previous_web(self, plan):
        self.log.append("restore")
        if self.fail == "restore":
            raise RuntimeError("simulated rollback failure")


class WebTransactionTests(unittest.TestCase):
    def plan(self, **kwargs):
        return mod.make_plan(policy=kwargs.get("policy", POLICY),
                             installed=kwargs.get("installed", INSTALLED),
                             candidate=kwargs.get("candidate", CANDIDATE),
                             backup=kwargs.get("backup", BACKUP),
                             runtime=kwargs.get("runtime", RUNTIME),
                             now=kwargs.get("now", NOW))

    def blocks(self, code, **kwargs):
        with self.assertRaisesRegex(mod.Blocked, code):
            self.plan(**kwargs)

    def test_plan_allowlist_is_only_web(self):
        plan = self.plan()
        self.assertEqual(("centenario-qa-lab2", NEW, IMAGE_NEW),
                         (plan.project, plan.candidate_sha, plan.candidate_image_id))
        self.assertEqual("WEB_ONLY_NO_DB_NO_SILO", plan.action)

    def test_policy_does_not_authorize_database_storage_or_main(self):
        for modkey in (
            {"allowed_services": ["web", "api"]},
            {"allow_db_migrations": True},
            {"allow_storage_changes": True},
            {"branch": "main"},
            {"host": "srv01"},
            {"project": "pmgm-srv01"},
            {"approved": False},
        ):
            self.blocks("SCOPE_NOT_APPROVED", policy={**POLICY, **modkey})

    def test_candidate_must_be_signed_and_fast_forward(self):
        for modkey in (
            {"state": "UNSIGNED"},
            {"complete_release_gate": False},
            {"image_attestation_verified": False},
            {"fast_forward_from_sha": "0" * 40},
            {"source_sha": OLD},
            {"web_image_id": "garbage"},
        ):
            self.blocks("CANDIDATE_PROVENANCE_UNVERIFIED",
                        candidate={**CANDIDATE, **modkey})
        self.blocks("PHYSICAL_BASELINE_UNVERIFIED", installed={"source_sha": None})

    def test_backup_requires_offhost_and_all_data(self):
        for modkey in (
            {"checkpoint_at": NOW - timedelta(days=8)},
            {"offhost_ciphertext_sha256_matches": False},
            {"pg_both_and_globals": False},
            {"silo_objects": False},
            {"restic_integrity": False},
            {"isolated_restore_procedure_tested": False},
            {"sha256": None},
        ):
            self.blocks("CURRENT_OFFHOST_BACKUP_UNVERIFIED",
                        backup={**BACKUP, **modkey})

    def test_runtime_rejects_migrations_or_changed_volumes(self):
        for modkey in (
            {"hostname": "srv01"}, {"compose_project": "pmgm-srv01"},
            {"running_web_image_id": IMAGE_NEW},
            {"postgres_volume_preserved": False},
            {"silo_volume_preserved": False},
            {"keycloak_database_preserved": False},
            {"rollback_image_pinned": False},
            {"migration_on_startup_disabled": False},
            {"network_unchanged": False},
        ):
            self.blocks("HOST_OR_ROLLBACK_PREFLIGHT_FAILED",
                        runtime={**RUNTIME, **modkey})

    def test_success_has_no_declaration_of_authenticated_uat(self):
        result = mod.coordinate(self.plan(), FakeAdapter())
        self.assertEqual("WEB_HEALTHY_PENDING_AUTHENTICATED_QA", result["status"])

    def test_reverify_blocks_before_mutation(self):
        adapter = FakeAdapter("reverify")
        with self.assertRaisesRegex(mod.Blocked, "PREFLIGHT_CHANGED"):
            mod.coordinate(self.plan(), adapter)
        self.assertEqual(["reverify"], adapter.log)

    def test_failed_apply_rolls_back_even_if_activation_partially_failed(self):
        for failure in ("activate", "new_image", "dependencies", "health"):
            with self.subTest(failure=failure):
                adapter = FakeAdapter(failure)
                with self.assertRaisesRegex(mod.Blocked, "APPLY_FAILED_ROLLBACK_VERIFIED"):
                    mod.coordinate(self.plan(), adapter)
                self.assertIn("restore", adapter.log)
                self.assertIn("inspect:old", adapter.log)

    def test_rollback_failure_is_explicit_not_success(self):
        for failure in ("restore", "rollback_image", "rollback_health"):
            adapter = FakeAdapter(failure)
            if failure != "restore":
                adapter.fail = "health"
                original_restore = adapter.restore_previous_web
                if failure == "rollback_image":
                    adapter.verify_running_web = lambda image: image != IMAGE_OLD
                else:
                    calls = [0]
                    def only_rollback_fails():
                        calls[0] += 1
                        return calls[0] != 2
                    adapter.verify_public_health = only_rollback_fails
            with self.assertRaisesRegex(mod.Blocked, "ROLLBACK_FAILED_MANUAL_RECOVERY"):
                if failure == "restore":
                    # Make apply fail, then rollback itself fail.
                    adapter.activate_web = lambda plan: (_ for _ in ()).throw(RuntimeError("apply"))
                mod.coordinate(self.plan(), adapter)


if __name__ == "__main__":
    unittest.main()
