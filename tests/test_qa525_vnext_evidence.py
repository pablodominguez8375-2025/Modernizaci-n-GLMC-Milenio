"""Hermetic tests: plan stages advance only with verifier-derived evidence."""
import copy
import importlib.util
from pathlib import Path
import unittest

import test_qa525_vnext_baseline_plan as bp
import test_qa525_vnext_integration_contract as ic

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "qa" / "qa525-vnext-evidence.py"
spec = importlib.util.spec_from_file_location("qa525_vnext_evidence", SCRIPT)
ev = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ev)
verify, plan_mod = ev.verify, ev.plan_mod


def api_only(base, goal):
    obs = ic.observation(base, goal)
    obs["images"]["web"] = base["images"]["web"]
    obs["layers"]["web"] = copy.deepcopy(base["layers"]["web"])
    obs["health"]["web_sha"] = base["web_sha"]
    return obs


def result(kind, base, goal, observation):
    target = None if kind == "ROLLED_BACK" else goal
    out = verify.verify(ic.Observed(observation), base, kind, target)
    assert out["ok"], out
    return out


def results(base, goal):
    return {
        "API_VERIFIED": result("API_SWITCHED", base, goal, api_only(base, goal)),
        "WEB_VERIFIED": result("WEB_SWITCHED", base, goal, ic.observation(base, goal)),
        "VERIFYING": result("APPLIED", base, goal, ic.observation(base, goal)),
        "ROLLBACK_VERIFYING": result("ROLLED_BACK", base, goal,
                                     ic.observation(base, goal, use_target=False)),
    }


class EvidenceGlueTests(unittest.TestCase):
    def setUp(self):
        self.base, self.goal = bp.baseline(), bp.target()
        self.plan = bp.make_plan(self.base, self.goal)
        self.res = results(self.base, self.goal)

    def to(self, plan, *stages):
        for stage in stages:
            plan = (ev.advance_with_verification(plan, stage, self.res[stage])
                    if stage in ev.STAGE_KIND else
                    plan_mod.advance(plan, stage, bp.evidence(plan, stage)))
        return plan

    def test_full_forward_path_uses_verifier_derived_evidence(self):
        plan = self.to(self.plan, "API_SWITCHING", "API_VERIFIED", "WEB_SWITCHING",
                       "WEB_VERIFIED", "VERIFYING")
        self.assertEqual(plan_mod.validate(plan), "VERIFYING")
        by_stage = {e["to"]: e["proof"]["evidence_sha256"] for e in plan["events"]}
        for stage in ev.STAGE_KIND:
            if stage in by_stage:
                self.assertEqual(by_stage[stage], self.res[stage]["result_checksum_sha256"])
        self.assertFalse(plan["may_apply"])

    def test_rollback_path_uses_a_rolled_back_result_of_the_baseline(self):
        plan = self.to(self.plan, "API_SWITCHING", "ROLLING_BACK", "ROLLBACK_VERIFYING")
        self.assertEqual(plan_mod.validate(plan), "ROLLBACK_VERIFYING")

    def test_a_result_of_another_kind_cannot_be_reused_for_the_stage(self):
        plan = self.to(self.plan, "API_SWITCHING")
        for wrong in ("WEB_VERIFIED", "VERIFYING", "ROLLBACK_VERIFYING"):
            with self.subTest(wrong=wrong), self.assertRaisesRegex(ev.Stop, "RESULT_KIND_MISMATCH"):
                ev.advance_with_verification(plan, "API_VERIFIED", self.res[wrong])

    def test_result_for_another_baseline_or_target_is_refused(self):
        other_base = copy.deepcopy(self.base)
        other_base["web_sha"] = "c" * 40
        foreign = results(other_base, self.goal)["WEB_VERIFIED"]
        plan = self.to(self.plan, "API_SWITCHING", "API_VERIFIED", "WEB_SWITCHING")
        with self.assertRaisesRegex(ev.Stop, "RESULT_BASELINE_MISMATCH"):
            ev.advance_with_verification(plan, "WEB_VERIFIED", foreign)
        other_goal = copy.deepcopy(self.goal)
        other_goal["images"]["web"] = "sha256:" + "e" * 64
        foreign = results(self.base, other_goal)["WEB_VERIFIED"]
        with self.assertRaisesRegex(ev.Stop, "RESULT_TARGET_MISMATCH"):
            ev.advance_with_verification(plan, "WEB_VERIFIED", foreign)

    def test_failed_edited_or_forged_results_are_refused(self):
        plan = self.to(self.plan, "API_SWITCHING")
        good = self.res["API_VERIFIED"]
        failed = dict(good, ok=False, code="HEALTH_FAILED")
        with self.assertRaises(ev.Stop):
            ev.proof_from_verification(plan, "API_VERIFIED", failed)
        edited = dict(good, evidence_sha256="sha256:" + "0" * 64)  # checksum now stale
        with self.assertRaisesRegex(ev.Stop, "RESULT_CHECKSUM_INVALID"):
            ev.proof_from_verification(plan, "API_VERIFIED", edited)
        for junk in (None, {}, [], "ok"):
            with self.subTest(junk=junk), self.assertRaises(ev.Stop):
                ev.proof_from_verification(plan, "API_VERIFIED", junk)

    def test_intent_stop_and_unknown_stages_are_not_handled_here(self):
        for stage in ("API_SWITCHING", "WEB_SWITCHING", "ROLLING_BACK", "RECOVERY_REQUIRED",
                      "APPLIED", "ROLLED_BACK", "", None, 7):
            with self.subTest(stage=stage), self.assertRaisesRegex(ev.Stop, "EVIDENCE_STAGE_NOT_VERIFIABLE"):
                ev.proof_from_verification(self.plan, stage, self.res["API_VERIFIED"])

    def test_the_plan_still_enforces_order_and_kind_of_each_stage(self):
        with self.assertRaisesRegex(ev.Stop, "EVIDENCE_PLAN_"):  # skipping API_SWITCHING
            ev.advance_with_verification(self.plan, "API_VERIFIED", self.res["API_VERIFIED"])
        plan = self.to(self.plan, "API_SWITCHING", "API_VERIFIED", "WEB_SWITCHING", "WEB_VERIFIED")
        # APPLIED and WEB_SWITCHED share the same observed state but are different kinds.
        with self.assertRaisesRegex(ev.Stop, "RESULT_KIND_MISMATCH"):
            ev.advance_with_verification(plan, "VERIFYING", self.res["WEB_VERIFIED"])

    def test_invalid_plan_is_refused_before_looking_at_the_result(self):
        broken = copy.deepcopy(self.plan)
        broken["stage"] = "VERIFYING"
        with self.assertRaisesRegex(ev.Stop, "EVIDENCE_PLAN_INVALID"):
            ev.proof_from_verification(broken, "API_VERIFIED", self.res["API_VERIFIED"])

    def test_module_is_pure_and_never_executes(self):
        source = SCRIPT.read_text(encoding="utf-8")
        for forbidden in ("import subprocess", "import socket", "import os", "import shutil",
                          "urllib", "open(", "os.system", "docker"):
            self.assertNotIn(forbidden, source, forbidden)
        self.assertIn("LIBRARY_ONLY_NO_EXECUTION", source)


if __name__ == "__main__":
    unittest.main()
