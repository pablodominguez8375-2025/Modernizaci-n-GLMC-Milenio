"""QA525 held-plane ↔ durable GO nonce integration tests; SYNTHETIC ONLY.

No root, sudo, service, network or real legacy lock files are accessed.
A fake in-memory lock/lease port exercises the existing RootPlane contract;
the GO nonce marker uses a private temporary directory and the real kernel.
Neither component authenticates Sponsor, APP, OPS, binaries or physical GO.
"""
import hashlib
import importlib.util
import os
from pathlib import Path
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[1]


def load(label, relative):
    spec = importlib.util.spec_from_file_location(label, BASE / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


rootlocks = load("qa525_held_nonce_rootlocks", "scripts/qa/qa525-vnext-root-locks.py")
nonce = load("qa525_held_nonce_spent", "scripts/qa/qa525-vnext-go-nonce-journal.py")
fakes = load("qa525_held_nonce_test_fakes", "tests/test_qa525_vnext_root_locks.py")

NONCE = "K" * 40
BINDING = {key: "sha256:" + char * 64
           for key, char in zip(nonce.FIELDS, "abcde")}


class HeldPlaneNonceTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="qa525-held-nonce-")
        self.addCleanup(self.tmp.cleanup)
        self.root = str(Path(os.path.realpath(self.tmp.name)) / "journal")
        os.mkdir(self.root, mode=0o700)
        self.uid = os.getuid()
        self.port = fakes.Port()
        self.log = []
        self.legacy_journal = fakes.Journal(self.log)

    def flight(self):
        # This is a FAKE root port, not a real euid=0 operation.
        return rootlocks.root_flight(self.port, self.legacy_journal,
                                     "synthetic", "synthetic", "synthetic", 100)

    def markers(self):
        return sorted(p for p in os.listdir(self.root) if p.endswith(".spent"))

    def spend_inside(self, plane, value=NONCE, *, now=100, expires_at=200):
        # The synthetic *harness* refuses to call consume without a HELD plane.
        # This is NOT a production authorization adapter or a GO signature check.
        state = plane.assert_held()
        self.assertEqual(state["legacy_locks_held"], len(rootlocks.LEGACY_LOCKS))
        self.assertIs(state["root_lease_held"], True)
        return nonce.consume(self.root, value, BINDING, uid=self.uid,
                             now=now, expires_at=expires_at)

    def test_go_nonce_marked_once_inside_five_held_locks_and_live_lease(self):
        with self.flight() as plane:
            self.assertEqual([p for op, p in self.port.calls if op == "flock"],
                             list(rootlocks.LEGACY_LOCKS))
            receipt = self.spend_inside(plane)
            self.assertTrue(receipt["consumed"])
            self.assertEqual(len(self.markers()), 1)
            persisted = Path(self.root, self.markers()[0]).read_bytes()
            self.assertEqual(receipt["record_sha256"],
                             "sha256:" + hashlib.sha256(persisted).hexdigest())
            for flag in ("may_install", "may_apply", "sponsor_authorized_here",
                         "bootstrap_authenticated_here"):
                self.assertIs(receipt[flag], False)
            self.assertEqual(plane.assert_held()["legacy_locks_held"], 5)
            with self.assertRaisesRegex(nonce.Stop, "^NONCE_REPLAY$"):
                self.spend_inside(plane)
        self.assertEqual(self.log, ["begin", "lease-close"])
        self.assertEqual(self.port.closed, list(reversed(rootlocks.LEGACY_LOCKS)))
        with self.assertRaisesRegex(rootlocks.Stop, "^ROOTLOCK_PLANE_CLOSED$"):
            self.spend_inside(plane)
        with self.flight() as next_plane:
            with self.assertRaisesRegex(nonce.Stop, "^NONCE_REPLAY$"):
                self.spend_inside(next_plane)
        self.assertEqual(len(self.markers()), 1)

    def test_busy_legacy_lock_blocks_before_any_spent_nonce(self):
        self.port.busy.add(rootlocks.LEGACY_LOCKS[2])
        with self.assertRaisesRegex(rootlocks.Stop, "^ROOTLOCK_BUSY$"):
            with self.flight() as plane:
                self.spend_inside(plane)
        self.assertEqual(self.markers(), [])
        self.assertEqual(self.log, [])
        self.assertEqual(self.port.closed, list(reversed(rootlocks.LEGACY_LOCKS[:3])))

    def test_journal_begin_failure_blocks_before_nonce(self):
        self.legacy_journal = fakes.Journal(self.log, fail=True)
        with self.assertRaisesRegex(rootlocks.Stop, "^ROOTLOCK_JOURNAL_BEGIN_DENIED$"):
            with self.flight() as plane:
                self.spend_inside(plane)
        self.assertEqual(self.markers(), [])
        self.assertEqual(len(self.port.closed), 5)

    def test_lock_replacement_before_nonce_fails_closed(self):
        with self.flight() as plane:
            self.port.nodes[rootlocks.LEGACY_LOCKS[0]] = fakes.fstat_(99999)
            with self.assertRaisesRegex(rootlocks.Stop, "^ROOTLOCK_LOST$"):
                self.spend_inside(plane)
        self.assertEqual(self.markers(), [])

    def test_lost_lease_before_nonce_fails_closed(self):
        with self.flight() as plane:
            plane.lease.held = False
            with self.assertRaisesRegex(rootlocks.Stop, "^ROOTLOCK_LEASE_LOST$"):
                self.spend_inside(plane)
        self.assertEqual(self.markers(), [])

    def test_lost_lock_after_nonce_blocks_next_nonce_and_preserves_first(self):
        with self.flight() as plane:
            self.spend_inside(plane)
            self.port.nodes[rootlocks.LEGACY_LOCKS[4]] = fakes.fstat_(99998)
            with self.assertRaisesRegex(rootlocks.Stop, "^ROOTLOCK_LOST$"):
                self.spend_inside(plane, "Z" * 40)
        self.assertEqual(len(self.markers()), 1)
        self.assertEqual(self.markers()[0],
                         hashlib.sha256(NONCE.encode("ascii")).hexdigest() + ".spent")

    def test_expired_nonce_never_creates_spent_marker_even_inside_held_plane(self):
        with self.flight() as plane:
            with self.assertRaisesRegex(nonce.Stop, "^NONCE_EXPIRED$"):
                self.spend_inside(plane, now=201, expires_at=200)
            self.assertEqual(plane.assert_held()["legacy_locks_held"], 5)
        self.assertEqual(self.markers(), [])

    def test_no_writable_execution_adapter_is_created(self):
        self.assertTrue(rootlocks.STATUS.endswith("NOT_HOST_AUTHORITY"))
        self.assertIsNotNone(nonce.consume)
        self.assertNotIn("sponsor_authorized_here", rootlocks.RootPlane.assert_held.__code__.co_names)


if __name__ == "__main__":
    unittest.main()
