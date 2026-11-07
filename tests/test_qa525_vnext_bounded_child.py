"""Bounded child runner: REAL subprocesses, real wall clock, no network, no host."""
import ast
import base64
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import textwrap
import time
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


bc = load("qa525_bounded_child_test", "scripts/qa/qa525-vnext-bounded-child.py")
child = load("qa525_https_fetch_child_test", "scripts/qa/qa525-vnext-https-fetch-child.py")
URL = bc.fetch_lib.ORIGIN + "/health/web"
PY = sys.executable


def alive(pid):
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    try:
        with open("/proc/%d/stat" % pid) as handle:
            return handle.read().rsplit(")", 1)[1].split()[0] != "Z"
    except OSError:
        return False


class BoundedChildBase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)

    def script(self, body, name="child.py"):
        path = Path(self.tmp.name) / name
        path.write_text(textwrap.dedent(body))
        return str(path)

    def run_script(self, body, **kw):
        kw.setdefault("timeout", 5)
        return bc.run_bounded([PY, "-I", self.script(body)], **kw)

    def code(self, call):
        with self.assertRaises(bc.Stop) as c:
            call()
        self.assertIsNone(c.exception.__cause__)
        return str(c.exception)


class BoundedChildRun(BoundedChildBase):
    def test_normal_run_returns_code_and_stdout_only(self):
        code, out = self.run_script("import sys; print('hi'); sys.stderr.write('SECRET-ERR'); sys.exit(7)")
        self.assertEqual((code, out), (7, b"hi\n"))

    def test_hung_child_is_killed_at_the_deadline(self):
        # a blocking read stands in for an unbounded getaddrinfo
        body = "import socket; a, b = socket.socketpair(); a.recv(1)"
        started = time.monotonic()
        self.assertEqual(self.code(lambda: self.run_script(body, timeout=0.6)), "BOUNDED_TIMEOUT")
        self.assertLess(time.monotonic() - started, 2.5)

    def test_whole_process_group_dies_not_just_the_leader(self):
        pidfile = Path(self.tmp.name) / "grandchild.pid"
        body = """
            import os, subprocess, sys, time
            g = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
            open(%r, "w").write(str(g.pid))
            time.sleep(60)
        """ % str(pidfile)
        self.assertEqual(self.code(lambda: self.run_script(body, timeout=1.0)), "BOUNDED_TIMEOUT")
        pid = int(pidfile.read_text())
        for _ in range(40):
            if not alive(pid):
                break
            time.sleep(0.05)
        self.assertFalse(alive(pid))

    def test_straggler_left_after_normal_exit_is_killed(self):
        pidfile = Path(self.tmp.name) / "straggler.pid"
        body = """
            import subprocess, sys
            g = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"],
                                 stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            open(%r, "w").write(str(g.pid))
        """ % str(pidfile)
        self.assertEqual(self.run_script(body)[0], 0)
        pid = int(pidfile.read_text())
        for _ in range(40):
            if not alive(pid):
                break
            time.sleep(0.05)
        self.assertFalse(alive(pid))

    def test_output_flood_is_cut_and_group_killed(self):
        body = "import sys\nwhile True: sys.stdout.write('a' * 65536)"
        started = time.monotonic()
        self.assertEqual(self.code(lambda: self.run_script(body, max_output=100000)),
                         "BOUNDED_OUTPUT_TOO_LARGE")
        self.assertLess(time.monotonic() - started, 3)

    def test_stderr_flood_counts_but_is_never_returned(self):
        body = "import sys\nwhile True: sys.stderr.write('x' * 65536)"
        self.assertEqual(self.code(lambda: self.run_script(body, max_output=100000)),
                         "BOUNDED_OUTPUT_TOO_LARGE")

    def test_slow_drip_cannot_extend_the_deadline(self):
        body = "import sys, time\nfor _ in range(200):\n    sys.stdout.write('a'); sys.stdout.flush(); time.sleep(0.1)"
        started = time.monotonic()
        self.assertEqual(self.code(lambda: self.run_script(body, timeout=0.7)), "BOUNDED_TIMEOUT")
        self.assertLess(time.monotonic() - started, 2.5)

    def test_environment_is_minimal_and_stdin_closed(self):
        os.environ["QA525_TEST_SECRET"] = "leak"
        self.addCleanup(os.environ.pop, "QA525_TEST_SECRET", None)
        body = "import json, os, sys; print(json.dumps([sorted(os.environ), os.getcwd(), sys.stdin.read()]))"
        _, out = self.run_script(body)
        names, cwd, data = json.loads(out)
        self.assertNotIn("QA525_TEST_SECRET", names)
        self.assertTrue(set(names) <= {"PATH", "LANG", "LC_ALL", "LC_CTYPE"})
        self.assertEqual((cwd, data), ("/", ""))

    def test_stdin_is_devnull_even_if_the_parent_has_a_live_pipe(self):
        read_end, write_end = os.pipe()
        saved = os.dup(0)
        try:
            os.dup2(read_end, 0)
            os.write(write_end, b"parent-secret")
            _, out = self.run_script("import os; print(os.readlink('/proc/self/fd/0'))")
        finally:
            os.dup2(saved, 0)
            for fd in (saved, read_end, write_end):
                os.close(fd)
        self.assertEqual(out.strip(), b"/dev/null")

    def test_runs_in_its_own_session(self):
        _, out = self.run_script("import os; print(os.getsid(0) == os.getpid())")
        self.assertEqual(out.strip(), b"True")

    def test_argv_and_limits_are_validated_before_spawning(self):
        for bad in ([], "x", ["python3"], ["/a/../b"], ["/ok", 1], ["/ok", "a\x00b"], ["/a b"],
                    ["/ok"] * 17, ["/ok", "x" * 3000], None):
            with self.subTest(argv=bad):
                self.assertEqual(self.code(lambda: bc.run_bounded(bad, timeout=1)), "BOUNDED_ARGV_INVALID")
        for timeout in (0, -1, 121, None, True, "1"):
            with self.subTest(timeout=timeout):
                self.assertEqual(self.code(lambda: bc.run_bounded([PY], timeout=timeout)),
                                 "BOUNDED_LIMITS_INVALID")
        for cap in (0, True, 10 ** 9, "x"):
            with self.subTest(cap=cap):
                self.assertEqual(self.code(lambda: bc.run_bounded([PY], timeout=1, max_output=cap)),
                                 "BOUNDED_LIMITS_INVALID")

    def test_missing_executable_is_fixed_code_without_leak(self):
        self.assertEqual(self.code(lambda: bc.run_bounded(["/nonexistent/qa525-x"], timeout=1)),
                         "BOUNDED_SPAWN_FAILED")


class BoundedChildSelectorFailure(BoundedChildBase):
    def test_selector_creation_failure_still_kills_and_reaps_the_child(self):
        import os, time
        from unittest import mock
        pidfile = Path(self.tmp.name) / "pid"
        body = "import os,time\nopen(%r,'w').write(str(os.getpid()))\ntime.sleep(30)\n" % str(pidfile)
        with mock.patch.object(bc.selectors, "DefaultSelector", side_effect=OSError("no fds")):
            self.assertEqual(self.code(lambda: self.run_script(body)), "BOUNDED_RUN_FAILED")
        pid = None
        for _ in range(50):
            if pidfile.exists() and pidfile.read_text():
                pid = int(pidfile.read_text()); break
            time.sleep(0.05)
        if pid is not None:  # the child had time to start: it must be gone
            with self.assertRaises(ProcessLookupError):
                os.kill(pid, 0)


class BoundedChildFetchAdapter(BoundedChildBase):
    def fake(self, code, out):
        return lambda argv, timeout: (code, out)

    def call(self, run, **kw):
        args = dict(python="/usr/bin/python3", child="/opt/x/child.py", timeout=25, run=run)
        args.update(kw)
        return bc.fetch_via_child(URL, **args)

    def good(self, body=b"ok"):
        return json.dumps({"status": 200, "final_url": URL,
                           "body": base64.b64encode(body).decode()}).encode()

    def test_good_result_and_exact_argv(self):
        seen = {}
        def run(argv, timeout):
            seen.update(argv=argv, timeout=timeout)
            return 0, self.good()
        self.assertEqual(self.call(run), (200, URL, b"ok"))
        self.assertEqual(seen, {"argv": ["/usr/bin/python3", "-I", "/opt/x/child.py", URL], "timeout": 25})

    def test_only_allowed_urls_and_clean_paths(self):
        run = lambda *a, **k: self.fail("must not spawn")
        with self.assertRaises(bc.Stop):
            bc.fetch_via_child(URL + "?x=1", python="/usr/bin/python3", child="/a/b", timeout=1, run=run)
        for bad in (None, "rel", "/a/../b", "/a b"):
            with self.assertRaises(bc.Stop):
                bc.fetch_via_child(URL, python=bad, child="/a/b", timeout=1, run=run)
            with self.assertRaises(bc.Stop):
                bc.fetch_via_child(URL, python="/usr/bin/python3", child=bad, timeout=1, run=run)

    def test_non_string_url_is_a_fixed_refusal_not_a_type_error(self):
        run = lambda *a, **k: self.fail("must not spawn")
        for bad in ([URL], {"u": URL}, b"x", 7, None, (URL,)):
            with self.subTest(bad=bad):
                self.assertEqual(self.code(lambda: bc.fetch_via_child(
                    bad, python="/usr/bin/python3", child="/a/b", timeout=1, run=run)),
                    "BOUNDED_URL_NOT_ALLOWED")

    def test_child_errors_map_to_fixed_codes(self):
        err = lambda c: json.dumps({"error": c}).encode()
        self.assertEqual(self.code(lambda: self.call(self.fake(2, err("HTTPSFETCH_TLS_FAILED")))),
                         "HTTPSFETCH_TLS_FAILED")
        for code, out in ((2, err("rm -rf /")), (2, err("HTTPSFETCH_x")), (2, err("HTTPSFETCH_INVENTED_CODE")),
                          (2, b'{"error":"HTTPSFETCH_DEADLINE","error":"HTTPSFETCH_FAILED"}'),
                          (0, b'{"status":200,"status":500,"final_url":"%s","body":""}' % URL.encode()),
                          (0, b'{"status":NaN,"final_url":"%s","body":""}' % URL.encode()), (3, b""), (1, self.good()),
                          (2, b"not json"), (3, err("HTTPSFETCH_TLS_FAILED")), (0, b""), (0, b"[]"), (-9, b"")):
            with self.subTest(code=code, out=out):
                self.assertEqual(self.code(lambda: self.call(self.fake(code, out))), "BOUNDED_CHILD_FAILED")

    def test_allowed_error_codes_match_exactly_what_the_fetch_library_can_emit(self):
        import re as _re
        source = (ROOT / "scripts/qa/qa525-vnext-https-fetch.py").read_text()
        self.assertEqual(set(_re.findall(r"HTTPSFETCH_[A-Z_]+", source)), set(bc.FETCH_CODES))
        for code in bc.FETCH_CODES:
            self.assertEqual(self.code(lambda: self.call(self.fake(2, json.dumps({"error": code}).encode()))), code)

    def test_malformed_success_documents_are_rejected(self):
        b64 = base64.b64encode(b"x").decode()
        docs = [{"status": 200, "final_url": URL}, {"status": "200", "final_url": URL, "body": b64},
                {"status": True, "final_url": URL, "body": b64}, {"status": 99, "final_url": URL, "body": b64},
                {"status": 200, "final_url": 1, "body": b64}, {"status": 200, "final_url": URL, "body": "!!"},
                {"status": 200, "final_url": URL, "body": b64, "extra": 1},
                {"status": 200, "final_url": URL,
                 "body": base64.b64encode(b"a" * (bc.fetch_lib.MAX_BODY + 1)).decode()}]
        for doc in docs:
            with self.subTest(doc=str(doc)[:60]):
                self.assertEqual(self.code(lambda: self.call(self.fake(0, json.dumps(doc).encode()))),
                                 "BOUNDED_CHILD_FAILED")

    def test_real_subprocess_through_the_bounded_runner(self):
        body = "import base64, json; print(json.dumps({'status': 200, 'final_url': %r, 'body': base64.b64encode(b'hi').decode()}))" % URL
        script = self.script(body)
        self.assertEqual(bc.fetch_via_child(URL, python=PY, child=script, timeout=5), (200, URL, b"hi"))

    def test_real_child_entry_refuses_a_disallowed_url_before_any_network(self):
        # the URL is rejected inside the child before any socket is opened
        code, out = bc.run_bounded(
            [PY, "-I", str(ROOT / "scripts/qa/qa525-vnext-https-fetch-child.py"),
             "https://evil.example/health/web"], timeout=5)
        self.assertEqual((code, json.loads(out)), (2, {"error": "HTTPSFETCH_URL_NOT_ALLOWED"}))


class BoundedChildEntry(unittest.TestCase):
    class Lib:
        class Stop(Exception):
            pass

        def __init__(self, result=None, exc=None):
            self.result, self.exc = result, exc

        def build_fetch(self):
            def fetch(url):
                if self.exc:
                    raise self.exc
                return self.result
            return fetch

    def test_success_error_and_crash_contracts(self):
        out = io.StringIO()
        self.assertEqual(child.main(["c", URL], lib=self.Lib((200, URL, b"ok")), out=out), 0)
        doc = json.loads(out.getvalue())
        self.assertEqual((doc["status"], base64.b64decode(doc["body"])), (200, b"ok"))
        out = io.StringIO()
        lib = self.Lib(exc=None)
        lib.exc = lib.Stop("HTTPSFETCH_DEADLINE")
        self.assertEqual(child.main(["c", URL], lib=lib, out=out), 2)
        self.assertEqual(json.loads(out.getvalue()), {"error": "HTTPSFETCH_DEADLINE"})
        out = io.StringIO()
        self.assertEqual(child.main(["c", URL], lib=self.Lib(exc=RuntimeError("secret")), out=out), 3)
        self.assertEqual(out.getvalue(), "")
        self.assertEqual(child.main(["c"], lib=self.Lib()), 3)
        self.assertEqual(child.main(["c", URL, "x"], lib=self.Lib()), 3)


class BoundedChildSurface(unittest.TestCase):
    def test_no_default_executable_no_shell_no_env_inheritance(self):
        src = (ROOT / "scripts/qa/qa525-vnext-bounded-child.py").read_text()
        tree = ast.parse(src)
        mods = {a.name for n in ast.walk(tree) if isinstance(n, ast.Import) for a in n.names}
        self.assertEqual(mods, {"base64", "importlib.util", "json", "os", "re", "selectors",
                                "signal", "subprocess", "time"})
        names = {n.attr for n in ast.walk(tree) if isinstance(n, ast.Attribute)}
        for forbidden in ("system", "environ", "getenv", "popen", "execv", "execve", "fork",
                          "check_output", "run", "call"):
            self.assertNotIn(forbidden, names)
        for node in ast.walk(tree):
            if isinstance(node, ast.keyword) and node.arg == "shell":
                self.assertIs(node.value.value, False)
        self.assertNotIn("docker", src.lower().replace("never runs docker", ""))

    def test_child_entry_reads_no_environment(self):
        tree = ast.parse((ROOT / "scripts/qa/qa525-vnext-https-fetch-child.py").read_text())
        names = {n.attr for n in ast.walk(tree) if isinstance(n, ast.Attribute)}
        for forbidden in ("environ", "getenv", "system", "Popen", "load_cert_chain"):
            self.assertNotIn(forbidden, names)


if __name__ == "__main__":
    unittest.main()
