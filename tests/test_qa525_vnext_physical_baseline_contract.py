"""QA525: physical 6/5 Compose stack, 7 running + 3 exited one-shots.

Pure hermetic regression fixtures from the operator's *sanitized* inventory
(#525 comments 6699949734/6699974381). These are synthetic IDs/paths:
they do NOT authenticate the host or authorize an APPLY.
"""
import copy
import importlib.util
from pathlib import Path
import unittest

HERE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


reader = load("qa525_read_real_shape", "qa525-vnext-state-reader.py")
plan = load("qa525_plan_real_shape", "qa525-vnext-baseline-plan.py")
verify = load("qa525_verify_real_shape", "qa525-vnext-verify.py")

BASE = "/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json"
API_LAYERS = [
    BASE,
    "/opt/centenario/qa-api-refresh/api-legacy.json",
    "/etc/pmgm/qa754-identity-overlay.stage.json",
    "/opt/centenario/qa525-signed-prepare/api-725bc319.json",
    "/opt/centenario/qa525-signed-prepare/api-41d9c490.json",
    "/opt/centenario/qa525-signed-prepare/api-6eed4f00.json",
]
WEB_LAYERS = [
    BASE,
    "/opt/centenario/qa-lab-firstboot/recovery-v2/web-legacy.json",
    "/opt/centenario/qa525-signed-prepare/web-725bc319.json",
    "/opt/centenario/qa525-signed-prepare/web-41d9c490.json",
    "/opt/centenario/qa525-signed-prepare/web-6eed4f00.json",
]
# Not exact host paths or image hashes; only the sequence and shape are factual.
RUNNING = ("api", "web", "keycloak", "postgres", "minio", "gateway", "clamav")
ONESHOTS = ("db-grants", "migrator", "minio-init")
WEB_REV = "d" * 40


def sample_rows():
    rows = {}
    for index, name in enumerate(RUNNING + ONESHOTS, 1):
        hex_digit = format(index, "x")
        state = "running" if name in RUNNING else "exited"
        layers = API_LAYERS if name == "api" else WEB_LAYERS if name == "web" else [BASE]
        rows[name] = [
            hex_digit * 64, "sha256:" + hex_digit * 64, state,
            "2026-11-06T00:00:00Z", 0, name,
            "/opt/centenario/qa-lab-firstboot/recovery-v2",
            ",".join(layers), [], {}, {}, 0,
        ]
    return rows


class Fake:
    def __init__(self, observed):
        self.observed = observed

    def observe(self):
        return copy.deepcopy(self.observed)


class PhysicalBaselineContractTests(unittest.TestCase):
    def test_7_running_3_exited_6_api_5_web_is_valid_synthetic_baseline(self):
        rows = sample_rows()
        base = reader.baseline_from_rows(rows, WEB_REV)
        self.assertEqual(set(rows), set(RUNNING + ONESHOTS))
        self.assertEqual(len(base["layers"]["api"]), 6)
        self.assertEqual(len(base["layers"]["web"]), 5)
        self.assertEqual(base["layers"]["api"], API_LAYERS)
        self.assertEqual(base["layers"]["web"], WEB_LAYERS)
        self.assertEqual(set(base["oneshots"]), set(ONESHOTS))
        self.assertTrue(all(x["exit_code"] == 0 for x in base["oneshots"].values()))
        self.assertEqual(plan.validate_baseline(base), verify.validate_baseline(base))
        observed = reader.observation_from_rows(rows, {"ok": True, "web_sha": WEB_REV})
        self.assertTrue(verify.verify(Fake(observed), base, "ROLLED_BACK")["ok"])

    def test_missing_or_extra_service_is_never_a_baseline(self):
        for name in RUNNING + ONESHOTS:
            with self.subTest(missing=name):
                rows = sample_rows()
                del rows[name]
                with self.assertRaisesRegex(reader.Stop, "PROJECT_CONTAINER_SET_CHANGED"):
                    reader.baseline_from_rows(rows, WEB_REV)
        rows = sample_rows()
        rows["foreign-service"] = copy.deepcopy(rows["api"])
        with self.assertRaisesRegex(reader.Stop, "PROJECT_CONTAINER_SET_CHANGED"):
            reader.baseline_from_rows(rows, WEB_REV)

    def test_oneshot_must_be_exited_zero_not_running_or_failed(self):
        for name in ONESHOTS:
            for state, code in (("running", 0), ("exited", 1), ("restarting", 0), ("exited", True)):
                with self.subTest(name=name, state=state, code=code):
                    rows = sample_rows()
                    rows[name][2], rows[name][11] = state, code
                    with self.assertRaises(reader.Stop):
                        reader.baseline_from_rows(rows, WEB_REV)
        rows = sample_rows()
        base = reader.baseline_from_rows(rows, WEB_REV)
        rows["migrator"][11] = 1
        observed = reader.observation_from_rows(rows, {"ok": True, "web_sha": WEB_REV})
        result = verify.verify(Fake(observed), base, "ROLLED_BACK")
        self.assertFalse(result["ok"])
        self.assertEqual(result["code"], "ONESHOT_CHANGED")

    def test_oneshot_restarts_after_clean_baseline_are_refused_by_observation(self):
        """Mutation guard: reader must check STATE as well as exit code."""
        for name in ONESHOTS:
            for state in ("running", "restarting", "created"):
                with self.subTest(oneshot=name, state=state):
                    rows = sample_rows()
                    baseline = reader.baseline_from_rows(rows, WEB_REV)
                    self.assertEqual(baseline["oneshots"][name]["exit_code"], 0)
                    rows[name][2] = state  # same container ID and exit code 0
                    with self.assertRaisesRegex(
                        reader.Stop, "ONESHOT_NOT_EXITED_" + name.upper()
                    ):
                        reader.observation_from_rows(
                            rows, {"ok": True, "web_sha": WEB_REV}
                        )

    def test_compose_reordered_missing_or_phantom_layer_is_not_rollback(self):
        base_rows = sample_rows()
        base = reader.baseline_from_rows(base_rows, WEB_REV)
        for service, edited in (
            ("api", API_LAYERS[:-1]),
            ("api", API_LAYERS[:-2] + API_LAYERS[-1:] + API_LAYERS[-2:-1]),
            ("api", API_LAYERS + ["/opt/centenario/qa525-signed-prepare/orphan.json"]),
            ("web", WEB_LAYERS[:-1]),
            ("web", WEB_LAYERS + ["/opt/centenario/qa525-signed-prepare/orphan.json"]),
            ("web", WEB_LAYERS[:-2] + WEB_LAYERS[-1:] + WEB_LAYERS[-2:-1]),
        ):
            with self.subTest(service=service, edited=edited):
                rows = sample_rows()
                rows[service][7] = ",".join(edited)
                observed = reader.observation_from_rows(rows, {"ok": True, "web_sha": WEB_REV})
                result = verify.verify(Fake(observed), base, "ROLLED_BACK")
                self.assertFalse(result["ok"])
                self.assertEqual(result["code"], "LAYERS_MISMATCH_" + service.upper())

    def test_invalid_compose_structure_and_different_first_base_are_rejected(self):
        rows = sample_rows()
        rows["web"][7] = "/opt/other-base.json," + ",".join(WEB_LAYERS[1:])
        baseline = reader.baseline_from_rows(rows, WEB_REV)
        with self.assertRaisesRegex(plan.Stop, "BASELINE_LAYERS_INVALID"):
            plan.validate_baseline(baseline)
        for invalid in (API_LAYERS + [API_LAYERS[0]], ["/opt/bad/../path.json"] + API_LAYERS[1:]):
            rows = sample_rows()
            rows["api"][7] = ",".join(invalid)
            with self.assertRaises(reader.Stop):
                reader.baseline_from_rows(rows, WEB_REV)

    def test_plan_target_preserves_full_history_and_appends_one_per_target(self):
        base = reader.baseline_from_rows(sample_rows(), WEB_REV)
        target = {
            "images": {"api": "sha256:" + "b" * 64, "web": "sha256:" + "c" * 64},
            "layers": {
                "api": API_LAYERS + ["/opt/centenario/qa525-signed-prepare/api-candidate.json"],
                "web": WEB_LAYERS + ["/opt/centenario/qa525-signed-prepare/web-candidate.json"],
            },
            "web_sha": "e" * 40,
        }
        self.assertTrue(plan.validate_target(base, target).startswith("sha256:"))
        self.assertEqual(len(target["layers"]["api"]), 7)
        self.assertEqual(len(target["layers"]["web"]), 6)
        # The planner validates shape, not provenance: external signature and
        # signed per-file hashes remain mandatory before physical execution.
        self.assertEqual(verify.validate_target(base, "APPLIED", target), target)


if __name__ == "__main__":
    unittest.main()
