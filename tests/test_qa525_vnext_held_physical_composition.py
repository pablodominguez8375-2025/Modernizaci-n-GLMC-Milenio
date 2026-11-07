"""QA525 integrated B7 -> upper CommandGuard -> lower systemd runner.
All world/process effects are synthetic. NEVER invoke Docker, systemd or host.

Depends on reviewed PR #923 (plan-bound bridge) and #921 (upper policy).
Until both land on dev this test is intentionally NOT green; do not add skips.
"""
import ast
import importlib.util
from pathlib import Path
import unittest

from test_qa525_vnext_root_locks import Port, rl, fstat_
from test_qa525_vnext_b7_held_readonly import b7
from test_qa525_vnext_b7_root_boundary import bound
import test_qa525_vnext_engine as e

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, BASE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


# Defer imports until the integration suite itself runs: generic readiness
# scanners must still import/inspect this test file while #921/#923 are DRAFT.
# Missing dependencies FAIL the integration tests explicitly, never skip.
def load_required_dependencies():
    global held, upper, lower
    try:
        held = load("qa525_three_layer_held", "qa525-vnext-held-flight-bridge.py")
        upper = load("qa525_three_layer_upper", "qa525-vnext-physical-command-guard.py")
        lower = load("qa525_three_layer_lower", "qa525-vnext-held-physical-runner.py")
    except (FileNotFoundError, ImportError) as exc:
        raise AssertionError("QA525_COMPOSITION_DEPENDENCY_NOT_IN_DEV_921_923") from None


engine = e.engine
trust = e.trust


def observation_shape(pin):
    return {
        "status": b7.SHAPE_STATUS,
        "baseline_sha256": pin,
        "observation_sha256": "sha256:" + "e" * 64,
        "running_services_expected": 7,
        "exited_zero_oneshots_verified": 3,
        "api_compose_layers": 6, "web_compose_layers": 5,
        "samples": 3,
        "sourced_from_authenticated_host": False,
        "sponsor_approved_here": False, "g6_verified_here": False,
        "may_apply": False, "may_install": False,
    }


class Scope:
    def __init__(self, owner, unit):
        self.unit = unit
        self.owner = owner

    def state(self):
        return self.owner.scope_states.get(self.unit, "ABSENT")


class ThreeLayerCompositionTests(unittest.TestCase):
    """Actual modules and journal/locks with an injected fake physical world."""

    def setUp(self):
        load_required_dependencies()
        self.h = e.EngineTests("test_happy_path_records_scopes_then_applies_under_all_locks")
        self.h.setUp()
        self.port = Port()
        self.pin = engine.observer.plan_mod.validate_baseline(self.h.base)
        engine.scopes.journal = engine.flight.journal
        self.pins = {key: "sha256:" + "a" * 64 for key in lower.PIN_KEYS}
        self.factory_calls = 0
        self.factories = []
        self.guards = []
        self.effects = []
        self.durable_stages = []
        self.scope_states = {}
        self.pins_ok = True
        self.approval_ok = True
        self.intent_ok = True
        self.scope_ok = True
        self.effect_mode = None
        self.baseline_reads = 0
        original = self.h.reader.capture_baseline

        def one_baseline():
            self.baseline_reads += 1
            if self.baseline_reads != 1:
                raise AssertionError("SECOND_BASELINE_CAPTURE_DENIED")
            return original()

        self.h.reader.capture_baseline = one_baseline

    def tearDown(self):
        self.h.tearDown()

    def require_intent(self, plane, stage):
        """Read real durable journal tip, not an in-memory stage assertion."""
        try:
            _root, _nonce, _adm, rows = engine.flight.store._chain(plane.lease)
            actual = rows[-1][0]["plan"]["stage"]
            self.durable_stages.append((stage, actual))
            return self.intent_ok is True and actual == stage
        except Exception:
            return False

    def require_scope(self, plane, unit):
        try:
            result = engine.scopes.require_recorded(
                plane.lease, self.h.fx.digest, unit)
            return self.scope_ok is True and type(result) is str and result.startswith("sha256:")
        except Exception:
            return False

    def effect(self, argv):
        self.effects.append(tuple(argv))
        outcome = self.h.runner(argv)
        if self.effect_mode == "revoke_pins":
            self.pins_ok = False
        elif self.effect_mode == "lose_lock":
            self.port.nodes[rl.LEGACY_LOCKS[0]] = fstat_(90210)
        elif self.effect_mode == "leave_cgroup_active":
            self.scope_states[engine.switch.containment.unit_name(
                self.h.nonce, argv[-1])] = "ACTIVE"
        elif self.effect_mode == "raise_after_effect":
            raise RuntimeError("simulated ambiguous child completion")
        return outcome

    def factory(self, plane, boundary, report, plan):
        self.factory_calls += 1
        self.factories.append((plane, boundary, report, plan))
        self.assertEqual(len(self.port.fds), 5)
        self.assertIs(boundary._plane, plane)
        self.assertEqual(plan["nonce"], plane.lease.nonce)
        self.assertEqual(plan["admission_sha256"], self.h.fx.digest)
        self.assertEqual(plan["baseline_sha256"], report["baseline_sha256"])
        self.assertEqual(plan["baseline"]["layers"], self.h.base["layers"])
        self.assertEqual(plan["target"]["layers"], self.h.target["layers"])
        self.assertEqual(engine.flight.plan_mod.validate(plan), "APPLYING")
        spec = dict(
            nonce=plan["nonce"], admission_sha256=plan["admission_sha256"],
            project="centenario", workdir="/opt/centenario",
            env_file="/etc/pmgm/qa.env",
            baseline_layers=plan["baseline"]["layers"],
            target_layers=plan["target"]["layers"],
        )
        runner = lower.bind(
            root_locks=rl, boundary_module=bound,
            containment=engine.switch.containment, scopes=engine.scopes,
            plane=plane, boundary=boundary, report=report, spec=spec,
            pins=self.pins, verify_pins=lambda pins: (
                self.pins_ok is True and pins == self.pins),
            execute=self.effect,
        )
        guard = upper.CommandGuard(
            containment=engine.switch.containment,
            plan_mod=engine.flight.plan_mod, plan=plan,
            boundary=boundary, record=report,
            project=spec["project"], workdir=spec["workdir"],
            env_file=spec["env_file"],
            run_effect=runner,
            scope_factory=lambda unit: Scope(self, unit),
            require_intent=lambda stage: self.require_intent(plane, stage),
            require_scope_recorded=lambda unit: self.require_scope(plane, unit),
            verify_pins=lambda argv: self.pins_ok is True,
            verify_approval=lambda stage: self.approval_ok is True,
        )
        for side in ("target", "baseline"):
            for service in ("api", "web"):
                self.assertEqual(
                    tuple(guard.command_for(side, service)),
                    tuple(engine.switch.containment.systemd_run_argv(
                        engine.switch.containment.unit_name(plane.lease.nonce, service),
                        spec["project"], spec["workdir"], spec["env_file"],
                        plan[side]["layers"][service], service,
                    )),
                )
        self.guards.append(guard)
        return guard

    def run_flight(self, **changes):
        self.assertFalse(self.effects)
        params = dict(
            root_locks=rl, bridge=engine.bridge, boundary=bound, b7=b7,
            observer=engine.observer, scopes=engine.scopes,
            switch=engine.switch, flight=engine.flight,
            g6_anchor=engine.g6_anchor, port=self.port,
            root=self.h.fx.root, admission=self.h.fx.admission,
            admission_sha256=self.h.fx.digest,
            admission_bytes=trust.canonical(self.h.record),
            approval_bytes=self.h.approval, policy_hash=trust.POLICY,
            baseline_sha256=self.pin,
            reader=self.h.reader, target=self.h.target,
            project="centenario", workdir="/opt/centenario",
            env_file="/etc/pmgm/qa.env",
            clock=lambda: trust.NOW, inspector=lambda: observation_shape(self.pin),
            runner_factory=self.factory, external_gate=lambda stage: True,
            g6_evidence=self.h.g6_evidence,
            scope_factory=lambda unit: Scope(self, unit), settle=lambda: None,
        )
        params.update(changes)
        return held.run_held(**params)

    def test_forward_two_services_uses_one_plan_one_plane_and_durable_intents(self):
        result = self.run_flight()
        self.assertEqual(result["terminal_outcome"], "APPLIED")
        self.assertEqual(self.h.terminal(), "APPLIED")
        self.assertEqual(self.h.runs, [("api", "target"), ("web", "target")])
        self.assertEqual(self.factory_calls, 1)
        self.assertEqual(self.baseline_reads, 1)
        self.assertEqual(self.durable_stages,
                         [("API_SWITCHING", "API_SWITCHING"),
                          ("WEB_SWITCHING", "WEB_SWITCHING")])
        self.assertEqual(len(self.effects), 2)
        self.assertEqual(self.port.fds, {})
        for key in ("signature_verified_here", "sponsor_approved_here",
                    "may_install", "may_apply", "physical_host_authenticated_here"):
            self.assertIs(result[key], False)
        guard = self.guards[0]
        with self.assertRaises(upper.Stop):
            guard(guard.command_for("target", "api"))
        self.assertEqual(len(self.effects), 2)

    def test_web_failure_rolls_back_both_services_under_same_lease(self):
        self.h.fail.add(("web", "target"))
        result = self.run_flight()
        self.assertEqual(result["terminal_outcome"], "ROLLED_BACK")
        self.assertEqual(self.h.world, {"api": "baseline", "web": "baseline"})
        self.assertEqual(self.h.runs,
                         [("api", "target"), ("web", "target"),
                          ("web", "baseline"), ("api", "baseline")])
        self.assertEqual(self.durable_stages,
                         [("API_SWITCHING", "API_SWITCHING"),
                          ("WEB_SWITCHING", "WEB_SWITCHING"),
                          ("ROLLING_BACK", "ROLLING_BACK"),
                          ("ROLLING_BACK", "ROLLING_BACK")])
        self.assertEqual(len(self.effects), 4)
        self.assertEqual(self.factory_calls, 1)
        self.assertEqual(self.port.fds, {})

    def test_no_child_when_scope_manifest_denies_or_cgroup_is_active(self):
        self.scope_ok = False
        with self.assertRaises(Exception):
            self.run_flight()
        self.assertEqual(self.effects, [])
        self.assertEqual(self.port.fds, {})

    def test_no_child_for_active_cgroup_even_with_recorded_scope(self):
        self.scope_states[engine.switch.containment.unit_name(self.h.nonce, "api")] = "ACTIVE"
        with self.assertRaises(Exception):
            self.run_flight()
        self.assertEqual(self.effects, [])
        self.assertEqual(self.port.fds, {})

    def test_truthy_approval_is_not_a_boolean_authorization(self):
        self.approval_ok = 1
        # The injected callback enforces identity (is True), not mere truthiness.
        with self.assertRaises(Exception):
            self.run_flight()
        self.assertEqual(self.effects, [])

    def test_child_revokes_pins_cannot_execute_second_target(self):
        self.effect_mode = "revoke_pins"
        with self.assertRaises(Exception):
            self.run_flight()
        self.assertEqual(self.h.runs[0], ("api", "target"))
        self.assertNotIn(("web", "target"), self.h.runs)
        self.assertEqual(len(self.effects), 1)
        self.assertEqual(self.port.fds, {})

    def test_lost_lock_after_child_blocks_other_mutation(self):
        self.effect_mode = "lose_lock"
        with self.assertRaises(Exception):
            self.run_flight()
        self.assertEqual(self.h.runs[0], ("api", "target"))
        self.assertNotIn(("web", "target"), self.h.runs)
        self.assertEqual(len(self.effects), 1)
        self.assertEqual(self.port.fds, {})

    def test_ambiguous_child_exception_never_replays_target(self):
        self.effect_mode = "raise_after_effect"
        with self.assertRaises(Exception):
            self.run_flight()
        self.assertEqual(self.h.runs[0], ("api", "target"))
        self.assertNotIn(("web", "target"), self.h.runs)
        self.assertEqual(self.h.runs.count(("api", "target")), 1)
        # Rollback may have been attempted after a persisted API intent;
        # never interpret the ambiguous child as permission to retry target.
        self.assertGreaterEqual(len(self.effects), 1)
        self.assertEqual(self.port.fds, {})

    def test_invalid_g6_stops_before_any_factory_or_child(self):
        evidence = dict(self.h.g6_evidence)
        evidence["review_bytes"] = b"untrusted"
        with self.assertRaises(held.Stop):
            self.run_flight(g6_evidence=evidence)
        self.assertEqual(self.factory_calls, 0)
        self.assertEqual(self.effects, [])
        self.assertEqual(self.port.fds, {})

    def test_source_has_no_process_execution_and_is_not_a_host_installer(self):
        source = Path(__file__).read_text()
        tree = ast.parse(source)
        imported = {alias.name for node in ast.walk(tree)
                    if isinstance(node, (ast.Import, ast.ImportFrom))
                    for alias in node.names}
        self.assertFalse(imported & {"subprocess", "socket", "pty"})
        self.assertNotIn("os.system(", source)
        self.assertNotIn("Popen(", source)
        self.assertFalse(callable(getattr(held, "main", None)))
        self.assertFalse(callable(getattr(lower, "main", None)))


if __name__ == "__main__":
    unittest.main()
