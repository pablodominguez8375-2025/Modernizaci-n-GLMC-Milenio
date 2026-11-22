"""Disposable rootless filesystem tests for QA525 phase persistence."""
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).resolve().parents[1] / "scripts/qa" / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


contract = load("schema_contract", "qa525-vnext-schema-cut-journal-contract.py")
store = load("schema_store", "qa525-vnext-schema-cut-journal-store.py")
nonce = load("nonce_port", "qa525-vnext-go-nonce-journal.py")
BINDING = {"nonce": "a" * 64, "go_sha256": "sha256:" + "b" * 64,
           "plan_sha256": "sha256:" + "c" * 64}


class Plane:
    held = True

    def assert_held(self):
        if not self.held:
            raise RuntimeError("lost")


class StoreTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        os.chmod(self.tmp.name, 0o700)
        self.plane = Plane()
        self.kw = dict(uid=os.geteuid(), plane=self.plane,
                       contract=contract, nonce_journal=nonce)
        self.flight = Path(self.tmp.name) / BINDING["nonce"]

    def append(self, phase):
        return store.append(self.tmp.name, BINDING, phase, **self.kw)

    def test_durable_order_and_terminal(self):
        for phase in contract.STEPS:
            result = self.append(phase)
            self.assertIs(result["may_apply"], False)
        self.assertEqual(9, len(list(self.flight.glob("*.event"))))
        with self.assertRaisesRegex(contract.Stop, "SCHEMA_JOURNAL_TERMINAL"):
            self.append("RESERVED")
        with self.assertRaisesRegex(contract.Stop, "SCHEMA_JOURNAL_TERMINAL"):
            self.append("UAT_PASS")

    def test_partial_ddl_class_and_no_preddl_rollback(self):
        for phase in contract.STEPS[:4]:
            self.append(phase)
        result = self.append("PARTIAL_SCHEMA_STOP")
        self.assertEqual("PARTIAL_SCHEMA_STOP", result["phase"])
        with self.assertRaisesRegex(contract.Stop, "SCHEMA_JOURNAL_TERMINAL"):
            self.append("SCHEMA_104")
        self.assertEqual(5, len(list(self.flight.glob("*.event"))))

    def test_lost_plane_and_corrupt_record_stop_without_new_event(self):
        self.plane.held = False
        with self.assertRaisesRegex(store.Stop, "SCHEMA_STORE_PLANE_LOST"):
            self.append("RESERVED")
        self.assertFalse(self.flight.exists())
        self.plane.held = True
        self.append("RESERVED")
        first = self.flight / "00000000.event"
        first.write_bytes(b"partial")
        with self.assertRaises(contract.Stop):
            self.append("BACKUP_VERIFIED")
        self.assertFalse((self.flight / "00000001.event").exists())

    def test_symlink_or_stray_entry_stops(self):
        self.append("RESERVED")
        (self.flight / "stray").write_text("x")
        with self.assertRaisesRegex(store.Stop, "SCHEMA_STORE_CONTENT_INVALID"):
            self.append("BACKUP_VERIFIED")
        (self.flight / "stray").unlink()
        (self.flight / "00000000.event").unlink()
        os.symlink("/dev/null", self.flight / "00000000.event")
        with self.assertRaisesRegex(store.Stop, "SCHEMA_STORE_EVENT_UNTRUSTED"):
            self.append("BACKUP_VERIFIED")


if __name__ == "__main__":
    unittest.main()
