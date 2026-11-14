import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


term = load("qa525_autoweb_terminal_test", "scripts/qa/qa525-vnext-autoweb-terminal.py")
root_plane = load("qa525_autoweb_root_for_terminal", "scripts/qa/qa525-vnext-autoweb-root-plane.py")
D = "sha256:" + "1" * 64


class Physical:
    def __init__(self):
        self.held = True
    def assert_held(self):
        if not self.held:
            raise RuntimeError("lost")
        return {"schema": root_plane.SCHEMA, "legacy_locks_held": 5,
                "physical_root_plane_v1": True, "may_apply": False, "may_install": False}


root_plane.PhysicalPlaneCapability = Physical


def record(**change):
    base = {
        "schema": term.SCHEMA, "status": "ACTIVE", "policy_id": "policy-20261114",
        "policy_generation": 3, "sequence": 10, "release_id": "sha256:" + "2" * 64,
        "nonce": "autoweb-nonce-20261114-000001", "stage_record_sha256": D,
        "final_stage": "VERIFYING", "installed_before_sha256": "sha256:" + "3" * 64,
        "expected_after_sha256": "sha256:" + "4" * 64,
        "observed_after_sha256": "sha256:" + "4" * 64,
        "health_receipt_sha256": "sha256:" + "5" * 64,
        "rollback_receipt_sha256": None, "previous_terminal_sha256": None,
        "critical": False, "finished_at": "2026-11-14T14:40:00Z",
        "may_apply": False, "authenticated_by": "EXTERNAL_SIGNATURE_VERIFIER_V1",
    }
    base.update(change)
    return base


def stage(value):
    return {"policy_id": value["policy_id"], "policy_generation": value["policy_generation"],
            "sequence": value["sequence"], "release_id": value["release_id"],
            "nonce": value["nonce"], "record_sha256": value["stage_record_sha256"],
            "stage": value["final_stage"]}


def measurements(value):
    return {key: value[key] for key in ("installed_before_sha256", "expected_after_sha256",
            "observed_after_sha256", "health_receipt_sha256", "rollback_receipt_sha256")}


def auth(value, verifier=lambda *_: True, plane=None):
    return term.authenticate(value, b"sig", verifier, stage(value), measurements(value),
                             plane or Physical(), root_plane)


class TerminalTests(unittest.TestCase):
    def test_active_terminal_requires_signature_and_returns_opaque_capability(self):
        calls = []
        value = record()
        cap = term.authenticate(value, b"sig", lambda payload, sig: calls.append((payload, sig)) or True,
                                stage(value), measurements(value), Physical(), root_plane)
        checked, fingerprint = term.require_capability(cap)
        self.assertEqual(checked["status"], "ACTIVE")
        self.assertEqual(fingerprint, term.digest(checked))
        self.assertEqual(calls[0][1], b"sig")
        with self.assertRaisesRegex(term.Stop, "CAPABILITY_REQUIRED"):
            term.require_capability(record())

    def test_signature_failure_exception_and_non_bool_are_rejected(self):
        for verifier in (lambda *_: False, lambda *_: 1,
                         lambda *_: (_ for _ in ()).throw(RuntimeError("secret"))):
            with self.subTest(verifier=repr(verifier)), self.assertRaisesRegex(term.Stop, "SIGNATURE_INVALID"):
                auth(record(), verifier)

    def test_active_binding_mutants_fail_closed(self):
        cases = {
            "extra": dict(record(), extra=True),
            "wrong stage": record(final_stage="ROLLBACK_VERIFYING"),
            "measurement mismatch": record(observed_after_sha256="sha256:" + "9" * 64),
            "candidate equals installed": record(expected_after_sha256="sha256:" + "3" * 64,
                                                   observed_after_sha256="sha256:" + "3" * 64),
            "rollback present": record(rollback_receipt_sha256=D),
            "critical": record(critical=True),
            "authority": record(may_apply=True),
            "checksum auth": record(authenticated_by="CHECKSUM_ONLY_NOT_A_SIGNATURE"),
            "bad stage digest": record(stage_record_sha256="bad"),
            "bad sequence": record(sequence=0),
            "bad time": record(finished_at="yesterday"),
        }
        for name, value in cases.items():
            with self.subTest(name=name), self.assertRaises(term.Stop):
                auth(value)

    def test_blocked_requires_rollback_measurement_and_never_advances(self):
        blocked = record(status="BLOCKED", final_stage="ROLLBACK_VERIFYING",
                         rollback_receipt_sha256="sha256:" + "6" * 64,
                         observed_after_sha256="sha256:" + "3" * 64)
        cap = auth(blocked)
        self.assertEqual(term.require_capability(cap)[0]["status"], "BLOCKED")
        for mutant in (record(status="BLOCKED", final_stage="ROLLBACK_VERIFYING"),
                       record(status="BLOCKED", rollback_receipt_sha256=D),
                       record(status="BLOCKED", final_stage="ROLLBACK_VERIFYING",
                              rollback_receipt_sha256=D,
                              observed_after_sha256="sha256:" + "9" * 64,
                              critical=False),
                       record(status="BLOCKED", final_stage="ROLLBACK_VERIFYING",
                              rollback_receipt_sha256=D,
                              observed_after_sha256="sha256:" + "3" * 64,
                              critical=True)):
            with self.assertRaises(term.Stop):
                auth(mutant)
        critical = record(status="BLOCKED", final_stage="ROLLBACK_VERIFYING",
                          rollback_receipt_sha256=D,
                          observed_after_sha256="sha256:" + "9" * 64,
                          critical=True)
        self.assertEqual(term.require_capability(auth(critical))[0]["critical"], True)

    def test_stage_and_measurements_are_separate_exact_trust_inputs(self):
        value = record()
        bad_stage = dict(stage(value), sequence=12)
        with self.assertRaisesRegex(term.Stop, "STAGE_BINDING_MISMATCH"):
            term.authenticate(value, b"sig", lambda *_: True, bad_stage, measurements(value),
                              Physical(), root_plane)
        bad_measure = dict(measurements(value), observed_after_sha256="sha256:" + "9" * 64)
        with self.assertRaisesRegex(term.Stop, "MEASUREMENTS_MISMATCH"):
            term.authenticate(value, b"sig", lambda *_: True, stage(value), bad_measure,
                              Physical(), root_plane)

    def test_direct_constructor_does_not_bypass_signature(self):
        forged = term._AuthenticatedTerminal(record())
        with self.assertRaisesRegex(term.Stop, "CAPABILITY_REQUIRED"):
            term.require_capability(forged)

    def test_physical_plane_and_held_lease_are_required(self):
        value, plane = record(), Physical()
        plane.held = False
        with self.assertRaisesRegex(term.Stop, "PHYSICAL_PLANE_NOT_HELD"):
            auth(value, plane=plane)
        with self.assertRaisesRegex(term.Stop, "PHYSICAL_PLANE_INVALID"):
            term.authenticate(value, b"sig", lambda *_: True, stage(value), measurements(value),
                              object(), root_plane)

    def test_module_has_no_host_effects_or_cli(self):
        source = (ROOT / "scripts/qa/qa525-vnext-autoweb-terminal.py").read_text()
        for forbidden in ("subprocess", "socket", "requests", "urllib", "os.system", "open("):
            self.assertNotIn(forbidden, source)


if __name__ == "__main__":
    unittest.main()
