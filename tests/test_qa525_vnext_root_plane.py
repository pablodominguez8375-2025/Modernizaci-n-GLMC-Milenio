"""QA525 launcher-only plane: 5 locks (synthetic port) + a REAL flock lease; no API_WEB journal involved."""
import fcntl
import importlib.util
import os
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[1]


def load(label, rel):
    spec = importlib.util.spec_from_file_location(label, BASE / rel)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


plane_mod = load("qa525_plane_mod", "scripts/qa/qa525-vnext-root-plane.py")
rl = load("qa525_plane_rl", "scripts/qa/qa525-vnext-root-locks.py")
fakes = load("qa525_plane_fakes", "tests/test_qa525_vnext_root_locks.py")
entry = load("qa525_plane_entry", "scripts/qa/qa525-vnext-root-entry.py")
UID = os.getuid()


class PlaneTests(unittest.TestCase):
    def setUp(self):
        self.dir = os.path.realpath(tempfile.mkdtemp(prefix="plane"))
        self.addCleanup(shutil.rmtree, self.dir, True)
        os.chmod(self.dir, 0o700)
        self.port = fakes.Port()

    def fly(self, port=None, directory=None):
        return plane_mod.launcher_flight(rl, port or self.port, directory or self.dir, uid=UID)

    def stop(self, code, **kw):
        with self.assertRaises(plane_mod.Stop if code.startswith("PLANE_") else rl.Stop) as ctx:
            with self.fly(**kw):
                pass
        self.assertEqual(str(ctx.exception), code)

    def lease_path(self):
        return os.path.join(self.dir, plane_mod.LEASE_NAME)

    def competitor_blocked(self):
        fd = os.open(self.lease_path(), os.O_RDONLY | os.O_CLOEXEC)
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            fcntl.flock(fd, fcntl.LOCK_UN)
            return False
        except BlockingIOError:
            return True
        finally:
            os.close(fd)

    def test_order_five_locks_then_lease_then_reverse_release_without_any_journal_begin(self):
        with self.fly() as plane:
            self.assertEqual([p for k, p in self.port.calls if k == "flock"], list(rl.LEGACY_LOCKS))
            self.assertEqual(plane.assert_held()["legacy_locks_held"], 5)
            self.assertTrue(self.competitor_blocked())  # a second process could not take the lease
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS)))
        self.assertFalse(self.competitor_blocked())  # released
        with self.assertRaises(rl.Stop) as ctx:
            plane.assert_held()
        self.assertEqual(str(ctx.exception), "ROOTLOCK_PLANE_CLOSED")

    def test_a_busy_legacy_lock_releases_earlier_ones_and_never_takes_the_lease(self):
        self.port.busy.add(rl.LEGACY_LOCKS[3])
        self.stop("ROOTLOCK_BUSY")
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS[:4])))  # incl. the busy one
        self.assertFalse(os.path.exists(self.lease_path()))

    def test_a_held_lease_stops_busy_and_releases_every_lock(self):
        other = os.open(self.lease_path(), os.O_RDWR | os.O_CREAT | os.O_CLOEXEC, 0o600)
        self.addCleanup(os.close, other)
        fcntl.flock(other, fcntl.LOCK_EX | fcntl.LOCK_NB)
        self.stop("PLANE_LEASE_BUSY")
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS)))

    def test_not_root_or_bad_port_and_unsafe_lease_dir_or_file(self):
        bad = fakes.Port()
        bad.euid_value = 1000
        self.stop("ROOTLOCK_NOT_ROOT", port=bad)
        self.stop("ROOTLOCK_PORT_INVALID", port=object())
        os.chmod(self.dir, 0o750)
        self.stop("PLANE_LEASE_DIR_UNSAFE")
        os.chmod(self.dir, 0o700)
        self.stop("PLANE_LEASE_DIR_UNSAFE", directory=os.path.join(self.dir, "missing"))
        os.symlink("/etc/passwd", self.lease_path())  # O_NOFOLLOW: never follows a planted link
        self.stop("PLANE_LEASE_UNAVAILABLE")
        os.unlink(self.lease_path())
        Path(self.lease_path()).write_text("x")
        os.chmod(self.lease_path(), 0o644)
        self.stop("PLANE_LEASE_UNTRUSTED")
        self.assertEqual(len(self.port.closed) % 5, 0)  # every attempt released all five locks
        self.assertEqual(len(self.port.fds), 0)

    def test_lease_liveness_is_the_kernel_lock_not_an_open_fd(self):
        with self.fly() as plane:
            self.assertTrue(plane.journal.lease_is_held(plane.lease))
            fcntl.flock(plane.lease.fd, fcntl.LOCK_UN)  # fd still open, lock gone
            self.assertFalse(plane.journal.lease_is_held(plane.lease))
            with self.assertRaises(rl.Stop) as ctx:
                plane.assert_held()
            self.assertEqual(str(ctx.exception), "ROOTLOCK_LEASE_LOST")
            fcntl.flock(plane.lease.fd, fcntl.LOCK_EX | fcntl.LOCK_NB)  # restore for a clean exit

    def test_lease_file_replaced_under_us_is_lost(self):
        with self.fly() as plane:
            os.rename(self.lease_path(), self.lease_path() + ".old")
            Path(self.lease_path()).write_text("")
            os.chmod(self.lease_path(), 0o600)
            with self.assertRaises(rl.Stop) as ctx:
                plane.assert_held()
            self.assertEqual(str(ctx.exception), "ROOTLOCK_LEASE_LOST")

    def test_failed_release_is_a_stop_never_success_and_everything_is_still_attempted(self):
        self.port.close_fail.add(rl.LEGACY_LOCKS[1])
        with self.assertRaises(plane_mod.Stop) as ctx:
            with self.fly():
                pass
        self.assertEqual(str(ctx.exception), "PLANE_LOCK_CLOSE_FAILED")
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS)))  # none skipped
        self.assertNotIn("secret", str(ctx.exception))
        self.assertFalse(self.competitor_blocked())  # the lease was released first

    def test_it_composes_with_the_entry_flight_plane_adapter(self):
        adapter = entry.FlightPlane(self.fly())
        with adapter:
            self.assertIs(adapter.still_held(), True)
            os.close(adapter._plane.lease.fd)  # lease fd closed behind our back
            adapter._plane.lease.fd = None
            self.assertIs(adapter.still_held(), False)

    def test_no_api_web_journal_or_admission_is_touched(self):
        import ast
        tree = ast.parse((BASE / "scripts/qa/qa525-vnext-root-plane.py").read_text())
        calls = {n.func.attr for n in ast.walk(tree) if isinstance(n, ast.Call) and isinstance(n.func, ast.Attribute)}
        imports = {a.name for n in ast.walk(tree) if isinstance(n, ast.Import) for a in n.names}
        self.assertNotIn("begin", calls)  # never the API_WEB flight journal
        self.assertTrue(imports <= {"fcntl", "os", "stat"})  # no subprocess, no sockets, no network


if __name__ == "__main__":
    unittest.main()
