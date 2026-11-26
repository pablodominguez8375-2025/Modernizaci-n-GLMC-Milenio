"""Hermetic tests for the manual cut driver skeleton (temp dirs only; no docker, no host)."""
import fcntl
import hashlib
import importlib.util
import os
import pathlib
import subprocess
import tempfile
import unittest

SRC = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-manual-cut-e5c40d3a.py"
spec = importlib.util.spec_from_file_location("cut", SRC)
cut = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cut)
D = cut.DOCKER


class Loader(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        self.d = os.path.realpath(self.t.name)

    def put(self, name, text, mode=0o644):
        p = os.path.join(self.d, name)
        with open(p, "wb") as h:
            h.write(text)
        os.chmod(p, mode)
        return hashlib.sha256(text).hexdigest()

    def test_loads_pinned_and_executes_only_pinned_bytes(self):
        h = self.put("m.py", b"VALUE = 7\n")
        self.assertEqual(cut.load_libs(self.d, {"m.py": h})["m.py"].VALUE, 7)

    def test_short_reads_are_reassembled_and_overflow_stops(self):
        payload = b"VALUE = 7\\n"
        path = os.path.join(self.d, "m.py")
        with open(path, "wb") as h:
            h.write(payload)
        os.chmod(path, 0o644)
        expected = hashlib.sha256(payload).hexdigest()
        original = os.read
        calls = []

        def short_read(fd, size):
            calls.append(size)
            return original(fd, min(size, 2))

        os.read = short_read
        try:
            self.assertEqual(cut.load_libs(self.d, {"m.py": expected})["m.py"].VALUE, 7)
        finally:
            os.read = original
        self.assertGreater(len(calls), 1)

        with open(path, "ab") as h:
            h.write(b"X")
        with self.assertRaises(cut.Stop) as ctx:
            cut.load_libs(self.d, {"m.py": expected})
        self.assertEqual(str(ctx.exception), "CUT_LIB_TOO_LARGE")

    def test_mismatch_unpinned_missing_unsafe(self):
        h = self.put("m.py", b"X=1\n")
        with self.assertRaises(cut.Stop) as c:
            cut.load_libs(self.d, {"m.py": "0" * 64})
        self.assertEqual(str(c.exception), "CUT_LIB_PIN_MISMATCH")
        with self.assertRaises(cut.Stop) as c:
            cut.load_libs(self.d, {"m.py": None})
        self.assertEqual(str(c.exception), "CUT_LIB_PIN_MISSING")
        with self.assertRaises(cut.Stop) as c:
            cut.load_libs(self.d, {"nope.py": h})
        self.assertEqual(str(c.exception), "CUT_LIB_UNREADABLE")
        os.chmod(os.path.join(self.d, "m.py"), 0o666)
        with self.assertRaises(cut.Stop) as c:
            cut.load_libs(self.d, {"m.py": h})
        self.assertEqual(str(c.exception), "CUT_LIB_UNSAFE")

    def test_symlink_rejected(self):
        h = self.put("real.py", b"X=1\n")
        os.symlink(os.path.join(self.d, "real.py"), os.path.join(self.d, "m.py"))
        with self.assertRaises(cut.Stop):
            cut.load_libs(self.d, {"m.py": h})

    def test_shipped_pins_fixed_and_nonce_journal_pin_still_open(self):
        self.assertIsNone(cut.LIB_PINS["qa525-vnext-go-nonce-journal.py"])  # explicit TODO: cannot run until pinned
        with self.assertRaises(cut.Stop):
            cut.load_libs(self.d)


class Runner(unittest.TestCase):
    def test_only_exact_tuples(self):
        r = cut.make_runner([(D, "image", "inspect", "x")])
        for bad in ((D, "image", "inspect", "y"), (D, "rm", "-f", "x"), ("/bin/sh", "-c", "id"),
                    (D, "image", "inspect", "x", "--extra"), ()):
            with self.assertRaises(cut.Stop) as c:
                r(bad, 5)
            self.assertEqual(str(c.exception), "CUT_ARGV_NOT_ALLOWED")

    def test_allowlist_must_be_docker_strings(self):
        for bad in ([], [("/bin/sh", "-c", "id")], [(D, 1)]):
            with self.assertRaises(cut.Stop):
                cut.make_runner(bad)

    def test_fixed_env_no_shell_stderr_dropped(self):
        seen = {}

        def fake(argv, **kw):
            seen.update(kw, argv=argv)
            return subprocess.CompletedProcess(argv, 0, b"ok", b"SECRETERR")
        orig = subprocess.run
        subprocess.run = fake
        self.addCleanup(setattr, subprocess, "run", orig)
        rc, out = cut.make_runner([(D, "ps")])((D, "ps"), 5, b"in")
        self.assertEqual((rc, out), (0, b"ok"))
        self.assertIs(seen["shell"], False)
        self.assertEqual(seen["env"], cut.ENV)
        self.assertEqual(seen["stderr"], subprocess.DEVNULL)

    def test_stdin_must_be_bytes(self):
        with self.assertRaises(cut.Stop):
            cut.make_runner([(D, "ps")])((D, "ps"), 5, "text")


class PlaneTests(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        d = os.path.realpath(self.t.name)
        self.paths = [os.path.join(d, "l%d" % i) for i in range(5)]
        for p in self.paths:
            with open(p, "w"):
                pass
            os.chmod(p, 0o600)

    def test_held_and_busy(self):
        with cut.Plane(self.paths) as pl:
            pl.assert_held()
            other = os.open(self.paths[2], os.O_RDONLY)
            with self.assertRaises(BlockingIOError):
                fcntl.flock(other, fcntl.LOCK_EX | fcntl.LOCK_NB)
            os.close(other)
            with self.assertRaises(cut.Stop) as c:
                cut.Plane(self.paths).__enter__()
            self.assertEqual(str(c.exception), "CUT_LOCK_BUSY_OR_MISSING")

    def test_missing_and_unsafe_mode(self):
        os.unlink(self.paths[3])
        with self.assertRaises(cut.Stop):
            cut.Plane(self.paths).__enter__()
        with open(self.paths[3], "w"):
            pass
        os.chmod(self.paths[3], 0o666)
        with self.assertRaises(cut.Stop) as c:
            cut.Plane(self.paths).__enter__()
        self.assertEqual(str(c.exception), "CUT_LOCK_UNSAFE")

    def test_replaced_lock_file_is_plane_lost(self):
        with cut.Plane(self.paths) as pl:
            os.unlink(self.paths[0])
            with open(self.paths[0], "w"):
                pass
            with self.assertRaises(cut.Stop) as c:
                pl.assert_held()
            self.assertEqual(str(c.exception), "CUT_PLANE_LOST")

    def test_released_on_exit(self):
        with cut.Plane(self.paths):
            pass
        with cut.Plane(self.paths):
            pass


class Modes(unittest.TestCase):
    def test_fail_closed_skeleton(self):
        self.assertEqual(cut.main([]), 1)
        self.assertEqual(cut.main([cut.EXECUTE_TOKEN]), 1)
        self.assertEqual(cut.main(["--other"]), 2)
        self.assertEqual(cut.main([cut.EXECUTE_TOKEN, "x"]), 2)

    def test_unwired_step_stops(self):
        with self.assertRaises(cut.Stop) as c:
            cut.unwired("FLIGHT")()
        self.assertEqual(str(c.exception), "CUT_STEP_NOT_WIRED_FLIGHT")


if __name__ == "__main__":
    unittest.main()
