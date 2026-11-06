"""Offline tests for the INACTIVE service switcher (all effects injected)."""
import importlib.util
from pathlib import Path
import unittest

import test_qa525_vnext_baseline_plan as bp

PATH = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-switch.py"
spec = importlib.util.spec_from_file_location("qa525_switch_contract", PATH)
sw = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sw)

NONCE = "qa525-vnext-switch-nonce-20261106"
FILES = ["/opt/centenario/base.json", "/opt/centenario/new-api.json"]


class FakeScope:
    def __init__(self, unit, log, states):
        self.unit, self.log, self.states = unit, log, list(states)

    def state(self):
        self.log.append("state:" + self.unit.rsplit("-", 1)[1])
        return self.states.pop(0) if len(self.states) > 1 else self.states[0]


class SwitchTests(unittest.TestCase):
    def setUp(self):
        self.log = []
        self.code = 0
        self.runner_error = False
        self.record_error = False
        self.scope_states = ["ABSENT"]
        self.snapshots = [{"a": 1}]

    def make(self, files=FILES, **over):
        def runner(argv):
            self.log.append(("run", argv))
            if self.runner_error:
                raise OSError("secret path leaked")
            return self.code

        def record(unit):
            self.log.append("record")
            if self.record_error:
                raise OSError("disk")

        def observe():
            return self.snapshots[0] if len(self.snapshots) == 1 else self.snapshots.pop(0)

        args = dict(nonce=NONCE, project="centenario", workdir="/opt/centenario",
                    env_file="/etc/pmgm/qa.env", files=files, runner=runner,
                    scope_factory=lambda unit: FakeScope(unit, self.log, self.scope_states),
                    record_unit=record, observe=observe, settle=lambda: None)
        args.update(over)
        return sw.ServiceSwitcher(**args)

    def runs(self):
        return [item for item in self.log if isinstance(item, tuple)]

    def test_switch_builds_only_the_fixed_scoped_compose_up_argv(self):
        result = self.make().switch("api")
        (_, argv), = self.runs()
        unit = sw.containment.unit_name(NONCE, "api")
        self.assertEqual(argv, sw.containment.systemd_run_argv(
            unit, "centenario", "/opt/centenario", "/etc/pmgm/qa.env", FILES, "api"))
        self.assertEqual(argv[0], "/usr/bin/systemd-run")
        self.assertIn("--force-recreate", argv)
        self.assertEqual(result["unit"], unit)
        for flag in ("may_apply", "signature_verified_here", "physical_runtime_verified"):
            self.assertIs(result[flag], False)

    def test_per_service_layers_are_used_for_each_service(self):
        layers = {"api": ["/opt/centenario/base.json", "/opt/centenario/api.json"],
                  "web": ["/opt/centenario/base.json", "/opt/centenario/web.json"]}
        switcher = self.make(files=layers)
        switcher.switch_all()
        by_service = {argv[-1]: argv for _, argv in self.runs()}
        self.assertIn("/opt/centenario/web.json", by_service["web"])
        self.assertNotIn("/opt/centenario/api.json", by_service["web"])
        self.assertIn("/opt/centenario/api.json", by_service["api"])
        self.assertNotIn("/opt/centenario/web.json", by_service["api"])
        for bad in ({"api": FILES}, {"api": FILES, "web": FILES, "db": FILES},
                    {"api": FILES, "web": []}):
            with self.subTest(files=bad), \
                    self.assertRaisesRegex(sw.Stop, "^SWITCH_COMMAND_INVALID$"):
                self.make(files=bad)

    def test_for_plan_rollback_uses_the_baseline_layers_not_the_target(self):
        plan = bp.make_plan()
        kwargs = dict(nonce=NONCE, project="centenario", workdir="/opt/centenario",
                      env_file="/etc/pmgm/qa.env",
                      runner=lambda argv: self.log.append(("run", argv)) or 0,
                      scope_factory=lambda unit: FakeScope(unit, self.log, ["ABSENT"]),
                      record_unit=lambda unit: None, observe=lambda: {"a": 1},
                      settle=lambda: None)
        sw.ServiceSwitcher.for_plan(plan, "baseline", **kwargs).switch_all()
        used = [item for _, argv in self.runs() for item in argv if item.endswith(".json")]
        for path in plan["baseline"]["layers"]["api"] + plan["baseline"]["layers"]["web"]:
            self.assertIn(path, used)
        for path in plan["target"]["layers"]["api"] + plan["target"]["layers"]["web"]:
            if path not in plan["baseline"]["layers"]["api"] + plan["baseline"]["layers"]["web"]:
                self.assertNotIn(path, used)
        for bad in ("current", None):
            with self.subTest(which=bad), \
                    self.assertRaisesRegex(sw.Stop, "^SWITCH_PLAN_INVALID$"):
                sw.ServiceSwitcher.for_plan(plan, bad, **kwargs)
        with self.assertRaisesRegex(sw.Stop, "^SWITCH_PLAN_INVALID$"):
            sw.ServiceSwitcher.for_plan({}, "baseline", **kwargs)

    def test_unit_is_recorded_before_the_runner_starts(self):
        self.make().switch("web")
        self.assertLess(self.log.index("record"),
                        next(i for i, x in enumerate(self.log) if isinstance(x, tuple)))

    def test_record_failure_means_nothing_was_started(self):
        self.record_error = True
        with self.assertRaisesRegex(sw.Stop, "^SWITCH_RECORD_FAILED$"):
            self.make().switch("api")
        self.assertEqual(self.runs(), [])

    def test_busy_scope_before_launch_runs_nothing(self):
        self.scope_states = ["ACTIVE"]
        with self.assertRaisesRegex(sw.Stop, "^SWITCH_CHILDREN_STILL_ALIVE$"):
            self.make().switch("api")
        self.assertEqual(self.runs(), [])

    def test_indeterminate_scope_before_launch_runs_nothing(self):
        self.scope_states = ["WHO-KNOWS"]
        with self.assertRaisesRegex(sw.Stop, "^SWITCH_SCOPE_STATE_UNKNOWN$"):
            self.make().switch("api")
        self.assertEqual(self.runs(), [])

    def test_nonzero_or_non_int_exit_code_is_a_failure(self):
        for code in (1, None, True, "0", 0.0):
            self.log.clear()
            self.code = code
            with self.subTest(code=code), \
                    self.assertRaisesRegex(sw.Stop, "^SWITCH_COMMAND_FAILED$"):
                self.make().switch("api")

    def test_runner_exception_is_not_echoed(self):
        self.runner_error = True
        with self.assertRaises(sw.Stop) as caught:
            self.make().switch("api")
        self.assertEqual(str(caught.exception), "SWITCH_RUNNER_FAILED")

    def test_child_still_alive_after_runner_is_fatal(self):
        self.scope_states = ["ABSENT", "ACTIVE"]
        with self.assertRaisesRegex(sw.Stop, "^SWITCH_CHILDREN_STILL_ALIVE$"):
            self.make().switch("api")

    def test_runtime_that_keeps_changing_is_fatal(self):
        self.snapshots = [{"a": 1}, {"a": 2}, {"a": 3}]
        with self.assertRaisesRegex(sw.Stop, "^SWITCH_RUNTIME_STILL_CHANGING$"):
            self.make().switch("web")

    def test_invalid_inputs_fail_before_any_effect(self):
        bad = (dict(files=["relative.json"]), dict(project="BAD NAME"),
               dict(nonce="short"), dict(files=[]), dict(workdir="/opt/../etc"))
        for over in bad:
            with self.subTest(over=over), \
                    self.assertRaisesRegex(sw.Stop, "^SWITCH_COMMAND_INVALID$"):
                self.make(**over)
        with self.assertRaisesRegex(sw.Stop, "^SWITCH_ADAPTER_INVALID$"):
            self.make(runner=None)
        with self.assertRaisesRegex(sw.Stop, "^SWITCH_SERVICE_INVALID$"):
            self.make().switch("db")
        self.assertEqual(self.runs(), [])

    def test_switch_all_rolls_web_then_api_and_stops_on_first_failure(self):
        self.make().switch_all()
        services = [argv[-1] for _, argv in self.runs()]
        self.assertEqual(services, ["web", "api"])
        self.log.clear()
        self.code = 2
        with self.assertRaises(sw.Stop):
            self.make().switch_all()
        self.assertEqual(len(self.runs()), 1)
        with self.assertRaisesRegex(sw.Stop, "^SWITCH_ORDER_INVALID$"):
            self.make().switch_all(("web", "web"))

    def test_module_has_no_execution_or_io_imports(self):
        source = PATH.read_text(encoding="utf-8")
        for needle in ("import subprocess", "import socket", "import os",
                       "import urllib", "import requests", "os.system(",
                       "Popen(", "shell=True"):
            self.assertNotIn(needle, source)


if __name__ == "__main__":
    unittest.main()
