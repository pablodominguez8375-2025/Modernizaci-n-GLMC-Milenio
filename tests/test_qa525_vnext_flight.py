"""Offline tests for the INACTIVE flight orchestrator (fake adapters, tempdir)."""
import copy
import datetime as dt
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

import test_qa525_vnext_baseline_plan as bp
import test_qa525_vnext_journal as jt
from test_qa525_vnext_integration_contract import observation

PATH = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-flight.py"
spec = importlib.util.spec_from_file_location("qa525_flight_contract", PATH)
fl = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fl)


class _Reader:
    def __init__(self, obs):
        self.obs = obs

    def observe(self):
        return copy.deepcopy(self.obs)


class FlightTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = jt.JournalFixture(self.tmp.name)
        self.lease = fl.journal.begin(self.fx.root, self.fx.admission,
                                      self.fx.digest, jt.NOW)
        self.base, self.goal = bp.baseline(), bp.target()
        self.plan = bp.make_plan(self.base, self.goal, nonce=jt.NONCE,
                                 admission=self.fx.digest)
        self.calls = []
        self.fail_obs = set()
        self.fail_action = set()
        self.denied = set()

    def tearDown(self):
        self.lease.close()
        self.tmp.cleanup()

    def shot(self, kind):
        obs = observation(self.base, self.goal, use_target=kind != "ROLLED_BACK")
        if kind == "API_SWITCHED":
            obs["images"]["web"] = self.base["images"]["web"]
            obs["layers"]["web"] = copy.deepcopy(self.base["layers"]["web"])
            obs["health"]["web_sha"] = self.base["web_sha"]
        if kind in self.fail_obs:
            obs["health"]["ok"] = False
        return obs

    def observe(self, kind):
        self.calls.append("observe:" + kind)
        return fl.evidence.verify.verify(
            _Reader(self.shot(kind)), self.base, kind,
            None if kind == "ROLLED_BACK" else self.goal, samples=2)

    def action(self, name):
        def run():
            self.calls.append(name)
            if name in self.fail_action:
                raise RuntimeError("secret detail must not leak")
        return run

    def gate(self, stage):
        self.calls.append("gate:" + stage)
        return stage not in self.denied

    def run_flight(self, now=jt.NOW, **over):
        args = dict(switch_api=self.action("api"), switch_web=self.action("web"),
                    rollback=self.action("rollback"), observe=self.observe,
                    gate=self.gate, now=now)
        args.update(over)
        return fl.run_apply(self.lease, self.plan, **args)

    def stored_stage(self):
        return fl.store.inspect(self.lease)["stage"]

    def terminal_status(self):
        path = self.fx.root / ("terminal-" + jt.NONCE + ".json")
        return json.loads(path.read_text())["status"] if path.exists() else None

    def test_single_journal_owner_binding(self):
        self.assertIs(fl.store.journal, fl.journal)
        self.assertIs(fl.terminal.journal, fl.journal)

    def test_happy_path_persists_each_stage_and_writes_terminal(self):
        result = self.run_flight()
        self.assertEqual(result["status"], "APPLIED")
        self.assertEqual(result["stage"], "VERIFYING")
        for flag in ("may_apply", "may_install", "signature_verified_here",
                     "physical_runtime_verified"):
            self.assertIs(result[flag], False)
        self.assertEqual(self.calls, [
            "gate:API_SWITCHING", "api", "observe:API_SWITCHED",
            "gate:WEB_SWITCHING", "web", "observe:WEB_SWITCHED",
            "observe:APPLIED"])
        self.assertEqual(self.terminal_status(), "APPLIED")
        self.assertEqual(fl.journal.inspect(self.fx.root, jt.NONCE), "APPLIED")

    def test_intent_is_durable_before_the_action_runs(self):
        seen = []
        def api():
            seen.append(self.stored_stage())
        def web():
            seen.append(self.stored_stage())
        self.run_flight(switch_api=api, switch_web=web)
        self.assertEqual(seen, ["API_SWITCHING", "WEB_SWITCHING"])

    def test_gate_denied_before_first_intent_changes_nothing(self):
        self.denied = {"API_SWITCHING"}
        with self.assertRaisesRegex(fl.Stop, "^FLIGHT_GATE_DENIED$"):
            self.run_flight()
        self.assertNotIn("api", self.calls)
        self.assertNotIn("rollback", self.calls)
        self.assertEqual(self.stored_stage(), "APPLYING")
        self.assertIsNone(self.terminal_status())
        self.assertEqual(fl.journal.inspect(self.fx.root, jt.NONCE), "APPLYING")

    def test_gate_denied_before_web_rolls_back_the_api_change(self):
        self.denied = {"WEB_SWITCHING"}
        result = self.run_flight()
        self.assertEqual(result["status"], "ROLLED_BACK")
        self.assertEqual(result["cause"], "FLIGHT_GATE_DENIED")
        self.assertEqual(self.calls.count("api"), 1)
        self.assertNotIn("web", self.calls)
        self.assertIn("rollback", self.calls)
        self.assertEqual(self.terminal_status(), "ROLLED_BACK")

    def test_api_verification_failure_rolls_back_once(self):
        self.fail_obs = {"API_SWITCHED"}
        result = self.run_flight()
        self.assertEqual(result["status"], "ROLLED_BACK")
        self.assertEqual(self.calls.count("api"), 1)
        self.assertEqual(self.calls.count("rollback"), 1)
        self.assertNotIn("web", self.calls)
        self.assertEqual(self.terminal_status(), "ROLLED_BACK")

    def test_action_exception_is_not_echoed_and_triggers_rollback(self):
        self.fail_action = {"web"}
        result = self.run_flight()
        self.assertEqual(result["status"], "ROLLED_BACK")
        self.assertEqual(result["cause"], "FLIGHT_WEB_SWITCH_FAILED")
        self.assertNotIn("secret", json.dumps(result))

    def test_failed_rollback_persists_recovery_required_and_stops(self):
        self.fail_obs = {"API_SWITCHED", "ROLLED_BACK"}
        with self.assertRaisesRegex(fl.Stop, "^FLIGHT_RECOVERY_REQUIRED$"):
            self.run_flight()
        self.assertEqual(self.stored_stage(), "RECOVERY_REQUIRED")
        self.assertEqual(self.calls.count("rollback"), 1)
        self.assertIsNone(self.terminal_status())

    def test_rollback_action_exception_also_requires_recovery(self):
        self.fail_obs = {"WEB_SWITCHED"}
        self.fail_action = {"rollback"}
        with self.assertRaisesRegex(fl.Stop, "^FLIGHT_RECOVERY_REQUIRED$"):
            self.run_flight()
        self.assertEqual(self.stored_stage(), "RECOVERY_REQUIRED")

    def test_terminal_failure_never_rolls_back_a_verified_flight(self):
        naive = dt.datetime(2026, 11, 5, 13, 0)  # clock rejected by the terminal writer
        with self.assertRaisesRegex(fl.Stop, "^FLIGHT_RECOVERY_REQUIRED$"):
            self.run_flight(now=naive)
        self.assertNotIn("rollback", self.calls)
        self.assertEqual(self.stored_stage(), "RECOVERY_REQUIRED")
        self.assertIsNone(self.terminal_status())

    def test_foreign_or_closed_lease_runs_nothing(self):
        # A lease minted by ANOTHER journal module instance is not accepted.
        other = Path(self.tmp.name) / "other"
        other.mkdir()
        fx2 = jt.JournalFixture(str(other))
        foreign = jt.mod.begin(fx2.root, fx2.admission, fx2.digest, jt.NOW)
        try:
            with self.assertRaisesRegex(fl.Stop, "^FLIGHT_LEASE_OR_PLAN_REJECTED$"):
                fl.run_apply(foreign, self.plan, switch_api=self.action("api"),
                             switch_web=self.action("web"), rollback=self.action("rollback"),
                             observe=self.observe, gate=self.gate, now=jt.NOW)
        finally:
            foreign.close()
        self.lease.close()
        with self.assertRaisesRegex(fl.Stop, "^FLIGHT_LEASE_OR_PLAN_REJECTED$"):
            self.run_flight()
        self.assertEqual(self.calls, [])

    def test_plan_for_another_flight_and_missing_gate_run_nothing(self):
        wrong = bp.make_plan(self.base, self.goal, nonce="z" * 30,
                             admission=self.fx.digest)
        with self.assertRaisesRegex(fl.Stop, "^FLIGHT_LEASE_OR_PLAN_REJECTED$"):
            fl.run_apply(self.lease, wrong, switch_api=self.action("api"),
                         switch_web=self.action("web"), rollback=self.action("rollback"),
                         observe=self.observe, gate=self.gate, now=jt.NOW)
        with self.assertRaisesRegex(fl.Stop, "^FLIGHT_GATE_REQUIRED$"):
            self.run_flight(gate=None)
        self.assertEqual(self.calls, [])

    def test_no_io_or_execution_apis_in_flight_module(self):
        source = PATH.read_text(encoding="utf-8")
        for needle in ("import subprocess", "import socket", "import os",
                       "import urllib", "import requests", "os.system(",
                       "Popen(", "docker run"):
            self.assertNotIn(needle, source)


if __name__ == "__main__":
    unittest.main()
