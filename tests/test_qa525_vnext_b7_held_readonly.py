"""QA525 B7 held-read-only preflight: adversarial tests, no host effects."""
import ast
import copy
import importlib.util
from pathlib import Path
import unittest

from test_qa525_vnext_root_locks import Port, Journal, rl, fstat_

HERE = Path(__file__).resolve().parents[1]
SOURCE = HERE / "scripts/qa/qa525-vnext-b7-held-readonly.py"
spec = importlib.util.spec_from_file_location("qa525_b7_held_readonly_test", SOURCE)
b7 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(b7)
PIN = "sha256:" + "a" * 64
OBS = "sha256:" + "b" * 64


def shape(**changes):
    value = {
        "status": b7.SHAPE_STATUS,
        "baseline_sha256": PIN, "observation_sha256": OBS,
        "running_services_expected": 7, "exited_zero_oneshots_verified": 3,
        "api_compose_layers": 6, "web_compose_layers": 5, "samples": 3,
        "sourced_from_authenticated_host": False, "sponsor_approved_here": False,
        "g6_verified_here": False, "may_apply": False, "may_install": False,
    }
    value.update(changes)
    return value


class B7HeldReadonlyContractTests(unittest.TestCase):
    def setUp(self):
        self.port = Port()
        self.log = []
        self.journal = Journal(self.log)
        self.inspections = []

    def run_b7(self, inspector=None, **overrides):
        fn = inspector or (lambda: shape())
        data = {
            "port": self.port, "journal": self.journal,
            "root": "root", "admission": "approved-record",
            "admission_sha256": "sha256:" + "f" * 64,
            "now": "2026-11-07T14:54:00Z",
            "inspector": fn, "expected_baseline_sha256": PIN,
        }
        data.update(overrides)
        return b7.held_readonly(**data)

    def stop(self, code, **overrides):
        with self.assertRaises(b7.Stop) as err:
            self.run_b7(**overrides)
        self.assertEqual(str(err.exception), code)
        self.assertIsNone(err.exception.__cause__)

    def test_success_inside_five_locks_and_lease_then_release(self):
        def inspector():
            self.inspections.append(1)
            self.assertEqual([path for verb, path in self.port.calls if verb == "flock"],
                             list(rl.LEGACY_LOCKS))
            self.assertEqual(len(self.port.fds), 5)
            self.assertEqual(self.log, ["begin"])
            return shape()
        result = self.run_b7(inspector)
        self.assertEqual(self.inspections, [1])
        self.assertEqual(result["status"], b7.STATUS)
        self.assertEqual(result["baseline_sha256"], PIN)
        self.assertEqual(result["observation_sha256"], OBS)
        self.assertIs(result["may_apply"], False)
        self.assertIs(result["may_install"], False)
        for field in ("signature_verified_here", "host_identity_authenticated_here",
                      "pin_authenticated_here", "g6_verified_here",
                      "sponsor_approved_here"):
            self.assertIs(result[field], False)
        self.assertEqual(self.log, ["begin", "lease-close"])
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS)))
        self.assertEqual(self.port.fds, {})

    def test_inside_held_plane_allows_safe_continuation_before_unlock(self):
        """The caller, not B7, owns the lock/lease lifetime."""
        events = []

        def inspect():
            events.append("inspect")
            self.assertEqual(len(self.port.fds), 5)
            self.assertEqual(self.log, ["begin"])
            return shape()

        with b7.root_locks.root_flight(
                self.port, self.journal, "root", "approved-record",
                "sha256:" + "f" * 64, "2026-11-07T14:54:00Z") as plane:
            result = b7.inspect_inside_held_plane(
                plane=plane, inspector=inspect,
                expected_baseline_sha256=PIN)
            # A future signed entrypoint could now continue in THIS context,
            # but this synthetic test deliberately performs NO APPLY action.
            plane.assert_held()
            events.append("synthetic-continuation-without-apply")
            self.assertEqual(len(self.port.fds), 5)
            self.assertEqual(self.log, ["begin"])
            self.assertIs(result["may_apply"], False)
            self.assertIs(result["may_install"], False)
            self.assertIs(result["signature_verified_here"], False)
            self.assertIs(result["host_identity_authenticated_here"], False)
            self.assertIs(result["pin_authenticated_here"], False)
        self.assertEqual(events, ["inspect", "synthetic-continuation-without-apply"])
        self.assertEqual(self.log, ["begin", "lease-close"])
        self.assertEqual(self.port.fds, {})
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS)))

    def test_inside_held_plane_rejects_expired_plane_before_inspector(self):
        with b7.root_locks.root_flight(
                self.port, self.journal, "root", "approved-record",
                "sha256:" + "f" * 64, "2026-11-07T14:54:00Z") as plane:
            plane.assert_held()
        with self.assertRaises(b7.root_locks.Stop) as err:
            b7.inspect_inside_held_plane(
                plane=plane, inspector=lambda: self.fail("never inspect"),
                expected_baseline_sha256=PIN)
        self.assertEqual(str(err.exception), "ROOTLOCK_PLANE_CLOSED")
        self.assertEqual(self.log, ["begin", "lease-close"])

    def test_inside_held_plane_detects_inode_change_before_return(self):
        with b7.root_locks.root_flight(
                self.port, self.journal, "root", "approved-record",
                "sha256:" + "f" * 64, "2026-11-07T14:54:00Z") as plane:
            def tamper():
                self.port.nodes[rl.LEGACY_LOCKS[0]] = fstat_(999)
                return shape()
            with self.assertRaises(b7.root_locks.Stop):
                b7.inspect_inside_held_plane(
                    plane=plane, inspector=tamper,
                    expected_baseline_sha256=PIN)
            self.assertEqual(len(self.port.fds), 5)
        self.assertEqual(self.log, ["begin", "lease-close"])
        self.assertEqual(self.port.fds, {})

    def test_inside_held_plane_rejects_lease_loss_mid_inspection(self):
        with b7.root_locks.root_flight(
                self.port, self.journal, "root", "approved-record",
                "sha256:" + "f" * 64, "2026-11-07T14:54:00Z") as plane:
            def lose_lease():
                plane.lease.held = False
                return shape()
            with self.assertRaises(b7.root_locks.Stop) as err:
                b7.inspect_inside_held_plane(
                    plane=plane, inspector=lose_lease,
                    expected_baseline_sha256=PIN)
            self.assertEqual(str(err.exception), "ROOTLOCK_LEASE_LOST")
            self.assertEqual(len(self.port.fds), 5)
        self.assertEqual(self.log, ["begin", "lease-close"])
        self.assertEqual(self.port.fds, {})

    def test_inside_held_plane_inspector_failure_preserves_caller_lifetime(self):
        with b7.root_locks.root_flight(
                self.port, self.journal, "root", "approved-record",
                "sha256:" + "f" * 64, "2026-11-07T14:54:00Z") as plane:
            with self.assertRaises(b7.Stop) as err:
                b7.inspect_inside_held_plane(
                    plane=plane,
                    inspector=lambda: (_ for _ in ()).throw(
                        RuntimeError("private-secret")),
                    expected_baseline_sha256=PIN)
            self.assertEqual(str(err.exception), "B7_INSPECTION_FAILED")
            self.assertIsNone(err.exception.__cause__)
            plane.assert_held()
            self.assertEqual(len(self.port.fds), 5)
        self.assertEqual(self.log, ["begin", "lease-close"])

    def test_accepts_new_reviewed_seven_six_layer_shape(self):
        result = self.run_b7(lambda: shape(api_compose_layers=7, web_compose_layers=6))
        self.assertEqual(result["root_locks_held_during_inspection"], 5)
        self.assertEqual(result["journal_lease_held_during_inspection"], True)
        self.assertNotIn("api_compose_layers", result)  # no physical details leak

    def test_busy_lock_never_inspects_or_acquires_lease(self):
        self.port.busy.add(rl.LEGACY_LOCKS[2])
        self.stop("B7_ROOT_PLANE_DENIED",
                  inspector=lambda: self.fail("must not inspect under busy lock"))
        self.assertEqual(self.log, [])
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS[:3])))

    def test_not_root_never_runs_inspector(self):
        self.port.euid_value = 1001
        self.stop("B7_ROOT_PLANE_DENIED",
                  inspector=lambda: self.fail("no privileged boundary"))
        self.assertEqual(self.log, [])

    def test_journal_denied_never_inspects_and_releases_locks(self):
        self.journal = Journal(self.log, fail=True)
        self.stop("B7_ROOT_PLANE_DENIED",
                  inspector=lambda: self.fail("no lease"))
        self.assertEqual(self.log, ["begin"])
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS)))

    def test_changed_lock_inode_during_inspection_is_denied(self):
        def tamper():
            self.port.nodes[rl.LEGACY_LOCKS[0]] = fstat_(999)
            return shape()
        self.stop("B7_ROOT_PLANE_DENIED", inspector=tamper)
        self.assertEqual(self.log, ["begin", "lease-close"])
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS)))

    def test_inspector_exception_leaks_no_private_message(self):
        def boom():
            raise RuntimeError("secret host credential")
        self.stop("B7_INSPECTION_FAILED", inspector=boom)
        self.assertEqual(self.log, ["begin", "lease-close"])

    def test_invalid_baseline_pin_prevents_any_lock(self):
        for bad in (None, "sha256:xyz", "a" * 64, "sha256:" + "A" * 64,
                    "sha256:" + "a" * 63, b"sha256:" + b"a" * 64):
            with self.subTest(bad=bad):
                self.stop("B7_INPUT_INVALID", expected_baseline_sha256=bad)
                self.assertEqual(self.port.calls, [])

    def test_wrong_baseline_hash_denied(self):
        self.stop("B7_SHAPE_MISMATCH",
                  inspector=lambda: shape(baseline_sha256="sha256:" + "c" * 64))
        self.assertEqual(self.log, ["begin", "lease-close"])

    def test_never_accepts_truthy_flags_or_fake_authority(self):
        for field in ("may_apply", "may_install", "g6_verified_here",
                      "sponsor_approved_here", "sourced_from_authenticated_host"):
            for value in (True, 1, "false"):
                with self.subTest(field=field, value=value):
                    self.stop("B7_SHAPE_MISMATCH",
                              inspector=lambda f=field, v=value: shape(**{f: v}))

    def test_missing_extra_or_malformed_shape_denied(self):
        for report in (None, {}, {"status": b7.SHAPE_STATUS},
                       dict(shape(), forged=True),
                       dict((k, v) for k, v in shape().items() if k != "may_apply"),
                       shape(running_services_expected=True),
                       shape(exited_zero_oneshots_verified=2),
                       shape(api_compose_layers=5),
                       shape(web_compose_layers=4),
                       shape(samples=1),
                       shape(observation_sha256="sha256:" + "X" * 64),
                       shape(status="READ_ONLY_BUT_TRUST_ME")):
            with self.subTest(report=repr(report)[:70]):
                self.stop("B7_SHAPE_FIELDS_INVALID" if type(report) is not dict or
                          set(report) != b7.SHAPE_FIELDS else "B7_SHAPE_MISMATCH",
                          inspector=lambda r=report: r)

    def test_cleanup_failure_overrides_result_with_stop(self):
        self.port.close_fail.add(rl.LEGACY_LOCKS[0])
        self.stop("B7_ROOT_PLANE_DENIED")
        self.assertEqual(self.log, ["begin", "lease-close"])

    def test_never_imports_process_network_or_os_host_api(self):
        tree = ast.parse(SOURCE.read_text())
        imports = {alias.name for item in ast.walk(tree)
                   if isinstance(item, ast.Import) for alias in item.names}
        self.assertEqual(imports, {"hashlib", "hmac", "importlib.util", "re"})
        self.assertNotIn("subprocess", SOURCE.read_text())
        self.assertNotIn("os.system(", SOURCE.read_text())


if __name__ == "__main__":
    unittest.main()
