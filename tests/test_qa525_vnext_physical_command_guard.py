"""QA525 exact-4-command guard: hermetic validation, never run Docker/systemd."""
import ast
import copy
import importlib.util
from pathlib import Path
import unittest

import test_qa525_vnext_baseline_plan as bp
import test_qa525_vnext_b7_root_boundary as bt
from test_qa525_vnext_root_locks import Port, rl, fstat_

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, BASE / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


guardmod = load("qa525_physical_command_guard_test", "qa525-vnext-physical-command-guard.py")
containment = load("qa525_containment_for_command_guard", "qa525-vnext-containment.py")


class LiveGuardTests(unittest.TestCase):
    def setUp(self):
        self.port = Port()
        self.log = []
        self.journal = bt.NonceJournal(self.log)
        self.flight = rl.root_flight(self.port, self.journal,
                                     "root", "admission", bt.ADMISSION, "now")
        self.plane = self.flight.__enter__()
        self.plan = bp.make_plan(nonce=bt.NONCE, admission=bt.ADMISSION)
        self.boundary = bt.bound.Boundary(
            root_locks=rl, plane=self.plane, nonce=bt.NONCE,
            admission_sha256=bt.ADMISSION)
        self.record = self.boundary.inspect_once(
            b7=bt.B7Stub(),
            inspector=lambda: {"observation_sha256": bt.OBS},
            baseline_sha256=self.plan["baseline_sha256"])
        self.calls, self.intent_calls, self.verified, self.approval = [], [], [], []
        self.scope_states = {}
        self.runner_error = None

    def tearDown(self):
        self.flight.__exit__(None, None, None)

    def effect(self, argv):
        self.calls.append(list(argv))
        if self.runner_error is not None:
            raise self.runner_error
        return 0

    def scope(self, unit):
        class Scope:
            def __init__(self, name, state):
                self.unit, self._state = name, state

            def state(self):
                return self._state
        return Scope(unit, self.scope_states.get(unit, "ABSENT"))

    def intent(self, stage):
        self.intent_calls.append(stage)
        return True

    def pinned(self, argv):
        self.verified.append(tuple(argv))
        return True

    def approved(self, stage):
        self.approval.append(stage)
        return True

    def make(self, **changes):
        args = dict(
            containment=containment, plan_mod=bp.mod, plan=self.plan,
            boundary=self.boundary, record=self.record,
            project="centenario-qa-lab2",
            workdir="/opt/centenario", env_file="/etc/pmgm/qa.env",
            run_effect=self.effect, scope_factory=self.scope,
            require_intent=self.intent, require_scope_recorded=lambda unit: True,
            verify_pins=self.pinned, verify_approval=self.approved,
        )
        args.update(changes)
        return guardmod.CommandGuard(**args)

    def denied(self, code, callback):
        with self.assertRaises(guardmod.Stop) as exc:
            callback()
        self.assertEqual(str(exc.exception), code)
        self.assertIsNone(exc.exception.__cause__)

    def test_four_canonical_systemd_scope_commands_no_execution_by_default(self):
        guard = self.make()
        self.assertEqual(guard.status["allowed_commands"], 4)
        for side in ("target", "baseline"):
            for service in ("api", "web"):
                command = guard.command_for(side, service)
                self.assertEqual(command[0], "/usr/bin/systemd-run")
                self.assertEqual(command[1:5],
                                 ["--scope", "--collect", "--quiet", "--slice=system.slice"])
                self.assertEqual(command[-1], service)
                self.assertTrue(any(a.startswith("--unit=qa525-") for a in command))
        self.assertEqual(self.calls, [])
        for flag in ("signature_verified_here", "sponsor_approved_here",
                     "may_apply", "may_install"):
            self.assertIs(guard.status[flag], False)

    def test_api_web_forward_and_baseline_rollback_each_used_once(self):
        guard = self.make()
        for side in ("target", "baseline"):
            for service in ("api", "web"):
                self.assertEqual(guard(guard.command_for(side, service)), 0)
        self.assertEqual(self.approval,
                         ["API_SWITCHING", "WEB_SWITCHING",
                          "ROLLING_BACK", "ROLLING_BACK"])
        self.assertEqual(self.intent_calls, self.approval)
        self.assertEqual(len(self.calls), 4)
        self.assertEqual(guard.status["attempts"], 4)
        self.denied("COMMANDGUARD_RETRY_REFUSED",
                    lambda: guard(guard.command_for("target", "api")))
        self.assertEqual(len(self.calls), 4)

    def test_mutated_flags_services_layers_unit_and_extra_commands_denied(self):
        guard = self.make()
        good = guard.command_for("target", "api")
        bads = [
            " ".join(good), [], ["/bin/sh", "-c", "true"],
            good + ["--privileged"], good[:-1] + ["postgres"],
            good[:-1] + ["web"], ["sudo"] + good,
            good[:5] + ["--unit=wrong.scope"] + good[6:],
            good[:7] + ["/usr/bin/env"] + good[8:],
            good[:12] + ["/tmp/fake"] + good[13:],
            [x.replace("never", "always") for x in good],
            good[:17] + ["/tmp/unreviewed.yml"] + good[18:],
            good + [None],
        ]
        for bad in bads:
            with self.subTest(bad=bad):
                self.denied("COMMANDGUARD_ARGV_REFUSED", lambda command=bad: guard(command))
        self.assertEqual(self.calls, [])
        self.assertEqual(guard.status["attempts"], 0)

    def test_approval_pins_intent_and_scope_fail_before_execute_and_are_once_only(self):
        invalids = [
            dict(verify_approval=lambda stage: 1),
            dict(verify_approval=lambda stage: False),
            dict(verify_pins=lambda argv: "yes"),
            dict(require_intent=lambda stage: False),
            dict(require_scope_recorded=lambda unit: False),
        ]
        for fail in invalids:
            with self.subTest(fail=tuple(fail)):
                guard = self.make(**fail)
                cmd = guard.command_for("target", "api")
                self.denied("COMMANDGUARD_PRECONDITION_DENIED", lambda: guard(cmd))
                self.assertEqual(self.calls, [])
                self.denied("COMMANDGUARD_RETRY_REFUSED", lambda: guard(cmd))

    def test_scope_foreign_active_or_unreadable_is_stop_and_no_effect(self):
        unit = containment.unit_name(bt.NONCE, "api")
        for state in ("ACTIVE", "UNKNOWN"):
            self.scope_states[unit] = state
            guard = self.make()
            self.denied("COMMANDGUARD_CGROUP_UNSAFE",
                        lambda: guard(guard.command_for("target", "api")))
            self.assertEqual(self.calls, [])
        guard = self.make(scope_factory=lambda unit: None)
        self.denied("COMMANDGUARD_CGROUP_UNSAFE",
                    lambda: guard(guard.command_for("target", "api")))

    def test_active_api_scope_forbids_web_rollback_even_if_web_scope_is_absent(self):
        # Regression P1: a global flight MUST NOT mutate a different service
        # while an unresolved process from the API scope is still running.
        api = containment.unit_name(bt.NONCE, "api")
        self.scope_states[api] = "ACTIVE"
        guard = self.make()
        self.denied("COMMANDGUARD_CGROUP_UNSAFE",
                    lambda: guard(guard.command_for("baseline", "web")))
        self.assertEqual(self.calls, [])
        self.assertEqual(guard.status["attempts"], 1)

    def test_unknown_web_scope_forbids_api_target_without_any_child(self):
        web = containment.unit_name(bt.NONCE, "web")
        self.scope_states[web] = "UNKNOWN"
        guard = self.make()
        self.denied("COMMANDGUARD_CGROUP_UNSAFE",
                    lambda: guard(guard.command_for("target", "api")))
        self.assertEqual(self.calls, [])

    def test_other_scope_becomes_active_after_child_and_success_is_denied(self):
        web = containment.unit_name(bt.NONCE, "web")

        def awaken_other(argv):
            self.calls.append(list(argv))
            self.scope_states[web] = "ACTIVE"
            return 0

        guard = self.make(run_effect=awaken_other)
        self.denied("COMMANDGUARD_CGROUP_UNSAFE",
                    lambda: guard(guard.command_for("target", "api")))
        self.assertEqual(len(self.calls), 1)
        self.denied("COMMANDGUARD_RETRY_REFUSED",
                    lambda: guard(guard.command_for("target", "api")))

    def test_after_child_cgroup_active_denied_and_lock_loss_caught(self):
        unit = containment.unit_name(bt.NONCE, "api")

        def leave_live(argv):
            self.calls.append(argv)
            self.scope_states[unit] = "ACTIVE"
            return 0
        guard = self.make(run_effect=leave_live)
        self.denied("COMMANDGUARD_CGROUP_UNSAFE",
                    lambda: guard(guard.command_for("target", "api")))
        self.assertEqual(len(self.calls), 1)
        self.scope_states.clear()

        def swap_lock(argv):
            self.calls.append(argv)
            self.port.nodes[rl.LEGACY_LOCKS[0]] = fstat_(779)
            return 0
        guard = self.make(run_effect=swap_lock)
        self.denied("COMMANDGUARD_PLANE_LOST",
                    lambda: guard(guard.command_for("target", "api")))
        self.assertEqual(len(self.calls), 2)

    def test_uncertain_child_exception_never_retries_or_leaks_secret(self):
        guard = self.make(run_effect=lambda argv: (_ for _ in ()).throw(
            OSError("secret private creds")))
        cmd = guard.command_for("target", "api")
        self.denied("COMMANDGUARD_CHILD_UNCERTAIN", lambda: guard(cmd))
        self.denied("COMMANDGUARD_RETRY_REFUSED", lambda: guard(cmd))
        self.assertEqual(self.calls, [])

    def test_bad_result_type_is_uncertain_and_zero_is_valid(self):
        guard = self.make(run_effect=lambda argv: True)
        self.denied("COMMANDGUARD_CHILD_UNCERTAIN",
                    lambda: guard(guard.command_for("target", "api")))
        guard = self.make(run_effect=lambda argv: 2)
        self.assertEqual(guard(guard.command_for("target", "api")), 2)

    def test_live_boundary_must_match_nonce_and_record_identity(self):
        self.denied("COMMANDGUARD_FLIGHT_UNBOUND", lambda: self.make(record=dict(self.record)))
        another = copy.deepcopy(self.plan)
        another["nonce"] = "X" * 40
        self.denied("COMMANDGUARD_PLAN_INVALID", lambda: self.make(plan=another))

    def test_plan_tamper_missing_or_duplicate_commands_fail_before_any_effect(self):
        modified = copy.deepcopy(self.plan)
        modified["baseline"]["images"]["api"] = "sha256:" + "f" * 64
        self.denied("COMMANDGUARD_PLAN_INVALID", lambda: self.make(plan=modified))
        same_layers = copy.deepcopy(self.plan)
        same_layers["target"]["layers"] = copy.deepcopy(same_layers["baseline"]["layers"])
        same_layers = bp.mod.prepare(same_layers["baseline"], same_layers["target"],
                                    nonce=bt.NONCE, admission_sha256=bt.ADMISSION)
        self.denied("COMMANDGUARD_DUPLICATE_COMMAND", lambda: self.make(plan=same_layers))
        self.assertEqual(self.calls, [])

    def test_expired_or_released_plane_does_not_invoke_callback(self):
        guard = self.make()
        self.plane.lease.held = False
        self.denied("COMMANDGUARD_PLANE_LOST",
                    lambda: guard(guard.command_for("target", "api")))
        self.assertEqual(self.calls, [])

    def test_no_host_execution_or_dangerous_import_and_no_cli(self):
        source = (BASE / "qa525-vnext-physical-command-guard.py").read_text()
        names = {a.name for n in ast.walk(ast.parse(source))
                 if isinstance(n, ast.Import) for a in n.names}
        self.assertEqual(names, {"re"})
        for text in ("import subprocess", "import os", "import socket",
                     "Popen(", "shell=True", "os.system(", "sudo -"):
            self.assertNotIn(text, source)
        with self.assertRaises(SystemExit):
            exec(compile(source, "guard.py", "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
