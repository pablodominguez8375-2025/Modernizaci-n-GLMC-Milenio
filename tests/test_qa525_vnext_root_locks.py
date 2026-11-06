"""Offline contract tests for the root lock wrapper; strict in-memory port."""
import importlib.util
from pathlib import Path
import stat
import unittest
from types import SimpleNamespace as NS

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


rl = load("qa525_vnext_root_locks", "scripts/qa/qa525-vnext-root-locks.py")
sy = load("qa525_vnext_locks_for_root", "scripts/qa/qa525-vnext-locks.py")


def dstat(mode=0o755, uid=0):
    return NS(st_dev=1, st_ino=hash(("d", mode, uid)) % 10**6, st_uid=uid,
              st_gid=0, st_mode=stat.S_IFDIR | mode, st_nlink=2)


def fstat_(ino, mode=0o600, uid=0, nlink=1):
    return NS(st_dev=1, st_ino=ino, st_uid=uid, st_gid=0,
              st_mode=stat.S_IFREG | mode, st_nlink=nlink)


class Port:
    def __init__(self):
        self.euid_value = 0
        self.nodes = {}
        for path in rl.LEGACY_LOCKS:
            for d in rl._chain(path):
                self.nodes[d] = dstat(0o1777 if d == "/run/lock" else 0o755)
        for i, path in enumerate(rl.LEGACY_LOCKS):
            self.nodes[path] = fstat_(100 + i)
        self.fds, self.next = {}, 10
        self.calls, self.busy, self.closed = [], set(), []

    def euid(self):
        return self.euid_value

    def lstat(self, path):
        self.calls.append(("lstat", path))
        if path not in self.nodes:
            raise FileNotFoundError(path)
        return self.nodes[path]

    def open_ro(self, path):
        self.calls.append(("open", path))
        self.next += 1
        self.fds[self.next] = path
        return self.next

    def fstat(self, fd):
        return self.nodes[self.fds[fd]]

    def flock_nb(self, fd):
        self.calls.append(("flock", self.fds[fd]))
        return self.fds[fd] not in self.busy

    def unlock(self, fd):
        pass

    def close(self, fd):
        self.closed.append(self.fds.pop(fd))


class Lease:
    def __init__(self, log):
        self.log, self.held = log, True

    def close(self):
        self.log.append("lease-close")
        self.held = False


class Journal:
    def __init__(self, log, fail=False):
        self.log, self.fail = log, fail

    def begin(self, *a):
        self.log.append("begin")
        if self.fail:
            raise RuntimeError("secret detail")
        return Lease(self.log)

    def lease_is_held(self, lease):
        return lease.held


def fly(port, journal):
    return rl.root_flight(port, journal, "r", "a", "d", "now")


class RootLockTests(unittest.TestCase):
    def test_paths_match_synthetic_plane_and_legacy_order(self):
        self.assertEqual(rl.LEGACY_LOCKS, sy.LEGACY_LOCKS)

    def test_order_then_lease_then_reverse_release(self):
        port, log = Port(), []
        with fly(port, Journal(log)) as plane:
            self.assertEqual([p for k, p in port.calls if k == "flock"],
                             list(rl.LEGACY_LOCKS))
            st = plane.assert_held()
            self.assertEqual(st["legacy_locks_held"], 5)
            for key in ("may_apply", "may_install", "signature_verified_here",
                        "physical_runtime_verified",
                        "legacy_entrypoint_disabled_verified"):
                self.assertIs(st[key], False)
        self.assertEqual(log, ["begin", "lease-close"])
        self.assertEqual(port.closed, list(reversed(rl.LEGACY_LOCKS)))
        with self.assertRaises(rl.Stop) as c:
            plane.assert_held()
        self.assertEqual(str(c.exception), "ROOTLOCK_PLANE_CLOSED")

    def stop(self, port, journal=None):
        with self.assertRaises(rl.Stop) as c:
            with fly(port, journal or Journal([])):
                pass
        return str(c.exception)

    def test_not_root_and_bad_port(self):
        port = Port(); port.euid_value = 1000
        self.assertEqual(self.stop(port), "ROOTLOCK_NOT_ROOT")
        self.assertEqual(self.stop(object()), "ROOTLOCK_PORT_INVALID")

    def test_busy_lock_releases_earlier_ones_and_never_begins(self):
        port, log = Port(), []
        port.busy.add(rl.LEGACY_LOCKS[2])
        self.assertEqual(self.stop(port, Journal(log)), "ROOTLOCK_BUSY")
        self.assertEqual(log, [])
        self.assertEqual(port.closed, [rl.LEGACY_LOCKS[2], rl.LEGACY_LOCKS[1],
                                       rl.LEGACY_LOCKS[0]])

    def test_untrusted_file_variants(self):
        for make in (lambda: fstat_(1, uid=1000), lambda: fstat_(1, mode=0o644),
                     lambda: fstat_(1, nlink=2),
                     lambda: NS(**{**vars(fstat_(1)), "st_mode": stat.S_IFLNK | 0o600})):
            port = Port(); port.nodes[rl.LEGACY_LOCKS[0]] = make()
            self.assertEqual(self.stop(port), "ROOTLOCK_FILE_UNTRUSTED")

    def test_missing_file_and_dir(self):
        port = Port(); del port.nodes[rl.LEGACY_LOCKS[0]]
        self.assertEqual(self.stop(port), "ROOTLOCK_FILE_MISSING")
        port = Port(); del port.nodes["/opt/centenario"]
        self.assertEqual(self.stop(port), "ROOTLOCK_DIR_MISSING")

    def test_untrusted_directories(self):
        for d, node in (("/opt/centenario", dstat(0o775)), ("/opt", dstat(0o755, 1000)),
                        ("/opt/centenario", NS(**{**vars(dstat()), "st_mode": stat.S_IFLNK | 0o755}))):
            port = Port(); port.nodes[d] = node
            self.assertEqual(self.stop(port), "ROOTLOCK_DIR_UNTRUSTED")
        port = Port(); port.nodes["/opt/centenario/qa-auto-lite"] = dstat(0o1777)
        self.assertEqual(self.stop(port), "ROOTLOCK_DIR_UNTRUSTED")  # sticky only for /run/lock

    def test_run_lock_only_exact_modes(self):
        for mode in (0o777, 0o770, 0o1775):
            port = Port(); port.nodes["/run/lock"] = dstat(mode)
            self.assertEqual(self.stop(port), "ROOTLOCK_DIR_UNTRUSTED")
        for mode in (0o755, 0o700):
            port = Port(); port.nodes["/run/lock"] = dstat(mode)
            with fly(port, Journal([])):
                pass

    def test_swap_between_open_and_lock(self):
        port = Port(); orig = port.flock_nb
        def swap(fd):
            ok = orig(fd); port.nodes[rl.LEGACY_LOCKS[0]] = fstat_(999); return ok
        port.flock_nb = swap
        self.assertEqual(self.stop(port), "ROOTLOCK_FILE_CHANGED")

    def test_journal_denied_is_static_and_releases_locks(self):
        port = Port()
        code = self.stop(port, Journal([], fail=True))
        self.assertEqual(code, "ROOTLOCK_JOURNAL_BEGIN_DENIED")
        self.assertEqual(len(port.closed), 5)

    def test_lock_lost_and_lease_lost_detected(self):
        port, log = Port(), []
        with self.assertRaises(rl.Stop) as c:
            with fly(port, Journal(log)) as plane:
                port.nodes[rl.LEGACY_LOCKS[3]] = fstat_(777)
                plane.assert_held()
        self.assertEqual(str(c.exception), "ROOTLOCK_LOST")
        port, log = Port(), []
        with self.assertRaises(rl.Stop) as c:
            with fly(port, Journal(log)) as plane:
                plane.lease.held = False
                plane.assert_held()
        self.assertEqual(str(c.exception), "ROOTLOCK_LEASE_LOST")

    def test_lease_close_failure_still_releases_locks(self):
        port, log = Port(), []
        j = Journal(log); j.begin = lambda *a: NS(close=lambda: 1 / 0)
        j.lease_is_held = lambda lease: True
        with self.assertRaises(rl.Stop) as c:
            with fly(port, j):
                pass
        self.assertEqual(str(c.exception), "ROOTLOCK_JOURNAL_CLOSE_FAILED")
        self.assertEqual(len(port.closed), 5)

    def test_module_has_no_host_imports_or_entrypoint(self):
        import ast
        tree = ast.parse((ROOT / "scripts/qa/qa525-vnext-root-locks.py").read_text())
        names = {a.name for n in ast.walk(tree) if isinstance(n, (ast.Import, ast.ImportFrom))
                 for a in n.names} | {n.module for n in ast.walk(tree)
                                      if isinstance(n, ast.ImportFrom)}
        self.assertEqual(names, {"contextmanager", "stat", "contextlib"})


if __name__ == "__main__":
    unittest.main()
