"""Flight + ServiceSwitcher end to end with a FAKE world (no host, no process)."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

import test_qa525_vnext_baseline_plan as bp
import test_qa525_vnext_journal as jt
from test_qa525_vnext_integration_contract import observation

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, BASE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


fl = load("qa525_flight_for_switch_it", "qa525-vnext-flight.py")
sw = load("qa525_switch_for_switch_it", "qa525-vnext-switch.py")


class _Reader:
    def __init__(self, obs):
        self.obs = obs

    def observe(self):
        return copy.deepcopy(self.obs)


class FlightWithSwitcherTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = jt.JournalFixture(self.tmp.name)
        self.lease = fl.journal.begin(self.fx.root, self.fx.admission,
                                      self.fx.digest, jt.NOW)
        self.base, self.goal = bp.baseline(), bp.target()
        self.plan = bp.make_plan(self.base, self.goal, nonce=jt.NONCE,
                                 admission=self.fx.digest)
        self.world = {"api": "baseline", "web": "baseline"}
        self.runs = []
        self.fail_runner = {}  # (service, "target"|"baseline") -> exit code
        self.denied = set()

    def tearDown(self):
        self.lease.close()
        self.tmp.cleanup()

    # --- fake world -------------------------------------------------------
    def runner(self, argv):
        service = argv[-1]
        files = [argv[i + 1] for i, item in enumerate(argv) if item == "--file"]
        side = "target" if files == self.goal["layers"][service] else (
            "baseline" if files == self.base["layers"][service] else "unknown")
        self.runs.append((service, side))
        code = self.fail_runner.get((service, side), 0)
        if code == 0:
            self.world[service] = side
        return code

    def observe(self, kind):
        use_target = [self.world["api"] == "target", self.world["web"] == "target"]
        obs = observation(self.base, self.goal, use_target=True)
        for index, service in enumerate(("api", "web")):
            src = self.goal if use_target[index] else self.base
            obs["images"][service] = copy.deepcopy(src["images"][service])
            obs["layers"][service] = copy.deepcopy(src["layers"][service])
        obs["health"]["web_sha"] = (self.goal if use_target[1] else self.base)["web_sha"]
        return fl.evidence.verify.verify(
            _Reader(obs), self.base, kind,
            None if kind == "ROLLED_BACK" else self.goal, samples=2)

    def switcher(self, which):
        return sw.ServiceSwitcher.for_plan(
            self.plan, which, nonce=jt.NONCE, project="centenario",
            workdir="/opt/centenario", env_file="/etc/pmgm/qa.env",
            runner=self.runner,
            scope_factory=lambda unit: _Scope(unit),
            record_unit=lambda unit: None, observe=lambda: dict(self.world),
            settle=lambda: None)

    def fly(self):
        forward, back = self.switcher("target"), self.switcher("baseline")
        return fl.run_apply(
            self.lease, self.plan, switch_api=forward.switch_api,
            switch_web=forward.switch_web, rollback=back.switch_all,
            observe=self.observe, gate=lambda stage: stage not in self.denied,
            now=jt.NOW)

    def terminal(self):
        path = self.fx.root / ("terminal-" + jt.NONCE + ".json")
        return json.loads(path.read_text())["status"] if path.exists() else None

    # --- scenarios --------------------------------------------------------
    def test_forward_flight_applies_target_layers_service_by_service(self):
        result = self.fly()
        self.assertEqual(result["status"], "APPLIED")
        self.assertEqual(self.runs, [("api", "target"), ("web", "target")])
        self.assertEqual(self.terminal(), "APPLIED")

    def test_web_command_failure_rolls_back_with_baseline_layers_web_first(self):
        self.fail_runner[("web", "target")] = 1
        result = self.fly()
        self.assertEqual(result["status"], "ROLLED_BACK")
        self.assertEqual(result["cause"], "FLIGHT_WEB_SWITCH_FAILED")
        self.assertEqual(self.runs, [("api", "target"), ("web", "target"),
                                     ("web", "baseline"), ("api", "baseline")])
        self.assertEqual(self.world, {"api": "baseline", "web": "baseline"})
        self.assertEqual(self.terminal(), "ROLLED_BACK")

    def test_gate_denial_before_web_rolls_back_the_api_change_only_via_baseline(self):
        self.denied = {"WEB_SWITCHING"}
        result = self.fly()
        self.assertEqual(result["status"], "ROLLED_BACK")
        self.assertNotIn(("web", "target"), self.runs)
        self.assertEqual(self.world, {"api": "baseline", "web": "baseline"})

    def test_rollback_command_failure_requires_recovery_and_stops(self):
        self.fail_runner[("web", "target")] = 1
        self.fail_runner[("web", "baseline")] = 1
        with self.assertRaisesRegex(fl.Stop, "^FLIGHT_RECOVERY_REQUIRED$"):
            self.fly()
        self.assertEqual(fl.store.inspect(self.lease)["stage"], "RECOVERY_REQUIRED")
        self.assertIsNone(self.terminal())
        # api baseline was never attempted after the failed web rollback
        self.assertNotIn(("api", "baseline"), self.runs)


class _Scope:
    def __init__(self, unit):
        self.unit = unit

    def state(self):
        return "ABSENT"


if __name__ == "__main__":
    unittest.main()
