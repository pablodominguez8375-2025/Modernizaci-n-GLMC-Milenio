import hashlib
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ex = load("schema_cut_executor", "qa525-vnext-schema-cut-executor.py")
ct = load("schema_cut_contract", "qa525-vnext-schema-cut-journal-contract.py")
store = load("schema_cut_store", "qa525-vnext-schema-cut-journal-store.py")
BIND = {"nonce": "a" * 64, "go_sha256": "sha256:" + "1" * 64, "plan_sha256": "sha256:" + "2" * 64}


class Plane:
    def __init__(self, fail_after=None):
        self.calls, self.fail_after = 0, fail_after

    def assert_held(self):
        self.calls += 1
        if self.fail_after is not None and self.calls > self.fail_after:
            raise RuntimeError("lock secret path /root/x")


class Journal:
    """In-memory journal driven by the REAL contract, so terminal classes are the contract's own."""

    def __init__(self, fail_on=None):
        self.events, self.fail_on, self.log = [], fail_on, []

    def __call__(self, phase):
        if phase == self.fail_on:
            self.log.append(("FAILED", phase))
            raise OSError("disk /secret/path full")
        decision = ct.next_event(self.events, BIND, phase)
        self.events.append(decision["event"])
        self.log.append(("W", phase))
        return {"phase": phase, "event_sha256": decision["event_sha256"],
                "failure_class_if_interrupted": decision["failure_class_if_interrupted"], "may_apply": False}

    def phases(self):
        return [json.loads(raw)["phase"] for raw in self.events]


def good_schema(journal):
    for phase in ex.SCHEMA_SEQUENCE:
        journal(phase)


class Steps(dict):
    def __init__(self, journal, **override):
        super().__init__(
            preflight=lambda: self.note("preflight") or None,
            verify_backup=lambda: self.note("backup") or True,
            quiesce_writes=lambda: self.note("quiesce") or True,
            schema=lambda j: self.note("schema") or good_schema(j),
            revalidate=lambda: self.note("revalidate") or True,
            apply_api_web=lambda: self.note("apply") or {"terminal": "APPLIED"},
            uat=lambda: self.note("uat") or True)
        self.update(override)
        self.effects, self.journal = [], journal

    def note(self, name):
        self.effects.append((name, len(self.journal.events)))


def build(journal=None, **override):
    journal = journal or Journal()
    steps = Steps(journal, **override)
    return journal, steps


class ExecutorTests(unittest.TestCase):
    def test_happy_path_writes_all_ten_phases_in_order_and_grants_no_authority(self):
        journal, steps = build()
        result = ex.run(journal, Plane(), dict(steps))
        self.assertEqual(list(ex.PHASES_OK), journal.phases())
        self.assertEqual(ex.PHASES_OK, result["phases"])
        self.assertIs(result["may_apply"], False)
        self.assertEqual("QA525_SCHEMA_CUT_SEQUENCE_COMPLETE_NO_AUTHORITY", result["status"])

    def test_every_phase_is_durable_before_the_effect_it_announces(self):
        journal, steps = build()
        ex.run(journal, Plane(), dict(steps))
        written = dict(steps.effects)  # effect -> number of events already durable when it started
        self.assertEqual(0, written["preflight"])
        self.assertEqual(1, written["backup"])        # RESERVED
        self.assertEqual(2, written["quiesce"])       # + BACKUP_VERIFIED
        self.assertEqual(3, written["schema"])        # + WRITES_QUIESCED
        self.assertEqual(7, written["revalidate"])    # + 4 schema phases
        self.assertEqual(8, written["apply"])         # + APP_STARTED  (durable BEFORE the only API/WEB effect)
        self.assertEqual(9, written["uat"])           # + APP_APPLIED

    def test_preflight_failure_writes_nothing(self):
        for steps_override in ({"preflight": lambda: "no"}, {"preflight": lambda: (_ for _ in ()).throw(OSError("secret"))}):
            journal, steps = build(**steps_override)
            with self.assertRaises(ex.Stop) as caught:
                ex.run(journal, Plane(), dict(steps))
            self.assertEqual("SCHEMA_EXEC_PREFLIGHT_FAILED", str(caught.exception))
            self.assertEqual([], journal.phases())

    def test_backup_or_quiesce_failure_is_a_pre_ddl_stop(self):
        for key, code, last in (("verify_backup", "SCHEMA_EXEC_BACKUP_FAILED", ["RESERVED", "PRE_DDL_STOP"]),
                                ("quiesce_writes", "SCHEMA_EXEC_QUIESCE_FAILED",
                                 ["RESERVED", "BACKUP_VERIFIED", "PRE_DDL_STOP"])):
            for bad in (lambda: False, lambda: (_ for _ in ()).throw(RuntimeError("token=abc"))):
                journal, steps = build(**{key: bad})
                with self.assertRaises(ex.Stop) as caught:
                    ex.run(journal, Plane(), dict(steps))
                self.assertEqual(code, str(caught.exception))
                self.assertNotIn("token", repr(caught.exception))
                self.assertEqual(last, journal.phases())
                self.assertNotIn("schema", [name for name, _ in steps.effects])

    def test_schema_failure_before_ddl_boundary_is_pre_ddl_and_keeps_only_allowlisted_cause(self):
        def schema(_journal):
            raise RuntimeError("SCHEMA_ADAPTER_IMAGE_ID_MISMATCH")
        journal, steps = build(schema=schema)
        with self.assertRaises(ex.Stop) as caught:
            ex.run(journal, Plane(), dict(steps))
        self.assertEqual("SCHEMA_EXEC_SCHEMA_FAILED", str(caught.exception))
        self.assertEqual("SCHEMA_ADAPTER_IMAGE_ID_MISMATCH", caught.exception.cause)
        self.assertEqual(["RESERVED", "BACKUP_VERIFIED", "WRITES_QUIESCED", "PRE_DDL_STOP"], journal.phases())

        def leaky(_journal):
            raise RuntimeError("password=hunter2 SCHEMA_ADAPTER_X")
        journal, steps = build(schema=leaky)
        with self.assertRaises(ex.Stop) as caught:
            ex.run(journal, Plane(), dict(steps))
        self.assertIsNone(caught.exception.cause)
        self.assertNotIn("hunter2", repr(caught.exception))

    def test_schema_callee_that_journals_its_own_partial_is_not_duplicated(self):
        def schema(journal):
            journal("MIGRATION_STARTED")
            journal("PARTIAL_SCHEMA_STOP")
            raise RuntimeError("SCHEMA_ADAPTER_MIGRATION_FAILED")
        journal, steps = build(schema=schema)
        with self.assertRaises(ex.Stop) as caught:
            ex.run(journal, Plane(), dict(steps))
        self.assertEqual("SCHEMA_ADAPTER_MIGRATION_FAILED", caught.exception.cause)
        self.assertEqual(1, journal.phases().count("PARTIAL_SCHEMA_STOP"))
        self.assertEqual("PARTIAL_SCHEMA_STOP", journal.phases()[-1])

    def test_schema_callee_that_forgets_phases_is_stopped_with_the_contract_terminal(self):
        def schema(journal):
            journal("MIGRATION_STARTED")
            journal("SCHEMA_104")  # returns without GRANTS_VERIFIED / POSTGATE_PASS
        journal, steps = build(schema=schema)
        with self.assertRaises(ex.Stop) as caught:
            ex.run(journal, Plane(), dict(steps))
        self.assertEqual("SCHEMA_EXEC_SCHEMA_INCOMPLETE", str(caught.exception))
        self.assertEqual("PARTIAL_SCHEMA_STOP", journal.phases()[-1])
        self.assertNotIn("apply", [name for name, _ in steps.effects])

    def test_revalidation_failure_never_reaches_app_started_or_the_api_web_effect(self):
        journal, steps = build(revalidate=lambda: False)
        with self.assertRaises(ex.Stop) as caught:
            ex.run(journal, Plane(), dict(steps))
        self.assertEqual("SCHEMA_EXEC_REVALIDATION_FAILED", str(caught.exception))
        self.assertEqual("PARTIAL_SCHEMA_STOP", journal.phases()[-1])
        self.assertNotIn("APP_STARTED", journal.phases())
        self.assertNotIn("apply", [name for name, _ in steps.effects])

    def test_app_started_write_failure_is_ambiguous_and_the_effect_never_runs(self):
        journal, steps = build(Journal(fail_on="APP_STARTED"))
        with self.assertRaises(ex.Stop) as caught:
            ex.run(journal, Plane(), dict(steps))
        self.assertEqual("SCHEMA_EXEC_JOURNAL_AMBIGUOUS", str(caught.exception))
        self.assertNotIn("apply", [name for name, _ in steps.effects])
        self.assertEqual(1, [event for event in journal.log if event[0] == "FAILED"].__len__())
        self.assertEqual("POSTGATE_PASS", journal.phases()[-1])  # no terminal attempted after an ambiguous write

    def test_journal_failure_inside_the_schema_callee_is_ambiguous_and_nothing_is_retried(self):
        def schema(journal):
            journal("MIGRATION_STARTED")
            try:
                journal("SCHEMA_104")
            except Exception:
                try:
                    journal("PARTIAL_SCHEMA_STOP")  # an adapter-like callee that tries to record the failure
                except Exception:
                    pass
                raise RuntimeError("SCHEMA_ADAPTER_UNEXPECTED")
        journal, steps = build(Journal(fail_on="SCHEMA_104"), schema=schema)
        with self.assertRaises(ex.Stop) as caught:
            ex.run(journal, Plane(), dict(steps))
        self.assertEqual("SCHEMA_EXEC_JOURNAL_AMBIGUOUS", str(caught.exception))
        self.assertEqual(["RESERVED", "BACKUP_VERIFIED", "WRITES_QUIESCED", "MIGRATION_STARTED"], journal.phases())
        self.assertEqual(1, [event for event in journal.log if event[0] == "FAILED"].__len__())

    def test_api_web_failures_are_app_partial_and_only_an_explicit_applied_continues(self):
        bad = (lambda: (_ for _ in ()).throw(RuntimeError("compose secret")), lambda: {"terminal": "ROLLED_BACK"},
               lambda: {"terminal": "APPLIED", "extra": 1} and {"terminal": "applied"}, lambda: None, lambda: True)
        for apply in bad:
            journal, steps = build(apply_api_web=apply)
            with self.assertRaises(ex.Stop):
                ex.run(journal, Plane(), dict(steps))
            self.assertEqual("APP_PARTIAL_STOP", journal.phases()[-1])
            self.assertNotIn("APP_APPLIED", journal.phases())
            self.assertNotIn("uat", [name for name, _ in steps.effects])

    def test_uat_failure_stops_with_the_contract_terminal_and_never_claims_uat_pass(self):
        journal, steps = build(uat=lambda: False)
        with self.assertRaises(ex.Stop) as caught:
            ex.run(journal, Plane(), dict(steps))
        self.assertEqual("SCHEMA_EXEC_UAT_FAILED", str(caught.exception))
        self.assertNotIn("UAT_PASS", journal.phases())
        # The contract classifies an interruption after APP_APPLIED as UAT_STOP.
        self.assertEqual("UAT_STOP", journal.phases()[-1])
        self.assertEqual("APP_APPLIED", journal.phases()[-2])

    def test_lost_plane_stops_without_any_further_journal_write(self):
        journal, steps = build()
        clean = Plane()
        ex.run(journal, clean, dict(steps))
        self.assertGreater(clean.calls, 20)  # the plane is checked around every journal write and effect
        for allowed in range(0, clean.calls):
            journal, steps = build()
            plane = Plane(fail_after=allowed)
            with self.assertRaises(ex.Stop) as caught:
                ex.run(journal, plane, dict(steps))
            self.assertEqual("SCHEMA_EXEC_PLANE_LOST", str(caught.exception), allowed)
            self.assertNotIn("secret", repr(caught.exception))
            written = journal.phases()
            self.assertTrue(all(p not in ex.TERMINAL for p in written), allowed)  # nothing written blind
            self.assertEqual(plane.calls, allowed + 1)  # stopped at the first failed check, no further plane use

    def test_invalid_inputs_are_refused(self):
        journal, steps = build()
        for args in ((None, Plane(), dict(steps)), (journal, object(), dict(steps)), (journal, Plane(), {}),
                     (journal, Plane(), dict(steps, extra=lambda: None)),
                     (journal, Plane(), steps),
                     (journal, Plane(), dict(steps, uat="x"))):
            with self.assertRaises(ex.Stop) as caught:
                ex.run(*args)
            self.assertEqual("SCHEMA_EXEC_INPUT_INVALID", str(caught.exception))
        self.assertEqual([], journal.phases())

    def test_malformed_journal_record_is_ambiguous(self):
        for bad in (None, {}, {"phase": "RESERVED"}, {"phase": "BACKUP_VERIFIED", "failure_class_if_interrupted": "PRE_DDL_STOP"}):
            _, steps = build()
            with self.assertRaises(ex.Stop) as caught:
                ex.run(lambda phase, bad=bad: bad, Plane(), dict(steps))
            self.assertEqual("SCHEMA_EXEC_JOURNAL_AMBIGUOUS", str(caught.exception))
            self.assertEqual([], [name for name, _ in steps.effects if name != "preflight"])


class RealStoreIntegration(unittest.TestCase):
    """Sequencer + the REAL journal contract/store on a private directory (adapter replaced by a scripted callee)."""

    class NonceJournal:
        @staticmethod
        def _journal(root, uid):
            return os.open(root, os.O_RDONLY | os.O_DIRECTORY)

        @staticmethod
        def _write_all(fd, raw):
            view = memoryview(raw)
            while view:
                view = view[os.write(fd, view):]

    def run_cut(self, **override):
        with tempfile.TemporaryDirectory() as root:
            os.chmod(root, 0o700)
            plane = Plane()

            def journal(phase):
                return store.append(root, BIND, phase, uid=os.getuid(), plane=plane, contract=ct,
                                    nonce_journal=self.NonceJournal)
            steps = Steps(Journal(), **override)
            outcome = None
            try:
                outcome = ex.run(journal, plane, dict(steps))
            except ex.Stop as stop:
                outcome = stop
            names = sorted(os.listdir(os.path.join(root, BIND["nonce"])))
            phases = [json.loads(Path(root, BIND["nonce"], n).read_text())["phase"] for n in names]
            return outcome, phases, steps

    def test_full_cut_on_the_real_store(self):
        outcome, phases, _ = self.run_cut()
        self.assertEqual(ex.PHASES_OK, outcome["phases"])
        self.assertEqual(list(ex.PHASES_OK), phases)

    def test_failures_on_the_real_store_end_in_the_contract_terminal(self):
        outcome, phases, _ = self.run_cut(revalidate=lambda: False)
        self.assertEqual("SCHEMA_EXEC_REVALIDATION_FAILED", str(outcome))
        self.assertEqual("PARTIAL_SCHEMA_STOP", phases[-1])
        outcome, phases, _ = self.run_cut(apply_api_web=lambda: {"terminal": "FAILED"})
        self.assertEqual("APP_PARTIAL_STOP", phases[-1])
        outcome, phases, _ = self.run_cut(verify_backup=lambda: False)
        self.assertEqual(["RESERVED", "PRE_DDL_STOP"], phases)


if __name__ == "__main__":
    unittest.main()
