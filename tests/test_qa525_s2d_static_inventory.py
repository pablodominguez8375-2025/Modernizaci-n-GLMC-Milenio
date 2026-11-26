"""Hermetic tests for the S2d static inventory (synthetic files only; no real host content)."""
import hashlib
import importlib.util
import io
import json
import os
import pathlib
import stat
import tempfile
import unittest
from contextlib import redirect_stdout

SRC = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa/qa525-s2d-static-inventory.py"
spec = importlib.util.spec_from_file_location("s2d", SRC)
s2d = importlib.util.module_from_spec(spec)
spec.loader.exec_module(s2d)

SECRET = "SUPERSECRETTOKEN123"
UID = os.geteuid()


class Base(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dir = os.path.realpath(self.tmp.name)
        self.saved, self.saved_paths = dict(s2d.EXPECTED), dict(s2d.PATHS)
        self.addCleanup(lambda: (s2d.EXPECTED.clear(), s2d.EXPECTED.update(self.saved),
                                 s2d.PATHS.clear(), s2d.PATHS.update(self.saved_paths)))

    def put(self, fid, text, mode=0o600, pin=True):
        p = os.path.join(self.dir, fid + ".py")
        data = text if isinstance(text, bytes) else text.encode()
        with open(p, "wb") as h:
            h.write(data)
        os.chmod(p, mode)
        if pin:
            s2d.EXPECTED[fid] = hashlib.sha256(data).hexdigest()
        s2d.PATHS[fid] = p
        return p

    def inspect(self, text, **kw):
        p = self.put("qa-status", text, **kw)
        return s2d.inspect("qa-status", p, UID)


class Analysis(Base):
    def test_clean_is_never_pass(self):
        r = self.inspect("import os\nx = 1\n")
        self.assertEqual(r["status"], "ANALYZED")
        self.assertEqual(r["verdict"], "NO_UNRESOLVED_INDICATORS_STATIC_ONLY")
        self.assertNotIn("PASS", json.dumps(r))

    def test_categories_and_alias(self):
        src = ("import subprocess as sp, fcntl as f, os as o, shutil\n"
               "from pathlib import Path\n"
               "sp.run(['systemctl','stop','x'])\nsp.run(['docker','ps'])\n"
               "f.flock(3, 1)\no.replace('a','b')\no.chmod('a',0o600)\nshutil.rmtree('d')\n"
               "open('p','w')\nPath('q').write_text('z')\no.environ['A']\n")
        c = self.inspect(src)["counts"]
        self.assertEqual((c["subprocess"], c["service_ref"], c["docker_ref"], c["lock_flock"]), (2, 1, 1, 1))
        self.assertEqual((c["fs_destructive"], c["fs_perm"]), (2, 1))
        self.assertGreaterEqual(c["fs_write"], 2)
        self.assertGreaterEqual(c["env_access"], 1)

    def test_dynamic_and_unresolved_are_unknown(self):
        for src in ("exec('1')", "eval('1')", "__import__('os')", "import importlib\nimportlib.import_module('x')",
                    "import thirdparty", "from x import *", "f = g()()", "import os\nos.system('x')"):
            self.assertEqual(self.inspect(src)["verdict"], "UNKNOWN", src)

    def test_readonly_open_is_not_write(self):
        c = self.inspect("open('p','rb')\nimport os\nos.open('p', os.O_RDONLY)\n")["counts"]
        self.assertEqual(c["fs_write"], 0)

    def test_os_open_write_flags(self):
        c = self.inspect("import os\nos.open('p', os.O_WRONLY | os.O_CREAT)\n")["counts"]
        self.assertEqual(c["fs_write"], 1)


class Safety(Base):
    def test_hash_mismatch_never_parsed(self):
        p = self.put("qa-status", "import os\n")
        s2d.EXPECTED["qa-status"] = "0" * 64
        r = s2d.inspect("qa-status", p, UID)
        self.assertEqual(r["status"], "HASH_MISMATCH")
        self.assertIsNone(r["counts"])
        self.assertIsNone(r["sha256"])
        self.assertIsNone(r["size"])

    def test_symlink_fifo_hardlink_mode_size(self):
        p = self.put("qa-status", "x=1\n")
        link = os.path.join(self.dir, "ln.py")
        os.symlink(p, link)
        self.assertEqual(s2d.inspect("qa-status", link, UID)["status"], "UNREADABLE")  # O_NOFOLLOW on last component
        fifo = os.path.join(self.dir, "fifo.py")
        os.mkfifo(fifo)
        self.assertEqual(s2d.inspect("qa-status", fifo, UID)["status"], "NOT_REGULAR")
        os.link(p, os.path.join(self.dir, "hard.py"))
        self.assertEqual(s2d.inspect("qa-status", p, UID)["status"], "METADATA_VIOLATION")
        os.unlink(os.path.join(self.dir, "hard.py"))
        os.chmod(p, 0o660)
        self.assertEqual(s2d.inspect("qa-status", p, UID)["status"], "METADATA_VIOLATION")
        os.chmod(p, 0o600)
        old = s2d.MAX_SIZE
        s2d.MAX_SIZE = 2
        self.addCleanup(setattr, s2d, "MAX_SIZE", old)
        self.assertEqual(s2d.inspect("qa-status", p, UID)["status"], "TOO_LARGE")

    def test_wrong_owner(self):
        p = self.put("qa-status", "x=1\n")
        self.assertEqual(s2d.inspect("qa-status", p, UID + 1)["status"], "METADATA_VIOLATION")

    def test_mutation_during_read(self):
        p = self.put("qa-status", "x=1\n")
        orig = os.read

        def mutate(fd, n):
            d = orig(fd, n)
            with open(p, "r+b") as h:
                h.write(b"y")
            os.utime(p, ns=(1, 2))
            return d
        os.read = mutate
        try:
            self.assertEqual(s2d.inspect("qa-status", p, UID)["status"], "CHANGED_DURING_READ")
        finally:
            os.read = orig


class NoLeak(Base):
    def test_secret_never_in_output(self):
        src = ("%s = '%s'\ndef %s_fn():\n    return open('/x/%s', 'w')\nimport subprocess\n"
               "subprocess.run(['echo', '%s'])\n" % (SECRET, SECRET, SECRET, SECRET, SECRET))
        out = json.dumps(self.inspect(src))
        self.assertNotIn(SECRET, out)

    def test_secret_in_syntax_error_and_bytes(self):
        out = json.dumps(self.inspect("x = '%s\n def (" % SECRET))
        self.assertNotIn(SECRET, out)
        self.assertEqual(json.loads(out)["status"], "PARSE_ERROR")
        out = json.dumps(self.inspect(b"\xff\xfe" + SECRET.encode()))
        self.assertNotIn(SECRET, out)

    def test_secret_in_path_and_unreadable(self):
        out = json.dumps(s2d.inspect("qa-status", "/nonexistent/" + SECRET, UID))
        self.assertNotIn(SECRET, out)

    def test_output_keys_closed(self):
        r = self.inspect("import os\n")
        self.assertEqual(set(r), {"id", "status", "sha256", "size", "verdict", "counts"})
        self.assertEqual(set(r["counts"]), set(s2d.CATEGORIES))


class Findings(Base):
    """Regressions for the five ChatGPT REQUEST_CHANGES findings on exact head b18a7913."""

    def test_f1_alias_by_assignment_and_getattr(self):
        for src in ("f = eval\nf('1')", "import os\ng = getattr(os, 'sys' + 'tem')\ng('x')",
                    "import os\ngetattr(os, 'system')('x')", "def eval(x):\n    return x\neval = exec\neval('1')",
                    "mystery('1')", "import os\nh = os.system\nh('x')"):
            self.assertEqual(self.inspect(src)["verdict"], "UNKNOWN", src)

    def test_f1_os_write_and_shadowing(self):
        self.assertGreaterEqual(self.inspect("import os\nos.write(1, b'x')")["counts"]["fs_write"], 1)
        # a local def shadowing a builtin name is resolved (not flagged), its body is still scanned
        c = self.inspect("def run():\n    return 1\nrun()")["counts"]
        self.assertEqual((c["unresolved_call"], c["indirect_call"]), (0, 0))
        # reassigning an imported alias to something unresolved makes calls conservative
        self.assertEqual(self.inspect("import os\nos = make()\nos.system('x')")["verdict"], "UNKNOWN")

    def test_f1_ambiguous_flags(self):
        self.assertEqual(self.inspect("import os\nos.open('p', FLAGS)")["verdict"], "UNKNOWN")

    def test_f2_argparse_does_not_echo_arguments(self):
        import contextlib
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            rc = s2d.main(["--bogus-" + SECRET, SECRET, "--file", SECRET, "--expect-uid", SECRET])
        self.assertEqual(rc, 2)
        self.assertNotIn(SECRET, out.getvalue() + err.getvalue())
        self.assertEqual(out.getvalue() + err.getvalue(), "")

    def test_f3_paths_bound_to_contract(self):
        a = []
        for fid in s2d.EXPECTED:
            a += ["--file", "%s=%s" % (fid, self.put(fid, "import os\n"))]
        extra = ["--expect-uid", str(UID), "--allow-non-root"] if UID else []
        buf = io.StringIO()
        with redirect_stdout(buf):
            self.assertEqual(s2d.main(a + extra), 0)
            # any path different from the contract -> rejected, nothing opened
            other = os.path.join(self.dir, "other.py")
            b = [x if not x.startswith("qa-status=") else "qa-status=" + other for x in a]
            self.assertEqual(s2d.main(b + extra), 2)
            # unconfirmed contract path (None) -> rejected
            s2d.PATHS["prepare-local"] = None
            self.assertEqual(s2d.main(a + extra), 2)

    def test_f3_shipped_contract_not_confirmed_yet(self):
        self.assertEqual(self.saved_paths["run-with-backup-20261022"],
                         "/opt/centenario/qa-auto-lite/run-with-backup-20261022.py")
        self.assertTrue(any(v is None for v in self.saved_paths.values()))  # runs only after reviewed confirmation

    def test_f3_ancestor_symlink_and_writable_dir(self):
        real = os.path.join(self.dir, "real")
        os.mkdir(real)
        p = os.path.join(real, "f.py")
        pathlib.Path(p).write_bytes(b"x=1\n")
        os.chmod(p, 0o600)
        lnk = os.path.join(self.dir, "lnk")
        os.symlink(real, lnk)
        self.assertEqual(s2d.read_safe(os.path.join(lnk, "f.py"), UID)[0], "ANCESTOR_UNTRUSTED")
        os.chmod(real, 0o777)
        self.assertEqual(s2d.read_safe(p, UID)[0], "ANCESTOR_UNTRUSTED")

    def test_f4_analysis_failure_is_enumerated(self):
        orig = s2d.Scan.visit
        s2d.Scan.visit = lambda self, node: (_ for _ in ()).throw(RuntimeError(SECRET))
        self.addCleanup(setattr, s2d.Scan, "visit", orig)
        r = self.inspect("x = 1\n")
        self.assertEqual(r["status"], "ANALYSIS_ERROR")
        self.assertNotIn(SECRET, json.dumps(r))

    def test_f4_deep_nesting_recursion(self):
        r = self.inspect("x = " + "(" * 400 + "1" + ")" * 400 + "\n")
        self.assertIn(r["status"], ("PARSE_ERROR", "ANALYZED", "ANALYSIS_ERROR"))
        r = self.inspect("x = " + "[" * 3000 + "]" * 3000 + "\n")
        self.assertIn(r["status"], ("PARSE_ERROR", "ANALYZED", "ANALYSIS_ERROR"))


class Cli(Base):
    def args(self, skip=None, extra=()):
        out = []
        for fid in s2d.EXPECTED:
            if fid != skip:
                out += ["--file", "%s=%s" % (fid, self.put(fid, "import os\n"))]
        return out + list(extra)

    def run_cli(self, argv):
        buf = io.StringIO()
        with redirect_stdout(buf):
            rc = s2d.main(argv)
        return rc, buf.getvalue()

    def test_ok_and_closed_list(self):
        rc, out = self.run_cli(self.args(extra=["--expect-uid", str(UID), "--allow-non-root"] if UID else []))
        self.assertEqual(rc, 0)
        self.assertEqual(len(json.loads(out)["files"]), 5)

    def test_missing_unknown_duplicate_and_nonroot_rejected(self):
        a = self.args()
        self.assertEqual(self.run_cli(self.args(skip="prepare-local"))[0], 2)
        self.assertEqual(self.run_cli(a + ["--file", "other=/x"])[0], 2)
        self.assertEqual(self.run_cli(a + ["--file", "qa-status=/x"])[0], 2)
        self.assertEqual(self.run_cli(a + ["--expect-uid", "1234"])[0], 2)

    def test_expected_hashes_pinned(self):
        self.assertEqual(s2d.EXPECTED["run-with-backup-20261022"],
                         "b31b67a2a0d35cecd8e12e8b5fbcc072814a7fbb308aa9bf41c6213af6a38ce7")


if __name__ == "__main__":
    unittest.main()
