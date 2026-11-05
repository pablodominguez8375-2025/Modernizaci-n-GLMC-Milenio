"""QA525 baseline and monotonic stage planning: hermetic, no host access."""
import copy
import importlib.util
from pathlib import Path
import unittest

PATH = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-baseline-plan.py"
spec = importlib.util.spec_from_file_location("qa525_stage_plan", PATH)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

DEP = ("keycloak", "postgres", "minio", "gateway", "clamav")
ONESHOT = ("db-grants", "migrator", "minio-init")
EVIDENCE = "sha256:" + "f" * 64


def baseline():
    return {
        "images": {"api": "sha256:" + "1" * 64, "web": "sha256:" + "2" * 64},
        "dependencies": {name: format(i + 4, "x") * 64 for i, name in enumerate(DEP)},
        "oneshots": {name: {"id": format(i + 9, "x") * 64, "exit_code": 0}
                     for i, name in enumerate(ONESHOT)},
        "layers": {
            "api": ["/opt/centenario/base.json", "/opt/centenario/api.json",
                    "/etc/pmgm/qa754-identity-overlay.stage.json"],
            "web": ["/opt/centenario/base.json", "/opt/centenario/web.json"],
        },
        "web_sha": "a" * 40,
    }


def target():
    return {
        "images": {"api": "sha256:" + "c" * 64, "web": "sha256:" + "d" * 64},
        "layers": {
            "api": ["/opt/centenario/base.json", "/opt/centenario/new-api.json",
                    "/etc/pmgm/qa754-identity-overlay.stage.json"],
            "web": ["/opt/centenario/base.json", "/opt/centenario/new-web.json"],
        },
        "web_sha": "b" * 40,
    }


class BaselinePlanTests(unittest.TestCase):
    def test_valid_baseline_and_target_exact_sets_and_digests(self):
        b, t = baseline(), target()
        p = mod.prepare(b, t)
        self.assertEqual(mod.validate(p), "APPLYING")
        self.assertEqual(p["baseline_sha256"], mod.validate_baseline(b))
        self.assertEqual(p["target_sha256"], mod.validate_target(b, t))
        self.assertFalse(p["may_apply"])
        self.assertFalse(p["external_runtime_verified"])
        b["images"]["api"] = "sha256:" + "0" * 64
        self.assertNotEqual(b, p["baseline"])

    def test_every_monotonic_stage_without_terminal_applied(self):
        p = mod.prepare(baseline(), target())
        stages = ("API_SWITCHING", "API_VERIFIED", "WEB_SWITCHING",
                  "WEB_VERIFIED", "VERIFYING")
        for stage in stages:
            old = copy.deepcopy(p)
            p = mod.advance(p, stage, EVIDENCE)
            self.assertEqual(mod.validate(p), stage)
            self.assertEqual(len(p["events"]), stages.index(stage) + 1)
            self.assertNotEqual(p["chain_sha256"], old["chain_sha256"])
        with self.assertRaises(mod.Stop):
            mod.advance(p, "APPLIED", EVIDENCE)
        with self.assertRaises(mod.Stop):
            mod.advance(p, "ROLLED_BACK", EVIDENCE)
        with self.assertRaises(mod.Stop):
            mod.advance(p, "VERIFYING", EVIDENCE)

    def test_rollback_required_is_a_terminal_block_on_replay(self):
        p = mod.prepare(baseline(), target())
        failed = mod.advance(p, "RECOVERY_REQUIRED", EVIDENCE)
        self.assertEqual(mod.validate(failed), "RECOVERY_REQUIRED")
        with self.assertRaises(mod.Stop):
            mod.advance(failed, "API_SWITCHING", EVIDENCE)
        with self.assertRaises(mod.Stop):
            mod.advance(failed, "RECOVERY_REQUIRED", EVIDENCE)
        self.assertEqual(mod.validate(p), "APPLYING")

    def test_out_of_order_duplicate_invalid_stage_rejected(self):
        p = mod.prepare(baseline(), target())
        for stage in ("WEB_SWITCHING", "WEB_VERIFIED", "VERIFYING",
                      "APPLYING", "APPLIED", "ROLLED_BACK", 1, None):
            with self.subTest(stage=stage), self.assertRaises(mod.Stop):
                mod.advance(p, stage, EVIDENCE)
        first = mod.advance(p, "API_SWITCHING", EVIDENCE)
        with self.assertRaises(mod.Stop):
            mod.advance(first, "API_SWITCHING", EVIDENCE)
        for proof in ("bad", True, "", None, "sha256:" + "x" * 64):
            with self.subTest(proof=proof), self.assertRaises(mod.Stop):
                mod.advance(first, "API_VERIFIED", proof)

    def test_wrong_services_and_incomplete_oneshots_refused(self):
        for f in (
            lambda x: x["dependencies"].pop("clamav"),
            lambda x: x["dependencies"].update(extra="f" * 64),
            lambda x: x["oneshots"].pop("migrator"),
            lambda x: x["oneshots"]["db-grants"].update(exit_code=1),
            lambda x: x["oneshots"]["minio-init"].update(exit_code=True),
            lambda x: x["images"].update(api="latest"),
            lambda x: x["layers"]["web"].append("../unsafe"),
            lambda x: x["layers"]["api"].reverse(),
            lambda x: x["layers"]["web"].append("/opt/centenario/web.json"),
            lambda x: x.update(extra=True),
        ):
            b = baseline()
            f(b)
            with self.subTest(mutation=str(f)), self.assertRaises(mod.Stop):
                mod.validate_baseline(b)

    def test_target_must_change_both_images_and_web_revision(self):
        b = baseline()
        for f in (
            lambda t: t["images"].update(api=b["images"]["api"]),
            lambda t: t["images"].update(web=b["images"]["web"]),
            lambda t: t.update(web_sha=b["web_sha"]),
            lambda t: t["layers"]["web"].append("relative"),
            lambda t: t["layers"]["api"].append("/w/../unsafe"),
            lambda t: t.update(oneshots={}),
        ):
            t = target()
            f(t)
            with self.subTest(mutation=str(f)), self.assertRaises(mod.Stop):
                mod.validate_target(b, t)

    def test_tampered_baseline_target_and_checksum_detected(self):
        p = mod.prepare(baseline(), target())
        for f in (
            lambda x: x["baseline"]["images"].update(api="sha256:" + "e" * 64),
            lambda x: x["target"]["images"].update(web="sha256:" + "e" * 64),
            lambda x: x.update(stage="APPLIED"),
            lambda x: x.update(may_apply=True),
            lambda x: x.update(external_runtime_verified=True),
            lambda x: x["events"].append({"from": "APPLYING", "to": "VERIFYING",
                                         "evidence_sha256": EVIDENCE}),
            lambda x: x.update(chain_sha256="sha256:" + "0" * 64),
        ):
            changed = copy.deepcopy(p)
            f(changed)
            with self.subTest(mutation=str(f)), self.assertRaises(mod.Stop):
                mod.validate(changed)

    def test_helper_has_no_io_no_execution_no_terminal_writer(self):
        source = PATH.read_text(encoding="utf-8")
        for forbidden in ("import socket", "import os", "import subprocess",
                          "import urllib", "import requests", "from pathlib",
                          "os.system(", "subprocess.run(", "docker compose"):
            self.assertNotIn(forbidden, source)
        self.assertNotIn('"APPLIED", "ROLLED_BACK"', source)
        self.assertNotIn("os.fsync", source)


if __name__ == "__main__":
    unittest.main()
