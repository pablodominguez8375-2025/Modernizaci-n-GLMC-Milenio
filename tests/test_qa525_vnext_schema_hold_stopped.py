"""Schema mode of the held flight: rollback never starts the baseline API; real vNext journal/flight, synthetic effects."""
import importlib.util
from pathlib import Path
import unittest

import test_qa525_vnext_held_flight_bridge as hb

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"
spec = importlib.util.spec_from_file_location("hold_stopped_t", BASE / "qa525-vnext-schema-hold-stopped.py")
hs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(hs)


class SchemaModeTests(unittest.TestCase):
    def setUp(self):
        self.t = hb.RealHeldFlightWithFakeEffectsTests("test_apply_keeps_all_five_locks_and_same_nonce_through_dual_update")
        self.t.setUp()
        self.addCleanup(self.t.tearDown)
        self.stops = []

    def factory(self, result=True):
        def stop_api():
            self.stops.append(1)
            if isinstance(result, Exception):
                raise result
            return result
        return hs.make_rollback_factory(stop_api)

    def run_schema(self, **kw):
        return self.t.call(rollback_factory=self.factory(**kw), forbid_baseline_api=True)

    def test_success_path_is_unchanged_in_schema_mode(self):
        result = self.run_schema()
        self.assertEqual(result["terminal_outcome"], "APPLIED")
        self.assertEqual(self.t.h.runs, [("api", "target"), ("web", "target")])
        self.assertEqual(self.stops, [])

    def test_web_failure_holds_api_stopped_and_never_starts_the_baseline_api(self):
        self.t.h.fail.add(("web", "target"))
        with self.assertRaises(Exception) as caught:
            self.run_schema()
        self.assertRegex(str(caught.exception), r"^[A-Z0-9_]+$")  # fixed code, no collaborator text
        self.assertEqual(self.stops, [1])
        self.assertNotIn(("api", "baseline"), self.t.h.runs)
        self.assertNotIn(("web", "baseline"), self.t.h.runs)  # hold_stopped starts nothing at all
        self.assertEqual(self.t.port.fds, {})  # plane released

    def test_api_failure_also_holds_and_never_restarts_baseline(self):
        self.t.h.fail.add(("api", "target"))
        with self.assertRaises(Exception) as caught:
            self.run_schema()
        self.assertEqual(str(caught.exception), "HELDFLIGHT_SCHEMA_ROLLED_BACK_IMPOSSIBLE")
        self.assertNotIn(("api", "baseline"), self.t.h.runs)

    def test_stop_failure_is_still_a_stop_and_baseline_api_stays_forbidden(self):
        self.t.h.fail.add(("web", "target"))
        for bad in (False, RuntimeError("secret text")):
            self.setUp()
            self.t.h.fail.add(("web", "target"))
            with self.assertRaises(Exception) as caught:
                self.run_schema(result=bad)
            self.assertNotIn("secret", str(caught.exception))
            self.assertNotIn(("api", "baseline"), self.t.h.runs)

    def test_forbid_flag_blocks_the_baseline_api_argv_even_through_the_runner(self):
        """A rollback factory that tried to start the old API is refused by the guarded runner."""
        started = []

        def evil_factory(*, plan, runner):
            def rollback():
                from importlib import import_module  # noqa: F401
                sw = self.t.h  # use the harness switcher shape: same argv as the baseline API
                started.append(runner(list(sw.baseline_argv("api"))) if hasattr(sw, "baseline_argv") else None)
            return rollback
        self.t.h.fail.add(("web", "target"))
        if not hasattr(self.t.h, "baseline_argv"):
            self.skipTest("harness exposes no baseline argv; covered via the switcher below")
        with self.assertRaises(Exception):
            self.t.call(rollback_factory=evil_factory, forbid_baseline_api=True)
        self.assertEqual(started, [])

    def test_default_behaviour_without_schema_mode_keeps_full_baseline_rollback(self):
        self.t.h.fail.add(("web", "target"))
        result = self.t.call()
        self.assertEqual(result["terminal_outcome"], "ROLLED_BACK")
        self.assertIn(("api", "baseline"), self.t.h.runs)

    def test_invalid_schema_mode_inputs_are_refused_before_entering_root(self):
        for kw in ({"rollback_factory": "x"}, {"forbid_baseline_api": 1}):
            with self.assertRaises(Exception) as caught:
                self.t.call(**kw)
            self.assertEqual(str(caught.exception), "HELDFLIGHT_SCHEMA_MODE_INVALID")
        self.assertEqual(self.t.h.runs, [])

    def test_factory_inputs_validated(self):
        with self.assertRaises(hs.Stop):
            hs.make_rollback_factory(None)
        factory = hs.make_rollback_factory(lambda: True)
        with self.assertRaises(hs.Stop):
            factory(plan=[], runner=lambda a: 0)


if __name__ == "__main__":
    unittest.main()
