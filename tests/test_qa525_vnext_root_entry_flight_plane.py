"""QA525 pre-root entry running under the real RootPlane contract (root_flight) with a SYNTHETIC port."""
import array
import fcntl
import hashlib
import importlib.util
import os
from pathlib import Path
import socket
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[1]


def load(label, rel):
    spec = importlib.util.spec_from_file_location(label, BASE / rel)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


entry = load("qa525_fp_entry", "scripts/qa/qa525-vnext-root-entry.py")
recv = load("qa525_fp_recv", "scripts/qa/qa525-vnext-root-boundary-recv.py")
journal = load("qa525_fp_journal", "scripts/qa/qa525-vnext-go-nonce-journal.py")
rootlocks = load("qa525_fp_rootlocks", "scripts/qa/qa525-vnext-root-locks.py")
fakes = load("qa525_fp_fakes", "tests/test_qa525_vnext_root_locks.py")

DATA = b"signed-app-archive-bytes" * 40
SHA = "sha256:" + hashlib.sha256(DATA).hexdigest()
NONCE = "F" * 40
CLAIMS = dict(nonce=NONCE, app_archive_sha256=SHA, expires_at=2000,
              **{k: "sha256:" + c * 64 for k, c in zip(("ops", "policy", "plan", "admission"), "bcde")})
MODULES = {"qa525-vnext-root-boundary-recv.py": recv, "qa525-vnext-go-nonce-journal.py": journal}


class EntryUnderRootFlightTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="qa525-fp-")
        self.addCleanup(self.tmp.cleanup)
        self.jroot = str(Path(os.path.realpath(self.tmp.name)) / "journal")
        os.mkdir(self.jroot, 0o700)
        self.port, self.log = fakes.Port(), []
        self.a, self.b = socket.socketpair(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        self.fds = []
        self.addCleanup(self.clean)
        recv.prepare(self.b)

    def clean(self):
        for fd in self.fds:
            try:
                os.close(fd)
            except OSError:
                pass
        self.a.close()
        self.b.close()

    def send(self):
        fd = os.memfd_create("t", os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
        self.fds.append(fd)
        os.write(fd, DATA)
        fcntl.fcntl(fd, fcntl.F_ADD_SEALS, recv.SEALS)
        frame = recv.MAGIC + NONCE.encode() + b":" + SHA[7:].encode()
        self.a.sendmsg([frame], [(socket.SOL_SOCKET, socket.SCM_RIGHTS, array.array("i", [fd]))])

    def plane(self, journal_fake=None):
        flight = rootlocks.root_flight(self.port, journal_fake or fakes.Journal(self.log),
                                       "synthetic", "synthetic", "synthetic", 100)
        return entry.FlightPlane(flight)

    def run_entry(self, plane, child):
        return entry.run(MODULES.__getitem__, plane=plane, verify_go=lambda: dict(CLAIMS),
                         sock=self.b, peer_uid=os.getuid(), peer_pid=None, timeout=2,
                         journal_root=self.jroot, uid=os.getuid(), clock=lambda: 1000, child=child)

    def spent(self):
        return [n for n in os.listdir(self.jroot) if n.endswith(".spent")]

    def test_child_runs_with_five_locks_and_lease_held_then_all_released_in_order(self):
        seen = {}

        def child(fd):
            seen["closed_during"] = list(self.port.closed)
            seen["data"] = os.pread(fd, len(DATA), 0)
            return "ok"
        self.send()
        out = self.run_entry(self.plane(), child)
        self.assertEqual((out["child_outcome"], seen["data"], seen["closed_during"]), ("ok", DATA, []))
        self.assertEqual(self.port.closed, list(reversed(rootlocks.LEGACY_LOCKS)))
        self.assertEqual(self.log, ["begin", "lease-close"])
        self.assertEqual(len(self.spent()), 1)

    def test_busy_legacy_lock_never_reaches_receive_or_nonce(self):
        self.port.busy.add(rootlocks.LEGACY_LOCKS[1])
        self.send()
        with self.assertRaisesRegex(rootlocks.Stop, "^ROOTLOCK_BUSY$"):
            self.run_entry(self.plane(), lambda fd: "never")
        self.assertEqual(self.spent(), [])
        self.assertEqual(self.log, [])

    def test_lock_replaced_during_flight_is_a_stop_with_nonce_spent(self):
        def child(fd):
            self.port.nodes[rootlocks.LEGACY_LOCKS[3]] = fakes.fstat_(99999)
            return "ran"
        self.send()
        with self.assertRaisesRegex(entry.Stop, "^ENTRY_PLANE_LOST_DURING_FLIGHT$"):
            self.run_entry(self.plane(), child)
        self.assertEqual(len(self.spent()), 1)
        self.assertEqual(len(self.port.closed), 5)

    def test_lease_lost_during_flight_is_a_stop_with_nonce_spent(self):
        self.send()
        plane = self.plane()

        def child2(fd):
            plane._plane.lease.held = False
            return "ran"
        with self.assertRaisesRegex(entry.Stop, "^ENTRY_PLANE_LOST_DURING_FLIGHT$"):
            self.run_entry(plane, child2)
        self.assertEqual(len(self.spent()), 1)

    def test_wrapper_does_not_swallow_release_errors_and_reports_not_held_outside(self):
        plane = self.plane()
        self.assertFalse(plane.still_held())  # not entered
        self.port.close_fail.add(rootlocks.LEGACY_LOCKS[0])
        self.send()
        with self.assertRaisesRegex(rootlocks.Stop, "^ROOTLOCK_CLOSE_FAILED$"):
            self.run_entry(plane, lambda fd: "ran")
        self.assertEqual(len(self.spent()), 1)
        self.assertFalse(plane.still_held())


if __name__ == "__main__":
    unittest.main()
