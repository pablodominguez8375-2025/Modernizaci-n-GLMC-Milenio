"""Rootless schema-cut journal state and interruption contract."""
import importlib.util
import json
from pathlib import Path
import unittest

PATH = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-schema-cut-journal-contract.py"
spec = importlib.util.spec_from_file_location("qa525_schema_journal", PATH)
journal = importlib.util.module_from_spec(spec)
spec.loader.exec_module(journal)
BINDING = {"nonce": "a" * 64, "go_sha256": "sha256:" + "b" * 64,
           "plan_sha256": "sha256:" + "c" * 64}


def append(events, phase):
    result = journal.next_event(events, BINDING, phase)
    events.append(result["event"])
    return result


class SchemaJournalTests(unittest.TestCase):
    def test_complete_order_is_terminal_and_never_authorizes(self):
        events = []
        for phase in journal.STEPS:
            result = append(events, phase)
            self.assertIs(result["may_apply"], False)
        with self.assertRaisesRegex(journal.Stop, "SCHEMA_JOURNAL_TERMINAL"):
            append(events, "RESERVED")
        self.assertEqual(10, len(events))

    def test_failure_class_at_each_boundary(self):
        expected = {
            "RESERVED": "PRE_DDL_STOP", "BACKUP_VERIFIED": "PRE_DDL_STOP",
            "WRITES_QUIESCED": "PRE_DDL_STOP",
            "MIGRATION_STARTED": "PARTIAL_SCHEMA_STOP",
            "SCHEMA_104": "PARTIAL_SCHEMA_STOP",
            "GRANTS_VERIFIED": "PARTIAL_SCHEMA_STOP",
            "POSTGATE_PASS": "PARTIAL_SCHEMA_STOP",
            "APP_STARTED": "APP_PARTIAL_STOP",
            "APP_APPLIED": "UAT_STOP",
            "UAT_PASS": "UAT_STOP",
        }
        events = []
        for phase in journal.STEPS:
            result = append(events, phase)
            self.assertEqual(expected[phase], result["failure_class_if_interrupted"])
            if phase != "UAT_PASS":
                stopped = list(events)
                append(stopped, expected[phase])
                with self.assertRaisesRegex(journal.Stop, "SCHEMA_JOURNAL_TERMINAL"):
                    append(stopped, journal.STEPS[0])

    def test_out_of_order_replay_and_false_rollback_rejected(self):
        with self.assertRaisesRegex(journal.Stop, "SCHEMA_JOURNAL_ORDER_INVALID"):
            journal.next_event([], BINDING, "MIGRATION_STARTED")
        events = []
        append(events, "RESERVED")
        for phase in ("RESERVED", "MIGRATION_STARTED", "UAT_PASS", "PARTIAL_SCHEMA_STOP"):
            with self.subTest(phase=phase):
                with self.assertRaisesRegex(journal.Stop, "SCHEMA_JOURNAL_ORDER_INVALID"):
                    append(list(events), phase)
        append(events, "BACKUP_VERIFIED")
        append(events, "WRITES_QUIESCED")
        append(events, "MIGRATION_STARTED")
        with self.assertRaisesRegex(journal.Stop, "SCHEMA_JOURNAL_ORDER_INVALID"):
            append(events, "PRE_DDL_STOP")

    def test_uat_failure_cannot_be_mislabeled_as_partial_apply(self):
        events = []
        for phase in journal.STEPS[:-1]:
            append(events, phase)
        with self.assertRaisesRegex(journal.Stop, "SCHEMA_JOURNAL_ORDER_INVALID"):
            append(list(events), "APP_PARTIAL_STOP")
        append(events, "UAT_STOP")
        with self.assertRaisesRegex(journal.Stop, "SCHEMA_JOURNAL_TERMINAL"):
            append(events, "UAT_PASS")

    def test_binding_and_chain_tampering_rejected(self):
        events = []
        append(events, "RESERVED")
        append(events, "BACKUP_VERIFIED")
        tampered = json.loads(events[0])
        tampered["plan_sha256"] = "sha256:" + "d" * 64
        bad = json.dumps(tampered, sort_keys=True, separators=(",", ":")).encode() + b"\n"
        with self.assertRaisesRegex(journal.Stop, "SCHEMA_JOURNAL_EVENT_INVALID"):
            journal.next_event([bad, events[1]], BINDING, "WRITES_QUIESCED")
        changed = json.loads(events[1])
        changed["prev_sha256"] = "sha256:" + "f" * 64
        bad = json.dumps(changed, sort_keys=True, separators=(",", ":")).encode() + b"\n"
        with self.assertRaisesRegex(journal.Stop, "SCHEMA_JOURNAL_EVENT_INVALID"):
            journal.next_event([events[0], bad], BINDING, "WRITES_QUIESCED")
        with self.assertRaisesRegex(journal.Stop, "SCHEMA_JOURNAL_EVENT_INVALID"):
            journal.next_event(events, dict(BINDING, nonce="f" * 64), "WRITES_QUIESCED")


if __name__ == "__main__":
    unittest.main()
