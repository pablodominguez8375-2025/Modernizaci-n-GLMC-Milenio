"""QA525 vNext in-memory simulated checkpoints; NEVER a physical approval."""
import copy
import importlib.util
from pathlib import Path
import unittest

import test_qa525_vnext_baseline_plan as plan
from test_qa525_vnext_integration_contract import observation
import test_qa525_vnext_verify as check

PATH = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-dryrun-simulator.py"
spec = importlib.util.spec_from_file_location("qa525_inert_dryrun", PATH)
sim = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sim)


def partial_api(base, target):
    obs = observation(base, target)
    obs["images"]["web"] = base["images"]["web"]
    obs["layers"]["web"] = copy.deepcopy(base["layers"]["web"])
    obs["health"]["web_sha"] = base["web_sha"]
    return obs


def pair(obs):
    return [copy.deepcopy(obs), copy.deepcopy(obs)]


def fixture(mode="apply"):
    base, target = plan.baseline(), plan.target()
    flight = plan.make_plan(base, target)
    if mode == "apply":
        shots = {
            "api": pair(partial_api(base, target)),
            "web": pair(observation(base, target)),
            "final": pair(observation(base, target)),
        }
        names = sim.APPLY_INTENTS
    else:
        shots = {
            "api": pair(partial_api(base, target)),
            "rollback": pair(observation(base, target, use_target=False)),
        }
        names = sim.ROLLBACK_INTENTS
    claims = {}
    for i, stage in enumerate(names):
        proof = plan.evidence(flight, stage, serial=i + 1)
        claims[stage] = proof
    return base, target, flight, shots, claims


class InertDryRunContractTests(unittest.TestCase):
    def replay(self, mode="apply", mutate=None):
        b, t, p, shots, claims = fixture(mode)
        if mutate:
            mutate(b, t, p, shots, claims)
        return sim.replay(plan.mod, check.mod, b, t, p, mode=mode,
                          observations=shots, intents=claims)

    def test_two_checkpoint_and_final_simulation_has_no_authority(self):
        result = self.replay()
        self.assertEqual(result["status"], sim.SIMULATED)
        self.assertEqual(result["stage"], "VERIFYING")
        self.assertEqual(result["checked_kinds"],
                         ["API_SWITCHED", "WEB_SWITCHED", "APPLIED"])
        for flag in ("may_apply", "may_install", "physical_runtime_verified",
                     "signatures_verified", "journal_written"):
            self.assertIs(result[flag], False)

    def test_rollback_simulation_cannot_write_terminal(self):
        result = self.replay("rollback")
        self.assertEqual(result["stage"], "ROLLBACK_VERIFYING")
        self.assertEqual(result["checked_kinds"], ["API_SWITCHED", "ROLLED_BACK"])
        self.assertEqual(result["status"], sim.SIMULATED)
        self.assertFalse(result["journal_written"])

    def test_api_layer_drift_stops_before_web(self):
        def tamper(_b, _t, _p, shots, _proof):
            shots["api"][0]["layers"]["api"].append("/tmp/orphan-compose.json")
        with self.assertRaisesRegex(sim.Stop, "^DRYRUN_VERIFICATION_REJECTED$"):
            self.replay(mutate=tamper)

    def test_web_oneshot_difference_stops(self):
        def tamper(_b, _t, _p, shots, _proof):
            shots["web"][0]["oneshots"]["migrator"]["id"] = "f" * 64
            shots["web"][1]["oneshots"]["migrator"]["id"] = "f" * 64
        with self.assertRaisesRegex(sim.Stop, "^DRYRUN_VERIFICATION_REJECTED$"):
            self.replay(mutate=tamper)

    def test_replayed_intent_or_wrong_nonce_stops(self):
        def tamper(_b, _t, _p, _shots, claims):
            claims["WEB_SWITCHING"]["nonce"] = "z" * 64
        with self.assertRaisesRegex(sim.Stop, "^DRYRUN_STAGE_REJECTED$"):
            self.replay(mutate=tamper)
        def duplicate(_b, _t, _p, _shots, claims):
            claims["WEB_SWITCHING"]["evidence_sha256"] = (
                claims["API_SWITCHING"]["evidence_sha256"])
        with self.assertRaisesRegex(sim.Stop, "^DRYRUN_STAGE_REJECTED$"):
            self.replay(mutate=duplicate)

    def test_final_snapshot_flapping_stops(self):
        def tamper(_b, _t, _p, shots, _proof):
            shots["final"][1]["health"]["ok"] = False
        with self.assertRaisesRegex(sim.Stop, "^DRYRUN_VERIFICATION_REJECTED$"):
            self.replay(mutate=tamper)

    def test_initial_plan_mutation_and_missing_snapshot_stop(self):
        def tamper(_b, _t, p, _shots, _proof):
            p["target_sha256"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(sim.Stop, "^DRYRUN_PLAN_INVALID$"):
            self.replay(mutate=tamper)
        def missing(_b, _t, _p, shots, _proof):
            shots.pop("final")
        with self.assertRaisesRegex(sim.Stop, "^DRYRUN_INPUT_SET_INVALID$"):
            self.replay(mutate=missing)

    def test_rollback_not_equivalent_to_new_target(self):
        def tamper(_b, _t, _p, shots, _proof):
            shots["rollback"] = pair(shots["api"][0])
        with self.assertRaisesRegex(sim.Stop, "^DRYRUN_VERIFICATION_REJECTED$"):
            self.replay("rollback", mutate=tamper)

    def test_reject_bad_type_and_stage_without_source_echo(self):
        b, t, p, shots, claims = fixture()
        with self.assertRaisesRegex(sim.Stop, "^DRYRUN_MODE_INVALID$"):
            sim.replay(plan.mod, check.mod, b, t, p, mode=[],
                       observations=shots, intents=claims)
        past = plan.mod.advance(p, "API_SWITCHING", claims["API_SWITCHING"])
        with self.assertRaisesRegex(sim.Stop, "^DRYRUN_PLAN_INVALID$"):
            sim.replay(plan.mod, check.mod, b, t, past, mode="apply",
                       observations=shots, intents=claims)


if __name__ == "__main__":
    unittest.main()
