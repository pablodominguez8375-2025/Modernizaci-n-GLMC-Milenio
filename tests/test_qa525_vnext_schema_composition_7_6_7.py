"""Composition 7->6->7: REAL run_held + REAL StateReader(SCHEMA_POSTQUIESCE) + real plane/lease/journal; synthetic effects only.

S6q (old API stopped by the cut, web old, deps/oneshots as H0) -> API target -> WEB target => APPLIED.
Post-schema failures end as a stop (never ROLLED_BACK), the BASELINE api/web are never started, and NORMAL is unchanged.
"""
import copy
import importlib.util
from pathlib import Path
import unittest

import test_qa525_vnext_held_flight_bridge as hb

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"
_s = importlib.util.spec_from_file_location("hold_comp", BASE / "qa525-vnext-schema-hold-stopped.py")
hs = importlib.util.module_from_spec(_s)
_s.loader.exec_module(hs)
SR = hb.engine.observer.verify  # ensure modules loaded
_r = importlib.util.spec_from_file_location("sr_comp", BASE / "qa525-vnext-state-reader.py")
sr = importlib.util.module_from_spec(_r)
_r.loader.exec_module(sr)

P = "SCHEMA_POSTQUIESCE"
API_OLD, API_NEW = "1" * 64, "e" * 64


class World:
    """Rows provider derived from the engine fixture's H0 so the pinned baseline hash is unchanged."""

    def __init__(self, h, *, s6q=True):
        self.h, self.api_stopped = h, s6q
        self.stop_calls = 0

    def _row(self, name, cid, image, state, layers, code=0):
        return [cid, image, state, "2026-11-05T00:00:00Z", 0, name, "/w", ",".join(layers), [], {}, {}, code]

    def rows(self):
        base, target, world = self.h.base, self.h.target, self.h.world
        out = {}
        api_new = world["api"] == "target"
        src = target if api_new else base
        out["api"] = self._row("api", API_NEW if api_new else API_OLD, src["images"]["api"],
                               "exited" if (self.api_stopped and not api_new) else "running", src["layers"]["api"])
        wsrc = target if world["web"] == "target" else base
        out["web"] = self._row("web", "2" * 64, wsrc["images"]["web"], "running", wsrc["layers"]["web"])
        for n, cid in base["dependencies"].items():
            out[n] = self._row(n, cid, "sha256:" + "0" * 64, "running", ["/w/x.json"])
        for n, v in base["oneshots"].items():
            out[n] = self._row(n, v["id"], "sha256:" + "0" * 64, "exited", ["/w/x.json"], v["exit_code"])
        return copy.deepcopy(out)

    def health(self):
        t = self.h.world["web"] == "target"
        return {"ok": True, "web_sha": (self.h.target if t else self.h.base)["web_sha"]}

    def reader(self, profile):
        kw = {"profile": P, "api_container_id": API_OLD} if profile == P else {}
        return sr.StateReader(self.rows, lambda: self.h.base["web_sha"], self.health, **kw)

    def stop_api(self):
        self.stop_calls += 1
        self.api_stopped = True
        self.h.world["api"] = "baseline"  # nothing is started; the new API is just stopped
        return True


def schema_shape(pin):
    s = hb.shape(pin)
    s["status"], s["running_services_expected"] = hb.b7.SCHEMA_SHAPE_STATUS, 6
    return s


class Composition(unittest.TestCase):
    def setUp(self):
        self.t = hb.RealHeldFlightWithFakeEffectsTests("test_apply_keeps_all_five_locks_and_same_nonce_through_dual_update")
        self.t.setUp()
        self.addCleanup(self.t.tearDown)
        self.w = World(self.t.h)

    def schema_call(self, **kw):
        pin = self.t.pin
        args = dict(reader=self.w.reader(P), b7_profile=P, forbid_baseline_api=True,
                    rollback_factory=hs.make_rollback_factory(self.w.stop_api),
                    inspector=lambda: schema_shape(pin))
        args.update(kw)
        return self.t.call(**args)

    def test_exact_s6q_reaches_applied_without_any_baseline_start(self):
        r = self.schema_call()
        self.assertEqual(r["terminal_outcome"], "APPLIED")
        self.assertEqual(self.t.h.runs, [("api", "target"), ("web", "target")])
        self.assertEqual(self.w.stop_calls, 0)
        self.assertEqual(self.t.port.fds, {})
        self.assertEqual(self.t.h.terminal(), "APPLIED")

    def test_web_failure_ends_in_a_stop_with_api_held_stopped_and_no_baseline_start(self):
        self.t.h.fail.add(("web", "target"))
        with self.assertRaises(Exception):
            self.schema_call()
        self.assertEqual(self.w.stop_calls, 1)
        self.assertNotIn(("api", "baseline"), self.t.h.runs)
        self.assertNotIn(("web", "baseline"), self.t.h.runs)
        self.assertNotEqual(self.t.h.terminal(), "APPLIED")
        self.assertNotEqual(self.t.h.terminal(), "ROLLED_BACK")
        self.assertEqual(self.t.port.fds, {})

    def test_api_switch_failure_never_reports_rolled_back_nor_starts_baseline(self):
        self.t.h.fail.add(("api", "target"))
        with self.assertRaises(Exception):
            self.schema_call()
        self.assertEqual(self.t.h.runs, [("api", "target")])
        self.assertNotEqual(self.t.h.terminal(), "ROLLED_BACK")

    def test_stop_failure_is_a_stop_not_a_rollback(self):
        self.t.h.fail.add(("web", "target"))
        with self.assertRaises(Exception):
            self.schema_call(rollback_factory=hs.make_rollback_factory(lambda: False))
        self.assertNotIn(("api", "baseline"), self.t.h.runs)

    def test_non_s6q_world_is_refused_before_any_effect(self):
        for tweak in ("api_running", "web_stopped"):
            self.setUp()
            self.w.api_stopped = tweak != "api_running"
            reader = self.w.reader(P)
            if tweak == "web_stopped":
                orig = self.w.rows
                self.w.rows = lambda: dict(orig(), web=self.w._row("web", "2" * 64, self.t.h.base["images"]["web"],
                                                                   "exited", self.t.h.base["layers"]["web"]))
                reader = self.w.reader(P)
            with self.assertRaises(Exception):
                self.schema_call(reader=reader)
            self.assertEqual(self.t.h.runs, [])

    def test_wrong_pinned_api_id_is_refused(self):
        bad = sr.StateReader(self.w.rows, lambda: self.t.h.base["web_sha"], self.w.health,
                             profile=P, api_container_id="9" * 64)
        with self.assertRaises(Exception):
            self.schema_call(reader=bad)
        self.assertEqual(self.t.h.runs, [])

    def test_schema_profile_requires_the_hold_hooks(self):
        for kw in ({"rollback_factory": None}, {"forbid_baseline_api": False}):
            with self.assertRaises(Exception) as c:
                self.schema_call(**kw)
            self.assertEqual(str(c.exception), "HELDFLIGHT_SCHEMA_MODE_INVALID")
        self.assertEqual(self.t.h.runs, [])

    def test_normal_profile_still_applies_and_rolls_back_to_baseline(self):
        self.w.api_stopped = False
        r = self.t.call(reader=self.w.reader("NORMAL"))
        self.assertEqual(r["terminal_outcome"], "APPLIED")
        self.setUp()
        self.w.api_stopped = False
        self.t.h.fail.add(("web", "target"))
        r = self.t.call(reader=self.w.reader("NORMAL"))
        self.assertEqual(r["terminal_outcome"], "ROLLED_BACK")
        self.assertIn(("api", "baseline"), self.t.h.runs)


if __name__ == "__main__":
    unittest.main()
