"""Hermetic tests for the QA525 vNext host adapters. No process, Docker or host."""
import ast
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, BASE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ad = load("qa525_vnext_host_adapters_t", "qa525-vnext-host-adapters.py")
rt = load("qa525_vnext_runtime_for_adapters", "qa525-vnext-runtime.py")
qa = load("qa_lite_compose_adapter_for_adapters", "qa-lite-compose-adapter.py")
c = ad.containment
NONCE = "n" * 64
FILES = ["/w/base.yml", "/w/dual-new-api.json"]
HELD = {"legacy_locks_held": 5, "root_lease_held_synthetically": True}


def argv(service="api", files=None):
    unit = c.unit_name(NONCE, service)
    return c.systemd_run_argv(unit, "centenario", "/opt/centenario", "/etc/pmgm/qa.env",
                              files or FILES, service)


class Plane:
    def __init__(self, status=None, error=None):
        self.status, self.error, self.checks = status if status is not None else dict(HELD), error, 0

    def assert_held(self):
        self.checks += 1
        if self.error:
            raise self.error
        return self.status


class Run:
    def __init__(self, result=0):
        self.calls, self.result = [], result

    def __call__(self, command):
        self.calls.append(list(command))
        return self.result


class RunnerTests(unittest.TestCase):
    def test_no_default_executor_and_no_subprocess_in_the_module(self):
        source = (BASE / "qa525-vnext-host-adapters.py").read_text()
        imported = set()
        for node in ast.walk(ast.parse(source)):
            if isinstance(node, ast.Import):
                imported |= {a.name.split(".")[0] for a in node.names}
            elif isinstance(node, ast.ImportFrom):
                imported.add((node.module or "").split(".")[0])
        self.assertEqual(imported, {"importlib", "pathlib", "re", "time"})
        for token in ("subprocess", "Popen", "os.system", "shell=True", "socket",
                      "urllib", "os.environ", "getenv"):
            self.assertNotIn(token, source)
        self.assertFalse(hasattr(ad, "scope_runner"))
        with self.assertRaises(SystemExit):
            exec(compile(source, "a", "exec"),
                 {"__name__": "__main__", "__file__": str(BASE / "x.py")})

    def test_canonical_argv_without_authority_never_reaches_the_effect(self):
        run = Run()
        for plane in (None, object(), Plane(error=RuntimeError("lost")),
                      Plane(status={"legacy_locks_held": 4, "root_lease_held_synthetically": True}),
                      Plane(status={"legacy_locks_held": 5, "root_lease_held_synthetically": False}),
                      Plane(status="held")):
            with self.subTest(plane=plane):
                if plane is None or not hasattr(plane, "assert_held"):
                    with self.assertRaisesRegex(ad.Stop, "^ADAPTER_AUTHORITY_REQUIRED$"):
                        ad.held_runner(plane, run)
                else:
                    runner = ad.held_runner(plane, run)
                    with self.assertRaisesRegex(ad.Stop, "^ADAPTER_PLANE_NOT_HELD$"):
                        runner(argv())
        with self.assertRaisesRegex(ad.Stop, "^ADAPTER_AUTHORITY_REQUIRED$"):
            ad.held_runner(Plane(), None)
        self.assertEqual(run.calls, [])

    def test_held_plane_and_canonical_argv_run_once_and_return_the_exit_code(self):
        plane, run = Plane(), Run(result=3)
        self.assertEqual(ad.held_runner(plane, run)(argv("web")), 3)
        self.assertEqual(run.calls, [argv("web")])
        self.assertEqual(plane.checks, 1)

    def test_every_deviation_from_the_canonical_shape_is_refused_before_running(self):
        good = argv()
        mutations = {
            "binary": [good[0].replace("systemd-run", "sh")] + good[1:],
            "extra_token": good + ["--rm"], "prefix_token": ["/usr/bin/env"] + good,
            "docker_verb": good[:8] + ["rm"] + good[9:], "service": good[:-1] + ["db"],
            "other_unit": [x.replace("--unit=", "--unit=x") if x.startswith("--unit=") else x for x in good],
            "no_wait": [x for x in good if x != "--wait"],
            "pull_always": ["always" if x == "never" else x for x in good],
            "build": [x for x in good if x != "--no-build"],
            "shell_meta": good[:12] + ["/opt/x;reboot"] + good[13:],
            "empty": [], "string": "systemctl stop docker",
        }
        run, plane = Run(), Plane()
        runner = ad.held_runner(plane, run)
        for name, bad in mutations.items():
            with self.subTest(name=name):
                with self.assertRaises(ad.Stop):
                    runner(bad)
        self.assertEqual(run.calls, [])
        self.assertEqual(plane.checks, 0)  # shape is checked before authority

    def test_effect_errors_are_fixed_codes_without_echo(self):
        def boom(command):
            raise OSError("secret /root/token")
        with self.assertRaises(ad.Stop) as ctx:
            ad.held_runner(Plane(), boom)(argv())
        self.assertEqual(str(ctx.exception), "ADAPTER_RUN_FAILED")
        with self.assertRaisesRegex(ad.Stop, "^ADAPTER_RUN_FAILED$"):
            ad.held_runner(Plane(), lambda command: "0")(argv())


class ReadOnlyDockerTests(unittest.TestCase):
    CID = "a" * 64

    def test_forms_match_what_the_existing_reader_really_issues(self):
        calls = []

        def run(args):
            calls.append(list(args))
            if args[0] == "ps":
                return self.CID
            return json.dumps([self.CID, "sha256:" + "b" * 64, "running", "t", 0, "web",
                               "/w", "/w/base.yml", [], {}, {}, 0])
        read = ad.read_only_docker(run)
        qa.metadata(read)  # raises only if a form the real adapter uses were refused
        self.assertEqual(calls[0], ad.LIST_ARGV)
        self.assertEqual(calls[1][:3], ["inspect", "--format", ad.INSPECT_TEMPLATE])
        self.assertEqual(qa.PROJECT, ad.PROJECT)

    def test_runtime_reader_works_through_the_strict_wrapper(self):
        read = ad.read_only_docker(lambda a: self.CID if a[0] == "ps" else (
            "a" * 40 if a[:2] == ["image", "inspect"] else json.dumps(
                [self.CID, "sha256:" + "b" * 64, "running", "t", 0, "web", "/w",
                 "/w/base.yml", [], {}, {}, 0])))
        self.assertEqual(read(["image", "inspect", "--format", ad.REVISION_FORMAT,
                               "sha256:" + "b" * 64]), "a" * 40)

    def test_arbitrary_flags_formats_and_ids_never_reach_the_effect(self):
        run = Run()
        read = ad.read_only_docker(run)
        env_format = "{{json .Config.Env}}"
        bad = [
            ["inspect", "--format", env_format, self.CID],
            ["inspect", "--format", ad.INSPECT_TEMPLATE, "web"],
            ["inspect", "--format", ad.INSPECT_TEMPLATE, self.CID, "extra"],
            ["inspect", self.CID], ["inspect", "-f", env_format, self.CID],
            ["image", "inspect", "--format", env_format, "sha256:" + "b" * 64],
            ["image", "inspect", "--format", ad.REVISION_FORMAT, "latest"],
            ["ps", "-a"], ad.LIST_ARGV + ["--all"], ad.LIST_ARGV[:-1],
            ["rm", "-f", self.CID], ["exec", self.CID, "sh"], ["logs", self.CID],
            ["compose", "up"], [], "ps",
        ]
        for argv_ in bad:
            with self.subTest(argv=argv_):
                with self.assertRaisesRegex(ad.Stop, "^ADAPTER_VERB_REFUSED$"):
                    read(argv_)
        self.assertEqual(run.calls, [])

    def test_oversized_or_non_text_answers_and_effect_errors_fail_closed(self):
        for answer in ("x" * (ad.MAX_OUTPUT + 1), b"bytes", None):
            with self.subTest(kind=type(answer).__name__):
                with self.assertRaisesRegex(ad.Stop, "^ADAPTER_READ_FAILED$"):
                    ad.read_only_docker(lambda a, r=answer: r)(ad.LIST_ARGV)

        def boom(args):
            raise OSError("secret")
        with self.assertRaises(ad.Stop) as ctx:
            ad.read_only_docker(boom)(ad.LIST_ARGV)
        self.assertEqual(str(ctx.exception), "ADAPTER_READ_FAILED")
        with self.assertRaisesRegex(ad.Stop, "^ADAPTER_RUN_INVALID$"):
            ad.read_only_docker(None)


class MiscTests(unittest.TestCase):
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
            self.assertEqual(ad.cgroup_scope(c.unit_name(NONCE, "api"), Path(tmp)).root, Path(tmp))
        with self.assertRaisesRegex(ad.Stop, "^ADAPTER_SCOPE_INVALID$"):
            ad.cgroup_scope("not-a-unit")


if __name__ == "__main__":
    unittest.main()
