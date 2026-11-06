"""Hermetic tests for the QA525 vNext runtime wiring. No Docker, host or network."""
import importlib.util
import json
from pathlib import Path
import unittest

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, BASE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


rt = load("qa525_vnext_runtime_t", "qa525-vnext-runtime.py")
adapter = load("qa_lite_compose_adapter_t", "qa-lite-compose-adapter.py")
verify = load("qa525_vnext_verify_for_runtime", "qa525-vnext-verify.py")

SHA_OLD, SHA_NEW = "a" * 40, "b" * 40
SERVICES = ("api", "web", "keycloak", "postgres", "minio", "gateway", "clamav",
            "db-grants", "migrator", "minio-init")
ONESHOT = ("db-grants", "migrator", "minio-init")


def cid(i):
    return format(i + 1, "x") * 64


class FakeDocker:
    """Answers only the read shapes metadata() issues; records every argv."""

    def __init__(self, web_image="2" * 64, label=SHA_OLD):
        self.calls, self.web_image, self.label = [], web_image, label

    def __call__(self, args):
        self.calls.append(list(args))
        if args[0] == "ps":
            return "\n".join(cid(i) for i in range(len(SERVICES)))
        if args[0] == "inspect":
            index = int(args[-1][0], 16) - 1
            name = SERVICES[index]
            image = self.web_image if name == "web" else str(index % 10) * 64
            state = "exited" if name in ONESHOT else "running"
            return json.dumps([args[-1], "sha256:" + image, state, "t", 0, name, "/w",
                               "/w/base.yml", [], {}, {}, 0])
        if args[:2] == ["image", "inspect"]:
            return self.label + "\n"
        raise AssertionError("unexpected argv")


def build(docker=None, ok=True, served=SHA_OLD):
    docker = docker or FakeDocker()
    return docker, rt.build_reader(adapter, docker, lambda: ok, lambda: served)


class RuntimeTests(unittest.TestCase):
    def test_baseline_and_observation_come_from_read_only_calls(self):
        docker, reader = build()
        baseline = reader.capture_baseline()
        self.assertEqual(baseline["web_sha"], SHA_OLD)
        observation = reader.observe()
        self.assertEqual(observation["health"], {"ok": True, "web_sha": SHA_OLD})
        for call in docker.calls:
            self.assertTrue(call[0] in rt.READ_VERBS or call[:2] == ["image", "inspect"])
        result = verify.verify(reader, baseline, "ROLLED_BACK")
        self.assertTrue(result["ok"])

    def test_web_label_uses_the_image_of_the_same_metadata_read(self):
        docker, reader = build(FakeDocker(web_image="7" * 64))
        reader.capture_baseline()
        self.assertEqual(docker.calls[-1][-1], "sha256:" + "7" * 64)

    def test_mutating_verbs_are_refused_before_the_runner_runs(self):
        docker = FakeDocker()
        guarded = rt.read_only(docker)
        for argv in (["rm", "-f", "x"], ["exec", "x", "sh"], ["run", "img"],
                     ["compose", "up"], ["stop", "x"], ["image", "rm", "x"],
                     ["image", "prune"], ["system", "prune"], []):
            with self.subTest(argv=argv):
                with self.assertRaises(rt.Stop):
                    guarded(argv)
        with self.assertRaises(rt.Stop):
            guarded(["ps"], cwd="/")
        with self.assertRaises(rt.Stop):
            guarded("ps")
        self.assertEqual(docker.calls, [])

    def test_bad_label_and_runner_errors_are_fixed_codes_without_echo(self):
        for label in ("not-a-sha", "", "A" * 40, SHA_OLD + "x"):
            with self.subTest(label=label):
                _, reader = build(FakeDocker(label=label))
                with self.assertRaisesRegex(rt.state_reader.Stop, "^WEB_PROVENANCE_INVALID$"):
                    reader.capture_baseline()

        def boom(args):
            raise RuntimeError("secret-token /root/path")
        reader = rt.build_reader(adapter, boom, lambda: True, lambda: SHA_OLD)
        with self.assertRaises(rt.Stop) as ctx:
            reader.capture_baseline()
        self.assertEqual(str(ctx.exception), "RUNTIME_METADATA_FAILED")

    def test_web_revision_cannot_run_without_a_fresh_metadata_read(self):
        docker = FakeDocker()
        seen = {}

        class Spy:
            @staticmethod
            def metadata(run):
                seen["rows"] = adapter.metadata(run)
                return seen["rows"]
        reader = rt.build_reader(Spy, docker, lambda: True, lambda: SHA_OLD)
        reader.capture_baseline()
        with self.assertRaisesRegex(rt.Stop, "^RUNTIME_WEB_IMAGE_UNKNOWN$"):
            reader._web_revision()  # image id was consumed by the first use

    def test_health_failure_is_never_a_pass(self):
        _, reader = build(ok=False)
        self.assertFalse(reader.observe()["health"]["ok"])

        def raises():
            raise RuntimeError("x")
        for ok, served in ((raises, lambda: SHA_OLD), (lambda: True, raises),
                           (lambda: "yes", lambda: SHA_OLD), (lambda: True, lambda: None)):
            with self.subTest():
                r = rt.build_reader(adapter, FakeDocker(), ok, served)
                self.assertFalse(r.observe()["health"]["ok"])

    def test_wrong_served_revision_fails_verification(self):
        docker, reader = build()
        baseline = reader.capture_baseline()
        _, drifted = build(served=SHA_NEW)
        result = verify.verify(drifted, baseline, "ROLLED_BACK")
        self.assertFalse(result["ok"])

    def test_invalid_providers_are_rejected(self):
        with self.assertRaisesRegex(rt.Stop, "^RUNTIME_PROVIDERS_INVALID$"):
            rt.build_reader(object(), FakeDocker(), lambda: True, lambda: SHA_OLD)
        with self.assertRaisesRegex(rt.Stop, "^RUNTIME_RUN_INVALID$"):
            rt.build_reader(adapter, None, lambda: True, lambda: SHA_OLD)

    def test_library_has_no_host_effects(self):
        import ast
        source = (BASE / "qa525-vnext-runtime.py").read_text()
        imported = set()
        for node in ast.walk(ast.parse(source)):
            if isinstance(node, ast.Import):
                imported |= {a.name.split(".")[0] for a in node.names}
            elif isinstance(node, ast.ImportFrom):
                imported.add((node.module or "").split(".")[0])
        self.assertEqual(imported, {"importlib", "pathlib"})
        with self.assertRaises(SystemExit):
            exec(compile(source, "r", "exec"), {"__name__": "__main__", "__file__": str(BASE / "x.py")})


if __name__ == "__main__":
    unittest.main()
