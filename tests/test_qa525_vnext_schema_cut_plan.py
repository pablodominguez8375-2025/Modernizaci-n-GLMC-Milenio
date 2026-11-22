"""Negative cut-plan gates, entirely synthetic and rootless."""
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

PATH = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-schema-cut-plan.py"
SPEC = importlib.util.spec_from_file_location("qa525_schema_plan", PATH)
gate = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(gate)


def digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


def fixture():
    history = sorted(
        [f"20260101{i:06d}_Synthetic{i}" for i in range(99)] + [gate.PREVIOUS]
    )
    sha = "sha256:" + "a" * 64
    plan = {
        "schema": "qa525-schema-cut-plan-v1", "host": "glmdch",
        "environment": "QA_SYNTHETIC", "app_source_sha": "a" * 40,
        "app_archive_sha256": sha, "migrator_image_digest": sha,
        "api_image_id": sha, "web_image_id": sha, "api_container_id": "c" * 64,
        "grant_script_sha256": sha, "postgate_sha256": sha,
        "history_ids": history, "pending_ids": list(gate.REQUIRED),
    }
    pins = {k: plan[k] for k in gate.PINS if k in plan}
    pins["baseline_history_sha256"] = digest(
        "\n".join(history).encode("ascii")
    )
    return plan, pins


def encoded(plan, pins):
    raw = (json.dumps(plan, sort_keys=True, separators=(",", ":")) + "\n").encode()
    pins = dict(pins, plan_sha256=digest(raw))
    return raw, pins


class SchemaCutPlanTests(unittest.TestCase):
    def test_consistent_plan_is_only_a_claim(self):
        plan, pins = fixture()
        result = gate.check_plan(*encoded(plan, pins))
        self.assertEqual(100, result["migration_count_before"])
        self.assertEqual(gate.REQUIRED, result["pending_ids"])
        self.assertIs(result["may_apply"], False)
        self.assertEqual("c" * 64, result["api_container_id"])

    def test_tampered_bytes_fail_even_if_json_would_parse(self):
        plan, pins = fixture()
        raw, pins = encoded(plan, pins)
        with self.assertRaisesRegex(gate.Stop, "SCHEMA_PLAN_PIN_MISMATCH"):
            gate.check_plan(raw.replace(b"glmdch", b"otherx"), pins)

    def test_wrong_history_fails_even_with_resigned_plan(self):
        plan, pins = fixture()
        plan["history_ids"][0] = "20260101000000_Foreign"
        with self.assertRaisesRegex(gate.Stop, "SCHEMA_PLAN_HISTORY_PIN_MISMATCH"):
            gate.check_plan(*encoded(plan, pins))

    def test_extra_pending_migration_fails(self):
        plan, pins = fixture()
        plan["pending_ids"].append("20261119010200_Unexpected")
        with self.assertRaisesRegex(gate.Stop, "SCHEMA_PLAN_HISTORY_INVALID"):
            gate.check_plan(*encoded(plan, pins))

    def test_mutable_image_tag_cannot_be_pinned(self):
        plan, pins = fixture()
        plan["migrator_image_digest"] = pins["migrator_image_digest"] = "workshops104:latest"
        with self.assertRaisesRegex(gate.Stop, "SCHEMA_PLAN_INPUT_INVALID"):
            gate.check_plan(*encoded(plan, pins))

    def test_container_id_is_exact_and_bound_by_plan_digest(self):
        plan, pins = fixture()
        for wrong in ("", "latest", "C" * 64, "c" * 63):
            with self.subTest(wrong=wrong):
                plan["api_container_id"] = wrong
                with self.assertRaisesRegex(gate.Stop, "SCHEMA_PLAN_SHAPE_INVALID"):
                    gate.check_plan(*encoded(plan, pins))
        plan, pins = fixture()
        raw, pins = encoded(plan, pins)
        with self.assertRaisesRegex(gate.Stop, "SCHEMA_PLAN_PIN_MISMATCH"):
            gate.check_plan(raw.replace(b"c" * 64, b"d" * 64), pins)

    def test_duplicate_key_rejected(self):
        plan, pins = fixture()
        raw, pins = encoded(plan, pins)
        bad = raw.replace(b'"host":"glmdch"', b'"host":"glmdch","host":"glmdch"')
        pins["plan_sha256"] = digest(bad)
        with self.assertRaisesRegex(gate.Stop, "SCHEMA_PLAN_JSON_INVALID"):
            gate.check_plan(bad, pins)


    def test_go_claims_supply_all_plan_pins(self):
        plan, pins = fixture()
        raw, pins = encoded(plan, pins)
        go = {
            "status": "QA525_GO_CUT_CONSISTENT_ONLY_UNAUTHENTICATED_NO_APPLY",
            "purpose": "SCHEMA_API_WEB_QA_ONLY",
            "schema_contract_shape_only": True,
            "callback_consistency_only": True,
            "may_apply": False,
            "schema_pins": {k: pins[k] for k in gate.GO_PIN_FIELDS},
            "app_source_sha": pins["app_source_sha"],
            "app_archive_sha256": pins["app_archive_sha256"],
            "plan_sha256": pins["plan_sha256"],
        }
        self.assertIs(gate.check_go_plan(raw, go)["may_apply"], False)
        with self.assertRaisesRegex(gate.Stop, "SCHEMA_PLAN_GO_BINDING_INVALID"):
            gate.check_go_plan(raw, dict(go, purpose="API_WEB_QA_ONLY"))
        with self.assertRaisesRegex(gate.Stop, "SCHEMA_PLAN_BINDING_MISMATCH"):
            gate.check_go_plan(raw, dict(go, schema_pins=dict(
                go["schema_pins"], migrator_image_digest=digest(b"wrong"))))
        with self.assertRaisesRegex(gate.Stop, "SCHEMA_PLAN_PIN_MISMATCH"):
            gate.check_go_plan(raw, dict(go, plan_sha256=digest(b"wrong")))


if __name__ == "__main__":
    unittest.main()
