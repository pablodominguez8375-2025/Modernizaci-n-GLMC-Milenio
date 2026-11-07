"""Pruebas que fijan defensas de ``qa525-vnext-held-flight-bridge.py`` que la
suite original no detectaba al mutarlas (revisión adversarial de #917).

Sin host, Docker ni red: mismos efectos falsos y fixtures que la suite base.
Se compone con una instancia del caso base (no se hereda) para no volver a
ejecutar sus pruebas ni duplicar nombres de clase.
"""
import unittest
from unittest import mock

import test_qa525_vnext_held_flight_bridge as base
from test_qa525_vnext_root_locks import rl, fstat_

host, engine = base.host, base.engine
STOPS = (host.Stop, engine.flight.Stop)


class HeldFlightBridgeSurvivorGuards(unittest.TestCase):
    def setUp(self):
        self.t = base.RealHeldFlightWithFakeEffectsTests(
            "test_apply_keeps_all_five_locks_and_same_nonce_through_dual_update")
        self.t.setUp()
        self.addCleanup(self.t.tearDown)

    def assert_no_child(self):
        self.assertEqual(self.t.h.runs, [])
        self.assertEqual(self.t.port.fds, {})

    def test_scope_unit_must_be_recorded_before_any_child_is_spawned(self):
        stop = engine.scopes.Stop("SCOPE_UNIT_NOT_RECORDED")
        with mock.patch.object(engine.scopes, "require_recorded", side_effect=stop):
            with self.assertRaises(STOPS):
                self.t.call()
        self.assert_no_child()

    def test_lock_lost_while_a_child_runs_blocks_the_next_step(self):
        t = self.t

        def factory(plane, bnd, report, plan):
            # Guard against false-green arity TypeError before the child.
            self.assertEqual(engine.flight.plan_mod.validate(plan), "APPLYING")
            self.assertEqual(plan["nonce"], plane.lease.nonce)
            bnd.require_current_record(report)
            def runner(argv):
                code = t.h.runner(argv)
                if ("api", "target") in t.h.runs:
                    t.port.nodes[rl.LEGACY_LOCKS[0]] = fstat_(7777)
                return code
            return runner

        with self.assertRaises(STOPS):
            t.call(runner_factory=factory)
        self.assertIn(("api", "target"), t.h.runs)
        self.assertEqual(t.h.runs.count(("api", "target")), 1)
        self.assertNotIn(("web", "target"), t.h.runs)
        self.assertEqual(t.port.fds, {})

    def test_lock_lost_right_after_the_last_child_never_records_applied(self):
        t = self.t

        def factory(plane, bnd, report, plan):
            # Guard against false-green arity TypeError before the child.
            self.assertEqual(engine.flight.plan_mod.validate(plan), "APPLYING")
            self.assertEqual(plan["nonce"], plane.lease.nonce)
            bnd.require_current_record(report)
            def runner(argv):
                code = t.h.runner(argv)
                if ("web", "target") in t.h.runs:
                    t.port.nodes[rl.LEGACY_LOCKS[0]] = fstat_(6666)
                return code
            return runner

        with self.assertRaises(STOPS):
            t.call(runner_factory=factory)
        self.assertIn(("web", "target"), t.h.runs)
        self.assertEqual(t.h.runs.count(("web", "target")), 1)
        self.assertNotEqual(t.h.terminal(), "APPLIED")
        self.assertEqual(t.port.fds, {})

    def test_gate_must_return_exactly_true(self):
        for truthy in (1, "yes", object(), [0]):
            with self.subTest(value=repr(truthy)[:20]):
                t = base.RealHeldFlightWithFakeEffectsTests(
                    "test_apply_keeps_all_five_locks_and_same_nonce_through_dual_update")
                t.setUp()
                try:
                    with self.assertRaises(STOPS):
                        t.call(external_gate=lambda stage, v=truthy: v)
                    self.assertEqual(t.h.runs, [])
                finally:
                    t.tearDown()

    def _tampered_g6(self, **override):
        real = engine.g6_anchor.compare

        def compare(**kw):
            result = dict(real(**kw))
            result.update(override)
            return result
        return mock.patch.object(engine.g6_anchor, "compare", compare)

    def test_g6_result_must_match_the_external_anchor_and_the_lease_nonce(self):
        for override in ({"approval_sha256": "sha256:" + "0" * 64},
                         {"nonce": "0" * 32}):
            with self.subTest(override=sorted(override)):
                t = base.RealHeldFlightWithFakeEffectsTests(
                    "test_apply_keeps_all_five_locks_and_same_nonce_through_dual_update")
                t.setUp()
                try:
                    with self._tampered_g6(**override):
                        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_G6_DENIED"):
                            t.call()
                    self.assertEqual(t.h.runs, [])
                    self.assertEqual(t.inspected, 0)
                finally:
                    t.tearDown()

    def test_g6_changed_during_inspection_stops_before_the_plan_and_factory(self):
        t = self.t
        original = t.args

        def args(**changes):
            params = original(**changes)
            inner = params["inspector"]

            def inspector():
                t.h.g6_evidence["review_bytes"] = b'{"changed":"during-b7"}'
                return inner()
            params["inspector"] = inspector
            return params

        t.args = args
        with self.assertRaises(STOPS):
            t.call()
        self.assertEqual(t.factories, 0)
        self.assert_no_child()

    def test_admission_binding_must_match_before_anything_runs(self):
        t = self.t
        real = engine.bridge.BoundJournal

        class Rebound(real):
            def begin(self, *a, **k):
                lease = super().begin(*a, **k)
                self.last_binding = dict(self.last_binding,
                                         admission_sha256="sha256:" + "1" * 64)
                return lease

        class Bridge:
            BoundJournal = Rebound

        with self.assertRaisesRegex(host.Stop, "HELDFLIGHT_ADMISSION_UNBOUND"):
            t.call(bridge=Bridge)
        self.assertEqual(t.inspected, 0)
        self.assertEqual(t.factories, 0)
        self.assert_no_child()


if __name__ == "__main__":
    unittest.main()
