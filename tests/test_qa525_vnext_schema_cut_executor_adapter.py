"""Sequencer + adapter (#1074) + REAL journal contract/store: scripted docker/psql runner, rootless and synthetic."""
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest

HERE = Path(__file__).resolve().parent


def load(name, relative):
    spec = importlib.util.spec_from_file_location(name, HERE.parent / "scripts" / "qa" / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ex = load("cut_executor_i", "qa525-vnext-schema-cut-executor.py")
ct = load("cut_contract_i", "qa525-vnext-schema-cut-journal-contract.py")
store = load("cut_store_i", "qa525-vnext-schema-cut-journal-store.py")
adapter_tests = load("adapter_tests_i", "../../tests/test_qa525_vnext_schema_adapter.py")
ad = adapter_tests.ad
BIND = {"nonce": "c" * 64, "go_sha256": "sha256:" + "3" * 64, "plan_sha256": "sha256:" + "4" * 64}


class Plane:
    def assert_held(self):
        pass


class NonceJournal:
    @staticmethod
    def _journal(root, uid):
        return os.open(root, os.O_RDONLY | os.O_DIRECTORY)

    @staticmethod
    def _write_all(fd, raw):
        view = memoryview(raw)
        while view:
            view = view[os.write(fd, view):]


def cut(**answers):
    params = adapter_tests.params()
    fake = adapter_tests.Fake(params, **answers)
    with tempfile.TemporaryDirectory() as root:
        os.chmod(root, 0o700)
        plane = Plane()

        def journal(phase):
            return store.append(root, BIND, phase, uid=os.getuid(), plane=plane, contract=ct,
                                nonce_journal=NonceJournal)
        steps = {
            "preflight": lambda: None, "verify_backup": lambda: True, "quiesce_writes": lambda: True,
            "schema": lambda j: ad.run_phases(j, fake, params),
            "revalidate": lambda: True, "apply_api_web": lambda: {"terminal": "APPLIED"}, "uat": lambda: True,
        }
        try:
            outcome = ex.run(journal, plane, steps)
        except ex.Stop as stop:
            outcome = stop
        folder = Path(root, BIND["nonce"])
        phases = [json.loads(p.read_text())["phase"] for p in sorted(folder.iterdir())]
    return outcome, phases, fake


class SequencerWithAdapter(unittest.TestCase):
    def test_full_cut_through_the_adapter_and_the_real_store(self):
        outcome, phases, fake = cut()
        self.assertEqual(ex.PHASES_OK, outcome["phases"])
        self.assertEqual(list(ex.PHASES_OK), phases)
        self.assertEqual(["image_id", "migrate", "history", "grants", "acl", "gate"], fake.calls)

    def test_wrong_image_id_is_a_pre_ddl_stop_and_no_ddl_runs(self):
        outcome, phases, fake = cut(image_id=(0, b"sha256:" + b"0" * 64 + b"\n"))
        self.assertEqual("SCHEMA_EXEC_SCHEMA_FAILED", str(outcome))
        self.assertEqual("SCHEMA_ADAPTER_IMAGE_ID_MISMATCH", outcome.cause)
        self.assertEqual(["RESERVED", "BACKUP_VERIFIED", "WRITES_QUIESCED", "PRE_DDL_STOP"], phases)
        self.assertNotIn("migrate", fake.calls)

    def test_failed_migration_ends_in_one_partial_schema_stop_without_retry(self):
        outcome, phases, fake = cut(migrate=(2, b'{"status":"WORKSHOPS104_MIGRATION_BLOCKED_RECONCILE_REQUIRED"}\n'))
        self.assertEqual("SCHEMA_ADAPTER_MIGRATION_FAILED", outcome.cause)
        self.assertEqual(["RESERVED", "BACKUP_VERIFIED", "WRITES_QUIESCED", "MIGRATION_STARTED",
                          "PARTIAL_SCHEMA_STOP"], phases)
        self.assertEqual(1, fake.calls.count("migrate"))

    def test_gate_failure_after_grants_is_partial_and_api_web_never_starts(self):
        outcome, phases, _ = cut(gate=(0, b"nope\n"))
        self.assertEqual("SCHEMA_ADAPTER_GATE_FAILED", outcome.cause)
        self.assertEqual("PARTIAL_SCHEMA_STOP", phases[-1])
        self.assertNotIn("APP_STARTED", phases)


if __name__ == "__main__":
    unittest.main()
