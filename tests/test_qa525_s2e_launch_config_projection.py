"""Hermetic tests for the S2e projection (temp files only; no host)."""
import hashlib
import importlib.util
import io
import json
import os
import pathlib
import tempfile
import unittest

SRC = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-s2e-launch-config-projection.py"
spec = importlib.util.spec_from_file_location("s2e", SRC)
s2e = importlib.util.module_from_spec(spec)
spec.loader.exec_module(s2e)
UID = os.geteuid()
D = "a" * 64


class Projection(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        self.d = os.path.realpath(self.t.name)
        self.p = self.d + "/c.json"

    def put(self, data, mode=0o600):
        raw = data if isinstance(data, bytes) else json.dumps(data).encode()
        with open(self.p, "wb") as h:
            h.write(raw)
        os.chmod(self.p, mode)
        return raw

    def run_p(self, uid=UID, path=None):
        out = io.StringIO()
        rc = s2e.project(path or self.p, uid, out)
        return rc, out.getvalue()

    def test_projects_whitelist_only_and_hides_other_values(self):
        raw = self.put({"inbox": "/var/lib/x/in", "socket_path": "/run/s.sock", "token": "TOPSECRET",
                        "g6_pins": {"restic": D, "archive": "/var/lib/a.tgz", "secret": "hunter2"}})
        rc, out = self.run_p()
        self.assertEqual(rc, 0)
        self.assertIn("S2E_FILE_SHA256=" + hashlib.sha256(raw).hexdigest(), out)
        self.assertIn("S2E_INBOX=/var/lib/x/in", out)
        self.assertIn("S2E_G6PIN_secret=NOT_SHOWN", out)
        self.assertNotIn("TOPSECRET", out)
        self.assertNotIn("hunter2", out)
        self.assertIn("S2E_TEMP_ROOT=ABSENT_OR_INVALID", out)

    def test_closed_stops(self):
        self.assertEqual(self.run_p(path=self.d + "/none"), (1, "S2E_STOP:OPEN_FAILED\n"))
        os.symlink(self.p, self.d + "/l")
        self.put({"a": 1})
        self.assertEqual(self.run_p(path=self.d + "/l")[1], "S2E_STOP:OPEN_FAILED\n")
        self.assertEqual(self.run_p(uid=UID + 1)[1], "S2E_STOP:FILE_META\n")
        self.put({"a": 1}, 0o666)
        self.assertEqual(self.run_p()[1], "S2E_STOP:FILE_META\n")
        os.link(self.p, self.d + "/h")
        self.put({"a": 1}, 0o600)
        self.assertEqual(self.run_p()[1], "S2E_STOP:FILE_META\n")  # nlink 2
        os.unlink(self.d + "/h")
        self.put(b"{not json")
        self.assertEqual(self.run_p()[1], "S2E_STOP:JSON_INVALID\n")
        self.put(b"\xff\xfe")
        self.assertEqual(self.run_p()[1], "S2E_STOP:JSON_INVALID\n")
        self.put({"Bad-Key": 1})
        self.assertEqual(self.run_p()[1], "S2E_STOP:SHAPE_OR_KEY_INVALID\n")
        self.put({"g6_pins": {"BAD": D}})
        self.assertEqual(self.run_p()[1], "S2E_STOP:G6_PINS_SHAPE\n")
        self.put(b"")
        self.assertEqual(self.run_p()[1], "S2E_STOP:SIZE\n")
        self.put(b" " * 9000)
        self.assertEqual(self.run_p()[1], "S2E_STOP:SIZE\n")

    def test_dotdot_and_unnormalized_paths_are_not_shown(self):
        self.put({"inbox": "/a/../b", "temp_root": "/a//b", "socket_path": "/a/./b", "lease_dir": "rel/x",
                  "g6_pins": {"p": "/x/../y"}})
        rc, out = self.run_p()
        self.assertEqual(rc, 0)
        for k in ("INBOX", "TEMP_ROOT", "SOCKET_PATH", "LEASE_DIR"):
            self.assertIn("S2E_%s=ABSENT_OR_INVALID" % k, out)
        self.assertIn("S2E_G6PIN_p=NOT_SHOWN", out)

    def test_short_reads_are_reassembled_and_growth_or_truncation_stop(self):
        self.put({"inbox": "/var/in"})
        orig = os.read
        os.read = lambda fd, n: orig(fd, min(n, 3))
        try:
            rc, out = self.run_p()
        finally:
            os.read = orig
        self.assertEqual(rc, 0)
        self.assertIn("S2E_INBOX=/var/in", out)
        os.read = lambda fd, n: b"X" if n == 1 else orig(fd, n)  # probe byte present => file grew
        try:
            self.assertEqual(self.run_p()[1], "S2E_STOP:GREW\n")
        finally:
            os.read = orig
        os.read = lambda fd, n: b""  # file shrank under us
        try:
            self.assertEqual(self.run_p()[1], "S2E_STOP:TRUNCATED\n")
        finally:
            os.read = orig

    def test_fd_is_always_closed(self):
        self.put(b"{bad")
        before = len(os.listdir("/proc/self/fd"))
        for _ in range(5):
            self.run_p()
        self.assertEqual(len(os.listdir("/proc/self/fd")), before)


if __name__ == "__main__":
    unittest.main()
