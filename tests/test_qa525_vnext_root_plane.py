"""QA525 launcher-only plane: five REAL flock files (mapped legacy paths) + a REAL flock lease.
Liveness is the kernel's per-descriptor lock ownership, never 'some descriptor still holds it'."""
import fcntl
import importlib.util
import os
from pathlib import Path
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest
from types import SimpleNamespace as NS

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


def as_root(st):
    """Real device/inode (so kernel ownership is real); owner/mode presented the way the contract wants."""
    return NS(st_dev=st.st_dev, st_ino=st.st_ino, st_uid=0, st_gid=0,
              st_mode=stat.S_IFREG | 0o600, st_nlink=1)


class RealFilePort(fakes.Port):
    """The contract's synthetic directory chain, but the five lock files are REAL files and flock is REAL."""

    def __init__(self, base):
        super().__init__()
        self.real = {}
        for i, path in enumerate(rl.LEGACY_LOCKS):
            real = os.path.join(base, "lock%d" % i)
            Path(real).write_bytes(b"")
            os.chmod(real, 0o600)
            self.real[path] = real

    def lstat(self, path):
        self.calls.append(("lstat", path))
        if path in self.real:
            return as_root(os.lstat(self.real[path]))
        return super().lstat(path)

    def open_ro(self, path):
        self.calls.append(("open", path))
        fd = os.open(self.real[path], os.O_RDONLY | os.O_CLOEXEC | os.O_NOFOLLOW)
        self.fds[fd] = path
        return fd

    def fstat(self, fd):
        return as_root(os.fstat(fd))

    def flock_nb(self, fd):
        self.calls.append(("flock", self.fds[fd]))
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            return False
        return True

    def unlock(self, fd):
        fcntl.flock(fd, fcntl.LOCK_UN)

    def close(self, fd):
        path = self.fds.pop(fd)
        self.closed.append(path)
        os.close(fd)
        if path in self.close_fail:
            raise OSError("secret detail /private/path")


@unittest.skipUnless(sys.platform == "linux" and os.path.exists("/proc/self/fdinfo/0"), "Linux fdinfo required")
class PlaneTests(unittest.TestCase):
    def setUp(self):
        self.dir = os.path.realpath(tempfile.mkdtemp(prefix="plane"))
        self.addCleanup(shutil.rmtree, self.dir, True)
        os.chmod(self.dir, 0o755)
        self.lease_dir = os.path.join(self.dir, "journal")
        os.mkdir(self.lease_dir, 0o700)
        self.provision()
        self.port = RealFilePort(self.dir)

    def provision(self):
        path = os.path.join(self.lease_dir, plane_mod.LEASE_NAME)
        if os.path.lexists(path):
            os.unlink(path)
        Path(path).write_bytes(b"")
        os.chmod(path, 0o600)

    def lease_path(self):
        return os.path.join(self.lease_dir, plane_mod.LEASE_NAME)

    def fly(self, port=None, directory=None, **kw):
        args = dict(uid=UID, anchor=self.dir)
        args.update(kw)
        return plane_mod.launcher_flight(rl, port or self.port, directory or self.lease_dir, **args)

    def stop(self, code, **kw):
        with self.assertRaises((plane_mod.Stop, rl.Stop)) as ctx:
            with self.fly(**kw):
                pass
        self.assertEqual(str(ctx.exception), code)

    def lost(self, plane):
        with self.assertRaises(rl.Stop) as ctx:
            plane.assert_held()
        self.assertEqual(str(ctx.exception), "ROOTLOCK_LEASE_LOST")

    # ---- order, release and composition ----
    def test_order_five_locks_then_lease_then_reverse_release_without_any_journal_begin(self):
        with self.fly() as plane:
            self.assertEqual([p for k, p in self.port.calls if k == "flock"], list(rl.LEGACY_LOCKS))
            self.assertEqual(plane.assert_held()["legacy_locks_held"], 5)
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS)))
        with self.assertRaises(rl.Stop) as ctx:
            plane.assert_held()
        self.assertEqual(str(ctx.exception), "ROOTLOCK_PLANE_CLOSED")
        with self.fly():  # everything was really released: it can be taken again
            pass

    def test_a_busy_legacy_lock_releases_earlier_ones_and_never_takes_the_lease(self):
        other = os.open(self.port.real[rl.LEGACY_LOCKS[3]], os.O_RDONLY | os.O_CLOEXEC)
        self.addCleanup(os.close, other)
        fcntl.flock(other, fcntl.LOCK_EX | fcntl.LOCK_NB)
        self.stop("ROOTLOCK_BUSY")
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS[:4])))  # incl. the busy one
        lease = os.open(self.lease_path(), os.O_RDWR | os.O_CLOEXEC)
        self.addCleanup(os.close, lease)
        fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)  # the lease was never taken

    def test_a_held_lease_stops_busy_and_releases_every_lock(self):
        other = os.open(self.lease_path(), os.O_RDWR | os.O_CLOEXEC)
        self.addCleanup(os.close, other)
        fcntl.flock(other, fcntl.LOCK_EX | fcntl.LOCK_NB)
        self.stop("PLANE_LEASE_BUSY")
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS)))

    def test_failed_release_is_a_stop_never_success_and_everything_is_still_attempted(self):
        self.port.close_fail.add(rl.LEGACY_LOCKS[1])
        with self.assertRaises(plane_mod.Stop) as ctx:
            with self.fly():
                pass
        self.assertEqual(str(ctx.exception), "PLANE_LOCK_CLOSE_FAILED")
        self.assertEqual(self.port.closed, list(reversed(rl.LEGACY_LOCKS)))  # none skipped
        self.assertNotIn("secret", str(ctx.exception))

    def test_it_composes_with_the_entry_flight_plane_adapter(self):
        adapter = entry.FlightPlane(self.fly())
        with adapter:
            self.assertIs(adapter.still_held(), True)
            fcntl.flock(adapter._plane.lease.fd, fcntl.LOCK_UN)
            self.assertIs(adapter.still_held(), False)
            fcntl.flock(adapter._plane.lease.fd, fcntl.LOCK_EX | fcntl.LOCK_NB)

    # ---- P1: lease ownership is the kernel's per-descriptor record ----
    def test_lease_released_then_taken_by_another_descriptor_is_lost_even_though_still_busy(self):
        """ChatGPT's A/B/C scenario: A unlocks but keeps its fd, B takes the file, a probe would be refused."""
        with self.fly() as plane:
            lease = plane.lease
            fcntl.flock(lease.fd, fcntl.LOCK_UN)  # A: unlocked, fd still open
            b = os.open(self.lease_path(), os.O_RDWR | os.O_CLOEXEC)
            self.addCleanup(os.close, b)
            fcntl.flock(b, fcntl.LOCK_EX | fcntl.LOCK_NB)  # B holds the SAME inode
            c = os.open(self.lease_path(), os.O_RDONLY | os.O_CLOEXEC)
            self.addCleanup(os.close, c)
            with self.assertRaises(BlockingIOError):
                fcntl.flock(c, fcntl.LOCK_EX | fcntl.LOCK_NB)  # C is refused: a probe would say "held"
            self.assertTrue(plane._active)
            self.assertIs(plane.journal.lease_is_held(lease), False)
            self.lost(plane)
            fcntl.flock(b, fcntl.LOCK_UN)
            fcntl.flock(lease.fd, fcntl.LOCK_EX | fcntl.LOCK_NB)  # restore for a clean exit

    def test_lease_taken_over_by_another_PROCESS_after_our_unlock_is_lost(self):
        with self.fly() as plane:
            fcntl.flock(plane.lease.fd, fcntl.LOCK_UN)
            code = ("import fcntl,os,sys,time\n"
                    "fd=os.open(sys.argv[1],os.O_RDWR);fcntl.flock(fd,fcntl.LOCK_EX|fcntl.LOCK_NB)\n"
                    "print('held',flush=True);time.sleep(30)\n")
            proc = subprocess.Popen([sys.executable, "-c", code, self.lease_path()], stdout=subprocess.PIPE, text=True)
            try:
                self.assertEqual(proc.stdout.readline().strip(), "held")
                self.assertIs(plane.journal.lease_is_held(plane.lease), False)
                self.lost(plane)
            finally:
                proc.kill()
                proc.wait()
                proc.stdout.close()
            fcntl.flock(plane.lease.fd, fcntl.LOCK_EX | fcntl.LOCK_NB)

    def test_lease_file_replaced_under_us_is_lost(self):
        with self.fly() as plane:
            os.rename(self.lease_path(), self.lease_path() + ".old")
            Path(self.lease_path()).write_bytes(b"")
            os.chmod(self.lease_path(), 0o600)
            self.lost(plane)

    # ---- P1: the five legacy locks are checked against the kernel too ----
    def test_FIVE_LOCK_LOST_BUT_FD_OPEN_for_each_of_the_five_is_detected(self):
        for index in range(5):
            port = RealFilePort(self.dir)
            with self.fly(port=port) as plane:
                fd = next(f for f, p in port.fds.items() if p == rl.LEGACY_LOCKS[index])
                self.assertEqual(plane.assert_held()["legacy_locks_held"], 5)
                fcntl.flock(fd, fcntl.LOCK_UN)  # inode/mode/path identical, fd open, kernel lock GONE
                self.lost(plane)
                fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)

    def test_a_legacy_lock_taken_over_by_another_holder_after_our_unlock_is_lost(self):
        with self.fly() as plane:
            fd = next(f for f, p in self.port.fds.items() if p == rl.LEGACY_LOCKS[2])
            fcntl.flock(fd, fcntl.LOCK_UN)
            other = os.open(self.port.real[rl.LEGACY_LOCKS[2]], os.O_RDONLY | os.O_CLOEXEC)
            self.addCleanup(os.close, other)
            fcntl.flock(other, fcntl.LOCK_EX | fcntl.LOCK_NB)
            self.lost(plane)
            fcntl.flock(other, fcntl.LOCK_UN)
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)

    def test_unreadable_or_unsupported_fdinfo_is_never_held(self):
        with self.fly() as plane:
            self.assertIs(plane.journal.lease_is_held(plane.lease), True)
            plane.lease.view = lambda fd: (_ for _ in ()).throw(OSError("no fdinfo"))
            self.assertIs(plane.journal.lease_is_held(plane.lease), False)
            plane.lease.view = lambda fd: set()
            self.assertIs(plane.journal.lease_is_held(plane.lease), False)
            plane.lease.view = plane_mod._ofd_flocks

    def test_fdinfo_parser_ignores_blocked_posix_and_read_entries(self):
        path = os.path.join(self.dir, "x")
        Path(path).write_bytes(b"")
        fd = os.open(path, os.O_RDWR)
        self.addCleanup(os.close, fd)
        self.assertEqual(plane_mod._ofd_flocks(fd), set())  # nothing locked
        fcntl.flock(fd, fcntl.LOCK_SH)
        self.assertEqual(plane_mod._ofd_flocks(fd), set())  # shared (READ) is not exclusive ownership
        fcntl.flock(fd, fcntl.LOCK_EX)
        st = os.fstat(fd)
        self.assertEqual(plane_mod._ofd_flocks(fd), {(st.st_dev, st.st_ino)})
        fcntl.lockf(fd, fcntl.LOCK_UN)  # POSIX unlock must not touch the flock entry
        self.assertEqual(plane_mod._ofd_flocks(fd), {(st.st_dev, st.st_ino)})
        self.assertEqual(plane_mod._ofd_flocks(99999), set())

    # ---- P2: pre-provisioned lease in an anchored, root-owned chain ----
    def test_lease_directory_chain_and_file_are_strict(self):
        self.stop("PLANE_LEASE_DIR_UNSAFE", directory="relative/journal")
        self.stop("PLANE_LEASE_DIR_UNSAFE", directory=self.lease_dir + "/../journal")
        self.stop("PLANE_LEASE_DIR_UNSAFE", directory=os.path.join(self.dir, "missing"))
        os.chmod(self.lease_dir, 0o750)
        self.stop("PLANE_LEASE_DIR_UNSAFE")
        os.chmod(self.lease_dir, 0o700)
        os.chmod(self.dir, 0o775)  # writable ancestor
        self.stop("PLANE_LEASE_DIR_UNSAFE", anchor=os.path.dirname(self.dir))
        os.chmod(self.dir, 0o755)
        link = os.path.join(self.dir, "linkdir")
        os.symlink(self.lease_dir, link)  # symlinked leaf / intermediate directory
        self.stop("PLANE_LEASE_DIR_UNSAFE", directory=link)
        os.mkdir(os.path.join(self.dir, "mid"), 0o700)
        os.symlink(self.lease_dir, os.path.join(self.dir, "mid", "j"))
        self.stop("PLANE_LEASE_DIR_UNSAFE", directory=os.path.join(self.dir, "mid", "j"))
        self.stop("PLANE_LEASE_DIR_UNSAFE", uid=UID + 1)
        self.assertEqual(len(self.port.fds), 0)

    def test_lease_is_never_created_and_a_planted_or_loose_file_is_refused(self):
        os.unlink(self.lease_path())
        self.stop("PLANE_LEASE_MISSING")
        self.assertFalse(os.path.exists(self.lease_path()))  # not created implicitly
        os.symlink("/etc/passwd", self.lease_path())  # O_NOFOLLOW through the directory descriptor
        self.stop("PLANE_LEASE_UNAVAILABLE")
        os.unlink(self.lease_path())
        Path(self.lease_path()).write_bytes(b"")
        os.chmod(self.lease_path(), 0o644)
        self.stop("PLANE_LEASE_UNTRUSTED")
        os.chmod(self.lease_path(), 0o600)
        os.link(self.lease_path(), self.lease_path() + ".hard")
        self.stop("PLANE_LEASE_UNTRUSTED")
        self.assertEqual(len(self.port.fds), 0)  # locks always released

    def test_not_root_and_bad_port_are_refused_before_anything(self):
        bad = RealFilePort(self.dir)
        bad.euid_value = 1000
        self.stop("ROOTLOCK_NOT_ROOT", port=bad)
        self.stop("ROOTLOCK_PORT_INVALID", port=object())

    def test_no_api_web_journal_or_admission_is_touched(self):
        import ast
        tree = ast.parse((BASE / "scripts/qa/qa525-vnext-root-plane.py").read_text())
        calls = {n.func.attr for n in ast.walk(tree) if isinstance(n, ast.Call) and isinstance(n.func, ast.Attribute)}
        imports = {a.name for n in ast.walk(tree) if isinstance(n, ast.Import) for a in n.names}
        self.assertNotIn("begin", calls)  # never the API_WEB flight journal
        self.assertTrue(imports <= {"fcntl", "os", "stat"})  # no subprocess, no sockets, no network


if __name__ == "__main__":
    unittest.main()
