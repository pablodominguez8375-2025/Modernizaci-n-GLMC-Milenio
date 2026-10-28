"""QA525 candidate review: offline, adversarial and no host side effects."""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location(
    "qa525_change_policy_test", ROOT / "scripts/qa/operators/qa525-current-change-policy.py")
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

BASE = "a" * 40
TARGET = "b" * 40
SIGNED = {
    "status": "READY_FOR_SIGNATURE_VERIFICATION_ONLY",
    "scope": mod.gate.RESULT_SCOPE,
    "source_sha": TARGET,
    "release_tag": "qa-signed-" + TARGET,
    "release_asset_sha256": "sha256:" + "c" * 64,
}


def comparison(*names):
    return {
        "status": "ahead",
        "merge_base_commit": {"sha": BASE},
        "ahead_by": 4,
        "behind_by": 0,
        "files": [
            {"filename": name, "status": "modified"}
            for name in names
        ],
    }


class CandidatePolicyTests(unittest.TestCase):
    def test_frontend_only_is_host_preflight_candidate_not_deployment(self):
        result = mod.classify(BASE, SIGNED, comparison("frontend/src/LodgePanel.tsx"))
        self.assertEqual("ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY", result["status"])
        self.assertEqual([], result["reasons"])
        self.assertFalse(result["may_apply"])
        self.assertFalse(result["host_baseline_and_backup_verified"])
        self.assertTrue(result["source_signed_staging_required"])

    def test_program_bootstrap_is_always_review_required(self):
        result = mod.classify(BASE, SIGNED, comparison("backend/src/PMGM.Api/Program.cs"))
        self.assertEqual("BLOCKED_REVIEW_REQUIRED", result["status"])
        self.assertIn("API_BOOTSTRAP_CHANGE", result["reasons"])

    def test_migrations_and_dbcontext_block(self):
        for path in ("backend/src/PMGM.Api/Migrations/20261028_Change.cs",
                     "backend/src/PMGM.Api/Data/PmgmDbContext.cs"):
            with self.subTest(path=path):
                decision = mod.classify(BASE, SIGNED, comparison(path))
                self.assertIn("SCHEMA_OR_MODEL_CHANGE", decision["reasons"])

    def test_identity_accounts_and_frontend_access_block(self):
        names = (
            "backend/src/PMGM.Api/Modules/UserAccounts/UserAccountEndpoints.cs",
            "backend/src/PMGM.Api/Modules/UserAccounts/AccountTokenIntrospection.cs",
            "frontend/src/api/userAccounts.ts",
            "frontend/src/AccountLifecycleDrawer.tsx",
            "backend/src/PMGM.Api/Modules/Identity/Login.cs",
        )
        for name in names:
            with self.subTest(name=name):
                x = mod.classify(BASE, SIGNED, comparison(name))
                self.assertEqual("BLOCKED_REVIEW_REQUIRED", x["status"])
                self.assertIn("ACCOUNT_OR_IDENTITY_CHANGE", x["reasons"])

    def test_compose_docker_keycloak_and_frontend_build_block(self):
        for filename in ("backend/Dockerfile", "infra/compose.qa.yml",
                         "frontend/package-lock.json", "frontend/src/main.tsx",
                         "backend/src/PMGM.Api/appsettings.Production.json"):
            with self.subTest(filename=filename):
                r = mod.classify(BASE, SIGNED, comparison(filename))
                self.assertEqual("BLOCKED_REVIEW_REQUIRED", r["status"])

    def test_baseline_must_be_ancestor_and_exact_sha(self):
        for changed in (
                {"status": "diverged"},
                {"behind_by": 1},
                {"merge_base_commit": {"sha": "0" * 40}},
                {"ahead_by": 250},
                {"files": []},
        ):
            with self.subTest(changed=changed):
                with self.assertRaises(mod.Blocked):
                    mod.classify(BASE, SIGNED, {**comparison("frontend/src/LodgePanel.tsx"), **changed})
        with self.assertRaisesRegex(mod.Blocked, "BASELINE_EQUALS"):
            mod.classify(TARGET, SIGNED, comparison("frontend/src/LodgePanel.tsx"))
        with self.assertRaisesRegex(mod.Blocked, "BASELINE_SHA_INVALID"):
            mod.classify("../dev", SIGNED, comparison("frontend/src/LodgePanel.tsx"))

    def test_unsigned_or_old_selection_blocks_before_diff_analysis(self):
        for changed in ({"status": "APPLIED"}, {"scope": "MAY_DEPLOY"},
                        {"source_sha": "dev"}, {"source_sha": ""}):
            with self.subTest(changed=changed):
                with self.assertRaisesRegex(mod.Blocked, "UNVERIFIED_RELEASE"):
                    mod.classify(BASE, {**SIGNED, **changed}, comparison("frontend/src/LodgePanel.tsx"))

    def test_unexpected_duplicate_or_traversal_path_rejected(self):
        for name in ("../secrets", "/etc/pmgm/master-qa.env",
                     "frontend//src/x.ts", "frontend\\src\\bad.ts"):
            with self.subTest(name=name):
                with self.assertRaisesRegex(mod.Blocked, "DIFF_FILE_UNTRUSTED"):
                    mod.classify(BASE, SIGNED, comparison(name))
        d = comparison("frontend/src/LodgePanel.tsx", "frontend/src/LodgePanel.tsx")
        with self.assertRaisesRegex(mod.Blocked, "DIFF_FILE_UNTRUSTED"):
            mod.classify(BASE, SIGNED, d)

    def test_renamed_original_identity_requires_review(self):
        d = comparison("frontend/src/RegularityPage.tsx")
        d["files"][0]["status"] = "renamed"
        d["files"][0]["previous_filename"] = "frontend/src/UserAccountsPanel.tsx"
        x = mod.classify(BASE, SIGNED, d)
        self.assertIn("ACCOUNT_OR_IDENTITY_CHANGE", x["reasons"])
        self.assertIn("RENAMED_SOURCE_FILE", x["reasons"])

    def test_incomplete_rename_blocks(self):
        d = comparison("frontend/src/Panel.tsx")
        d["files"][0]["status"] = "renamed"
        with self.assertRaisesRegex(mod.Blocked, "RENAMED_PREVIOUS"):
            mod.classify(BASE, SIGNED, d)

    def test_real_case_api_account_lifecycle_requires_review(self):
        result = mod.classify(BASE, SIGNED, comparison(
            "backend/src/PMGM.Api/Program.cs",
            "backend/src/PMGM.Api/Modules/UserAccounts/AccountLifecyclePolicy.cs",
            "frontend/src/CandidateWorkshopIntakePage.tsx"))
        self.assertEqual(
            ["ACCOUNT_OR_IDENTITY_CHANGE", "API_BOOTSTRAP_CHANGE"], result["reasons"])

    def test_change_during_gate_blocks_even_clean_diff(self):
        first, later = SIGNED, {**SIGNED, "source_sha": "e" * 40}
        fetch = Mock(return_value=comparison("frontend/src/LodgePanel.tsx"))
        with patch.object(mod.gate, "evaluate", side_effect=[first, later]):
            with self.assertRaisesRegex(mod.Blocked, "CURRENT_RELEASE_CHANGED"):
                mod.evaluate(fetch, BASE)
        fetch.assert_called_once_with("/compare/" + BASE + "..." + TARGET)

    def test_current_stable_gate_retains_no_deploy_policy(self):
        fetch = Mock(return_value=comparison("frontend/src/LodgePanel.tsx"))
        with patch.object(mod.gate, "evaluate", return_value=SIGNED) as gate:
            outcome = mod.evaluate(fetch, BASE)
            self.assertEqual(2, gate.call_count)
        self.assertEqual("ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY", outcome["status"])
        self.assertFalse(outcome["may_apply"])

    def test_not_a_shell_docker_or_file_mutation_operator(self):
        src = (ROOT / "scripts/qa/operators/qa525-current-change-policy.py").read_text()
        for forbidden in ("subprocess", "os.system", "docker", "open(", "write_text(",
                          "write_bytes(", "systemctl", "GH_TOKEN"):
            # Documentation mentions "installer", no invocation exists.
            self.assertNotIn(forbidden, src)


if __name__ == "__main__":
    unittest.main()
