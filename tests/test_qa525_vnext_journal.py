"""Offline crash matrix for QA525 durable journal, no host/network/Docker."""
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import socket
import stat
import tempfile
import unittest
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "qa" / "qa525-vnext-journal.py"
spec = importlib.util.spec_from_file_location("qa525_vnext_journal", SCRIPT)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

NOW = dt.datetime(2026, 11, 5, 13, 0, tzinfo=dt.timezone.utc)
NONCE = "qa525-vnext-journal-nonce-20261105"


def fake_admission():
    return {
        "schema": "qa525-admission-v2", "status": "ADMITTED",
        "nonce_consumed": False, "nonce_consume_transition": "APPLYING",
        "network_allowed": False, "nonce": NONCE,
        "ops_source_sha": "a" * 40, "app_source_sha": "b" * 40,
        "policy_hash": "sha256:" + "c" * 64,
        "artifact_sha256": "sha256:" + "d" * 64,
        "signed_installable_sha256": "sha256:" + "e" * 64,
        "artifact_id": 4406, "silo_workflow_run": 5507,
        "admitted_at": "2026-11-05T12:55:00Z",
        "valid_until": "2026-11-05T13:25:00Z",
        "g6_expires_at": "2026-11-06T10:00:00Z",
    }


class JournalFixture:
    def __init__(self, path):
        self.root = Path(path) / "authoritative"
        self.root.mkdir(mode=0o700)
        self.root.chmod(0o700)
        self.admission = Path(path) / "admission.json"
        self.put(fake_admission())

    def put(self, obj):
        self.admission.write_text(json.dumps(obj, sort_keys=True) + "\n")
        self.admission.chmod(0o600)
        self.digest = "sha256:" + hashlib.sha256(self.admission.read_bytes()).hexdigest()

    def begin(self, now=NOW, callback=None):
        return mod.begin(self.root, self.admission, self.digest, now, callback)


class DurableJournalContract(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = JournalFixture(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def test_begin_is_durable_and_never_claims_applied(self):
        with self.fx.begin() as lease:
            self.assertEqual(lease.nonce, NONCE)
            self.assertEqual(mod.inspect(self.fx.root, NONCE), "APPLYING")
            marker = self.fx.root / ("nonce-consumed-" + NONCE + ".json")
            journal = self.fx.root / ("journal-" + NONCE + ".json")
            self.assertEqual(stat.S_IMODE(marker.stat().st_mode), 0o600)
            self.assertEqual(stat.S_IMODE(journal.stat().st_mode), 0o600)
            m = json.loads(marker.read_text())
            self.assertEqual(m["journal_path"], str(journal))
            self.assertEqual(m["admission_sha256"], self.fx.digest)
            j = json.loads(journal.read_text())
            self.assertEqual(j["terminal_verification"], "REQUIRED_EXTERNAL_VERIFIER")
            self.assertNotIn("APPLIED", j.values())
        with self.assertRaisesRegex(mod.Stop, "NONCE_ALREADY_CONSUMED"):
            self.fx.begin()

    def test_live_session_blocks_reconcile_and_second_begin(self):
        with self.fx.begin():
            with self.assertRaisesRegex(mod.Stop, "JOURNAL_OPERATION_ACTIVE"):
                mod.reconcile_after_crash(self.fx.root, NONCE, NOW)
            with self.assertRaisesRegex(mod.Stop, "JOURNAL_OPERATION_ACTIVE"):
                self.fx.begin()
        self.assertEqual(mod.reconcile_after_crash(self.fx.root, NONCE, NOW), "RECOVERY_REQUIRED")
        self.assertEqual(mod.inspect(self.fx.root, NONCE), "RECOVERY_REQUIRED")
        self.assertEqual(mod.reconcile_after_crash(self.fx.root, NONCE, NOW), "RECOVERY_REQUIRED")

    def test_crash_injection_matrix_consumes_nonce_at_most_once(self):
        phases = (
            "before_nonce", "after_nonce_fsync", "after_nonce_dirsync",
            "before_journal", "after_journal_temp_fsync",
            "after_journal_replace", "after_journal_dirsync",
        )
        for phase in phases:
            with self.subTest(phase=phase), tempfile.TemporaryDirectory() as directory:
                fx = JournalFixture(directory)
                def inject(name):
                    if name == phase:
                        raise SystemExit("synthetic abrupt exit")
                with self.assertRaises(SystemExit):
                    fx.begin(callback=inject)
                before = mod.inspect(fx.root, NONCE)
                if phase == "before_nonce":
                    self.assertEqual(before, "NOT_STARTED")
                    self.assertEqual(mod.reconcile_after_crash(fx.root, NONCE, NOW), "NOT_STARTED")
                else:
                    self.assertIn(before, ("MARKER_ONLY_RECOVERY_REQUIRED", "APPLYING"))
                    self.assertEqual(mod.reconcile_after_crash(fx.root, NONCE, NOW), "RECOVERY_REQUIRED")
                    with self.assertRaisesRegex(mod.Stop, "NONCE_ALREADY_CONSUMED"):
                        fx.begin()
                leftovers = list(fx.root.glob(".qa525-next-*"))
                if phase == "after_journal_temp_fsync":
                    self.assertEqual(len(leftovers), 1)
                else:
                    self.assertEqual(len(leftovers), 0)

    def test_reconcile_never_deletes_orphaned_temp(self):
        def boom(phase):
            if phase == "after_journal_temp_fsync":
                raise SystemExit()
        with self.assertRaises(SystemExit):
            self.fx.begin(callback=boom)
        abandoned = next(self.fx.root.glob(".qa525-next-*"))
        previous = abandoned.read_bytes()
        self.assertEqual(mod.reconcile_after_crash(self.fx.root, NONCE, NOW), "RECOVERY_REQUIRED")
        self.assertEqual(abandoned.read_bytes(), previous)

    def test_bad_digest_and_expired_admission_cannot_reserve_nonce(self):
        with self.assertRaisesRegex(mod.Stop, "ADMISSION_HASH_MISMATCH"):
            mod.begin(self.fx.root, self.fx.admission, "sha256:" + "0"*64, NOW)
        self.assertEqual(mod.inspect(self.fx.root, NONCE), "NOT_STARTED")
        with self.assertRaisesRegex(mod.Stop, "ADMISSION_EXPIRED"):
            self.fx.begin(NOW + dt.timedelta(minutes=26))
        self.assertEqual(mod.inspect(self.fx.root, NONCE), "NOT_STARTED")

    def test_untrusted_root_and_symlink_do_not_mutate(self):
        root = self.fx.root
        root.chmod(0o755)
        with self.assertRaisesRegex(mod.Stop, "JOURNAL_ROOT_UNTRUSTED"):
            self.fx.begin()
        root.chmod(0o700)
        link = Path(self.tmp.name) / "unsafe-root"
        link.symlink_to(root, target_is_directory=True)
        with self.assertRaisesRegex(mod.Stop, "JOURNAL_ROOT_UNTRUSTED"):
            mod.begin(link, self.fx.admission, self.fx.digest, NOW)
        self.assertEqual(mod.inspect(root, NONCE), "NOT_STARTED")

    def test_bad_receipt_mode_and_symlink_rejected(self):
        self.fx.admission.chmod(0o644)
        with self.assertRaisesRegex(mod.Stop, "JOURNAL_FILE_UNTRUSTED"):
            self.fx.begin()
        self.fx.admission.chmod(0o600)
        link = Path(self.tmp.name) / "admission-link.json"
        link.symlink_to(self.fx.admission)
        with self.assertRaisesRegex(mod.Stop, "JOURNAL_FILE_UNAVAILABLE"):
            mod.begin(self.fx.root, link, self.fx.digest, NOW)

    def test_network_attempts_are_absent_from_begin_reconcile(self):
        with mock.patch.object(socket.socket, "connect", side_effect=AssertionError("network forbidden")), \
             mock.patch.object(socket, "create_connection", side_effect=AssertionError("network forbidden")):
            with self.fx.begin():
                self.assertEqual(mod.inspect(self.fx.root, NONCE), "APPLYING")
            self.assertEqual(mod.reconcile_after_crash(self.fx.root, NONCE, NOW), "RECOVERY_REQUIRED")

    def test_recovery_required_does_not_reuse_or_reset_nonce(self):
        with self.fx.begin():
            pass
        mod.reconcile_after_crash(self.fx.root, NONCE, NOW)
        record = json.loads((self.fx.root / ("recovery-" + NONCE + ".json")).read_text())
        self.assertFalse(record["external_verified_rollback"])
        self.assertFalse(record["retry_allowed"])
        self.assertTrue((self.fx.root / ("nonce-consumed-" + NONCE + ".json")).exists())
        with self.assertRaisesRegex(mod.Stop, "NONCE_ALREADY_CONSUMED"):
            self.fx.begin()

    def test_no_runtime_apply_or_success_api_exported(self):
        self.assertFalse(hasattr(mod, "apply"))
        self.assertFalse(hasattr(mod, "mark_applied"))
        self.assertFalse(hasattr(mod, "mark_rolled_back"))
        self.assertFalse(hasattr(mod, "main"))


if __name__ == "__main__":
    unittest.main()
