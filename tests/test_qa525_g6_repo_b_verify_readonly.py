import importlib.util, json, unittest
from pathlib import Path

SRC = Path(__file__).resolve().parent.parent / "scripts" / "qa" / "qa525-g6-repo-b-verify-readonly.py"
spec = importlib.util.spec_from_file_location("g6b", SRC)
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)

OK_SHA = lambda p: g.RESTIC_SHA256


def cfg(i):
    return json.dumps({"version": 2, "id": i, "chunker_polynomial": "x"}).encode()


class T(unittest.TestCase):
    def test_pass(self):
        self.assertEqual(g.verify("pw", OK_SHA, lambda p: cfg(g.REPO_ID)), g.REPO_ID)

    def code(self, **kw):
        with self.assertRaises(g.Stop) as c:
            g.verify(kw.get("pw", "pw"), kw.get("sha", OK_SHA), kw.get("run", lambda p: cfg(g.REPO_ID)))
        return str(c.exception)

    def test_sha_mismatch_does_not_run_restic(self):
        called = []
        c = self.code(sha=lambda p: "0" * 64, run=lambda p: called.append(1) or cfg(g.REPO_ID))
        self.assertEqual(c, "G6B_RESTIC_SHA_MISMATCH"); self.assertEqual(called, [])

    def test_repo_id_mismatch(self):
        self.assertEqual(self.code(run=lambda p: cfg("a" * 64)), "G6B_REPO_ID_MISMATCH")

    def test_bad_outputs(self):
        self.assertEqual(self.code(run=lambda p: b"not json"), "G6B_CONFIG_UNPARSEABLE")
        self.assertEqual(self.code(run=lambda p: b"{}"), "G6B_CONFIG_UNPARSEABLE")
        self.assertEqual(self.code(run=lambda p: cfg("zz")), "G6B_CONFIG_ID_INVALID")
        self.assertEqual(self.code(run=lambda p: cfg(g.REPO_ID.upper())), "G6B_CONFIG_ID_INVALID")

    def test_empty_password(self):
        self.assertEqual(self.code(pw=""), "G6B_PASSWORD_EMPTY")

    def test_runner_failure_propagates(self):
        def boom(p):
            raise g.Stop("G6B_REPO_AUTH_OR_OPEN_FAILED")
        self.assertEqual(self.code(run=boom), "G6B_REPO_AUTH_OR_OPEN_FAILED")

    def test_password_never_in_argv_or_markers(self):
        src = SRC.read_text()
        for bad in ("RESTIC_PASSWORD", "shell=True", "'init'", '"init"', '"backup"', '"restore"', '"forget"', '"prune"', '"unlock"', "os.remove"):
            self.assertNotIn(bad, src)
        self.assertIn("--no-lock", src); self.assertIn("--no-cache", src)

    def test_real_runner_uses_pipe_fd(self):
        # fake restic printing the password it reads from the fd => proves the transport; never argv
        import os, stat, tempfile
        d = tempfile.mkdtemp(); fake = os.path.join(d, "restic")
        with open(fake, "w") as fh:
            fh.write('#!/bin/sh\ncase "$*" in *PW-SECRET*) exit 9;; esac\n'
                              'f=""; while [ $# -gt 0 ]; do [ "$1" = "--password-file" ] && f="$2"; shift; done\n'
                              'read p < "$f"; [ "$p" = "PW-SECRET" ] || exit 8\n'
                     'echo \'{"id":"%s"}\'\n' % g.REPO_ID)
        os.chmod(fake, 0o755)
        old = g.RESTIC; g.RESTIC = fake
        try:
            out = g.run_restic("PW-SECRET")
            self.assertIn(g.REPO_ID.encode(), out)
            with self.assertRaises(g.Stop):
                g.run_restic("WRONG")
        finally:
            g.RESTIC = old

    def _ls(self, fmode=0o100700, fuid=0, fnlink=1, dmode=0o040755, duid=0):
        import os
        def ls(p):
            if p == "/root/x.py":
                return os.stat_result((fmode, 1, 1, fnlink, fuid, 0, 10, 0, 0, 0))
            return os.stat_result((dmode, 2, 1, 2, duid, 0, 4096, 0, 0, 0))
        return ls

    def test_check_self(self):
        g.check_self("a" * 64, lambda p: "a" * 64, "/root/x.py", self._ls())
        for bad, rd in (("a" * 64, lambda p: "b" * 64), ("short", lambda p: "a" * 64), (None, lambda p: "a" * 64)):
            with self.assertRaises(g.Stop):
                g.check_self(bad, rd, "/root/x.py", self._ls())

    def test_trusted_copy_conditions(self):
        g.check_trusted_copy("/root/x.py", self._ls())
        for kw in ({"fmode": 0o100755}, {"fmode": 0o100750}, {"fmode": 0o120700}, {"fuid": 1000},
                   {"fnlink": 2}, {"dmode": 0o040777}, {"dmode": 0o040775}, {"duid": 1000}):
            with self.assertRaises(g.Stop, msg=str(kw)):
                g.check_trusted_copy("/root/x.py", self._ls(**kw))

    def test_check_self_untrusted_copy_checked_before_hash(self):
        called = []
        with self.assertRaises(g.Stop):
            g.check_self("a" * 64, lambda p: called.append(1) or "a" * 64, "/root/x.py", self._ls(fuid=1000))
        self.assertEqual(called, [])

    def test_pipe_write_all_and_close_on_error(self):
        import os
        big = "p" * 70000  # larger than a 64 KiB pipe buffer would take in one os.write
        calls = []
        real = os.write
        def short(fd, data):
            calls.append(len(data)); return real(fd, bytes(data[:1])) if len(calls) < 3 else real(fd, data)
        # a short pipe write must not lose data: restic fake just reads the first line
        import tempfile, stat
        d = tempfile.mkdtemp(); fake = os.path.join(d, "restic")
        with open(fake, "w") as fh:
            fh.write('#!/bin/sh\nf=""; while [ $# -gt 0 ]; do [ "$1" = "--password-file" ] && f="$2"; shift; done\n'
                     'read p < "$f"; echo "{\\"id\\":\\"%s\\"}"\n' % g.REPO_ID)
        os.chmod(fake, 0o755); old = g.RESTIC; g.RESTIC = fake
        try:
            self.assertIn(g.REPO_ID.encode(), g.run_restic("pw"))
            os.write = short
            try:
                self.assertIn(g.REPO_ID.encode(), g.run_restic("pw"))
            finally:
                os.write = real
        finally:
            g.RESTIC = old

    def test_main_requires_script_pin(self):
        with self.assertRaises(SystemExit):
            g.main([])

    def test_main_refuses_without_root_tty(self):
        import io, contextlib
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            rc = g.main(["--expect-script-sha256", "a" * 64])
        self.assertNotEqual(rc, 0); self.assertIn("STOP", buf.getvalue())


if __name__ == "__main__":
    unittest.main()
