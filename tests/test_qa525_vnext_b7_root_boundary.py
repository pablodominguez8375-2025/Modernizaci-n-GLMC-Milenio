"""QA525 B7 root-boundary: hermetic cases, ZERO physical effects."""
import ast
import importlib.util
from pathlib import Path
import unittest

from test_qa525_vnext_root_locks import Port, Journal, rl, fstat_

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"
SPEC = importlib.util.spec_from_file_location("qa525_b7_root_boundary", BASE / "qa525-vnext-b7-root-boundary.py")
bound = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(bound)

PIN = "sha256:" + "a" * 64
OBS = "sha256:" + "b" * 64
ADMISSION = "sha256:" + "f" * 64
NONCE = "N" * 40


class B7Stub:
    """Strictly read-only stand-in; the #913 integration test uses the real B7."""
    STATUS = "QA525_B7_HELD_READONLY_TEST_STATUS"

    def inspect_inside_held_plane(self, *, plane, inspector, expected_baseline_sha256):
        plane.assert_held()
        result = inspector()
        plane.assert_held()
        return {
            "status": self.STATUS,
            "baseline_sha256": expected_baseline_sha256,
            "observation_sha256": result["observation_sha256"],
            "running_services_verified_structurally": 7,
            "oneshot_exits_verified_structurally": 3,
            "root_locks_held_during_inspection": 5,
            "journal_lease_held_during_inspection": True,
            "may_apply": False, "may_install": False,
            "signature_verified_here": False,
            "host_identity_authenticated_here": False,
            "pin_authenticated_here": False,
            "g6_verified_here": False,
            "sponsor_approved_here": False,
        }


class MaliciousB7(B7Stub):
    def __init__(self, change):
        self.change = change

    def inspect_inside_held_plane(self, **kwargs):
        result = super().inspect_inside_held_plane(**kwargs)
        result.update(self.change)
        return result


class NonceJournal(Journal):
    def begin(self, *args):
        lease = super().begin(*args)
        lease.nonce = NONCE
        return lease


class BoundaryTests(unittest.TestCase):
    def setUp(self):
        self.port, self.log = Port(), []
        self.journal = NonceJournal(self.log)

    def flight(self):
        return rl.root_flight(self.port, self.journal,
                              "root", "approved", ADMISSION,
                              "2026-11-07T12:00:00Z")

    def make(self, plane, **kwargs):
        return bound.Boundary(root_locks=rl, plane=plane,
                              nonce=kwargs.get("nonce", NONCE),
                              admission_sha256=kwargs.get("admission_sha256", ADMISSION))

    def inspect(self, boundary, *, b7=None, inspector=None):
        return boundary.inspect_once(
            b7=b7 or B7Stub(), inspector=inspector or
            (lambda: {"observation_sha256": OBS}), baseline_sha256=PIN)

    def code(self, action):
        with self.assertRaises(bound.Stop) as err:
            action()
        self.assertIsNone(err.exception.__cause__)
        return str(err.exception)

    def test_single_held_plane_bound_report_has_no_authority(self):
        with self.flight() as plane:
            b = self.make(plane)
            report = self.inspect(b)
            self.assertTrue(b.require_current_record(report))
            self.assertTrue(report["flight_context_sha256"].startswith("sha256:"))
            for field in ("may_apply", "may_install", "authentication_verified_here",
                          "g6_verified_here", "sponsor_approved_here"):
                self.assertIs(report[field], False)
            self.assertEqual(len(self.port.fds), 5)
            self.assertEqual(self.log, ["begin"])
        self.assertEqual(self.log, ["begin", "lease-close"])
        self.assertEqual(self.port.fds, {})
        self.assertEqual(self.code(lambda: b.require_current_record(report)),
                         "ROOTBOUNDARY_PLANE_LOST")

    def test_duck_typed_and_synthetic_planes_cannot_claim_root(self):
        class Forged:
            def assert_held(self):
                return {"status": rl.STATUS, "legacy_locks_held": 5,
                        "root_lease_held": True}
        for fake in (Forged(), object(), None):
            self.assertEqual(self.code(lambda f=fake: self.make(f)),
                             "ROOTBOUNDARY_NOT_REAL_PLANE")
        with self.flight() as plane:
            class Derived(type(plane)):
                pass
            forged = Derived(plane.port, plane.journal, plane.lease, plane._entries)
            self.assertEqual(self.code(lambda: self.make(forged)),
                             "ROOTBOUNDARY_NOT_REAL_PLANE")

    def test_module_instance_mismatch_is_refused(self):
        new_spec = importlib.util.spec_from_file_location(
            "alternate_root_locks", BASE / "qa525-vnext-root-locks.py")
        other = importlib.util.module_from_spec(new_spec)
        new_spec.loader.exec_module(other)
        with self.flight() as plane:
            self.assertEqual(self.code(lambda: bound.Boundary(
                root_locks=other, plane=plane, nonce=NONCE,
                admission_sha256=ADMISSION)), "ROOTBOUNDARY_NOT_REAL_PLANE")

    def test_nonce_admission_and_digest_validation_before_accepting(self):
        with self.flight() as plane:
            for nonce in (None, "", "short", "*" * 40, " " * 40, 7):
                self.assertEqual(self.code(lambda n=nonce: self.make(plane, nonce=n)),
                                 "ROOTBOUNDARY_INPUT_INVALID")
            for digest in ("f" * 64, "sha256:" + "A" * 64, "sha256:x", 7, None):
                self.assertEqual(self.code(lambda d=digest: self.make(
                    plane, admission_sha256=d)), "ROOTBOUNDARY_INPUT_INVALID")
            self.assertEqual(self.code(lambda: self.make(plane, nonce="Y" * 40)),
                             "ROOTBOUNDARY_LEASE_NONCE_MISMATCH")
            a = self.make(plane)
            c = self.make(plane, admission_sha256="sha256:" + "e" * 64)
            ar, cr = self.inspect(a), self.inspect(c)
            self.assertNotEqual(ar["flight_context_sha256"], cr["flight_context_sha256"])
            self.assertEqual(self.code(lambda: c.require_current_record(ar)),
                             "ROOTBOUNDARY_RECORD_STALE")

    def test_record_copy_mutation_and_double_inspection_fail(self):
        with self.flight() as plane:
            b = self.make(plane)
            r = self.inspect(b)
            self.assertEqual(self.code(lambda: b.require_current_record(dict(r))),
                             "ROOTBOUNDARY_RECORD_STALE")
            saved = r["observation_sha256"]
            r["observation_sha256"] = "sha256:" + "c" * 64
            self.assertEqual(self.code(lambda: b.require_current_record(r)),
                             "ROOTBOUNDARY_RECORD_STALE")
            r["observation_sha256"] = saved
            self.assertTrue(b.require_current_record(r))
            self.assertEqual(self.code(lambda: self.inspect(b)),
                             "ROOTBOUNDARY_ALREADY_INSPECTED")

    def test_inode_or_lease_lost_mid_inspection_fails_without_record(self):
        with self.flight() as plane:
            b = self.make(plane)
            def replace():
                self.port.nodes[rl.LEGACY_LOCKS[1]] = fstat_(909)
                return {"observation_sha256": OBS}
            self.assertEqual(self.code(lambda: self.inspect(b, inspector=replace)),
                             "ROOTBOUNDARY_INSPECTION_STOP")
            self.assertIsNone(b._last_record)
        self.setUp()
        with self.flight() as plane:
            b = self.make(plane)
            def lose_lease():
                plane.lease.held = False
                return {"observation_sha256": OBS}
            self.assertEqual(self.code(lambda: self.inspect(b, inspector=lose_lease)),
                             "ROOTBOUNDARY_INSPECTION_STOP")
            self.assertIsNone(b._last_record)

    def test_lease_nonce_changed_during_inspection_is_refused(self):
        with self.flight() as plane:
            b = self.make(plane)
            def tamper_nonce():
                plane.lease.nonce = "Y" * 40
                return {"observation_sha256": OBS}
            self.assertEqual(self.code(lambda: self.inspect(b, inspector=tamper_nonce)),
                             "ROOTBOUNDARY_LEASE_NONCE_MISMATCH")
            self.assertIsNone(b._last_record)

    def test_actual_b7_is_used_under_the_same_held_root_plane(self):
        source = BASE / "qa525-vnext-b7-held-readonly.py"
        spec = importlib.util.spec_from_file_location("b7_real_boundary_test", source)
        b7 = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(b7)
        read_only_shape = {
            "status": b7.SHAPE_STATUS,
            "baseline_sha256": PIN, "observation_sha256": OBS,
            "running_services_expected": 7, "exited_zero_oneshots_verified": 3,
            "api_compose_layers": 6, "web_compose_layers": 5,
            "samples": 3,
            "sourced_from_authenticated_host": False,
            "sponsor_approved_here": False, "g6_verified_here": False,
            "may_apply": False, "may_install": False,
        }
        with self.flight() as plane:
            boundary = self.make(plane)
            record = boundary.inspect_once(
                b7=b7, inspector=lambda: read_only_shape,
                baseline_sha256=PIN)
            self.assertTrue(boundary.require_current_record(record))
            self.assertEqual(record["status"], bound.STATUS)
            self.assertEqual(len(self.port.fds), 5)
            self.assertEqual(self.log, ["begin"])
            self.assertIs(record["may_apply"], False)
        self.assertEqual(self.log, ["begin", "lease-close"])
        self.assertEqual(self.code(lambda: boundary.require_current_record(record)),
                         "ROOTBOUNDARY_PLANE_LOST")

    def test_external_inspector_failure_has_fixed_code_no_secret(self):
        with self.flight() as plane:
            b = self.make(plane)
            def secret():
                raise RuntimeError("private-secret")
            self.assertEqual(self.code(lambda: self.inspect(b, inspector=secret)),
                             "ROOTBOUNDARY_INSPECTION_STOP")
            plane.assert_held()  # boundary never releases caller's lease
            self.assertNotIn("secret", str(b._last_record))

    def test_rejects_malicious_status_authority_and_extra_fields(self):
        for bad in (
            {"may_apply": True},
            {"sponsor_approved_here": True},
            {"status": "SPOOF"},
            {"running_services_verified_structurally": 6},
            {"observation_sha256": "fake"},
            {"extra": 5},
        ):
            with self.subTest(bad=bad):
                with self.flight() as plane:
                    b = self.make(plane)
                    self.assertEqual(self.code(lambda: self.inspect(
                        b, b7=MaliciousB7(bad))), "ROOTBOUNDARY_REPORT_INVALID")
                    self.assertIsNone(b._last_record)
                self.setUp()

    def test_disallows_inspection_without_callable_or_valid_digest(self):
        with self.flight() as plane:
            b = self.make(plane)
            self.assertEqual(self.code(lambda: b.inspect_once(
                b7=B7Stub(), inspector=None, baseline_sha256=PIN)),
                "ROOTBOUNDARY_INSPECT_INPUT_INVALID")
            self.assertEqual(self.code(lambda: b.inspect_once(
                b7=B7Stub(), inspector=lambda: {}, baseline_sha256="bad")),
                "ROOTBOUNDARY_INSPECT_INPUT_INVALID")

    def test_source_never_runs_host_process_network_or_imports_docker(self):
        src = (BASE / "qa525-vnext-b7-root-boundary.py").read_text()
        imports = {alias.name for node in ast.walk(ast.parse(src))
                   if isinstance(node, ast.Import) for alias in node.names}
        self.assertEqual(imports, {"hashlib", "json", "re"})
        self.assertNotIn("subprocess", src)
        self.assertNotIn("socket", src)
        self.assertNotIn("import os", src)
        self.assertNotIn("docker", src.lower())
        self.assertNotIn("sudo ", src)
        self.assertFalse(hasattr(bound, "run_apply"))
        with self.assertRaises(SystemExit):
            exec(compile(src, "boundary.py", "exec"),
                 {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
