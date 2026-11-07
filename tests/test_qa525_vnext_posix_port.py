"""Offline tests for the POSIX lock port; temp files only, no host paths."""
import ast
import importlib.util
import os
from pathlib import Path
import stat
import tempfile
import unittest
from types import SimpleNamespace as NS
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


pp = load("qa525_vnext_posix_port", "scripts/qa/qa525-vnext-posix-port.py")
rl = load("qa525_vnext_root_locks_for_port", "scripts/qa/qa525-vnext-root-locks.py")


class TempPort(pp.PosixPort):
    """Test-only: maps the fixed lock paths to temp files, faking root owner."""

    def __init__(self, mapping):
        super().__init__()
        self.mapping = mapping

    def _real(self, path):
        return self.mapping.get(path)

    def lstat(self, path):
        path = self._path(path)
        real = self._real(path)
        if real is None:
            return NS(st_dev=1, st_ino=hash(path) % 10**6, st_uid=0, st_gid=0,
                      st_mode=stat.S_IFDIR | 0o755, st_nlink=2)
        st = os.lstat(real)
        return NS(st_dev=st.st_dev, st_ino=st.st_ino, st_uid=0, st_gid=0,
                  st_mode=st.st_mode, st_nlink=st.st_nlink)

    def open_ro(self, path):
        self._path(path, lock=True)
        fd = os.open(self.mapping[path], os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        self._open[fd] = path
        return fd

    def fstat(self, fd):
        self._fd(fd)
        st = os.fstat(fd)
        return NS(st_dev=st.st_dev, st_ino=st.st_ino, st_uid=0, st_gid=0,
                  st_mode=st.st_mode, st_nlink=st.st_nlink)

    def euid(self):
        return 0


class Journal:
    def __init__(self):
        self.log = []

    def begin(self, *a):
        self.log.append("begin")
        return NS(close=lambda: self.log.append("lease-close"), held=True)

    def lease_is_held(self, lease):
        return True


class PortTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.files = {}
        for i, p in enumerate(pp.LEGACY_LOCKS):
            f = Path(self.tmp.name) / f"l{i}"
            f.touch(mode=0o600)
            f.chmod(0o600)
            self.files[p] = str(f)

    def tearDown(self):
        self.tmp.cleanup()

    def test_paths_equal_wrapper_paths(self):
        self.assertEqual(pp.LEGACY_LOCKS, rl.LEGACY_LOCKS)

    def test_refuses_any_other_path(self):
        port = pp.PosixPort()
        for bad in ("/etc/passwd", "/opt/centenario/../etc/passwd", "/opt/centenario/qa-api-refresh/x",
                    "/opt/centenario/qa-api-refresh/operation.lock\0", "", None, b"/opt"):
            for call in (port.lstat, port.open_ro):
                with self.assertRaises(pp.Stop) as c:
                    call(bad)
                self.assertEqual(str(c.exception), "POSIXPORT_PATH_NOT_ALLOWED")
        with self.assertRaises(pp.Stop):
            port.open_ro("/opt")  # directories may be inspected, never locked

    def test_fd_tracking_rejects_foreign_descriptors(self):
        port = pp.PosixPort()
        fd = os.open(self.files[pp.LEGACY_LOCKS[0]], os.O_RDONLY)
        try:
            for call in (port.fstat, port.flock_nb, port.unlock, port.close):
                with self.assertRaises(pp.Stop) as c:
                    call(fd)
                self.assertEqual(str(c.exception), "POSIXPORT_FD_UNKNOWN")
            with self.assertRaises(pp.Stop):
                port.close(True)
        finally:
            os.close(fd)

    def test_real_flock_busy_then_free(self):
        a, b = TempPort(self.files), TempPort(self.files)
        p = pp.LEGACY_LOCKS[0]
        fa, fb = a.open_ro(p), b.open_ro(p)
        self.assertIs(a.flock_nb(fa), True)
        self.assertIs(b.flock_nb(fb), False)
        a.unlock(fa)
        self.assertIs(b.flock_nb(fb), True)
        a.close(fa)
        b.close(fb)
        with self.assertRaises(pp.Stop):
            b.close(fb)

    def test_open_uses_readonly_nofollow_cloexec(self):
        port, seen, real_open = pp.PosixPort(), {}, os.open
        def fake(path, flags, *a):
            seen.update(path=path, flags=flags)
            return real_open(self.files[pp.LEGACY_LOCKS[0]], os.O_RDONLY)
        with mock.patch.object(pp.os, "open", side_effect=fake):
            fd = port.open_ro(pp.LEGACY_LOCKS[0])
        self.assertEqual(seen["path"], pp.LEGACY_LOCKS[0])
        self.assertEqual(seen["flags"], os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        port.close(fd)

    def test_open_error_is_fixed_code(self):
        port = pp.PosixPort()
        with mock.patch.object(pp.os, "open", side_effect=OSError("secret /path")):
            with self.assertRaises(pp.Stop) as c:
                port.open_ro(pp.LEGACY_LOCKS[0])
        self.assertEqual(str(c.exception), "POSIXPORT_OPEN_FAILED")
        self.assertEqual(port._open, {})

    def test_unexpected_flock_error_is_fixed_code(self):
        port = TempPort(self.files)
        fd = port.open_ro(pp.LEGACY_LOCKS[0])
        with mock.patch.object(pp.fcntl, "flock", side_effect=OSError(5, "secret")):
            with self.assertRaises(pp.Stop) as c:
                port.flock_nb(fd)
        self.assertEqual(str(c.exception), "POSIXPORT_FLOCK_FAILED")
        port.close(fd)

    def test_close_error_untracks_and_is_fixed_code(self):
        port = TempPort(self.files)
        fd = port.open_ro(pp.LEGACY_LOCKS[0])
        with mock.patch.object(pp.os, "close", side_effect=OSError("secret")):
            with self.assertRaises(pp.Stop) as c:
                port.close(fd)
        self.assertEqual(str(c.exception), "POSIXPORT_CLOSE_FAILED")
        self.assertEqual(port._open, {})
        os.close(fd)

    def test_root_flight_end_to_end_with_real_flock(self):
        port, journal = TempPort(self.files), Journal()
        with rl.root_flight(port, journal, "r", "a", "d", "now") as plane:
            self.assertEqual(plane.assert_held()["legacy_locks_held"], 5)
            other = TempPort(self.files)
            fd = other.open_ro(pp.LEGACY_LOCKS[2])
            self.assertIs(other.flock_nb(fd), False)  # really held by the plane
            other.close(fd)
        self.assertEqual(journal.log, ["begin", "lease-close"])
        self.assertEqual(port._open, {})
        again = TempPort(self.files)
        fd = again.open_ro(pp.LEGACY_LOCKS[2])
        self.assertIs(again.flock_nb(fd), True)  # released
        again.close(fd)

    def test_root_flight_busy_lock_blocks_before_lease(self):
        holder = TempPort(self.files)
        fd = holder.open_ro(pp.LEGACY_LOCKS[3])
        self.assertIs(holder.flock_nb(fd), True)
        journal = Journal()
        with self.assertRaises(rl.Stop) as c:
            with rl.root_flight(TempPort(self.files), journal, "r", "a", "d", "now"):
                pass
        self.assertEqual(str(c.exception), "ROOTLOCK_BUSY")
        self.assertEqual(journal.log, [])
        holder.close(fd)

    def test_not_root_is_refused_by_wrapper(self):
        port = TempPort(self.files)
        port.euid = lambda: os.geteuid() or 1000
        with self.assertRaises(rl.Stop) as c:
            with rl.root_flight(port, Journal(), "r", "a", "d", "now"):
                pass
        self.assertEqual(str(c.exception), "ROOTLOCK_NOT_ROOT")

    def test_module_is_library_only(self):
        src = (ROOT / "scripts/qa/qa525-vnext-posix-port.py").read_text()
        tree = ast.parse(src)
        names = {a.name for n in ast.walk(tree) if isinstance(n, ast.Import) for a in n.names}
        self.assertEqual(names, {"errno", "fcntl", "os"})
        self.assertFalse(any(isinstance(n, ast.ImportFrom) for n in ast.walk(tree)))
        calls = {n.func.attr for n in ast.walk(tree) if isinstance(n, ast.Call)
                 and isinstance(n.func, ast.Attribute)}
        for forbidden in ("system", "popen", "execv", "execve", "fork", "kill", "unlink",
                          "remove", "rename", "chmod", "chown", "write", "mkdir"):
            self.assertNotIn(forbidden, calls)
        with self.assertRaises(SystemExit):
            exec(compile(src, "x", "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
