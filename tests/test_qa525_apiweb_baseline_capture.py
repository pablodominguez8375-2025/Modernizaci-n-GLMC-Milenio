"""Offline tests for the read-only API+WEB baseline capture CLI: NO Docker, NO host."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import unittest
from unittest.mock import patch

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-apiweb-baseline-capture.py"
spec = importlib.util.spec_from_file_location("qa525_baseline_capture_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

SERVICES = {"api": "a", "web": "b", "keycloak": "c", "postgres": "d", "minio": "e", "gateway": "f", "clamav": "1",
            "db-grants": "2", "migrator": "3", "minio-init": "4"}
ONESHOT = {"db-grants", "migrator", "minio-init"}
WEB_SHA = "9" * 40


def fake_docker(calls, drift=False, state_override=None):
    ids = {n: c * 64 for n, c in SERVICES.items()}
    by_id = {v: k for k, v in ids.items()}

    def run(args):
        calls.append(list(args))
        if args[0] == "ps":
            return "\\n".join(ids.values()).replace("\\n", "\n")
        if args[:2] == ["image", "inspect"]:
            return WEB_SHA
        name = by_id[args[-1]]
        image = "sha256:" + SERVICES[name] * 64
        if drift and name == "api" and sum(1 for c in calls if c[0] == "inspect" and c[-1] == args[-1]) > 1:
            image = "sha256:" + "0" * 64
        layers = "/l/base.yml,/l/" + name + ".yml"
        state = "exited" if name in ONESHOT else "running"
        if state_override and name == state_override[0]:
            state = state_override[1]
        return json.dumps([args[-1], image, state, "t", 0, name, "/w", layers, [], {}, {}, 0])
    return run


class BaselineCaptureTests(unittest.TestCase):
    def setUp(self):
        for path in mod.CHAIN:  # what verify_chain() does before capture() may run
            mod._VERIFIED[path.name] = path.read_bytes()
        self.addCleanup(mod._VERIFIED.clear)

    def test_captures_canonical_baseline_twice_with_only_read_verbs(self):
        calls = []
        result = mod.capture(fake_docker(calls))
        self.assertEqual("BASELINE_CAPTURED_READ_ONLY_NO_APPLY", result["status"])
        self.assertRegex(result["baseline_sha256"], r"^sha256:[0-9a-f]{64}$")  # same format as the apiweb.baseline_sha256 pin
        self.assertEqual(WEB_SHA, result["baseline"]["web_sha"])
        self.assertFalse(result["may_apply"] or result["wrote_files"] or result["go_spent"])
        self.assertEqual({"ps", "inspect", "image"}, {c[0] for c in calls})
        for verb in ("run", "up", "start", "stop", "restart", "rm", "build", "pull", "exec", "compose", "kill"):
            self.assertNotIn(verb, [c[0] for c in calls])

    def test_same_digest_on_repeat(self):
        self.assertEqual(mod.capture(fake_docker([]))["baseline_sha256"], mod.capture(fake_docker([]))["baseline_sha256"])

    def test_drift_between_reads_is_blocked(self):
        with self.assertRaisesRegex(mod.Blocked, "BASELINE_UNSTABLE_BETWEEN_READS"):
            mod.capture(fake_docker([], drift=True))

    def test_stopped_target_or_dirty_oneshot_is_not_a_baseline(self):
        with self.assertRaises(Exception):
            mod.capture(fake_docker([], state_override=("api", "exited")))
        with self.assertRaises(Exception):
            mod.capture(fake_docker([], state_override=("migrator", "running")))

    def test_only_explicit_capture_and_root_and_no_leak(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            self.assertEqual(2, mod.main([]))
            with patch.object(mod.os, "geteuid", return_value=1000), \
                    patch.object(mod.socket, "gethostname", return_value="glmdch"):
                self.assertEqual(2, mod.main(["--capture"]))
            with patch.object(mod, "verify_chain", return_value={}), \
                    patch.object(mod, "capture", side_effect=RuntimeError("TOKEN=secret")):
                self.assertEqual(2, mod.main(["--capture"]))
        self.assertIn("ROOT_REQUIRED", out.getvalue())
        self.assertNotIn("secret", out.getvalue())

    def test_wrong_host_is_refused_before_any_import_or_docker(self):
        with patch.object(mod.socket, "gethostname", return_value="srv01"), \
                patch.object(mod, "_load", side_effect=AssertionError("must not import")), \
                patch.object(mod, "_docker", side_effect=AssertionError("must not run docker")):
            out = io.StringIO()
            with contextlib.redirect_stdout(out), patch.object(mod.os, "geteuid", return_value=0):
                self.assertEqual(2, mod.main(["--capture"]))
        self.assertIn("HOST_NOT_AUTHORIZED", out.getvalue())

    def make_chain(self, tmp):
        files = []
        for name in ("self.py", "adapters.py", "containment.py", "reader.py", "plan.py"):
            p = Path(tmp) / name
            p.write_text("x = 1\n")
            files.append(p)
        return tuple(files)

    def test_chain_trust_rejects_tampered_symlinked_or_writable_dependency(self):
        import os
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            chain = self.make_chain(tmp)
            # non-root test user owns the files: the root-ownership rule must refuse them
            with patch.object(mod.socket, "gethostname", return_value="glmdch"):
                with self.assertRaisesRegex(mod.Blocked, "TRUST_PATH_UNSAFE"):
                    mod.verify_chain(chain, euid=0)
            # simulate a fully trusted tree (root-owned, 0755) by faking lstat, then break one rule at a time
            real = Path.lstat

            def fake(kind):
                def lstat(self_):
                    st = real(self_)
                    mode, uid = st.st_mode & ~0o7022, 0  # a clean root-owned tree, then break ONE rule
                    if kind == "writable" and self_ == chain[2]:
                        mode |= 0o002
                    if kind == "symlink" and self_ == chain[3]:
                        mode = (mode & 0o7777) | stat.S_IFLNK
                    return os.stat_result((mode, st.st_ino, st.st_dev, st.st_nlink, uid, st.st_gid, st.st_size,
                                           st.st_atime, st.st_mtime, st.st_ctime))
                return lstat
            import stat
            with patch.object(mod.socket, "gethostname", return_value="glmdch"):
                with patch.object(Path, "lstat", fake("ok")):
                    digests = mod.verify_chain(chain, euid=0)
                    self.assertEqual(5, len(digests))
                for kind in ("writable", "symlink"):
                    with self.subTest(kind), patch.object(Path, "lstat", fake(kind)):
                        with self.assertRaisesRegex(mod.Blocked, "TRUST_PATH_UNSAFE"):
                            mod.verify_chain(chain, euid=0)
                missing = chain[:4] + (Path(tmp) / "gone.py",)
                with patch.object(Path, "lstat", fake("ok")):
                    with self.assertRaisesRegex(mod.Blocked, "TRUST_PATH_MISSING"):
                        mod.verify_chain(missing, euid=0)

    def test_docker_runs_with_clean_environment_and_trusted_binary(self):
        seen = {}

        def fake_run(argv, **kw):
            seen.update(kw, argv=argv)
            return type("R", (), {"stdout": "ok"})()
        with patch.object(mod, "trusted", return_value=None) as trusted, patch.object(mod.subprocess, "run", fake_run), \
                patch.dict(mod.os.environ, {"DOCKER_HOST": "tcp://evil:2375", "DOCKER_CONFIG": "/tmp/x"}):
            self.assertEqual("ok", mod._docker(["ps"]))
        trusted.assert_called_once_with(mod.DOCKER)
        self.assertEqual({"PATH": "/usr/bin:/bin", "LC_ALL": "C"}, seen["env"])
        self.assertEqual("/usr/bin/docker", seen["argv"][0])

    def test_modules_execute_only_the_bytes_that_were_hashed(self):
        mod._VERIFIED.clear()
        with self.assertRaisesRegex(mod.Blocked, "MODULE_NOT_VERIFIED"):
            mod._load("x_unverified", "qa525-vnext-state-reader.py")
        qa = mod.QA
        mod._VERIFIED["qa525-vnext-state-reader.py"] = (qa / "qa525-vnext-state-reader.py").read_bytes()
        loaded = mod._load("x_verified", "qa525-vnext-state-reader.py")
        self.assertTrue(callable(loaded.StateReader))
        mod._VERIFIED["qa525-vnext-state-reader.py"] = b"raise SystemExit('swapped bytes executed')"
        with self.assertRaises(SystemExit):
            mod._load("x_swapped", "qa525-vnext-state-reader.py")  # proves exec uses the cached bytes, not the file on disk

    def test_chain_covers_every_module_the_helper_executes(self):
        names = {p.name for p in mod.CHAIN}
        self.assertEqual({"qa525-apiweb-baseline-capture.py", "qa525-vnext-host-adapters.py", "qa525-vnext-containment.py",
                          "qa525-vnext-state-reader.py", "qa525-vnext-baseline-plan.py"}, names)

    def test_source_is_read_only(self):
        text = SRC.read_text()
        for op in ("--force-recreate", "docker compose", "'up'", '"up"', "open(", "write_text", "systemctl", "pg_dump"):
            self.assertNotIn(op, text)


if __name__ == "__main__":
    unittest.main()
