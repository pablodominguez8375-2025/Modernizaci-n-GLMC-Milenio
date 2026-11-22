"""Offline schema child wiring: no root, Docker, signature or physical QA."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def module(name):
    spec = importlib.util.spec_from_file_location(name.replace("-", "_"), BASE / name)
    item = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(item)
    return item


child = module("qa525-vnext-root-schema-child.py")
store = module("qa525-vnext-schema-cut-journal-store.py")
contract = module("qa525-vnext-schema-cut-journal-contract.py")
nonce_journal = module("qa525-vnext-go-nonce-journal.py")


class ExecutorPort:
    """Port double until #1076's executor is rebased onto this PR."""
    STEPS = frozenset({"preflight", "verify_backup", "quiesce_writes", "schema",
                       "revalidate", "apply_api_web", "uat"})
    SCHEMA_SEQUENCE = ("MIGRATION_STARTED", "SCHEMA_104", "GRANTS_VERIFIED", "POSTGATE_PASS")
    PHASES_OK = contract.STEPS

    @staticmethod
    def run(journal, plane, steps):
        plane.assert_held()
        assert steps["preflight"]() is None
        journal("RESERVED")
        assert steps["verify_backup"]() is True
        journal("BACKUP_VERIFIED")
        assert steps["quiesce_writes"]() is True
        journal("WRITES_QUIESCED")
        steps["schema"](journal)
        assert steps["revalidate"]() is True
        journal("APP_STARTED")
        assert steps["apply_api_web"]() == {"terminal": "APPLIED"}
        journal("APP_APPLIED")
        assert steps["uat"]() is True
        journal("UAT_PASS")
        return {"phases": contract.STEPS}


executor = ExecutorPort
PAYLOAD = b"authenticated inner archive"
D = lambda value: "sha256:" + hashlib.sha256(value).hexdigest()
CLAIMS = {"purpose": "SCHEMA_API_WEB_QA_ONLY", "nonce": "a" * 64,
          "app_source_sha": "b" * 40, "app_archive_sha256": D(PAYLOAD),
          "go_sha256": D(b"go"), "plan_sha256": D(b"plan"), "expires_at": 1800000000}


class Plane:
    def assert_held(self):
        pass


class Host:
    def __init__(self):
        self.calls = 0

    def build_steps(self, **kwargs):
        self.calls += 1
        assert kwargs["archive_bytes"] == PAYLOAD
        return {"preflight": lambda: None, "verify_backup": lambda: True,
                "quiesce_writes": lambda: True,
                "schema": lambda journal: [journal(p) for p in executor.SCHEMA_SEQUENCE],
                "revalidate": lambda: True,
                "apply_api_web": lambda: {"terminal": "APPLIED"}, "uat": lambda: True}


class SchemaRootChildJournalTests(unittest.TestCase):
    def exercise_child(self, archive=PAYLOAD, claims=None, host=None):
        with tempfile.TemporaryDirectory() as directory:
            os.chmod(directory, 0o700)
            fd = os.memfd_create("schema-child")
            try:
                os.write(fd, archive)
                host = host or Host()
                result = child.act_for(
                    claims=dict(CLAIMS if claims is None else claims), fd=fd, plane=Plane(),
                    journal_root=directory, uid=os.getuid(), host=host, executor=executor,
                    store=store, contract=contract, nonce_journal=nonce_journal,
                    plan_bytes=b"plan")
                events = sorted((Path(directory) / CLAIMS["nonce"]).glob("*.event"))
                phases = [json.loads(path.read_text())["phase"] for path in events]
                return result, phases, host.calls
            finally:
                os.close(fd)

    def test_executes_each_phase_through_real_durable_store(self):
        result, phases, calls = self.exercise_child()
        self.assertEqual(list(executor.PHASES_OK), phases)
        self.assertEqual(executor.PHASES_OK, result["phases"])
        self.assertEqual(1, calls)

    def test_mismatched_archive_stops_before_host_and_journal(self):
        host = Host()
        with self.assertRaises(child.Stop) as raised:
            self.exercise_child(archive=b"different", host=host)
        self.assertEqual("SCHEMA_CHILD_ARCHIVE_MISMATCH", str(raised.exception))
        self.assertEqual(0, host.calls)

    def test_legacy_scope_cannot_enter_schema_child(self):
        with self.assertRaises(child.Stop) as raised:
            self.exercise_child(claims=dict(CLAIMS, purpose="API_WEB_QA_ONLY"))
        self.assertEqual("SCHEMA_CHILD_BINDING_INVALID", str(raised.exception))


if __name__ == "__main__":
    unittest.main()
