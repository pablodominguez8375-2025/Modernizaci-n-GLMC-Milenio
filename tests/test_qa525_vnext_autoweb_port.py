"""Offline mutant tests for the INACTIVE auto-web WEB-only port (#1010 audit P1-5)."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
_spec = importlib.util.spec_from_file_location("aw_port_contract", ROOT / "scripts/qa/qa525-vnext-autoweb-port.py")
P = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(P)

NONCE = "qa525-autoweb-port-nonce-20261114"
BASE = "/opt/centenario/base.json"
PLAN = {"baseline": {"layers": {"api": [BASE, "/opt/centenario/api-6eed4.json"],
                                "web": [BASE, "/opt/centenario/web-6eed4.json"]}},
        "target": {"layers": {"api": [BASE, "/opt/centenario/api-6eed4.json"],
                              "web": [BASE, "/opt/centenario/web-new.json"]}}}


class AutoWebPortScope:
    def __init__(self, unit):
        self.unit = unit

    def state(self):
        return "ABSENT"


class AutoWebPortTests(unittest.TestCase):
    def setUp(self):
        self.calls = []
        self.code = 0

    def port(self, plan=PLAN, runner=None):
        def real_runner(argv):
            self.calls.append(list(argv))
            return self.code
        return P.web_only_port(
            plan, nonce=NONCE, project="centenario-qa-lab2", workdir="/opt/centenario",
            env_file="/etc/pmgm/qa.env", runner=runner or real_runner,
            scope_factory=AutoWebPortScope, record_unit=lambda unit: None,
            observe=lambda: {"web": 1}, settle=lambda: None)

    def expected(self, which):
        unit = P.containment.unit_name(NONCE, "web")
        return P.containment.systemd_run_argv(unit, "centenario-qa-lab2", "/opt/centenario",
                                              "/etc/pmgm/qa.env", PLAN[which]["layers"]["web"], "web")

    def test_apply_runs_exact_target_web_argv_only(self):
        result = self.port().apply_web()
        self.assertEqual(self.calls, [self.expected("target")])
        self.assertEqual((result["service"], result["may_apply"]), ("web", False))
        self.assertTrue(self.calls[0][-1] == "web" and any(a.startswith("--unit=") and a.endswith("-web.scope") for a in self.calls[0]))
        self.assertNotIn("/opt/centenario/api-6eed4.json", self.calls[0])

    def test_rollback_runs_baseline_web_once(self):
        port = self.port()
        port.apply_web()
        port.rollback_web()
        self.assertEqual(self.calls[1], self.expected("baseline"))
        with self.assertRaisesRegex(P.Stop, "WEB_PORT_ROLLBACK_SPENT"):
            port.rollback_web()
        with self.assertRaisesRegex(P.Stop, "WEB_PORT_APPLY_SPENT"):
            port.apply_web()
        self.assertEqual(len(self.calls), 2)

    def test_failed_apply_is_not_retried(self):
        self.code = 1
        port = self.port()
        with self.assertRaisesRegex(P.Stop, "WEB_PORT_SWITCH_COMMAND_FAILED"):
            port.apply_web()
        with self.assertRaisesRegex(P.Stop, "WEB_PORT_APPLY_SPENT"):
            port.apply_web()

    def test_mutant_plan_changing_api_layers_rejected_before_runner(self):
        bad = {"baseline": PLAN["baseline"],
               "target": {"layers": {"api": [BASE, "/opt/centenario/api-new.json"],
                                     "web": PLAN["target"]["layers"]["web"]}}}
        with self.assertRaisesRegex(P.Stop, "WEB_PORT_API_LAYERS_CHANGE"):
            self.port(bad)
        self.assertEqual(self.calls, [])

    def introspected_switchers(self, port):
        found = []
        for fn in (port.apply_web, port.rollback_web):
            for cell in fn.__closure__ or ():
                obj = cell.cell_contents
                if callable(getattr(obj, "switch", None)):
                    found.append(obj)
        self.assertEqual(len(found), 2)
        return found

    def test_mutant_introspected_switcher_cannot_run_anything(self):
        """Review 5904937432 P1-1: no permit => even the exact WEB argv is rejected."""
        port = self.port()
        for sw in self.introspected_switchers(port):
            for attempt in (lambda: sw.switch("api"), lambda: sw.switch("web"), sw.switch_all,
                            sw.switch_api, sw.switch_web):
                with self.assertRaisesRegex(P.switch.Stop, "SWITCH_RUNNER_FAILED"):
                    attempt()
        self.assertEqual(self.calls, [])

    def test_mutant_second_web_after_apply_via_introspection(self):
        port = self.port()
        port.apply_web()
        for sw in self.introspected_switchers(port):
            with self.assertRaisesRegex(P.switch.Stop, "SWITCH_RUNNER_FAILED"):
                sw.switch("web")
        self.assertEqual(len(self.calls), 1)

    def test_mutant_rollback_first_is_rejected(self):
        """Review 5904937432 P1-2."""
        port = self.port()
        with self.assertRaisesRegex(P.Stop, "WEB_PORT_ROLLBACK_BEFORE_APPLY"):
            port.rollback_web()
        self.assertEqual(self.calls, [])
        port.apply_web()
        port.rollback_web()
        self.assertEqual(len(self.calls), 2)

    def test_rollback_allowed_after_failed_apply(self):
        self.code = 1
        port = self.port()
        with self.assertRaises(P.Stop):
            port.apply_web()
        self.code = 0
        port.rollback_web()
        self.assertEqual(self.calls[-1], self.expected("baseline"))

    def test_port_exposes_no_switcher_or_runner(self):
        port = self.port()
        public = {n for n in dir(port) if not n.startswith("_")}
        self.assertEqual(public, {"apply_web", "rollback_web"})  # review P1-3: no factory on the instance
        for name in ("switch_api", "switch_all", "switch", "runner", "forward", "from_plan"):
            self.assertFalse(hasattr(port, name), name)
        with self.assertRaises(AttributeError):
            port.switch_all = lambda: None
        with self.assertRaisesRegex(P.Stop, "WEB_PORT_USE_FACTORY"):
            P.WebOnlyPort()
        with self.assertRaisesRegex(P.Stop, "WEB_PORT_USE_FACTORY"):
            type(port)()

    def test_malformed_inputs_rejected(self):
        with self.assertRaisesRegex(P.Stop, "WEB_PORT_PLAN_INVALID"):
            self.port({"baseline": {}})
        with self.assertRaisesRegex(P.Stop, "WEB_PORT_ADAPTER_INVALID"):
            P.web_only_port(PLAN, nonce=NONCE, project="p", workdir="/w", env_file="/e",
                                    runner=None, scope_factory=AutoWebPortScope, record_unit=print,
                                    observe=dict, settle=dict)
        with self.assertRaisesRegex(P.Stop, "WEB_PORT_COMMAND_INVALID"):
            P.web_only_port(PLAN, nonce="short", project="centenario-qa-lab2",
                                    workdir="/opt/centenario", env_file="/etc/pmgm/qa.env",
                                    runner=lambda a: 0, scope_factory=AutoWebPortScope,
                                    record_unit=print, observe=dict, settle=dict)

    def test_library_only(self):
        source = (ROOT / "scripts/qa/qa525-vnext-autoweb-port.py").read_text()
        for word in ("import subprocess", "import os", "import socket"):
            self.assertNotIn(word, source)


if __name__ == "__main__":
    unittest.main()
