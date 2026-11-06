"""Fully assembled vNext engine over a fake world and private lock fixture."""
import copy
import datetime as dt
import hashlib
import importlib.util
import json
from unittest import mock
from pathlib import Path
import tempfile
import unittest

import test_qa525_vnext_baseline_plan as bp
import test_qa525_vnext_journal as jt
import test_qa525_vnext_trust_binding as trust
from test_qa525_vnext_integration_contract import observation

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "scripts/qa"
PATH = BASE / "qa525-vnext-engine.py"
spec = importlib.util.spec_from_file_location("qa525_engine_test", PATH)
engine = importlib.util.module_from_spec(spec)
spec.loader.exec_module(engine)


class Reader:
    def __init__(self, baseline, target, world):
        self.baseline, self.target, self.world = baseline, target, world

    def capture_baseline(self):
        return copy.deepcopy(self.baseline)

    def observe(self):
        obs = observation(self.baseline, self.target, use_target=True)
        for service in ("api", "web"):
            source = self.target if self.world[service] == "target" else self.baseline
            obs["images"][service] = copy.deepcopy(source["images"][service])
            obs["layers"][service] = copy.deepcopy(source["layers"][service])
        obs["health"]["web_sha"] = (
            self.target if self.world["web"] == "target" else self.baseline)["web_sha"]
        return obs


class Scope:
    def __init__(self, unit):
        self.unit = unit

    def state(self):
        return "ABSENT"


class EngineTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = jt.JournalFixture(self.tmp.name)
        self.record = trust.admission_record()
        self.fx.put(self.record)
        self.nonce = self.record["nonce"]
        # Pure synthetic G6 evidence, NOT a real backup or approval.
        stamp = "20261105T180000Z"
        name = "centenario-qa-g6-CIFRADO-" + stamp + ".tar.gz"
        self.review = {
            "g6_archive_name": name, "g6_archive_sha256": "a" * 64,
            "g6_restore_report_name": "isolated-restore-" + stamp + ".report.json",
            "g6_offhost_sha256_verified": True,
        }
        self.receipt = {
            "archive_name": name, "sha256": "a" * 64,
            "status": "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING",
        }
        self.restore = {
            "result": "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY",
            "archive_filename": name, "archive_sha256": "a" * 64,
            "postgres_tables_by_database": {"pmgm": 7, "pmgm_keycloak": 4},
            "silo_objects_verified": 2, "production_touched": False,
        }
        self.review_bytes = self._raw(self.review)
        self.receipt_bytes = self._raw(self.receipt)
        self.restore_bytes = self._raw(self.restore)
        anchor = trust.approval_record(self.record)
        anchor["g6_review_sha256"] = self._sha(self.review_bytes)
        self.approval = trust.canonical(anchor)
        self.g6_evidence = {
            "review_bytes": self.review_bytes,
            "receipt_bytes": self.receipt_bytes,
            "restore_bytes": self.restore_bytes,
            "pinned_admission_sha256": self._sha(trust.canonical(self.record)),
            "pinned_approval_sha256": self._sha(self.approval),
            "pinned_review_sha256": self._sha(self.review_bytes),
            "pinned_receipt_sha256": self._sha(self.receipt_bytes),
            "pinned_restore_sha256": self._sha(self.restore_bytes),
            "observed_archive_sha256": "sha256:" + "a" * 64,
        }
        self.lockdir = Path(self.tmp.name) / "locks"
        self.lockdir.mkdir(mode=0o700)
        self.lockdir.chmod(0o700)
        for name in engine.locks.SYNTHETIC_NAMES:
            path = self.lockdir / name
            path.touch(mode=0o600)
            path.chmod(0o600)
        self.base, self.target = bp.baseline(), bp.target()
        self.world = {"api": "baseline", "web": "baseline"}
        self.reader = Reader(self.base, self.target, self.world)
        self.runs = []
        self.fail = set()

    @staticmethod
    def _raw(record):
        return (json.dumps(record, sort_keys=True) + "\n").encode()

    @staticmethod
    def _sha(raw):
        return "sha256:" + hashlib.sha256(raw).hexdigest()

    def tearDown(self):
        self.tmp.cleanup()

    def runner(self, argv):
        service = argv[-1]
        files = [argv[i + 1] for i, value in enumerate(argv) if value == "--file"]
        side = "target" if files == self.target["layers"][service] else "baseline"
        self.runs.append((service, side))
        if (service, side) in self.fail:
            return 1
        self.world[service] = side
        return 0

    def fly(self, gate=lambda _stage: True, *, clock=None, evidence=None):
        return engine.run_synthetic(
            root=self.fx.root, admission=self.fx.admission,
            admission_sha256=self.fx.digest, clock=clock or (lambda: trust.NOW),
            admission_bytes=trust.canonical(self.record),
            approval_bytes=self.approval,
            g6_evidence=self.g6_evidence if evidence is None else evidence,
            trusted_policy_hash=trust.POLICY,
            synthetic_lock_dir=self.lockdir, reader=self.reader,
            target=self.target, project="centenario", workdir="/opt/centenario",
            env_file="/etc/pmgm/qa.env", runner=self.runner,
            scope_factory=Scope, settle=lambda: None, external_gate=gate)

    def terminal(self):
        path = self.fx.root / ("terminal-" + self.nonce + ".json")
        return json.loads(path.read_text())["status"] if path.exists() else None

    def test_happy_path_records_scopes_then_applies_under_all_locks(self):
        result = self.fly()
        self.assertEqual(result["status"], "APPLIED")
        self.assertEqual(self.runs, [("api", "target"), ("web", "target")])
        self.assertEqual(self.terminal(), "APPLIED")
        manifest = self.fx.root / ("scopes-" + self.nonce + ".json")
        self.assertTrue(manifest.exists())
        self.assertEqual(len(json.loads(manifest.read_text())["units"]), 2)

    def test_web_failure_uses_baseline_rollback_and_records_terminal(self):
        self.fail.add(("web", "target"))
        result = self.fly()
        self.assertEqual(result["status"], "ROLLED_BACK")
        self.assertEqual(self.runs, [("api", "target"), ("web", "target"),
                                     ("web", "baseline"), ("api", "baseline")])
        self.assertEqual(self.world, {"api": "baseline", "web": "baseline"})
        self.assertEqual(self.terminal(), "ROLLED_BACK")

    def test_denied_external_gate_cannot_reach_a_runner(self):
        closed = {"done": False}
        def gate(_stage):
            if not closed["done"]:
                # The engine checks the plane again after the external gate.
                # Locate and close one held lock through the active file table
                # is intentionally impossible from this callback; denial is
                # still proven by returning false before any runner call.
                closed["done"] = True
                return False
            return True
        with self.assertRaisesRegex(engine.flight.Stop, "^FLIGHT_GATE_DENIED$"):
            self.fly(gate)
        self.assertEqual(self.runs, [])
        self.assertIsNone(self.terminal())

    def _assert_gate_answer_denied(self, answer):
        with self.assertRaisesRegex(engine.flight.Stop, "^FLIGHT_GATE_DENIED$"):
            self.fly(lambda _stage: answer)
        self.assertEqual(self.runs, [])

    def test_truthy_one_gate_answer_is_a_denial(self):
        self._assert_gate_answer_denied(1)

    def test_truthy_string_gate_answer_is_a_denial(self):
        self._assert_gate_answer_denied("yes")

    def test_truthy_object_gate_answer_is_a_denial(self):
        self._assert_gate_answer_denied(object())

    def test_none_gate_answer_is_a_denial(self):
        self._assert_gate_answer_denied(None)

    def test_unbound_or_stale_approval_never_reaches_a_runner(self):
        bad = trust.approval_record(self.record)
        bad["g6_review_sha256"] = "sha256:" + "0" * 63  # malformed digest
        self.approval = trust.canonical(bad)
        with self.assertRaisesRegex(engine.Stop, "^ENGINE_PLANE_DENIED$"):
            self.fly()
        self.assertEqual(self.runs, [])
        self.assertFalse((self.fx.root / ("scopes-" + self.nonce + ".json")).exists())

    def test_wrong_policy_is_denied_before_nonce_or_runner(self):
        def fly_with_policy():
            return engine.run_synthetic(
                root=self.fx.root, admission=self.fx.admission,
                admission_sha256=self.fx.digest, clock=lambda: trust.NOW,
                admission_bytes=trust.canonical(self.record),
                approval_bytes=self.approval, g6_evidence=self.g6_evidence,
                trusted_policy_hash="sha256:" + "9" * 64,
                synthetic_lock_dir=self.lockdir, reader=self.reader,
                target=self.target, project="centenario", workdir="/opt/centenario",
                env_file="/etc/pmgm/qa.env", runner=self.runner,
                scope_factory=Scope, settle=lambda: None,
                external_gate=lambda _s: True)
        with self.assertRaisesRegex(engine.Stop, "^ENGINE_PLANE_DENIED$"):
            fly_with_policy()
        self.assertEqual(self.runs, [])
        self.assertIsNone(self.terminal())

    def test_window_closed_after_locks_were_taken_is_denied(self):
        late = trust.NOW.replace(hour=19)
        with self.assertRaisesRegex(engine.Stop, "^ENGINE_PLANE_DENIED$"):
            engine.run_synthetic(
                root=self.fx.root, admission=self.fx.admission,
                admission_sha256=self.fx.digest, clock=lambda: late,
                admission_bytes=trust.canonical(self.record),
                approval_bytes=self.approval, g6_evidence=self.g6_evidence, trusted_policy_hash=trust.POLICY,
                synthetic_lock_dir=self.lockdir, reader=self.reader,
                target=self.target, project="centenario", workdir="/opt/centenario",
                env_file="/etc/pmgm/qa.env", runner=self.runner,
                scope_factory=Scope, settle=lambda: None,
                external_gate=lambda _s: True)
        self.assertEqual(self.runs, [])

    def test_caller_digest_that_differs_from_the_bound_one_is_denied(self):
        with self.assertRaisesRegex(engine.Stop, "^ENGINE_PLANE_DENIED$"):
            engine.run_synthetic(
                root=self.fx.root, admission=self.fx.admission,
                admission_sha256="sha256:" + "0" * 64, clock=lambda: trust.NOW,
                admission_bytes=trust.canonical(self.record),
                approval_bytes=self.approval, g6_evidence=self.g6_evidence, trusted_policy_hash=trust.POLICY,
                synthetic_lock_dir=self.lockdir, reader=self.reader,
                target=self.target, project="centenario", workdir="/opt/centenario",
                env_file="/etc/pmgm/qa.env", runner=self.runner,
                scope_factory=Scope, settle=lambda: None,
                external_gate=lambda _s: True)
        self.assertEqual(self.runs, [])

    def test_clock_that_advances_while_locks_are_taken_is_denied(self):
        ticks = iter([trust.NOW, trust.NOW.replace(hour=19), trust.NOW.replace(hour=19)])
        with self.assertRaisesRegex(engine.Stop, "^ENGINE_PLANE_DENIED$"):
            engine.run_synthetic(
                root=self.fx.root, admission=self.fx.admission,
                admission_sha256=self.fx.digest, clock=lambda: next(ticks),
                admission_bytes=trust.canonical(self.record),
                approval_bytes=self.approval, g6_evidence=self.g6_evidence, trusted_policy_hash=trust.POLICY,
                synthetic_lock_dir=self.lockdir, reader=self.reader,
                target=self.target, project="centenario", workdir="/opt/centenario",
                env_file="/etc/pmgm/qa.env", runner=self.runner,
                scope_factory=Scope, settle=lambda: None,
                external_gate=lambda _s: True)
        self.assertEqual(self.runs, [])

    def _assert_close_failure_names(self, expected, fail=None):
        real = engine.flight.journal.Lease.close
        def broken(lease):
            real(lease)
            raise OSError("boom")
        if fail:
            self.fail.add(fail)
        with mock.patch.object(engine.flight.journal.Lease, "close", broken):
            with self.assertRaisesRegex(
                    engine.Stop, "^ENGINE_CLOSE_FAILED_AFTER_" + expected + "$"):
                self.fly()
        self.assertEqual(self.terminal(), expected)

    def test_failed_close_after_applied_names_applied(self):
        self._assert_close_failure_names("APPLIED")

    def test_failed_close_after_rollback_names_rolled_back(self):
        self._assert_close_failure_names("ROLLED_BACK", ("web", "target"))

    def test_runner_factory_receives_the_held_plane(self):
        seen = []
        def factory(plane):
            plane.assert_held()
            seen.append(plane)
            return self.runner
        result = engine.run_synthetic(
            root=self.fx.root, admission=self.fx.admission,
            admission_sha256=self.fx.digest, clock=lambda: trust.NOW,
            admission_bytes=trust.canonical(self.record),
            approval_bytes=self.approval, g6_evidence=self.g6_evidence, trusted_policy_hash=trust.POLICY,
            synthetic_lock_dir=self.lockdir, reader=self.reader,
            target=self.target, project="centenario", workdir="/opt/centenario",
            env_file="/etc/pmgm/qa.env", runner_factory=factory,
            scope_factory=Scope, settle=lambda: None, external_gate=lambda _s: True)
        self.assertEqual(result["status"], "APPLIED")
        self.assertEqual(len(seen), 1)

    def test_exactly_one_of_runner_or_factory_is_required(self):
        base = dict(
            root=self.fx.root, admission=self.fx.admission,
            admission_sha256=self.fx.digest, clock=lambda: trust.NOW,
            admission_bytes=trust.canonical(self.record),
            approval_bytes=self.approval, g6_evidence=self.g6_evidence, trusted_policy_hash=trust.POLICY,
            synthetic_lock_dir=self.lockdir, reader=self.reader,
            target=self.target, project="centenario", workdir="/opt/centenario",
            env_file="/etc/pmgm/qa.env", scope_factory=Scope,
            settle=lambda: None, external_gate=lambda _s: True)
        for extra in ({}, {"runner": self.runner, "runner_factory": lambda p: self.runner}):
            with self.assertRaisesRegex(engine.Stop, "^ENGINE_ADAPTER_INVALID$"):
                engine.run_synthetic(**base, **extra)
        self.assertEqual(self.runs, [])

    def test_missing_or_precomputed_g6_is_denied_before_any_effect(self):
        for bad in (False, True, {}, {"status": "PASS"}):
            with self.subTest(bad=bad):
                with self.assertRaisesRegex(engine.Stop, "^ENGINE_G6_EVIDENCE_INVALID$"):
                    self.fly(evidence=bad)
                self.assertEqual(self.runs, [])
                self.assertIsNone(self.terminal())

    def test_g6_mismatch_denied_under_locks_before_baseline_or_factory(self):
        broken = dict(self.g6_evidence,
                      pinned_review_sha256="sha256:" + "9" * 64)
        observed = {"factory": False}
        with self.assertRaisesRegex(engine.Stop, "^ENGINE_G6_EVIDENCE_DENIED$"):
            engine.run_synthetic(
                root=self.fx.root, admission=self.fx.admission,
                admission_sha256=self.fx.digest, clock=lambda: trust.NOW,
                admission_bytes=trust.canonical(self.record),
                approval_bytes=self.approval, trusted_policy_hash=trust.POLICY,
                g6_evidence=broken,
                synthetic_lock_dir=self.lockdir, reader=self.reader,
                target=self.target, project="centenario", workdir="/opt/centenario",
                env_file="/etc/pmgm/qa.env",
                runner_factory=lambda _plane: observed.__setitem__("factory", True),
                scope_factory=Scope, settle=lambda: None,
                external_gate=lambda _stage: True)
        self.assertFalse(observed["factory"])
        self.assertEqual(self.runs, [])
        self.assertFalse((self.fx.root / ("scopes-" + self.nonce + ".json")).exists())

    def test_g6_clock_is_read_again_after_locks_and_lease(self):
        ticks = iter([trust.NOW, trust.NOW, trust.NOW + dt.timedelta(minutes=30)])
        with self.assertRaisesRegex(engine.Stop, "^ENGINE_G6_EVIDENCE_DENIED$"):
            self.fly(clock=lambda: next(ticks))
        self.assertEqual(self.runs, [])
        self.assertIsNone(self.terminal())

    def test_caller_mutation_after_entry_does_not_change_sealed_g6(self):
        tampered = self._raw(dict(self.review, g6_archive_sha256="b" * 64))
        def gate(stage):
            if stage == "API_SWITCHING":
                # Mutate the caller's original dict, not the engine's snapshot.
                self.g6_evidence["review_bytes"] = tampered
            return True
        with mock.patch.object(
                engine.g6_anchor, "compare",
                wraps=engine.g6_anchor.compare) as compare:
            result = self.fly(gate)
        self.assertEqual(result["status"], "APPLIED")
        self.assertEqual(self.g6_evidence["review_bytes"], tampered)
        self.assertEqual(self.runs, [("api", "target"), ("web", "target")])
        self.assertGreaterEqual(compare.call_count, 3)
        self.assertTrue(all(call.kwargs["review_bytes"] == self.review_bytes
                            for call in compare.call_args_list))

    def test_mutable_or_custom_evidence_types_fail_before_journal(self):
        # Bytearray, list or subclasses could change after a prior check.
        class FakeSHA(str):
            pass
        cases = (
            ("review_bytes", bytearray(self.review_bytes)),
            ("receipt_bytes", memoryview(self.receipt_bytes)),
            ("restore_bytes", list(self.restore_bytes)),
            ("pinned_review_sha256",
             FakeSHA(self.g6_evidence["pinned_review_sha256"])),
            ("observed_archive_sha256",
             bytearray(self.g6_evidence["observed_archive_sha256"], "ascii")),
        )
        for field, value in cases:
            with self.subTest(field=field):
                bad = dict(self.g6_evidence, **{field: value})
                with self.assertRaisesRegex(engine.Stop,
                                            "^ENGINE_G6_EVIDENCE_INVALID$"):
                    self.fly(evidence=bad)
                self.assertEqual(self.runs, [])
                self.assertIsNone(self.terminal())

    def test_new_execution_intent_stage_fails_closed_before_journal(self):
        # If flight adds a future forward intent, the engine must not
        # silently skip its held-lock G6 check.
        with mock.patch.dict(engine.flight.plan_mod.PROOF_KINDS,
                             {"FUTURE_SWITCHING": "EXECUTION_INTENT"}):
            with self.assertRaisesRegex(
                    engine.Stop, "^ENGINE_FORWARD_STAGES_MISMATCH$"):
                self.fly()
        self.assertEqual(self.runs, [])
        self.assertIsNone(self.terminal())

    def test_g6_expiry_before_web_causes_rollback_not_abandon(self):
        # Clock: pre-lock, journal.begin, held G6, run_apply, API, WEB.
        expired = trust.NOW + dt.timedelta(minutes=30)
        ticks = iter([trust.NOW] * 5 + [expired])
        result = self.fly(clock=lambda: next(ticks))
        self.assertEqual(result["status"], "ROLLED_BACK")
        self.assertEqual(self.world, {"api": "baseline", "web": "baseline"})
        self.assertEqual(self.runs, [
            ("api", "target"), ("web", "baseline"), ("api", "baseline")])
        self.assertEqual(self.terminal(), "ROLLED_BACK")

    def test_fresh_g6_check_required_at_both_forward_intents(self):
        with mock.patch.object(
                engine.g6_anchor, "compare",
                wraps=engine.g6_anchor.compare) as compare:
            result = self.fly()
        self.assertEqual(result["status"], "APPLIED")
        self.assertGreaterEqual(compare.call_count, 3)
        self.assertTrue(all(call.kwargs["now"] == trust.NOW
                            for call in compare.call_args_list))

    def test_no_physical_or_shell_entrypoint_exists(self):
        raw = PATH.read_text(encoding="utf-8")
        for token in ("import subprocess", "import socket", "urllib", "requests",
                      "Popen(", "os.system(", "LEGACY_LOCKS", "--review-and-run"):
            self.assertNotIn(token, raw)
        with self.assertRaises(SystemExit):
            exec(compile(raw, str(PATH), "exec"), {"__name__": "__main__",
                                                    "__file__": str(PATH)})


if __name__ == "__main__":
    unittest.main()
