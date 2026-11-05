"""Hermetic tests for the QA525 vNext terminal-state verifier. No host/Docker/network."""
import copy
import importlib.util
from pathlib import Path
import re
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "qa" / "qa525-vnext-verify.py"
spec = importlib.util.spec_from_file_location("qa525_vnext_verify", SCRIPT)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

OLD_API, OLD_WEB = "sha256:" + "1" * 64, "sha256:" + "2" * 64
NEW_API, NEW_WEB = "sha256:" + "3" * 64, "sha256:" + "4" * 64
OLD_SHA, NEW_SHA = "a" * 40, "b" * 40
OLD_LAYERS = {"api": ["/w/base.yml", "/w/qa525-api-old.json"], "web": ["/w/base.yml"]}
NEW_LAYERS = {"api": ["/w/base.yml", "/w/dual-new-api.json"], "web": ["/w/base.yml", "/w/dual-new-web.json"]}


def baseline():
    return {"images": {"api": OLD_API, "web": OLD_WEB},
            "dependencies": {"db": "d" * 12, "keycloak": "e" * 12},
            "oneshots": {"migrate": {"id": "f" * 12, "exit_code": 0}},
            "layers": copy.deepcopy(OLD_LAYERS), "web_sha": OLD_SHA}


def target():
    return {"images": {"api": NEW_API, "web": NEW_WEB}, "layers": copy.deepcopy(NEW_LAYERS), "web_sha": NEW_SHA}


def observation(kind="APPLIED"):
    base = baseline()
    new = kind == "APPLIED"
    return {"images": {"api": NEW_API if new else OLD_API, "web": NEW_WEB if new else OLD_WEB},
            "running": {"api": True, "web": True},
            "dependencies": base["dependencies"], "oneshots": base["oneshots"],
            "layers": copy.deepcopy(NEW_LAYERS if new else OLD_LAYERS),
            "health": {"ok": True, "web_sha": NEW_SHA if new else OLD_SHA}}


class Fake:
    def __init__(self, *observations, error=None):
        self.items, self.error, self.calls = list(observations), error, 0

    def observe(self):
        self.calls += 1
        if self.error:
            raise self.error
        item = self.items[min(self.calls - 1, len(self.items) - 1)]
        return copy.deepcopy(item)


class VerifierTests(unittest.TestCase):
    def applied(self, **change):
        obs = observation()
        for key, value in change.items():
            obs[key] = value
        return mod.verify(Fake(obs), baseline(), "APPLIED", target())

    def test_applied_and_rolled_back_pass_and_bind_to_baseline(self):
        base_sha = mod.validate_baseline(baseline())
        ok = mod.verify(Fake(observation()), baseline(), "APPLIED", target())
        self.assertTrue(ok["ok"], ok)
        mod.require_ok(ok, base_sha, "APPLIED")
        rolled = mod.verify(Fake(observation("ROLLED_BACK")), baseline(), "ROLLED_BACK")
        self.assertTrue(rolled["ok"], rolled)
        mod.require_ok(rolled, base_sha, "ROLLED_BACK")
        self.assertNotEqual(ok["result_sha256"], rolled["result_sha256"])

    def test_each_runtime_divergence_is_not_ok(self):
        cases = {
            "IMAGE_MISMATCH_API": {"images": {"api": OLD_API, "web": NEW_WEB}},
            "IMAGE_MISMATCH_WEB": {"images": {"api": NEW_API, "web": OLD_WEB}},
            "SERVICE_NOT_RUNNING_WEB": {"running": {"api": True, "web": False}},
            "DEPENDENCY_CHANGED": {"dependencies": {"db": "9" * 12, "keycloak": "e" * 12}},
            "ONESHOT_CHANGED": {"oneshots": {"migrate": {"id": "f" * 12, "exit_code": 1}}},
            "LAYERS_MISMATCH_API": {"layers": {"api": NEW_LAYERS["api"] + ["/w/qa525-api-6eed4-orphan.json"],
                                               "web": NEW_LAYERS["web"]}},
            "LAYERS_MISMATCH_WEB": {"layers": {"api": NEW_LAYERS["api"], "web": ["/w/base.yml"]}},
            "HEALTH_FAILED": {"health": {"ok": False, "web_sha": NEW_SHA}},
        }
        for code, change in cases.items():
            with self.subTest(code=code):
                result = self.applied(**change)
                self.assertFalse(result["ok"])
                self.assertEqual(result["code"], code)
                with self.assertRaises(mod.Stop):
                    mod.require_ok(result, mod.validate_baseline(baseline()), "APPLIED")

    def test_health_for_wrong_revision_and_oneshot_missing_fail(self):
        self.assertEqual(self.applied(health={"ok": True, "web_sha": OLD_SHA})["code"], "HEALTH_FAILED")
        obs = observation()
        obs["oneshots"] = {}
        self.assertEqual(mod.verify(Fake(obs), baseline(), "APPLIED", target())["code"], "OBSERVATION_INVALID")

    def test_orphan_layer_order_and_duplicates_are_exact(self):
        swapped = {"api": list(reversed(NEW_LAYERS["api"])), "web": NEW_LAYERS["web"]}
        self.assertEqual(self.applied(layers=swapped)["code"], "LAYERS_MISMATCH_API")
        dup = {"api": [NEW_LAYERS["api"][0]] * 2, "web": NEW_LAYERS["web"]}
        self.assertEqual(self.applied(layers=dup)["code"], "OBSERVATION_INVALID")

    def test_truthy_or_loose_types_are_rejected(self):
        self.assertEqual(self.applied(running={"api": 1, "web": True})["code"], "OBSERVATION_INVALID")
        self.assertEqual(self.applied(health={"ok": 1, "web_sha": NEW_SHA})["code"], "OBSERVATION_INVALID")
        self.assertEqual(self.applied(health={"ok": True, "web_sha": NEW_SHA, "extra": 1})["code"],
                         "OBSERVATION_INVALID")
        obs = observation()
        obs["oneshots"]["migrate"]["exit_code"] = True  # a bool is not an exit code
        self.assertEqual(mod.verify(Fake(obs), baseline(), "APPLIED", target())["code"], "OBSERVATION_INVALID")
        extra = observation()
        extra["unexpected"] = 1
        self.assertEqual(mod.verify(Fake(extra), baseline(), "APPLIED", target())["code"], "OBSERVATION_INVALID")

    def test_adapter_failure_and_non_dict_are_indeterminate_not_success(self):
        failed = mod.verify(Fake(error=OSError("socket")), baseline(), "APPLIED", target())
        self.assertFalse(failed["ok"])
        self.assertEqual(failed["code"], "VERIFY_INDETERMINATE")
        self.assertEqual(mod.verify(Fake(["x"]), baseline(), "APPLIED", target())["code"], "OBSERVATION_INVALID")

    def test_unstable_runtime_between_samples_fails(self):
        flap = observation()
        flap["running"]["web"] = False
        # first sample healthy, second flips: never a success
        result = mod.verify(Fake(observation(), flap), baseline(), "APPLIED", target())
        self.assertFalse(result["ok"])
        other = observation()
        other["dependencies"] = {"db": "9" * 12, "keycloak": "e" * 12}
        self.assertEqual(mod.verify(Fake(observation(), other), baseline(), "APPLIED", target())["code"],
                         "DEPENDENCY_CHANGED")
        self.assertEqual(mod.verify(Fake(observation(), observation()), baseline(), "APPLIED", target())["ok"], True)

    def test_kind_and_target_confusion_is_rejected(self):
        with self.assertRaises(mod.Stop):
            mod.verify(Fake(observation()), baseline(), "ROLLED_BACK", target())  # rollback is always the baseline
        with self.assertRaises(mod.Stop):
            mod.verify(Fake(observation()), baseline(), "APPLIED")  # missing target
        same = target()
        same["images"]["api"] = OLD_API
        with self.assertRaisesRegex(mod.Stop, "APPLIED_TARGET_EQUALS_BASELINE"):
            mod.verify(Fake(observation()), baseline(), "APPLIED", same)
        with self.assertRaises(mod.Stop):
            mod.verify(Fake(observation()), baseline(), "DONE", target())
        # An unchanged baseline can never be reported as APPLIED.
        self.assertFalse(mod.verify(Fake(observation("ROLLED_BACK")), baseline(), "APPLIED", target())["ok"])

    def test_result_seal_cannot_be_forged_or_replayed(self):
        base_sha = mod.validate_baseline(baseline())
        ok = mod.verify(Fake(observation()), baseline(), "APPLIED", target())
        forged = dict(ok, ok=True, code="OK")
        forged["evidence_sha256"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(mod.Stop, "RESULT_SEAL_INVALID"):
            mod.require_ok(forged, base_sha, "APPLIED")
        with self.assertRaisesRegex(mod.Stop, "RESULT_KIND_MISMATCH"):
            mod.require_ok(ok, base_sha, "ROLLED_BACK")
        changed = baseline()
        changed["web_sha"] = "c" * 40
        with self.assertRaisesRegex(mod.Stop, "RESULT_BASELINE_MISMATCH"):
            mod.require_ok(ok, mod.validate_baseline(changed), "APPLIED")
        failed = self.applied(health={"ok": False, "web_sha": NEW_SHA})
        with self.assertRaisesRegex(mod.Stop, "RESULT_NOT_OK"):
            mod.require_ok(failed, base_sha, "APPLIED")
        with self.assertRaises(mod.Stop):
            mod.require_ok(dict(ok, extra=1), base_sha, "APPLIED")

    def test_malformed_baseline_and_sample_count_fail_closed(self):
        for change in (lambda b: b.pop("web_sha"), lambda b: b["images"].update(api="latest"),
                       lambda b: b["layers"]["api"].append("relative.yml"),
                       lambda b: b["oneshots"]["migrate"].update(exit_code="0"),
                       lambda b: b.update(extra=1), lambda b: b["dependencies"].clear()):
            bad = baseline()
            change(bad)
            with self.subTest(), self.assertRaises(mod.Stop):
                mod.validate_baseline(bad)
        for samples in (1, 6, "2", True):
            with self.subTest(samples=samples), self.assertRaises(mod.Stop):
                mod.verify(Fake(observation()), baseline(), "APPLIED", target(), samples=samples)

    def test_module_is_read_only_and_hermetic(self):
        source = SCRIPT.read_text(encoding="utf-8")
        for forbidden in ("import subprocess", "import socket", "import os", "import shutil",
                          "import requests", "urllib", "docker ", "open(", "os.system"):
            self.assertNotIn(forbidden, source, forbidden)
        self.assertIsNone(re.search(r"\bwrite\(|\.unlink\(|\.rename\(|\.replace\(", source))


if __name__ == "__main__":
    unittest.main()
