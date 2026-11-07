"""Hermetic tests for B7 RootPlane physical runner argv/cgroup allowlist.

The root plane is the REAL module with a fake POSIX lock port and genuine
private journal lease; all command effects are fake. No systemd or Docker.
"""
from contextlib import contextmanager
import ast
import importlib.util
from pathlib import Path
import unittest
from unittest import mock

from test_qa525_vnext_root_locks import Port, rl, fstat_
from test_qa525_vnext_b7_held_readonly import b7
from test_qa525_vnext_b7_root_boundary import bound
import test_qa525_vnext_engine as e

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"
SCRIPT = BASE / "qa525-vnext-held-physical-runner.py"
SPEC = importlib.util.spec_from_file_location("physical_runner_tests", SCRIPT)
physical = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(physical)

PINS = {name: "sha256:" + chr(97 + i) * 64
        for i, name in enumerate(sorted(physical.PIN_KEYS))}


def b7_shape(pin):
    return {
        "status": b7.SHAPE_STATUS,
        "baseline_sha256": pin,
        "observation_sha256": "sha256:" + "d" * 64,
        "running_services_expected": 7,
        "exited_zero_oneshots_verified": 3,
        "api_compose_layers": 6, "web_compose_layers": 5, "samples": 3,
        "sourced_from_authenticated_host": False,
        "sponsor_approved_here": False, "g6_verified_here": False,
        "may_apply": False, "may_install": False,
    }


class PhysicalRunnerTests(unittest.TestCase):
    def setUp(self):
        self.fx = e.EngineTests("test_happy_path_records_scopes_then_applies_under_all_locks")
        self.fx.setUp()
        self.port = Port()
        self.effects = []
        self.verify_calls = 0
        self.pin_ok = True
        self.code = 0
        self.baseline_pin = e.engine.observer.plan_mod.validate_baseline(self.fx.base)
        self.spec = dict(
            nonce=self.fx.nonce,
            project="centenario",
            workdir="/opt/centenario",
            env_file="/etc/pmgm/qa.env",
            baseline_layers=self.fx.base["layers"],
            target_layers=self.fx.target["layers"],
            admission_sha256=self.fx.fx.digest,
        )
        self.containment = e.engine.switch.containment
        self.scopes = e.engine.scopes

    def tearDown(self):
        self.fx.tearDown()

    def verify(self, pins):
        self.verify_calls += 1
        self.assertEqual(pins, PINS)
        return self.pin_ok

    def effect(self, argv):
        self.effects.append(argv)
        return self.code

    @contextmanager
    def active(self, *, manifest=True):
        journal = e.engine.bridge.BoundJournal(
            e.engine.flight.journal,
            e.trust.canonical(self.fx.record), self.fx.approval,
            trusted_policy_hash=e.trust.POLICY, clock=lambda: e.trust.NOW)
        self.scopes.journal = e.engine.flight.journal
        with rl.root_flight(
                self.port, journal, self.fx.fx.root, self.fx.fx.admission,
                self.fx.fx.digest, e.trust.NOW) as plane:
            bnd = bound.Boundary(
                root_locks=rl, plane=plane, nonce=plane.lease.nonce,
                admission_sha256=self.fx.fx.digest)
            record = bnd.inspect_once(
                b7=b7, inspector=lambda: b7_shape(self.baseline_pin),
                baseline_sha256=self.baseline_pin)
            if manifest:
                units = [self.containment.unit_name(plane.lease.nonce, name)
                         for name in ("api", "web")]
                self.scopes.initialize(
                    plane.lease, self.fx.fx.digest, units)
            yield plane, bnd, record

    def bind(self, plane, bnd, record, **extra):
        args = dict(root_locks=rl, boundary_module=bound,
                    containment=self.containment, scopes=self.scopes,
                    plane=plane, boundary=bnd, report=record,
                    spec=self.spec, pins=PINS, verify_pins=self.verify,
                    execute=self.effect)
        args.update(extra)
        return physical.bind(**args)

    def command(self, service, direction):
        return self.containment.systemd_run_argv(
            self.containment.unit_name(self.fx.nonce, service),
            self.spec["project"], self.spec["workdir"], self.spec["env_file"],
            self.spec[direction + "_layers"][service], service)

    def stopped(self, callback, expected):
        with self.assertRaises(physical.Stop) as error:
            callback()
        self.assertEqual(str(error.exception), expected)
        self.assertIsNone(error.exception.__cause__)

    def test_four_canonical_commands_under_same_lease_and_recorded_scopes(self):
        with self.active() as (plane, bnd, record):
            runner = self.bind(plane, bnd, record)
            for direction in ("target", "baseline"):
                for service in ("api", "web"):
                    self.assertEqual(runner(self.command(service, direction)), 0)
                    self.assertEqual(len(self.port.fds), 5)
            self.assertEqual(self.verify_calls, 8)
            self.assertEqual(len(self.effects), 4)
            self.assertTrue(all(x[0] == "/usr/bin/systemd-run" for x in self.effects))
        self.stopped(lambda: runner(self.command("api", "target")),
                     "PHYSRUNNER_PLANE_LOST")

    def test_scope_manifest_must_exist_before_first_child(self):
        with self.active(manifest=False) as (plane, bnd, record):
            runner = self.bind(plane, bnd, record)
            self.stopped(lambda: runner(self.command("api", "target")),
                         "PHYSRUNNER_SCOPE_NOT_RECORDED")
        self.assertEqual(self.effects, [])
        self.assertEqual(self.verify_calls, 0)

    def test_scope_store_receipt_requires_canonical_sha256_digest(self):
        # Clau's mutation R-no-digest: require_recorded must not be a
        # truthy string / bool / loose sha prefix. No pins or child may run.
        variants = (None, True, 1, b"sha256:" + b"a" * 64, "",
                    "yes", "sha256:broken", "sha256:" + "A" * 64,
                    "sha256:" + "a" * 63, "sha256:" + "a" * 65)
        with self.active() as (plane, bnd, record):
            runner = self.bind(plane, bnd, record)
            for bad in variants:
                with self.subTest(value=repr(bad)[:32]):
                    with mock.patch.object(self.scopes, "require_recorded",
                                           return_value=bad) as registered:
                        self.stopped(
                            lambda: runner(self.command("api", "target")),
                            "PHYSRUNNER_SCOPE_NOT_RECORDED")
                    registered.assert_called_once_with(
                        plane.lease, self.fx.fx.digest,
                        self.containment.unit_name(plane.lease.nonce, "api"))
        self.assertEqual(self.effects, [])
        self.assertEqual(self.verify_calls, 0)

    def test_scope_store_exception_is_closed_without_child(self):
        with self.active() as (plane, bnd, record):
            runner = self.bind(plane, bnd, record)
            with mock.patch.object(self.scopes, "require_recorded",
                                   side_effect=RuntimeError("never echo token")):
                self.stopped(lambda: runner(self.command("web", "baseline")),
                             "PHYSRUNNER_SCOPE_NOT_RECORDED")
        self.assertEqual(self.effects, [])
        self.assertEqual(self.verify_calls, 0)

    def test_unknown_or_mutated_argv_never_calls_effect(self):
        cmd = self.command("api", "target")
        variants = [
            cmd + ["--privileged"],
            cmd[:-1] + ["postgres"],
            ["sudo"] + cmd,
            ["/usr/bin/sh"] + cmd[1:],
            cmd[:3] + ["--system"] + cmd[3:],
            cmd[:9] + ["/tmp/outside.yml"] + cmd[10:],
            [x.replace("--collect", "--no-collect") for x in cmd],
            [x.replace("/opt/centenario", "/tmp/test") for x in cmd],
            " ".join(cmd),
            None,
            [],
        ]
        with self.active() as (plane, bnd, record):
            runner = self.bind(plane, bnd, record)
            for bad in variants:
                with self.subTest(value=str(bad)[:40]):
                    self.stopped(lambda x=bad: runner(x), "PHYSRUNNER_ARGV_REFUSED")
        self.assertEqual(self.effects, [])

    def test_false_or_truthy_pin_verification_never_starts(self):
        with self.active() as (plane, bnd, record):
            runner = self.bind(plane, bnd, record)
            self.pin_ok = 1  # truthy is NOT an authenticated True
            self.stopped(lambda: runner(self.command("api", "target")),
                         "PHYSRUNNER_PINS_UNVERIFIED")
        self.assertEqual(self.effects, [])

    def test_pin_changes_after_child_rejected_even_if_child_exits_zero(self):
        def revoke(argv):
            self.effects.append(argv)
            self.pin_ok = False
            return 0
        with self.active() as (plane, bnd, record):
            runner = self.bind(plane, bnd, record, execute=revoke)
            self.stopped(lambda: runner(self.command("web", "target")),
                         "PHYSRUNNER_PINS_CHANGED")
        self.assertEqual(len(self.effects), 1)
        self.assertEqual(self.verify_calls, 2)

    def test_lock_lost_during_external_pin_check_rejects_before_child(self):
        def poison(pins):
            self.port.nodes[rl.LEGACY_LOCKS[0]] = fstat_(99999)
            return True
        with self.active() as (plane, bnd, record):
            runner = self.bind(plane, bnd, record, verify_pins=poison)
            self.stopped(lambda: runner(self.command("api", "target")),
                         "PHYSRUNNER_PLANE_LOST")
        self.assertEqual(self.effects, [])

    def test_lock_lost_after_child_refuses_success(self):
        def poison(argv):
            self.effects.append(argv)
            self.port.nodes[rl.LEGACY_LOCKS[1]] = fstat_(99999)
            return 0
        with self.active() as (plane, bnd, record):
            runner = self.bind(plane, bnd, record, execute=poison)
            self.stopped(lambda: runner(self.command("api", "baseline")),
                         "PHYSRUNNER_PLANE_LOST")
        self.assertEqual(len(self.effects), 1)

    def test_foreign_or_copied_report_is_not_an_execution_capability(self):
        with self.active() as (plane, bnd, record):
            self.stopped(lambda: self.bind(plane, bnd, dict(record)),
                         "PHYSRUNNER_REPORT_INVALID")
            self.stopped(lambda: self.bind(object(), bnd, record),
                         "PHYSRUNNER_PLANE_INVALID")
        self.assertEqual(self.effects, [])

    def test_changes_to_original_spec_and_pins_cannot_change_allowlist(self):
        with self.active() as (plane, bnd, record):
            runner = self.bind(plane, bnd, record)
            original = self.command("api", "target")
            self.spec["workdir"] = "/tmp/evil"
            self.spec["target_layers"]["api"] = ["/tmp/evil.yml"]
            self.assertEqual(runner(original), 0)
            self.stopped(lambda: runner(self.command("api", "target")),
                         "PHYSRUNNER_ARGV_REFUSED")
        self.assertEqual(self.effects, [original])

    def test_invalid_spec_and_pins_denied_before_any_child(self):
        with self.active() as (plane, bnd, record):
            cases = (
                ({"pins": dict(PINS, unknown="sha256:" + "a" * 64)},
                 "PHYSRUNNER_INPUT_INVALID"),
                ({"pins": {**PINS, "python_sha256": "bad"}},
                 "PHYSRUNNER_INPUT_INVALID"),
                ({"spec": {**self.spec, "nonce": "a"}},
                 "PHYSRUNNER_INPUT_INVALID"),
                ({"spec": {**self.spec, "admission_sha256": "bad"}},
                 "PHYSRUNNER_INPUT_INVALID"),
                ({"spec": {**self.spec, "target_layers": {"api": [], "web": []}}},
                 "PHYSRUNNER_LAYERS_INVALID"),
            )
            for kw, expected in cases:
                with self.subTest(kw=list(kw)):
                    self.stopped(lambda k=kw: self.bind(plane, bnd, record, **k),
                                 expected)
        self.assertEqual(self.effects, [])

    def test_bad_effect_result_and_exception_not_leak_secret(self):
        with self.active() as (plane, bnd, record):
            self.code = "0"
            runner = self.bind(plane, bnd, record)
            self.stopped(lambda: runner(self.command("api", "target")),
                         "PHYSRUNNER_RESULT_INVALID")
        self.assertEqual(len(self.effects), 1)

    def test_no_host_executor_import_or_cli(self):
        source = SCRIPT.read_text()
        names = {alias.name for node in ast.walk(ast.parse(source))
                 if isinstance(node, ast.Import) for alias in node.names}
        self.assertEqual(names, {"re"})
        self.assertNotIn("import subprocess", source)
        self.assertNotIn("import os", source)
        self.assertNotIn("Popen(", source)
        self.assertFalse(hasattr(physical, "main"))
        with self.assertRaises(SystemExit):
            exec(compile(source, str(SCRIPT), "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
