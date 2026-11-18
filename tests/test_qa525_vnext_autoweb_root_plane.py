import importlib.util
from pathlib import Path
import unittest
from types import ModuleType

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


root = load("qa525_autoweb_root_plane_test", "scripts/qa/qa525-vnext-autoweb-root-plane.py")


class Plane:
    def __init__(self):
        self.held, self.calls = True, 0

    def assert_held(self):
        self.calls += 1
        if not self.held:
            raise RuntimeError("lost")
        return {"legacy_locks_held": 5, "root_lease_held": True,
                "may_apply": False, "may_install": False,
                "signature_verified_here": False, "physical_runtime_verified": False}


plane_mod = ModuleType("verified_root_plane")
plane_mod.RootPlane = Plane
plane_mod.LEGACY_LOCKS = ("a", "b", "c", "d", "e")


class RootPlaneTests(unittest.TestCase):
    def test_binds_only_physical_shape_and_rechecks_before_each_effect(self):
        plane, effects = Plane(), []
        capability = root.bind(plane, plane_mod)
        runner = root.held_runner(capability, lambda argv: effects.append(argv) or 0,
                                  lambda argv: list(argv) if argv == ["web"] else (_ for _ in ()).throw(ValueError()))
        self.assertEqual(runner(["web"]), 0)
        self.assertEqual(effects, [["web"]])
        self.assertGreaterEqual(plane.calls, 4)  # bind + before guard + before/after effect
        with self.assertRaisesRegex(root.Stop, "EFFECT_BUDGET_SPENT"):
            runner(["web"])
        plane.held = False
        fresh = root.held_runner(capability, lambda argv: effects.append(argv) or 0,
                                 lambda argv: list(argv))
        with self.assertRaisesRegex(root.Stop, "NOT_HELD"):
            fresh(["web"])
        self.assertEqual(effects, [["web"]])

    def test_lease_lost_after_effect_is_indeterminate_not_success(self):
        plane, effects = Plane(), []
        capability = root.bind(plane, plane_mod)
        def effect(argv):
            effects.append(list(argv))
            plane.held = False
            return 0
        runner = root.held_runner(capability, effect, lambda argv: list(argv))
        with self.assertRaisesRegex(root.Stop, "PLANE_LOST_AFTER_EFFECT"):
            runner(["web"])
        self.assertEqual(effects, [["web"]])

    def test_runner_exception_is_indeterminate_and_never_retried(self):
        plane = Plane()
        capability = root.bind(plane, plane_mod)
        runner = root.held_runner(capability, lambda _: (_ for _ in ()).throw(RuntimeError("secret")),
                                  lambda argv: list(argv))
        with self.assertRaisesRegex(root.Stop, "EFFECT_INDETERMINATE"):
            runner(["web"])
        with self.assertRaisesRegex(root.Stop, "EFFECT_BUDGET_SPENT"):
            runner(["web"])

    def test_two_effect_budget_supports_apply_and_single_rollback_only(self):
        plane, effects = Plane(), []
        capability = root.bind(plane, plane_mod)
        runner = root.held_runner(capability, lambda argv: effects.append(list(argv)) or 0,
                                  lambda argv: list(argv), max_effects=2)
        self.assertEqual(runner(["target-web"]), 0)
        self.assertEqual(runner(["baseline-web"]), 0)
        with self.assertRaisesRegex(root.Stop, "EFFECT_BUDGET_SPENT"):
            runner(["target-web"])
        self.assertEqual(effects, [["target-web"], ["baseline-web"]])

    def test_synthetic_or_forged_status_is_rejected(self):
        class Synthetic(Plane):
            def assert_held(self):
                return {"legacy_locks_held": 5, "root_lease_held": True,
                        "root_lease_held_synthetically": True, "may_apply": False,
                        "may_install": False, "signature_verified_here": False,
                        "physical_runtime_verified": False}
        for candidate in (Synthetic(), object()):
            with self.subTest(candidate=type(candidate).__name__), self.assertRaises(root.Stop):
                root.bind(candidate, plane_mod)

    def test_argv_reject_and_closed_capability_never_call_effect(self):
        plane, effects = Plane(), []
        cap = root.bind(plane, plane_mod)
        runner = root.held_runner(cap, lambda argv: effects.append(argv) or 0,
                                  lambda argv: (_ for _ in ()).throw(ValueError("api")))
        with self.assertRaisesRegex(root.Stop, "ARGV_REJECTED"):
            runner(["api"])
        self.assertEqual(effects, [])
        cap.close()
        with self.assertRaisesRegex(root.Stop, "CAPABILITY_CLOSED"):
            runner(["web"])
        self.assertEqual(effects, [])

    def test_only_exact_capability_can_create_runner(self):
        with self.assertRaisesRegex(root.Stop, "AUTHORITY_REQUIRED"):
            root.held_runner(object(), lambda _: 0, lambda x: x)

    def test_module_has_no_host_imports_or_default_effect(self):
        source = (ROOT / "scripts/qa/qa525-vnext-autoweb-root-plane.py").read_text()
        for forbidden in ("subprocess", "socket", "requests", "urllib", "os.system", "open("):
            self.assertNotIn(forbidden, source)


if __name__ == "__main__":
    unittest.main()
