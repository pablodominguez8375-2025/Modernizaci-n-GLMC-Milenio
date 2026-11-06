"""Offline safety/crash tests for QA525 durable stage snapshots. No host/CLI."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest

import test_qa525_vnext_journal as jt
import test_qa525_vnext_baseline_plan as bp

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/qa/qa525-vnext-stage-store.py"
spec = importlib.util.spec_from_file_location("qa525_stage_store_contract", SCRIPT)
store = importlib.util.module_from_spec(spec)
spec.loader.exec_module(store)


class DurableStageStoreTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = jt.JournalFixture(self.tmp.name)
        # Same import instance as stage_store.journal: a foreign Lease must fail.
        self.lease = store.journal.begin(self.fx.root, self.fx.admission,
                                         self.fx.digest, jt.NOW)
        self.plan = bp.make_plan(nonce=jt.NONCE, admission=self.fx.digest)

    def tearDown(self):
        self.lease.close()
        self.tmp.cleanup()

    def next(self, plan, stage):
        return bp.mod.advance(plan, stage, bp.evidence(plan, stage))

    def file(self, index):
        return self.fx.root / ("stage-" + jt.NONCE + "-%04d.json" % index)

    def test_initialize_is_fsynced_private_and_non_authorizing(self):
        status = store.initialize(self.lease, self.plan)
        self.assertEqual(status["status"], store.STATUS)
        self.assertEqual(status["stage"], "APPLYING")
        self.assertEqual(status["sequence"], 0)
        self.assertTrue(status["record_sha256"].startswith("sha256:"))
        for field in ("may_apply", "may_install", "signature_verified_here",
                      "external_runtime_verified"):
            self.assertIs(status[field], False)
        self.assertEqual(self.file(0).stat().st_mode & 0o777, 0o600)
        self.assertEqual(status, store.inspect(self.lease))
        record = json.loads(self.file(0).read_text())
        self.assertEqual(record["nonce"], jt.NONCE)
        self.assertEqual(record["admission_sha256"], self.fx.digest)
        self.assertIsNone(record["previous_record_sha256"])
        self.assertIs(record["may_apply"], False)

    def test_forward_chain_persists_every_intent_before_next_step(self):
        store.initialize(self.lease, self.plan)
        old_sha = store.inspect(self.lease)["record_sha256"]
        for index, stage in enumerate(("API_SWITCHING", "API_VERIFIED",
                                       "WEB_SWITCHING", "WEB_VERIFIED",
                                       "VERIFYING"), 1):
            self.plan = self.next(self.plan, stage)
            status = store.append(self.lease, self.plan)
            self.assertEqual(status["stage"], stage)
            self.assertEqual(status["sequence"], index)
            persisted = json.loads(self.file(index).read_text())
            self.assertEqual(persisted["previous_record_sha256"], old_sha)
            self.assertEqual(persisted["plan"]["chain_sha256"],
                             self.plan["chain_sha256"])
            old_sha = status["record_sha256"]
        self.assertFalse(any(f.name.startswith("terminal-")
                             for f in self.fx.root.iterdir()))
        self.assertNotIn("APPLIED", [v["stage"] for v in
                         (json.loads(self.file(i).read_text()) for i in range(6))])
        self.assertFalse(status["may_apply"])

    def test_rollback_intent_and_verification_are_distinct_durable_steps(self):
        store.initialize(self.lease, self.plan)
        for stage in ("API_SWITCHING", "ROLLING_BACK", "ROLLBACK_VERIFYING"):
            self.plan = self.next(self.plan, stage)
            self.assertEqual(store.append(self.lease, self.plan)["stage"], stage)
        with self.assertRaises(store.Stop):
            store.append(self.lease, self.plan)
        self.assertEqual(len(list(self.fx.root.glob("stage-*.json"))), 4)
        self.assertFalse(list(self.fx.root.glob("terminal-*.json")))

    def test_reject_skip_replay_and_uninitialized_advance(self):
        with self.assertRaisesRegex(store.Stop, "^STAGE_NOT_INITIALIZED$"):
            store.inspect(self.lease)
        first = self.next(self.plan, "API_SWITCHING")
        with self.assertRaisesRegex(store.Stop, "^STAGE_NOT_INITIALIZED$"):
            store.append(self.lease, first)
        store.initialize(self.lease, self.plan)
        with self.assertRaisesRegex(store.Stop, "^STAGE_ALREADY_INITIALIZED$"):
            store.initialize(self.lease, self.plan)
        with self.assertRaisesRegex(store.Stop, "^STAGE_TRANSITION_MISMATCH$"):
            store.append(self.lease, self.plan)
        skipped = self.next(first, "API_VERIFIED")
        with self.assertRaisesRegex(store.Stop, "^STAGE_TRANSITION_MISMATCH$"):
            store.append(self.lease, skipped)
        store.append(self.lease, first)
        with self.assertRaisesRegex(store.Stop, "^STAGE_TRANSITION_MISMATCH$"):
            store.append(self.lease, first)

    def test_wrong_nonce_admission_cut_and_modified_baseline_fail_closed(self):
        foreign = bp.make_plan(nonce="x" * 64, admission=self.fx.digest)
        with self.assertRaisesRegex(store.Stop, "^STAGE_INITIAL_MISMATCH$"):
            store.initialize(self.lease, foreign)
        foreign = bp.make_plan(nonce=jt.NONCE,
                               admission="sha256:" + "f" * 64)
        with self.assertRaisesRegex(store.Stop, "^STAGE_INITIAL_MISMATCH$"):
            store.initialize(self.lease, foreign)
        store.initialize(self.lease, self.plan)
        first = self.next(self.plan, "API_SWITCHING")
        for which in ("nonce", "admission_sha256", "baseline_sha256",
                      "target_sha256"):
            with self.subTest(which=which):
                bad = copy.deepcopy(first)
                bad[which] = ("x" * 64 if which == "nonce" else
                              "sha256:" + "f" * 64)
                with self.assertRaises(store.Stop):
                    store.append(self.lease, bad)
        changed = copy.deepcopy(first)
        changed["baseline"]["web_sha"] = "c" * 40
        with self.assertRaises(store.Stop):
            store.append(self.lease, changed)

    def test_closed_fabricated_foreign_and_swapped_leases_are_rejected(self):
        store.initialize(self.lease, self.plan)
        fake = store.journal.Lease(self.lease.fd, self.lease.root, self.lease.nonce)
        with self.assertRaisesRegex(store.Stop, "^STAGE_LEASE_NOT_HELD$"):
            store.inspect(fake)
        with self.assertRaisesRegex(store.Stop, "^STAGE_LEASE_NOT_HELD$"):
            store.append(object(), self.next(self.plan, "API_SWITCHING"))
        with tempfile.TemporaryDirectory() as other:
            foreign_fx = jt.JournalFixture(other)
            foreign = foreign_fx.begin()  # different module instance
            try:
                with self.assertRaisesRegex(store.Stop, "^STAGE_LEASE_NOT_HELD$"):
                    store.inspect(foreign)
            finally:
                foreign.close()
        spare = os.open(os.devnull, os.O_RDONLY)
        original_fd = self.lease.fd
        try:
            self.lease.fd = spare
            with self.assertRaisesRegex(store.Stop, "^STAGE_LEASE_NOT_HELD$"):
                store.inspect(self.lease)
        finally:
            self.lease.fd = original_fd
            os.close(spare)
        self.lease.close()
        with self.assertRaisesRegex(store.Stop, "^STAGE_LEASE_NOT_HELD$"):
            store.inspect(self.lease)

    def test_snapshot_gap_and_foreign_nonce_block_recovery(self):
        store.initialize(self.lease, self.plan)
        self.plan = self.next(self.plan, "API_SWITCHING")
        store.append(self.lease, self.plan)
        self.file(0).unlink()
        with self.assertRaisesRegex(store.Stop, "^STAGE_CHAIN_GAP$"):
            store.inspect(self.lease)

    def test_foreign_stages_or_unknown_suffix_block_new_flight(self):
        path = self.fx.root / ("stage-" + "y" * 64 + "-0000.json")
        path.write_text("{}")
        path.chmod(0o600)
        with self.assertRaisesRegex(store.Stop, "^STAGE_FOREIGN_NONCE$"):
            store.initialize(self.lease, self.plan)
        path.unlink()
        junk = self.fx.root / ("stage-" + jt.NONCE + "-bad.json")
        junk.write_text("{}")
        junk.chmod(0o600)
        with self.assertRaisesRegex(store.Stop, "^STAGE_UNRECOGNIZED_ENTRY$"):
            store.initialize(self.lease, self.plan)

    def test_noncanonical_duplicate_and_tampered_record_are_fail_closed(self):
        store.initialize(self.lease, self.plan)
        path = self.file(0)
        original = path.read_bytes()
        for payload in (original[:-1], original + b" ", b'{"schema":"a","schema":"b"}\n',
                        b'{"nonfinite":1e999}\n', b"[]\n"):
            with self.subTest(payload=payload[:25]):
                path.write_bytes(payload)
                path.chmod(0o600)
                with self.assertRaises(store.Stop):
                    store.inspect(self.lease)
        path.write_bytes(original)
        self.assertEqual(store.inspect(self.lease)["stage"], "APPLYING")
        data = json.loads(original)
        data["may_apply"] = True
        path.write_text(json.dumps(data, sort_keys=True,
                                   separators=(",", ":")) + "\n")
        path.chmod(0o600)
        with self.assertRaisesRegex(store.Stop, "^STAGE_AUTHORITY_FORGED$"):
            store.inspect(self.lease)

    def test_symlink_and_hardlink_record_are_not_trusted(self):
        store.initialize(self.lease, self.plan)
        path = self.file(0)
        alternate = self.fx.root / "private-copy"
        alternate.write_bytes(path.read_bytes())
        alternate.chmod(0o600)
        path.unlink()
        path.symlink_to(alternate)
        with self.assertRaisesRegex(store.Stop, "^STAGE_UNTRUSTED_SNAPSHOT$"):
            store.inspect(self.lease)
        path.unlink()
        os.link(alternate, path)
        with self.assertRaisesRegex(store.Stop, "^STAGE_UNTRUSTED_SNAPSHOT$"):
            store.inspect(self.lease)

    def test_crash_between_temp_and_link_never_claims_committed_stage(self):
        for phase in ("after_journal_temp_fsync", "after_journal_link"):
            with self.subTest(phase=phase), tempfile.TemporaryDirectory() as td:
                fx = jt.JournalFixture(td)
                lease = store.journal.begin(fx.root, fx.admission, fx.digest, jt.NOW)
                try:
                    base = bp.make_plan(nonce=jt.NONCE, admission=fx.digest)
                    def explode(event):
                        if event == phase:
                            raise SystemExit("synthetic SIGKILL boundary")
                    with self.assertRaises(SystemExit):
                        store.initialize(lease, base, checkpoint=explode)
                    self.assertEqual(len(list(fx.root.glob(".qa525-next-*"))), 1)
                    with self.assertRaisesRegex(store.Stop, "^STAGE_CRASH_RESIDUE$"):
                        store.inspect(lease)
                    self.assertFalse(list(fx.root.glob("terminal-*.json")))
                finally:
                    lease.close()

    def test_interrupt_after_directory_fsync_leaves_valid_record(self):
        def explode(event):
            if event == "after_journal_dirsync":
                raise SystemExit("synthetic post-commit stop")
        with self.assertRaises(SystemExit):
            store.initialize(self.lease, self.plan, checkpoint=explode)
        self.assertEqual(store.inspect(self.lease)["sequence"], 0)

    def test_reconciled_journal_rejects_more_stage_writes(self):
        store.initialize(self.lease, self.plan)
        self.lease.close()
        self.assertEqual(store.journal.reconcile_after_crash(self.fx.root,
                                                              jt.NONCE, jt.NOW),
                         "RECOVERY_REQUIRED")
        with self.assertRaisesRegex(store.Stop, "^STAGE_LEASE_NOT_HELD$"):
            store.inspect(self.lease)

    def test_ci_readiness_is_triggered_for_new_vnext_modules_and_suites(self):
        workflow = (ROOT / ".github/workflows/qa-auto-agent-readiness.yml").read_text()
        self.assertEqual(workflow.count("'scripts/qa/qa525-vnext-*.py'"), 2)
        self.assertEqual(workflow.count("'tests/test_qa525_vnext_*.py'"), 2)
        self.assertIn("unittest discover -s tests -p test_qa_agent_readiness.py",
                      workflow)

    def test_module_is_library_only_without_execute_or_authorization(self):
        raw = SCRIPT.read_text()
        for absent in ("import subprocess", "import socket", "docker compose",
                       "os.system(", "urllib", "requests.", "sudo ",
                       "def apply(", "def install("):
            self.assertNotIn(absent, raw)
        self.assertIn("NO-APPLY", raw)
        self.assertIn("STAGE_STORE_LIBRARY_ONLY_NO_EXECUTION", raw)


if __name__ == "__main__":
    unittest.main()
