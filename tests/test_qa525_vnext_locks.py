"""Offline lock-plane regression; synthetic private tempfile paths only."""
import fcntl
import importlib.util
import os
from pathlib import Path
import stat
import tempfile
import unittest
from unittest import mock

import test_qa525_vnext_journal as jt

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "scripts/qa/qa525-vnext-locks.py"
spec = importlib.util.spec_from_file_location("qa525_vnext_synthetic_lock_plane", SOURCE)
plane = importlib.util.module_from_spec(spec)
spec.loader.exec_module(plane)


class LockPlaneTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = jt.JournalFixture(self.tmp.name)
        self.dir = Path(self.tmp.name) / "locks"
        self.dir.mkdir(mode=0o700)
        self.dir.chmod(0o700)
        for name in plane.SYNTHETIC_NAMES:
            item = self.dir / name
            with item.open("xb"):
                pass
            item.chmod(0o600)

    def tearDown(self):
        self.tmp.cleanup()

    def attempt(self, journal=jt.mod):
        return plane.synthetic_flight(
            journal, self.fx.root, self.fx.admission, self.fx.digest,
            jt.NOW, synthetic_lock_dir=self.dir)

    def lockfile(self, index):
        return self.dir / plane.SYNTHETIC_NAMES[index]

    def test_five_in_exact_order_then_root_lease_all_held(self):
        observed = []
        original = plane._lock_one
        def tracked(path):
            observed.append(path.name)
            return original(path)
        with mock.patch.object(plane, "_lock_one", side_effect=tracked):
            with self.attempt() as flight:
                status = flight.assert_held()
                self.assertEqual(observed, list(plane.SYNTHETIC_NAMES))
                self.assertEqual(status["legacy_locks_held"], 5)
                self.assertTrue(status["root_lease_held_synthetically"])
                for key in ("may_apply", "may_install",
                            "signature_verified_here", "physical_runtime_verified",
                            "legacy_entrypoint_disabled_verified"):
                    self.assertIs(status[key], False)
                self.assertTrue(jt.mod.lease_is_held(flight.lease))
                self.assertEqual(jt.mod.inspect(self.fx.root, jt.NONCE), "APPLYING")
        with self.assertRaisesRegex(plane.Stop, "^PLANE_CLOSED$"):
            flight.assert_held()

    def test_contention_at_each_lock_denies_before_nonce_consumed(self):
        for index in range(5):
            with self.subTest(index=index):
                fd = os.open(self.lockfile(index), os.O_RDONLY | os.O_NOFOLLOW)
                try:
                    fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
                    with self.assertRaisesRegex(plane.Stop, "^PLANE_LOCK_BUSY$"):
                        with self.attempt():
                            self.fail("acquired a competing lock")
                    self.assertEqual(jt.mod.inspect(self.fx.root, jt.NONCE),
                                     "NOT_STARTED")
                finally:
                    os.close(fd)

    def test_missing_file_is_not_created_and_no_nonce_written(self):
        path = self.lockfile(3)
        path.unlink()
        with self.assertRaisesRegex(plane.Stop, "^PLANE_LOCK_MISSING$"):
            with self.attempt():
                self.fail("missing lock was accepted")
        self.assertFalse(path.exists())
        self.assertEqual(jt.mod.inspect(self.fx.root, jt.NONCE), "NOT_STARTED")

    def test_mode_or_directory_drift_fails_before_journal(self):
        path = self.lockfile(2)
        path.chmod(0o644)
        with self.assertRaisesRegex(plane.Stop, "^PLANE_LOCK_UNTRUSTED$"):
            with self.attempt():
                self.fail("world-readable lock accepted")
        path.chmod(0o600)
        self.dir.chmod(0o755)
        with self.assertRaisesRegex(plane.Stop, "^PLANE_SYNTHETIC_DIR_UNTRUSTED$"):
            with self.attempt():
                self.fail("untrusted lock dir accepted")
        self.dir.chmod(0o700)
        self.assertEqual(jt.mod.inspect(self.fx.root, jt.NONCE), "NOT_STARTED")

    def test_foreign_root_or_admission_denied_before_journal_begin(self):
        journal = mock.Mock()
        journal.begin.side_effect = AssertionError("journal.begin called")
        with tempfile.TemporaryDirectory() as foreign:
            foreign_root = Path(foreign) / "authoritative"
            foreign_root.mkdir(mode=0o700)
            foreign_root.chmod(0o700)
            with self.assertRaisesRegex(plane.Stop, "^PLANE_SYNTHETIC_SCOPE_INVALID$"):
                with self.attempt(journal=journal)._recreate_cm():
                    self.fail("foreign journal root accepted")
            foreign_admission = Path(foreign) / "admission.json"
            foreign_admission.write_bytes(self.fx.admission.read_bytes())
            foreign_admission.chmod(0o600)
            with self.assertRaisesRegex(plane.Stop, "^PLANE_SYNTHETIC_SCOPE_INVALID$"):
                with plane.synthetic_flight(
                        journal, self.fx.root, foreign_admission, self.fx.digest,
                        jt.NOW, synthetic_lock_dir=self.dir):
                    self.fail("foreign admission accepted")
        journal.begin.assert_not_called()
        self.assertEqual(jt.mod.inspect(self.fx.root, jt.NONCE), "NOT_STARTED")

    def test_symlink_and_hardlink_lock_file_denied(self):
        p = self.lockfile(0)
        copy = self.dir / "real-lock-private"
        p.rename(copy)
        p.symlink_to(copy)
        with self.assertRaisesRegex(plane.Stop, "^PLANE_LOCK_UNTRUSTED$"):
            with self.attempt():
                self.fail("symlink lock accepted")
        p.unlink()
        os.link(copy, p)
        with self.assertRaisesRegex(plane.Stop, "^PLANE_LOCK_UNTRUSTED$"):
            with self.attempt():
                self.fail("hardlinked lock accepted")
        self.assertEqual(jt.mod.inspect(self.fx.root, jt.NONCE), "NOT_STARTED")

    def test_inode_substitution_detected_after_acquisition(self):
        with self.attempt() as flight:
            p = self.lockfile(1)
            p.unlink()
            with p.open("xb"):
                pass
            p.chmod(0o600)
            with self.assertRaisesRegex(plane.Stop, "^PLANE_LOCK_LOST$"):
                flight.assert_held()

    def test_closed_fd_is_not_treated_as_lock_authority(self):
        with self.attempt() as flight:
            fd = flight._entries[0][1]
            os.close(fd)
            with self.assertRaisesRegex(plane.Stop, "^PLANE_LOCK_LOST$"):
                flight.assert_held()

    def test_closed_journal_lease_fails_closed(self):
        with self.attempt() as flight:
            flight.lease.close()
            with self.assertRaisesRegex(plane.Stop, "^PLANE_JOURNAL_LEASE_LOST$"):
                flight.assert_held()

    def test_root_lease_contention_releases_all_legacy_locks(self):
        with jt.mod.begin(self.fx.root, self.fx.admission, self.fx.digest, jt.NOW):
            with self.assertRaisesRegex(plane.Stop, "^PLANE_JOURNAL_BEGIN_DENIED$"):
                with self.attempt():
                    self.fail("second root lease accepted")
            # Failure to obtain the root lock must release all legacy fds.
            for name in plane.SYNTHETIC_NAMES:
                fd = os.open(self.dir / name, os.O_RDONLY | os.O_NOFOLLOW)
                try:
                    fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
                finally:
                    os.close(fd)

    def test_foreign_module_without_lease_checks_rejected(self):
        with self.assertRaisesRegex(plane.Stop, "^PLANE_JOURNAL_INVALID$"):
            with self.attempt(journal=object()):
                self.fail("no journal guard accepted")
        self.assertEqual(jt.mod.inspect(self.fx.root, jt.NONCE), "NOT_STARTED")

    def test_physical_paths_never_accessed_and_no_shell_interface(self):
        with self.assertRaisesRegex(plane.Stop, "^PLANE_SYNTHETIC_DIR_REQUIRED$"):
            with plane.synthetic_flight(jt.mod, self.fx.root,
                                        self.fx.admission, self.fx.digest,
                                        jt.NOW, synthetic_lock_dir=None):
                self.fail("accepted physical path")
        self.assertEqual(len(plane.LEGACY_LOCKS), 5)
        self.assertEqual(tuple(Path(x).name for x in plane.LEGACY_LOCKS),
                         plane.SYNTHETIC_NAMES)
        self.assertEqual(plane.ROOT_LOCK,
                         "/opt/centenario/qa525-signed-prepare/.qa525-transaction.lock")
        raw = SOURCE.read_text(encoding="utf-8")
        for token in ("subprocess", "docker compose", "os.system(", "Popen(",
                      "socket.socket", "--review-and-run"):
            self.assertNotIn(token, raw)


if __name__ == "__main__":
    unittest.main()
