"""QA525 API_WEB child over the REAL held flight: real root-locks plane (fake POSIX port), real B7/Boundary/journal/G6
anchor/flight/switch code, synthetic effects only. Proves the re-entrant plane is accepted by the real Boundary and
that failures before/after the first switch roll back under the SAME five locks without re-acquiring them."""
import hashlib
import importlib.util
import os
import types
import unittest
from pathlib import Path

import datetime as dt

import test_qa525_vnext_held_flight_bridge as held_t
from test_qa525_vnext_root_locks import Port, rl

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"
hspec = importlib.util.spec_from_file_location("qa525_t_apiweb_host_int", Path(__file__).resolve().parents[1]
                                               / "scripts" / "qa" / "qa525-vnext-root-apiweb-host.py")
apiweb_host = importlib.util.module_from_spec(hspec)
hspec.loader.exec_module(apiweb_host)
spec = importlib.util.spec_from_file_location("qa525_t_apiweb_child_int", BASE / "qa525-vnext-root-apiweb-child.py")
child = importlib.util.module_from_spec(spec)
spec.loader.exec_module(child)
DATA = b"signed-zip" * 40
CLAIMS = {"purpose": "API_WEB_QA_ONLY", "nonce": "e" * 64, "app_source_sha": "b" * 40,
          "app_archive_sha256": "sha256:" + hashlib.sha256(DATA).hexdigest(), "expires_at": 1800000300}
INPUT_KEYS = ("clock", "external_gate", "runner_factory", "scope_factory", "settle", "inspector", "reader",
              "root", "admission", "admission_sha256", "admission_bytes", "approval_bytes", "policy_hash",
              "baseline_sha256", "target", "project", "workdir", "env_file")


class ApiwebIntegrationTests(unittest.TestCase):
    def setUp(self):
        self.t = held_t.RealHeldFlightWithFakeEffectsTests(
            "test_apply_keeps_all_five_locks_and_same_nonce_through_dual_update")
        self.t.setUp()
        self.addCleanup(self.t.tearDown)
        self.port = self.t.port
        entries = []
        for path in rl.LEGACY_LOCKS:
            fd, initial = rl._lock_one(self.port, path)
            entries.append((path, fd, initial))
        self.outer = rl.RootPlane(self.port, types.SimpleNamespace(lease_is_held=lambda lease: True),
                                  object(), tuple(entries))
        self.fds = []

    def act(self, **over):
        params = self.t.args(**over)
        host = types.SimpleNamespace(build=lambda *a: {
            "g6_evidence": params["g6_evidence"], "inputs": {k: params[k] for k in INPUT_KEYS}})
        mods = dict(root_locks=rl, bridge=held_t.engine.bridge, boundary=held_t.bound, b7=held_t.b7,
                    observer=held_t.engine.observer, scopes=held_t.engine.scopes, switch=held_t.engine.switch,
                    flight=held_t.engine.flight, g6_anchor=held_t.engine.g6_anchor, run_held=held_t.host)
        return child.act_for(claims=dict(CLAIMS), modules=mods, host=host, outer_plane=lambda: self.outer,
                             port=self.port, authority={}, host_modules={}, effects=None)

    def fd(self):
        fd = os.memfd_create("z")
        os.write(fd, DATA)
        self.fds.append(fd)
        return fd

    def tearDown(self):
        for fd in self.fds:
            os.close(fd)

    def test_applied_under_the_launcher_plane_without_reacquiring_or_releasing_its_locks(self):
        self.assertEqual(len(self.port.fds), 5)
        self.assertEqual(self.act()(self.fd()), "APIWEB_APPLIED")
        self.assertEqual(len(self.port.fds), 5)  # same five fds: never re-locked, never released by the child
        self.assertEqual(self.t.h.runs, [("api", "target"), ("web", "target")])
        self.assertEqual(self.t.h.terminal(), "APPLIED")
        self.outer.assert_held()

    def test_failure_at_the_first_switch_rolls_back_and_is_not_success(self):
        self.t.h.fail.add(("api", "target"))
        with self.assertRaises(child.Stop) as err:
            self.act()(self.fd())
        self.assertEqual(str(err.exception), "APIWEB_ROLLED_BACK")
        self.assertEqual(self.t.h.world, {"api": "baseline", "web": "baseline"})
        self.assertEqual(len(self.port.fds), 5)

    def test_failure_after_the_first_switch_rolls_back_both_services(self):
        self.t.h.fail.add(("web", "target"))
        with self.assertRaises(child.Stop) as err:
            self.act()(self.fd())
        self.assertEqual(str(err.exception), "APIWEB_ROLLED_BACK")
        self.assertEqual(self.t.h.world, {"api": "baseline", "web": "baseline"})
        self.assertEqual(self.t.h.runs, [("api", "target"), ("web", "target"), ("web", "baseline"), ("api", "baseline")])

    def test_denied_gate_never_runs_a_child_process(self):
        with self.assertRaises(child.Stop):
            self.act(external_gate=lambda stage: False)(self.fd())
        self.assertEqual(self.t.h.runs, [])

    def expiring_gate(self, expire_when, clock_fault_after=None):
        """REAL host gate over a clock that is valid until `expire_when()` is true (e.g. after the first switch)."""
        state = {"t": 1800000000}

        def clock():
            if clock_fault_after is not None and clock_fault_after():
                raise RuntimeError("clock")
            if expire_when():
                state["t"] = 1800000301
            return dt.datetime.fromtimestamp(state["t"], dt.timezone.utc)
        return apiweb_host.make_gate(clock, CLAIMS["expires_at"])

    def test_go_expiring_after_the_first_switch_forbids_web_forward_but_rolls_back_to_baseline(self):
        h = self.t.h
        gate = self.expiring_gate(lambda: ("api", "target") in h.runs)
        with self.assertRaises(child.Stop) as err:
            self.act(external_gate=gate)(self.fd())
        self.assertEqual(str(err.exception), "APIWEB_ROLLED_BACK")
        self.assertNotIn(("web", "target"), h.runs)  # no new forward mutation after expiry
        self.assertEqual(h.runs, [("api", "target"), ("web", "baseline"), ("api", "baseline")])  # dual rollback, no forward web
        self.assertEqual(h.world, {"api": "baseline", "web": "baseline"})
        self.assertEqual(len(self.port.fds), 5)

    def test_expiry_plus_induced_failure_still_restores_both_services(self):
        h = self.t.h
        h.fail.add(("web", "target"))
        gate = self.expiring_gate(lambda: ("api", "target") in h.runs)
        with self.assertRaises(child.Stop) as err:
            self.act(external_gate=gate)(self.fd())
        self.assertEqual(str(err.exception), "APIWEB_ROLLED_BACK")
        self.assertEqual(h.world, {"api": "baseline", "web": "baseline"})

    def test_clock_fault_during_recovery_does_not_block_the_rollback(self):
        h = self.t.h
        h.fail.add(("web", "target"))
        gate = self.expiring_gate(lambda: False, clock_fault_after=lambda: ("web", "target") in h.runs)
        with self.assertRaises(child.Stop) as err:
            self.act(external_gate=gate)(self.fd())
        self.assertEqual(str(err.exception), "APIWEB_ROLLED_BACK")
        self.assertEqual(h.world, {"api": "baseline", "web": "baseline"})

    def test_a_lost_outer_lock_stops_before_any_lease_or_effect(self):
        path, fd, _ = self.outer._entries[2]
        self.port.fds.pop(fd, None)
        with self.assertRaises(Exception):
            self.act()(self.fd())
        self.assertEqual(self.t.h.runs, [])


if __name__ == "__main__":
    unittest.main()
