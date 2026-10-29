"""Hermetic public-diff risk packet tests: no GitHub, Docker or secrets."""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import Mock

FILE = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-sensitive-change-review.py"
spec = importlib.util.spec_from_file_location("qa525_sensitive_report_tests", FILE)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
BASE = "a" * 40
HEAD = "b" * 40


def fixture(names, rename=None):
    data = {
        "status": "ahead",
        "merge_base_commit": {"sha": BASE},
        "behind_by": 0,
        "ahead_by": 17,
        "files": [{"filename": name, "status": "modified"} for name in names],
    }
    if rename:
        data["files"].append({
            "filename": rename[1], "previous_filename": rename[0],
            "status": "renamed",
        })
    selected = {
        "status": "READY_FOR_SIGNATURE_VERIFICATION_ONLY",
        "scope": mod.policy.gate.RESULT_SCOPE,
        "source_sha": HEAD,
        "release_asset_sha256": "sha256:" + "c" * 64,
    }
    return selected, data


class SensitiveReportTests(unittest.TestCase):
    def test_identity_and_bootstrap_generate_mandatory_manual_review(self):
        sel, diff = fixture([
            "backend/src/PMGM.Api/Program.cs",
            "backend/src/PMGM.Api/Modules/UserAccounts/AccountLifecyclePolicy.cs",
            "frontend/src/App.tsx",
        ])
        result = mod.report(BASE, sel, diff)
        self.assertEqual("BLOCKED_SENSITIVE_CHANGE_SUPERVISED_REVIEW_REQUIRED",
                         result["status"])
        self.assertEqual(["ACCOUNT_OR_IDENTITY_CHANGE", "API_BOOTSTRAP_CHANGE"],
                         [g["reason"] for g in result["groups"]])
        self.assertEqual(3, result["changed_files_total"])
        self.assertEqual(17, result["changed_commit_count"])
        self.assertFalse(result["may_apply"])
        self.assertFalse(result["manual_review_completed"])
        self.assertFalse(result["signed_asset_download_verified"])
        self.assertIn("Keycloak/OIDC", " ".join(result["groups"][0]["review_items"]))

    def test_no_sensitive_files_never_authorizes_apply(self):
        sel, diff = fixture(["frontend/src/components/Welcome.tsx"])
        result = mod.report(BASE, sel, diff)
        self.assertEqual(
            "ANALYTICAL_DIFF_NO_SENSITIVE_FLAGS_HOST_REVIEW_STILL_REQUIRED",
            result["status"])
        self.assertEqual([], result["groups"])
        self.assertFalse(result["may_build"])
        self.assertFalse(result["may_apply"])

    def test_migrations_demand_separate_restore_approval(self):
        sel, diff = fixture([
            "backend/src/PMGM.Api/Migrations/20261029_X.cs",
            "infrastructure/docker-compose.srv01.yml",
        ])
        result = mod.report(BASE, sel, diff)
        self.assertEqual(
            ["INFRASTRUCTURE_CHANGE", "SCHEMA_OR_MODEL_CHANGE"],
            [g["reason"] for g in result["groups"]])
        self.assertIn("migration", " ".join(result["manual_evidence_required"]).lower())

    def test_renamed_sensitive_path_included_with_previous_name(self):
        sel, diff = fixture(["frontend/src/Welcome.tsx"], rename=(
            "backend/src/PMGM.Api/Modules/UserAccounts/Old.cs",
            "frontend/src/New.tsx",
        ))
        result = mod.report(BASE, sel, diff)
        self.assertEqual(
            ["ACCOUNT_OR_IDENTITY_CHANGE", "RENAMED_SOURCE_FILE"],
            [g["reason"] for g in result["groups"]])
        self.assertIn("backend/src/PMGM.Api/Modules/UserAccounts/Old.cs",
                      result["groups"][0]["affected_paths_sample"])

    def test_disallows_broken_diff_ancestry(self):
        sel, diff = fixture(["backend/src/PMGM.Api/Program.cs"])
        diff["merge_base_commit"]["sha"] = "c" * 40
        with self.assertRaises(mod.policy.Blocked):
            mod.report(BASE, sel, diff)

    def test_disallows_more_than_299_files(self):
        sel, diff = fixture([f"frontend/src/p{i}.tsx" for i in range(300)])
        with self.assertRaises(mod.policy.Blocked):
            mod.report(BASE, sel, diff)

    def test_path_sample_truncation_is_explicit(self):
        sel, diff = fixture([
            f"backend/src/PMGM.Api/Modules/UserAccounts/A{i}.cs" for i in range(40)
        ])
        result = mod.report(BASE, sel, diff)
        group = result["groups"][0]
        self.assertEqual(40, group["affected_path_count"])
        self.assertEqual(25, len(group["affected_paths_sample"]))
        self.assertTrue(group["sample_truncated"])

    def test_blocks_unrecognized_change_status(self):
        sel, diff = fixture(["frontend/src/App.tsx"])
        diff["files"][0]["status"] = "UNTRUSTED"
        with self.assertRaises(mod.policy.Blocked):
            mod.report(BASE, sel, diff)

    def test_remote_gate_is_checked_twice_around_compare(self):
        sel, diff = fixture(["backend/src/PMGM.Api/Program.cs"])
        calls = []
        def fetch(path):
            calls.append(path)
            if path.startswith("/compare/"):
                return diff
            raise AssertionError("gate mock must not call remote")
        previous = mod.policy.gate.evaluate
        try:
            mod.policy.gate.evaluate = Mock(side_effect=[sel, sel])
            report = mod.live_report(BASE, fetch)
            self.assertIn("SENSITIVE", report["status"])
            self.assertEqual(2, mod.policy.gate.evaluate.call_count)
            self.assertEqual(["/compare/" + BASE + "..." + HEAD], calls)
        finally:
            mod.policy.gate.evaluate = previous

    def test_changed_signed_gate_blocks(self):
        sel, diff = fixture(["backend/src/PMGM.Api/Program.cs"])
        newer = dict(sel, source_sha="c" * 40)
        previous = mod.policy.gate.evaluate
        try:
            mod.policy.gate.evaluate = Mock(side_effect=[sel, newer])
            with self.assertRaisesRegex(mod.Blocked, "SIGNED_DEV_CHANGED"):
                mod.live_report(BASE, lambda _: diff)
        finally:
            mod.policy.gate.evaluate = previous

    def test_no_execution_or_deployment_interface(self):
        source = FILE.read_text()
        for forbidden in ("subprocess", "os.system", "docker compose up",
                          "pg_dump", "systemctl", "ssh ", "may_apply\": True"):
            self.assertNotIn(forbidden, source)
        self.assertIn('"may_apply": False', source)


if __name__ == "__main__":
    unittest.main()
