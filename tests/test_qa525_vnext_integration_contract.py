"""QA525 inactive vNext cross-module contract; no Docker, host, network or keys.

A combined PR must pass together, not just satisfy separate per-module tests.
These checks do NOT authenticate signatures, permissions, scope, G6 or actual QA.
"""
import copy
import unittest

import test_qa525_vnext_trust_binding as trust
import test_qa525_vnext_baseline_plan as plan
import test_qa525_vnext_verify as check


class Observed:
    def __init__(self, obs):
        self.obs = copy.deepcopy(obs)

    def observe(self):
        return copy.deepcopy(self.obs)


def observation(base, target, use_target=True):
    return {
        "images": copy.deepcopy(target["images"] if use_target else base["images"]),
        "running": {"api": True, "web": True},
        "dependencies": copy.deepcopy(base["dependencies"]),
        "oneshots": copy.deepcopy(base["oneshots"]),
        "layers": copy.deepcopy(target["layers"] if use_target else base["layers"]),
        "health": {"ok": True,
                   "web_sha": target["web_sha"] if use_target else base["web_sha"]},
    }


class IntegratedVNextContractTests(unittest.TestCase):
    def make(self):
        raw = trust.admission_record()
        anchor = trust.approval_record(raw)
        # This is deliberately an unsigned, synthetic pair. It is NOT authority.
        result = trust.binding.bind(trust.canonical(raw), trust.canonical(anchor),
                                    trusted_policy_hash=trust.POLICY, now=trust.NOW)
        b, t = plan.baseline(), plan.target()
        flight = plan.make_plan(b, t, nonce=result["nonce"],
                                admission=result["admission_sha256"])
        return result, b, t, flight

    def test_all_modules_agree_baseline_target_and_flight_without_install(self):
        matched, b, t, p = self.make()
        self.assertEqual(plan.mod.validate_baseline(b),
                         check.mod.validate_baseline(b))
        self.assertEqual(plan.mod.validate_target(b, t),
                         check.mod.expected_target_sha256(b, "APPLIED", t))
        self.assertEqual(p["admission_sha256"], matched["admission_sha256"])
        self.assertEqual(p["nonce"], matched["nonce"])
        report = check.mod.verify(Observed(observation(b, t)), b, "APPLIED", t)
        self.assertIs(report["ok"], True)
        checked = check.mod.require_ok(report, p["baseline_sha256"],
                                       "APPLIED", p["target_sha256"])
        self.assertEqual(checked["baseline_sha256"], p["baseline_sha256"])
        self.assertEqual(checked["target_sha256"], p["target_sha256"])
        for next_step in ("API_SWITCHING", "API_VERIFIED", "WEB_SWITCHING",
                          "WEB_VERIFIED", "VERIFYING"):
            p = plan.mod.advance(p, next_step, plan.evidence(p, next_step))
        self.assertEqual(plan.mod.validate(p), "VERIFYING")
        self.assertFalse(p["may_apply"])
        self.assertFalse(p["external_runtime_verified"])
        for key in ("signature_verified_here", "g6_verified_here",
                    "sponsor_approved_here", "may_apply", "may_install"):
            self.assertIs(matched[key], False)
        # Neither an observed healthy runtime NOR a checksum may write APPLIED.
        with self.assertRaises(plan.mod.Stop):
            plan.mod.advance(p, "APPLIED", {})

    def test_mid_transaction_checkpoints_bind_observed_api_then_web(self):
        _, b, t, p = self.make()
        p = plan.mod.advance(p, "API_SWITCHING", plan.evidence(p, "API_SWITCHING"))
        api_only = observation(b, t)
        api_only["images"]["web"] = b["images"]["web"]
        api_only["layers"]["web"] = copy.deepcopy(b["layers"]["web"])
        api_only["health"]["web_sha"] = b["web_sha"]
        api_result = check.mod.verify(Observed(api_only), b, "API_SWITCHED", t)
        self.assertTrue(api_result["ok"], api_result)
        api_target = check.mod.expected_target_sha256(b, "API_SWITCHED", t)
        check.mod.require_ok(api_result, p["baseline_sha256"], "API_SWITCHED", api_target)
        api_proof = plan.evidence(p, "API_VERIFIED")
        api_proof["evidence_sha256"] = api_result["result_checksum_sha256"]
        p = plan.mod.advance(p, "API_VERIFIED", api_proof)
        p = plan.mod.advance(p, "WEB_SWITCHING", plan.evidence(p, "WEB_SWITCHING"))
        web_result = check.mod.verify(Observed(observation(b, t)), b, "WEB_SWITCHED", t)
        self.assertTrue(web_result["ok"], web_result)
        web_target = check.mod.expected_target_sha256(b, "WEB_SWITCHED", t)
        check.mod.require_ok(web_result, p["baseline_sha256"], "WEB_SWITCHED", web_target)
        web_proof = plan.evidence(p, "WEB_VERIFIED")
        web_proof["evidence_sha256"] = web_result["result_checksum_sha256"]
        p = plan.mod.advance(p, "WEB_VERIFIED", web_proof)
        self.assertEqual(plan.mod.validate(p), "WEB_VERIFIED")
        with self.assertRaises(check.mod.Stop):
            check.mod.require_ok(api_result, p["baseline_sha256"],
                                 "WEB_SWITCHED", web_target)
        self.assertFalse(p["may_apply"])

    def test_recovery_plan_and_verifier_agree_without_rollback_authority(self):
        _, b, t, p = self.make()
        p = plan.mod.advance(p, "API_SWITCHING", plan.evidence(p, "API_SWITCHING"))
        rollback = plan.mod.advance(p, "ROLLING_BACK", plan.evidence(p, "ROLLING_BACK"))
        rollback = plan.mod.advance(rollback, "ROLLBACK_VERIFYING",
                                    plan.evidence(rollback, "ROLLBACK_VERIFYING"))
        self.assertEqual(plan.mod.validate(rollback), "ROLLBACK_VERIFYING")
        report = check.mod.verify(Observed(observation(b, t, use_target=False)),
                                  b, "ROLLED_BACK")
        self.assertTrue(report["ok"], report)
        expected = check.mod.expected_target_sha256(b, "ROLLED_BACK", None)
        check.mod.require_ok(report, rollback["baseline_sha256"],
                             "ROLLED_BACK", expected)
        with self.assertRaises(plan.mod.Stop):
            plan.mod.advance(rollback, "ROLLED_BACK", {})
        self.assertFalse(rollback["may_apply"])

    def test_different_nonce_or_target_and_missing_oneshot_fail_closed(self):
        _, b, t, p = self.make()
        wrong_proof = plan.evidence(p, "API_SWITCHING")
        wrong_proof["nonce"] = "q" * 64
        with self.assertRaises(plan.mod.Stop):
            plan.mod.advance(p, "API_SWITCHING", wrong_proof)
        wrong_target = copy.deepcopy(t)
        wrong_target["images"]["web"] = "sha256:" + "e" * 64
        false_target = check.mod.expected_target_sha256(b, "APPLIED", wrong_target)
        report = check.mod.verify(Observed(observation(b, t)), b, "APPLIED", t)
        with self.assertRaises(check.mod.Stop):
            check.mod.require_ok(report, p["baseline_sha256"], "APPLIED", false_target)
        broken = observation(b, t)
        broken["oneshots"].pop("migrator")
        report = check.mod.verify(Observed(broken), b, "APPLIED", t)
        self.assertFalse(report["ok"])
        with self.assertRaises(check.mod.Stop):
            check.mod.require_ok(report, p["baseline_sha256"], "APPLIED",
                                 p["target_sha256"])


if __name__ == "__main__":
    unittest.main()
