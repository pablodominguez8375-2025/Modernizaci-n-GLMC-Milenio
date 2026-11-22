import importlib.util
from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import patch

path = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-schema-cut-pre4-binding.py"
spec = importlib.util.spec_from_file_location("qa525_pre4_binding", path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

ARGS = dict(config_bytes=b"config", config_sha256="sha256:"+"a"*64,
            go_bytes=b"go", go_signature_bytes=b"sig", ops_v3_receipt={},
            app_subject={}, now_utc=None, verify_go_signature=lambda *args: True,
            nonce_unused=lambda *args: True, plan_bytes=b"plan")

def fake_loader(go_overrides=None, plan_overrides=None, config_overrides=None):
    cfg = dict(pins={"ops_source_sha": "x"}, schema_pins={"migrator_image_digest": "y"}, may_apply=False)
    go = dict(may_apply=False, schema_contract_shape_only=True, go_bytes_sha256="go-hash",
              admission_sha256="admission-hash", nonce="nonce")
    plan = dict(may_apply=False, status="QA525_SCHEMA_PLAN_CONSISTENT_ONLY_NO_AUTHORITY", plan_sha256="plan-hash")
    cfg.update(config_overrides or {})
    go.update(go_overrides or {})
    plan.update(plan_overrides or {})
    def load(filename):
        if filename.endswith("-config.py"):
            return SimpleNamespace(check_config=lambda raw, **kw: cfg)
        if filename.endswith("-contract.py"):
            def check_cut(**kwargs):
                assert kwargs["pins"] == cfg["pins"]
                assert kwargs["schema_pins"] == cfg["schema_pins"]
                assert kwargs["purpose"] == "SCHEMA_API_WEB_QA_ONLY"
                return go
            return SimpleNamespace(check_cut=check_cut)
        if filename.endswith("-plan.py"):
            def check_go_plan(raw, result):
                assert raw == b"plan" and result is go
                return plan
            return SimpleNamespace(check_go_plan=check_go_plan)
        raise AssertionError(filename)
    return load

class BindingTests(unittest.TestCase):
    def test_bound_result_cannot_authorize(self):
        with patch.object(module, "_load", side_effect=fake_loader()):
            result = module.check_binding(**ARGS)
        self.assertEqual(result["status"], "QA525_SCHEMA_PRE4_CONSISTENCY_ONLY")
        self.assertFalse(result["may_apply"])
        self.assertFalse(result["authenticated_here"])
        self.assertFalse(result["nonce_spent_here"])

    def test_rejects_any_authority_claim(self):
        for key, changes in [("config", {"may_apply":True}),
                             ("go", {"may_apply":True}),
                             ("plan", {"may_apply":True}),
                             ("scope", {"schema_contract_shape_only":False}),
                             ("plan_status", {"status":"WRONG"})]:
            opts = dict(config_overrides=changes) if key == "config" else (
                dict(plan_overrides=changes) if key in ("plan", "plan_status") else dict(go_overrides=changes))
            with self.subTest(key=key), patch.object(module, "_load", side_effect=fake_loader(**opts)):
                with self.assertRaisesRegex(module.Stop, "^SCHEMA_PRE4_BINDING_DENIED$"):
                    module.check_binding(**ARGS)

    def test_failure_is_opaque(self):
        def denied(_):
            raise RuntimeError("secret internal path")
        with patch.object(module, "_load", side_effect=denied):
            with self.assertRaisesRegex(module.Stop, "^SCHEMA_PRE4_BINDING_DENIED$"):
                module.check_binding(**ARGS)

if __name__ == "__main__":
    unittest.main()
