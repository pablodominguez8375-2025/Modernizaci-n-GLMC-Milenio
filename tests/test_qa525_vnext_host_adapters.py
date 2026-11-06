"""Hermetic tests for the QA525 vNext host adapters. No process, Docker or host."""
import ast
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, BASE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ad = load("qa525_vnext_host_adapters_t", "qa525-vnext-host-adapters.py")
c = ad.containment
NONCE = "n" * 64
FILES = ["/w/base.yml", "/w/dual-new-api.json"]


def argv(service="api", files=None, project="centenario"):
    unit = c.unit_name(NONCE, service)
    return c.systemd_run_argv(unit, project, "/opt/centenario", "/etc/pmgm/qa.env",
                              files or FILES, service)


class Recorder:
    def __init__(self, code=0, stdout="x\n"):
        self.calls, self.code, self.stdout = [], code, stdout

    def __call__(self, command, **kwargs):
        self.calls.append((command, kwargs))
        return subprocess.CompletedProcess(command, self.code, stdout=self.stdout)


class RunnerTests(unittest.TestCase):
    def test_canonical_argv_runs_without_shell_env_or_captured_output(self):
        rec = Recorder(code=0)
        self.assertEqual(ad.scope_runner(argv(), run=rec), 0)
        command, kw = rec.calls[0]
        self.assertEqual(command, argv())
        self.assertIs(kw["shell"], False)
        self.assertEqual(kw["env"]["PATH"], "/usr/bin:/bin")
        self.assertIs(kw["stdout"], subprocess.DEVNULL)
        self.assertIs(kw["stderr"], subprocess.DEVNULL)
        self.assertIs(kw["stdin"], subprocess.DEVNULL)
        self.assertLessEqual(kw["timeout"], 150)

    def test_nonzero_exit_code_is_returned_for_the_switcher_to_judge(self):
        self.assertEqual(ad.scope_runner(argv("web"), run=Recorder(code=3)), 3)

    def test_every_deviation_from_the_canonical_shape_is_refused_before_running(self):
        good = argv()
        mutations = {
            "binary": [good[0].replace("systemd-run", "sh")] + good[1:],
            "extra_token": good + ["--rm"],
            "prefix_token": ["/usr/bin/env"] + good,
            "docker_verb": good[:8] + ["rm"] + good[9:],
            "service": good[:-1] + ["db"],
            "other_unit": [x.replace("--unit=", "--unit=x") if x.startswith("--unit=") else x for x in good],
            "no_wait": [x for x in good if x != "--wait"],
            "pull_always": ["always" if x == "never" else x for x in good],
            "build": [x for x in good if x != "--no-build"],
            "shell_meta": good[:12] + ["/opt/x;reboot"] + good[13:],
            "empty": [],
            "string": "systemctl stop docker",
        }
        for name, bad in mutations.items():
            with self.subTest(name=name):
                rec = Recorder()
                with self.assertRaises(ad.Stop):
                    ad.scope_runner(bad, run=rec)
                self.assertEqual(rec.calls, [])

    def test_unit_must_match_the_service_it_recreates(self):
        bad = argv("api")
        bad[-1] = "web"
        rec = Recorder()
        with self.assertRaises(ad.Stop):
            ad.scope_runner(bad, run=rec)
        self.assertEqual(rec.calls, [])

    def test_process_errors_become_fixed_codes_without_echo(self):
        def boom(command, **kwargs):
            raise OSError("secret /root/token")
        with self.assertRaises(ad.Stop) as ctx:
            ad.scope_runner(argv(), run=boom)
        self.assertEqual(str(ctx.exception), "ADAPTER_RUN_FAILED")
        with self.assertRaisesRegex(ad.Stop, "^ADAPTER_RUN_FAILED$"):
            ad.scope_runner(argv(), run=lambda *a, **k: object())


class ReadAndMiscTests(unittest.TestCase):
    def test_docker_read_allows_only_read_verbs(self):
        rec = Recorder(stdout="  ok \n")
        self.assertEqual(ad.docker_read(["ps", "-a"], run=rec), "ok")
        self.assertEqual(rec.calls[0][0][:3], ["/usr/bin/docker", "--host", "unix:///var/run/docker.sock"])
        for bad in (["rm", "x"], ["exec", "x"], ["image", "rm", "x"], ["compose", "up"], [], "ps"):
            with self.subTest(bad=bad):
                rec2 = Recorder()
                with self.assertRaisesRegex(ad.Stop, "^ADAPTER_VERB_REFUSED$"):
                    ad.docker_read(bad, run=rec2)
                self.assertEqual(rec2.calls, [])

    def test_docker_read_failure_is_a_fixed_code(self):
        def boom(command, **kwargs):
            raise subprocess.CalledProcessError(1, command, output="secret")
        with self.assertRaisesRegex(ad.Stop, "^ADAPTER_READ_FAILED$"):
            ad.docker_read(["ps"], run=boom)

    def test_settle_is_bounded(self):
        slept = []
        ad.settle_pause(1.5, sleep=slept.append)
        self.assertEqual(slept, [1.5])
        for bad in (-1, 31, "2", None):
            with self.subTest(bad=bad):
                with self.assertRaises(ad.Stop):
                    ad.settle_pause(bad, sleep=slept.append)

    def test_cgroup_scope_factory_builds_the_read_only_view(self):
        with tempfile.TemporaryDirectory() as tmp:
            scope = ad.cgroup_scope(c.unit_name(NONCE, "api"), Path(tmp))
            self.assertEqual(scope.root, Path(tmp))
        with self.assertRaisesRegex(ad.Stop, "^ADAPTER_SCOPE_INVALID$"):
            ad.cgroup_scope("not-a-unit")

    def test_module_has_no_entrypoint_network_or_secret_access(self):
        source = (BASE / "qa525-vnext-host-adapters.py").read_text()
        imported = set()
        for node in ast.walk(ast.parse(source)):
            if isinstance(node, ast.Import):
                imported |= {a.name.split(".")[0] for a in node.names}
            elif isinstance(node, ast.ImportFrom):
                imported.add((node.module or "").split(".")[0])
        self.assertEqual(imported, {"importlib", "pathlib", "subprocess", "time"})
        for token in ("shell=True", "os.system", "socket", "urllib", "os.environ", "getenv", "/root/.ssh"):
            self.assertNotIn(token, source)
        with self.assertRaises(SystemExit):
            exec(compile(source, "a", "exec"),
                 {"__name__": "__main__", "__file__": str(BASE / "x.py")})


if __name__ == "__main__":
    unittest.main()
