"""QA525 G6 child core: hermetic, no privileged or host operations."""
import importlib.util
from pathlib import Path
import tempfile
import types
import unittest

BASE = Path(__file__).resolve().parents[1] / "scripts/qa"


def read(name):
    spec = importlib.util.spec_from_file_location("g6_test_" + name.replace("-", "_"), BASE / name)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


core = read("qa525-vnext-root-g6-child-core.py")
COMMANDS = ("preflight", "init", "backup", "restore", "review")
SHA = "sha256:" + "a" * 64


class G6CoreTest(unittest.TestCase):
    def setUp(self):
        self.calls = []
        self.verified = types.SimpleNamespace(record={"files": {
            "qa525-vnext-g6-v2-run.py": "a" * 64,
            "qa525-vnext-g6-v2-captor.py": "b" * 64}})
        self.runner = types.SimpleNamespace(
            ENTRYPOINT="qa525-vnext-g6-v2-run.py",
            SIBLINGS={"captor": "qa525-vnext-g6-v2-captor.py"},
            parse=lambda argv: (argv[0], {}),
            main=lambda *args, **kw: self.calls.append((args, kw)) or 0)
        self.loader = lambda verified, name: self.assertIs(verified, self.verified) or name
        self.pins = {"g6_command": "preflight", "g6_operation_sha256": SHA}
        self.claims = {"purpose": "G6_V2_ONLY", "nonce": "e" * 64,
                       "app_source_sha": "b" * 40,
                       "app_archive_sha256": SHA, "g6_command": "preflight"}

    def bind(self, **kw):
        a = dict(command="preflight", argv=["preflight"], g6_pins=self.pins,
                 verified=self.verified, verified_loader=self.loader, runner=self.runner)
        a.update(kw)
        return core.build(**a)

    def invoke(self, fn, claims=None):
        with tempfile.TemporaryFile() as f:
            return fn(claims=self.claims if claims is None else claims, fd=f.fileno())

    def test_positive_returns_only_after_runner_zero(self):
        fn = self.bind()
        self.assertEqual(self.invoke(fn), "G6_PREFLIGHT_DONE")
        self.assertEqual(len(self.calls), 1)
        self.assertEqual(self.calls[0][0][0], ["preflight"])
        self.assertIs(self.calls[0][1]["verified"], self.verified)

    def test_five_children_have_fixed_commands(self):
        for command in COMMANDS:
            with self.subTest(command):
                child = read("qa525-vnext-root-g6-%s.py" % command)
                pins = dict(self.pins, g6_command=command)
                bound = child.act_for(bridge=types.SimpleNamespace(build=core.build),
                                      runner=self.runner, argv=[command], g6_pins=pins,
                                      verified=self.verified, verified_loader=self.loader)
                self.assertTrue(callable(bound))
                if command != "preflight":
                    with self.assertRaises(core.Stop):
                        self.invoke(bound)

    def test_bad_bindings_fail_before_a_runner(self):
        variants = ({"command": "backup"}, {"argv": []},
                    {"g6_pins": dict(self.pins, g6_operation_sha256="x")},
                    {"verified": None}, {"verified_loader": None})
        for case in variants:
            with self.subTest(case):
                with self.assertRaises(core.Stop):
                    self.bind(**case)
        self.assertEqual(self.calls, [])

    def test_cross_scope_and_forged_claims_fail(self):
        bound = self.bind()
        for bad in (dict(self.claims, purpose="LAUNCHER_ONLY"),
                    dict(self.claims, g6_command="restore"),
                    dict(self.claims, nonce="z" * 64),
                    dict(self.claims, new_key=True)):
            with self.subTest(bad):
                with self.assertRaises(core.Stop):
                    self.invoke(bound, claims=bad)
        self.assertEqual(self.calls, [])

    def test_missing_modules_fail_before_any_action(self):
        runner = types.SimpleNamespace(**vars(self.runner))
        runner.SIBLINGS = {"captor": "qa525-vnext-g6-v2-captor.py",
                           "producer": "missing.py"}
        with self.assertRaises(core.Stop):
            self.bind(runner=runner)
        self.assertEqual(self.calls, [])

    def test_failure_never_turns_into_success(self):
        runner = types.SimpleNamespace(**vars(self.runner))
        runner.main = lambda *a, **kw: 1
        bound = self.bind(runner=runner)
        with self.assertRaises(core.Stop) as err:
            self.invoke(bound)
        self.assertEqual(str(err.exception), "G6CH_RUN_FAILED")

    def test_real_g6_runner_main_uses_verified_loader_and_closed_preflight_grammar(self):
        """End-to-end real run.main + real parser; stub ONLY host IO and command body."""
        import unittest.mock
        actual = read("qa525-vnext-g6-v2-run.py")
        self.verified.record["files"] = {
            name: "a" * 64 for name in (
                actual.ENTRYPOINT, *actual.SIBLINGS.values())}
        seen = []
        expected_env = object()
        loader = lambda verified, name: (
            self.assertIs(verified, self.verified), seen.append(("module", name)))[0]
        def env_for(command, verified, load_module):
            self.assertEqual(command, "preflight")
            self.assertIs(verified, self.verified)
            self.assertIs(load_module, loader)
            seen.append(("env", command))
            return expected_env
        def preflight(env, opts):
            self.assertIs(env, expected_env)
            self.assertEqual(opts, {})
            seen.append(("handler", "preflight"))
            return 0
        with unittest.mock.patch.object(actual, "env_from_verified", side_effect=env_for), \
                unittest.mock.patch.dict(actual.HANDLERS, {"preflight": preflight}):
            fn = self.bind(runner=actual, verified_loader=loader)
            self.assertEqual(self.invoke(fn), "G6_PREFLIGHT_DONE")
        self.assertEqual(seen[0][0], "module")
        self.assertIn(("env", "preflight"), seen)
        self.assertEqual(seen[-1], ("handler", "preflight"))

    def test_actual_runner_parser_rejects_invalid_signed_options_before_nonce(self):
        actual = read("qa525-vnext-g6-v2-run.py")
        self.verified.record["files"] = {
            name: "a" * 64 for name in (
                actual.ENTRYPOINT, *actual.SIBLINGS.values())}
        for argv in (["preflight", "--restic-sha256", "not-a-sha"],
                     ["preflight", "--unlisted", "x"],
                     ["backup"],
                     ["preflight", "--restic-sha256", "a" * 64, "--restic-sha256", "a" * 64]):
            with self.subTest(argv):
                with self.assertRaises(core.Stop):
                    self.bind(runner=actual, argv=argv)
        self.assertEqual(self.calls, [])

    def test_wrong_fd_is_rejected(self):
        bound = self.bind()
        with self.assertRaises(core.Stop):
            bound(claims=self.claims, fd=-1)
        self.assertEqual(self.calls, [])


if __name__ == "__main__":
    unittest.main()
