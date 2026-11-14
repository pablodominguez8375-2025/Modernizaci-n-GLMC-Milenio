"""Offline QA525 REAL RootPlane/B7/flight orchestration with synthetic effects.

No host, Docker, network, sudo or real systemd unit is run. The *actual* vNext
journal, admission bytes/G6 anchor, B7 and flight/rollback code are exercised
against a private temp journal and a fake POSIX root-lock port.
"""
import ast
import importlib.util
from pathlib import Path
import unittest

from test_qa525_vnext_root_locks import Port, rl, fstat_
from test_qa525_vnext_b7_held_readonly import b7
from test_qa525_vnext_b7_root_boundary import bound
import test_qa525_vnext_engine as engtest
engine, trust, Scope = engtest.engine, engtest.trust, engtest.Scope

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"
SCRIPT = BASE / "qa525-vnext-held-flight-bridge.py"
SPEC = importlib.util.spec_from_file_location("qa525_held_flight_bridge_t", SCRIPT)
host = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(host)


def shape(pin):
    """B7's 10-service read-only adapter contract, no Docker calls."""
    return {
        "status": b7.SHAPE_STATUS,
        "baseline_sha256": pin,
        "observation_sha256": "sha256:" + "e" * 64,
        "running_services_expected": 7,
        "exited_zero_oneshots_verified": 3,
        "api_compose_layers": 6,
        "web_compose_layers": 5,
        "samples": 3,
        "sourced_from_authenticated_host": False,
        "sponsor_approved_here": False,
        "g6_verified_here": False,
        "may_apply": False,
        "may_install": False,
    }


class RealHeldFlightWithFakeEffectsTests(unittest.TestCase):
    def setUp(self):
        # Uses existing independently tested vNext fixture; no host execution.
        self.h = engtest.EngineTests("test_happy_path_records_scopes_then_applies_under_all_locks")
        self.h.setUp()
        self.port = Port()
        self.gates = []
        self.inspected = 0
        self.factories = 0
        self.pin = engine.observer.plan_mod.validate_baseline(self.h.base)

    def tearDown(self):
        self.h.tearDown()

    def args(self, **changes):
        def inspect():
            self.inspected += 1
            self.assertEqual(len(self.port.fds), 5)
            return shape(changes.get("baseline_sha256", self.pin))

        def factory(plane, bnd, report, plan):
            self.factories += 1
            self.assertEqual(len(self.port.fds), 5)
            self.assertEqual(plane.lease.nonce, self.h.nonce)
            bnd.require_current_record(report)
            self.assertEqual(engine.flight.plan_mod.validate(plan), "APPLYING")
            self.assertEqual(plan["nonce"], self.h.nonce)
            self.assertEqual(plan["admission_sha256"], self.h.fx.digest)
            self.assertEqual(plan["baseline_sha256"], self.pin)
            self.assertEqual(plan["baseline"]["layers"], self.h.base["layers"])
            self.assertEqual(plan["target"]["layers"], self.h.target["layers"])
            return self.h.runner

        def gate(stage):
            self.gates.append(stage)
            return True

        params = dict(
            root_locks=rl, bridge=engine.bridge, boundary=bound, b7=b7,
            observer=engine.observer, scopes=engine.scopes,
            switch=engine.switch, flight=engine.flight,
            g6_anchor=engine.g6_anchor,
            port=self.port,
            root=self.h.fx.root, admission=self.h.fx.admission,
            admission_sha256=self.h.fx.digest,
            admission_bytes=trust.canonical(self.h.record),
            approval_bytes=self.h.approval,
            policy_hash=trust.POLICY,
            baseline_sha256=self.pin,
            reader=self.h.reader, target=self.h.target,
            project="centenario", workdir="/opt/centenario",
            env_file="/etc/pmgm/qa.env",
            clock=lambda: trust.NOW,
            inspector=inspect, runner_factory=factory, external_gate=gate,
            g6_evidence=self.h.g6_evidence,
            scope_factory=Scope, settle=lambda: None,
        )
        params.update(changes)
        return params

    def call(self, **changes):
        return host.run_held(**self.args(**changes))

    def test_apply_keeps_all_five_locks_and_same_nonce_through_dual_update(self):
        result = self.call()
        self.assertEqual(result["status"], host.STATUS)
        self.assertEqual(result["terminal_outcome"], "APPLIED")
        self.assertEqual(result["nonce"], self.h.nonce)
        self.assertEqual(result["baseline_sha256"], self.pin)
        self.assertEqual(self.h.runs, [("api", "target"), ("web", "target")])
        self.assertEqual(self.inspected, 1)
        self.assertEqual(self.factories, 1)
        self.assertEqual(self.port.fds, {})  # fully released only after flight
        for key in ("may_apply", "may_install", "signature_verified_here",
                    "sponsor_approved_here", "g6_custody_verified_here",
                    "physical_host_authenticated_here"):
            self.assertIs(result[key], False)
        self.assertEqual(self.h.terminal(), "APPLIED")

    def test_web_failure_rolls_back_both_services_under_same_plane(self):
        self.h.fail.add(("web", "target"))
        result = self.call()
        self.assertEqual(result["terminal_outcome"], "ROLLED_BACK")
        self.assertEqual(self.h.terminal(), "ROLLED_BACK")
        self.assertEqual(self.h.world, {"api": "baseline", "web": "baseline"})
        self.assertEqual(self.h.runs, [
            ("api", "target"), ("web", "target"),
            ("web", "baseline"), ("api", "baseline")])
        self.assertEqual(self.port.fds, {})

    def test_denied_gate_stops_before_any_child(self):
        params = self.args(external_gate=lambda stage: False)
        with self.assertRaises((host.Stop, engine.flight.Stop)):
            host.run_held(**params)
        self.assertEqual(self.h.runs, [])
        self.assertEqual(self.factories, 1)
        self.assertEqual(self.port.fds, {})

    def test_incorrect_g6_blocks_before_inspection_factory_and_runner(self):
        wrong = dict(self.h.g6_evidence)
        wrong["review_bytes"] = b'{"bad":"source"}'
        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_G6_DENIED"):
            self.call(g6_evidence=wrong)
        self.assertEqual(self.inspected, 0)
        self.assertEqual(self.factories, 0)
        self.assertEqual(self.h.runs, [])
        self.assertEqual(self.port.fds, {})

    def test_v1_nine_field_or_malformed_generation_pins_are_input_invalid(self):
        legacy = {k: v for k, v in self.h.g6_evidence.items()
                  if k not in ("downloaded_offhost_sha256",
                               "expected_restic_repo_id", "expected_key_epoch")}
        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_G6_INPUT_INVALID"):
            self.call(g6_evidence=legacy)
        for key, value in (("expected_restic_repo_id", "A" * 64),
                           ("expected_key_epoch", "e" * 31),
                           ("downloaded_offhost_sha256", "a" * 64)):
            bad = dict(self.h.g6_evidence, **{key: value})
            with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_G6_INPUT_INVALID"):
                self.call(g6_evidence=bad)
        self.assertEqual(self.factories, 0)
        self.assertEqual(self.h.runs, [])

    def test_offhost_substitution_blocks_before_inspection_factory_and_runner(self):
        wrong = dict(self.h.g6_evidence,
                     downloaded_offhost_sha256="sha256:" + "b" * 64)
        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_G6_DENIED"):
            self.call(g6_evidence=wrong)
        self.assertEqual(self.inspected, 0)
        self.assertEqual(self.factories, 0)
        self.assertEqual(self.h.runs, [])

    def test_baseline_reread_difference_is_a_stop_without_mutation(self):
        mismatch = "sha256:" + "f" * 64
        params = self.args(baseline_sha256=mismatch)
        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_BASELINE_CHANGED"):
            host.run_held(**params)
        self.assertEqual(self.h.runs, [])
        self.assertEqual(self.factories, 0)
        self.assertEqual(self.inspected, 1)
        self.assertEqual(self.port.fds, {})

    def test_g6_input_changed_during_api_gate_stops_before_web_and_rolls_back(self):
        def change(stage):
            self.gates.append(stage)
            if stage == "WEB_SWITCHING":
                self.h.g6_evidence["review_bytes"] = b'{"changed":true}'
            return True
        result = self.call(external_gate=change)
        self.assertEqual(result["terminal_outcome"], "ROLLED_BACK")
        self.assertEqual(self.h.world, {"api": "baseline", "web": "baseline"})
        self.assertEqual(self.h.terminal(), "ROLLED_BACK")

    def test_dropped_lock_after_first_stage_disallows_next_mutation(self):
        def change(stage):
            self.gates.append(stage)
            if stage == "WEB_SWITCHING":
                self.port.nodes[rl.LEGACY_LOCKS[0]] = fstat_(8888)
            return True
        with self.assertRaises((host.Stop, engine.flight.Stop)):
            self.call(external_gate=change)
        self.assertNotIn(("web", "target"), self.h.runs)
        self.assertEqual(self.port.fds, {})

    def test_factory_gets_actual_same_flight_validated_plan_not_second_capture(self):
        captured = []
        def pinned(plane, bnd, report, plan):
            captured.append(plan)
            self.assertEqual(plan["nonce"], plane.lease.nonce)
            self.assertEqual(plan["baseline_sha256"], report["baseline_sha256"])
            self.assertEqual(plan["baseline"]["layers"], self.h.base["layers"])
            self.assertEqual(plan["target"]["layers"], self.h.target["layers"])
            bnd.require_current_record(report)
            return self.h.runner
        before = self.h.reader.capture_baseline
        calls = []
        def only_once():
            calls.append("read")
            if len(calls) > 1:
                raise RuntimeError("SECOND_RUNTIME_BASELINE_DISALLOWED")
            return before()
        self.h.reader.capture_baseline = only_once
        result = self.call(runner_factory=pinned)
        self.assertEqual(result["terminal_outcome"], "APPLIED")
        self.assertEqual(calls, ["read"])
        self.assertEqual(len(captured), 1)
        self.assertEqual(self.h.runs, [("api", "target"), ("web", "target")])

    def test_mutating_factory_copy_does_not_change_plan_or_launch_child(self):
        def mutate(plane, bnd, report, plan):
            plan["target"]["layers"]["api"].append("/tmp/unapproved.yml")
            return self.h.runner
        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_RUNNER_PLAN_MUTATED"):
            self.call(runner_factory=mutate)
        self.assertEqual(self.h.runs, [])
        self.assertEqual(self.port.fds, {})
        self.assertEqual(self.h.terminal(), None)

    def test_legacy_three_argument_factory_cannot_skip_plan_binding(self):
        def old_factory(plane, bnd, report):
            raise AssertionError("must not be called")
        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_RUNNER_INVALID"):
            self.call(runner_factory=old_factory)
        self.assertEqual(self.h.runs, [])
        self.assertEqual(self.port.fds, {})

    def test_factory_change_to_g6_is_denied_before_mutation(self):
        def mutate_g6(plane, bnd, report, plan):
            self.h.g6_evidence["review_bytes"] = b"bad G6 bytes"
            return self.h.runner
        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_G6_CHANGED"):
            self.call(runner_factory=mutate_g6)
        self.assertEqual(self.h.runs, [])
        self.assertEqual(self.port.fds, {})

    def test_factory_changes_bound_report_is_denied_before_mutation(self):
        def mutate_report(plane, bnd, report, plan):
            report["observation_sha256"] = "sha256:" + "9" * 64
            return self.h.runner
        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_RUNNER_INVALID"):
            self.call(runner_factory=mutate_report)
        self.assertEqual(self.h.runs, [])
        self.assertEqual(self.port.fds, {})

    def test_required_effect_cannot_be_omitted(self):
        params = self.args(runner_factory=None)
        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_ADAPTER_REQUIRED"):
            host.run_held(**params)
        self.assertEqual(self.inspected, 0)
        self.assertEqual(self.h.runs, [])
        self.assertEqual(self.port.fds, {})

    def test_g6_precomputed_boolean_denied(self):
        params = self.args(g6_evidence={"pass": True})
        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_G6_INPUT_INVALID"):
            host.run_held(**params)
        self.assertEqual(self.h.runs, [])

    def test_new_unreviewed_intent_is_denied_before_entering_root(self):
        from unittest import mock
        with mock.patch.dict(engine.flight.plan_mod.PROOF_KINDS,
                             {"FUTURE_SWITCHING": "EXECUTION_INTENT"}):
            with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_INTENT_SCHEMA_INVALID"):
                self.call()
        self.assertEqual(self.port.fds, {})
        self.assertEqual(self.h.runs, [])

    def test_no_cli_or_host_os_api_and_only_caller_injected_effects(self):
        src = SCRIPT.read_text()
        imports = {
            alias.name for n in ast.walk(ast.parse(src))
            if isinstance(n, ast.Import) for alias in n.names
        } | {
            n.module for n in ast.walk(ast.parse(src))
            if isinstance(n, ast.ImportFrom)
        }
        self.assertEqual(imports, {"copy", "re", "types"})
        for text in ("import subprocess", "Popen(", "shell=True", "import os",
                     "import socket", "docker run", "sudo "):
            self.assertNotIn(text, src)
        with self.assertRaises(SystemExit):
            exec(compile(src, str(SCRIPT), "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
