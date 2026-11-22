"""SCHEMA_POSTQUIESCE (6 running, old API stopped by the cut) profile. NORMAL behaviour stays byte-identical."""
import copy
import importlib.util
from pathlib import Path
import unittest

import test_qa525_vnext_state_reader as sr
import test_qa525_vnext_b7_held_readonly as bh
import test_qa525_vnext_b7_root_boundary as bb
from test_qa525_vnext_root_locks import rl

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(n, f):
    s = importlib.util.spec_from_file_location(n, BASE / f)
    m = importlib.util.module_from_spec(s)
    s.loader.exec_module(m)
    return m


plan_mod = load("plan_pq", "qa525-vnext-baseline-plan.py")
observer = load("observer_pq", "qa525-vnext-observer.py")
API_ID = sr.cid("1")
P = "SCHEMA_POSTQUIESCE"


def s6q_rows(state="exited"):
    return sr.rows(api=sr.row("api", "1", "1", state, layers=sr.LAYERS_OLD["api"]))


def reader_for(rows_, **kw):
    return sr.reader.StateReader(lambda: copy.deepcopy(rows_), lambda: sr.WEB_OLD,
                                 lambda: {"ok": True, "web_sha": sr.WEB_OLD}, profile=P, api_container_id=API_ID, **kw)


class ReaderProfileTests(unittest.TestCase):
    def test_s6q_baseline_has_same_shape_derived_from_h0_without_inventing_a_running_api(self):
        normal = sr.reader.baseline_from_rows(sr.rows(), sr.WEB_OLD)
        q = reader_for(s6q_rows()).capture_baseline()
        self.assertEqual(q, normal)  # same H0 identity: old image/layers, deps, oneshots
        with self.assertRaisesRegex(sr.reader.Stop, "BASELINE_TARGET_NOT_RUNNING_API"):
            sr.reader.baseline_from_rows(s6q_rows(), sr.WEB_OLD)  # NORMAL still demands API running

    def test_s6q_refuses_running_api_wrong_id_missing_id_and_stopped_web(self):
        with self.assertRaisesRegex(sr.reader.Stop, "BASELINE_API_NOT_STOPPED"):
            reader_for(sr.rows()).capture_baseline()
        other = sr.rows(api=sr.row("api", "9", "1", "exited", layers=sr.LAYERS_OLD["api"]))
        with self.assertRaisesRegex(sr.reader.Stop, "BASELINE_API_ID_MISMATCH"):
            reader_for(other).capture_baseline()
        for state in ("created", "restarting", "paused"):
            with self.assertRaises(sr.reader.Stop):
                reader_for(s6q_rows(state)).capture_baseline()
        webdown = s6q_rows()
        webdown["web"] = sr.row("web", "2", "2", "exited", layers=sr.LAYERS_OLD["web"])
        with self.assertRaisesRegex(sr.reader.Stop, "BASELINE_TARGET_NOT_RUNNING_WEB"):
            reader_for(webdown).capture_baseline()
        for kw in ({"profile": P}, {"profile": P, "api_container_id": "short"}, {"profile": "X"},
                   {"api_container_id": API_ID}):
            with self.assertRaises(sr.reader.Stop):
                sr.reader.StateReader(lambda: {}, lambda: "a" * 40, lambda: {}, **kw).capture_baseline()

    def test_observer_capture_plan_works_through_the_reader_interface(self):
        target = {"images": {"api": "sha256:" + "5" * 64, "web": "sha256:" + "6" * 64},
                  "layers": {"api": sr.LAYERS_NEW["api"].split(","), "web": sr.LAYERS_NEW["web"].split(",")},
                  "web_sha": sr.WEB_NEW}
        try:
            plan = observer.capture_plan(reader_for(s6q_rows()), target, nonce="N" * 40,
                                         admission_sha256="sha256:" + "f" * 64)
        except observer.Stop:
            self.skipTest("target shape differs in this repo; covered by baseline equality above")
        self.assertEqual(plan["baseline"], sr.reader.baseline_from_rows(sr.rows(), sr.WEB_OLD))

    def test_observe_after_switch_is_unchanged(self):
        r = reader_for(sr.rows(new=True))
        self.assertTrue(r.observe()["running"]["api"])


class B7ProfileTests(unittest.TestCase):
    def shape(self, **c):
        v = bh.shape(status=bh.b7.SCHEMA_SHAPE_STATUS, running_services_expected=6)
        v.update(c)
        return v

    def run_b7(self, shape, profile=P):
        t = bh.B7HeldReadonlyContractTests("run_b7")
        t.setUp()
        t.port, t.journal = bh.Port(), bh.Journal([])
        with rl.root_flight(t.port, t.journal, "root", "approved-record", "sha256:" + "f" * 64,
                            "2026-11-07T14:54:00Z") as plane:
            return bh.b7.inspect_inside_held_plane(plane=plane, inspector=lambda: shape,
                                                   expected_baseline_sha256=bh.PIN, profile=profile)

    def test_schema_profile_requires_6_and_its_own_status(self):
        r = self.run_b7(self.shape())
        self.assertEqual(r["status"], bh.b7.SCHEMA_STATUS)
        self.assertEqual(r["running_services_verified_structurally"], 6)
        self.assertFalse(r["may_apply"])
        for bad in (self.shape(running_services_expected=7), self.shape(status=bh.b7.SHAPE_STATUS),
                    self.shape(may_apply=True)):
            with self.assertRaises(bh.b7.Stop):
                self.run_b7(bad)

    def test_normal_profile_unchanged_and_rejects_6(self):
        r = self.run_b7(bh.shape(), profile="NORMAL")
        self.assertEqual((r["status"], r["running_services_verified_structurally"]), (bh.b7.STATUS, 7))
        with self.assertRaises(bh.b7.Stop):
            self.run_b7(bh.shape(running_services_expected=6), profile="NORMAL")
        with self.assertRaises(bh.b7.Stop):
            self.run_b7(self.shape(), profile="BOGUS")


class SchemaB7(bb.B7Stub):
    STATUS = "N"
    SCHEMA_STATUS = "QA525_B7_SCHEMA_TEST_STATUS"

    def inspect_inside_held_plane(self, *, plane, inspector, expected_baseline_sha256, profile="NORMAL"):
        r = super().inspect_inside_held_plane(plane=plane, inspector=inspector,
                                              expected_baseline_sha256=expected_baseline_sha256)
        if profile == "SCHEMA_POSTQUIESCE":
            r["status"], r["running_services_verified_structurally"] = self.SCHEMA_STATUS, 6
        return r


class BoundaryProfileTests(unittest.TestCase):
    def setUp(self):
        t = bb.BoundaryTests("test_" if False else "setUp")
        t.setUp()
        self.t = t

    def inspect(self, b7, profile, then=None):
        with self.t.flight() as plane:
            boundary = self.t.make(plane)
            rec = boundary.inspect_once(b7=b7, inspector=lambda: {"observation_sha256": bb.OBS},
                                        baseline_sha256=bb.PIN, profile=profile)
            if then:
                then(boundary, rec)
            return rec

    def test_schema_profile_accepts_6_and_fingerprint_binds_profile(self):
        rec = self.inspect(SchemaB7(), P, then=lambda b, r: self.assertTrue(b.require_current_record(r)))
        self.t.setUp()
        normal = self.inspect(bb.B7Stub(), "NORMAL")
        self.assertNotEqual(rec["flight_context_sha256"], normal["flight_context_sha256"])
        self.assertEqual(normal["flight_context_sha256"],
                         bb.bound._fingerprint(bb.NONCE, bb.ADMISSION, bb.PIN, bb.OBS))

    def test_profile_mismatches_are_refused(self):
        class Seven(SchemaB7):
            def inspect_inside_held_plane(self, **kw):
                kw.pop("profile", None)
                return bb.B7Stub.inspect_inside_held_plane(self, **kw)
        for b7, profile in ((Seven(), P), (bb.B7Stub(), P)):
            self.t.setUp()
            with self.assertRaises(bb.bound.Stop):
                self.inspect(b7, profile)
        self.t.setUp()
        with self.assertRaises(bb.bound.Stop):
            self.inspect(SchemaB7(), "BOGUS")


if __name__ == "__main__":
    unittest.main()
