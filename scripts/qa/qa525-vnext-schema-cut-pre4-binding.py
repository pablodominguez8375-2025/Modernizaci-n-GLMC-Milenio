#!/usr/bin/env python3
"""QA525 schema PRE-4 binding gate. Pure, fail-closed; not an authenticator or root entrypoint.

The caller MUST independently authenticate the configuration bytes and its external
digest, signed GO, OPS receipt, APP attestation, and exact plan bytes. This gate
only prevents those independently checked inputs from drifting apart. It MUST
run before nonce consumption. No IO, locks, subprocesses, or host effects.
"""
import importlib.util
from pathlib import Path

class Stop(Exception):
    """Bounded diagnostic without caller data."""

def _load(filename):
    path = Path(__file__).resolve().with_name(filename)
    spec = importlib.util.spec_from_file_location("qa525_schema_" + filename.replace("-", "_").replace(".", "_"), path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def check_binding(*, config_bytes, config_sha256, go_bytes, go_signature_bytes,
                  ops_v3_receipt, app_subject, now_utc, verify_go_signature,
                  nonce_unused, plan_bytes):
    """Check consistency only. Signature/nonce callbacks are NOT trusted by this module."""
    try:
        cfg = _load("qa525-vnext-schema-cut-config.py").check_config(
            config_bytes, expected_sha256=config_sha256)
        go = _load("qa525-vnext-go-cut-contract.py").check_cut(
            go_bytes=go_bytes, go_signature_bytes=go_signature_bytes,
            pins=cfg["pins"], schema_pins=cfg["schema_pins"],
            ops_v3_receipt=ops_v3_receipt, app_subject=app_subject,
            now_utc=now_utc, verify_go_signature=verify_go_signature,
            nonce_unused=nonce_unused, purpose="SCHEMA_API_WEB_QA_ONLY")
        plan = _load("qa525-vnext-schema-cut-plan.py").check_go_plan(plan_bytes, go)
    except Exception:
        raise Stop("SCHEMA_PRE4_BINDING_DENIED") from None
    if (cfg.get("may_apply") is not False or go.get("may_apply") is not False
            or plan.get("may_apply") is not False
            or go.get("schema_contract_shape_only") is not True
            or plan.get("status") != "QA525_SCHEMA_PLAN_CONSISTENT_ONLY_NO_AUTHORITY"):
        raise Stop("SCHEMA_PRE4_BINDING_DENIED")
    return {
        "status": "QA525_SCHEMA_PRE4_CONSISTENCY_ONLY",
        "go_bytes_sha256": go["go_bytes_sha256"],
        "plan_sha256": plan["plan_sha256"],
        "admission_sha256": go["admission_sha256"],
        "nonce": go["nonce"],
        "may_apply": False,
        "authenticated_here": False,
        "nonce_spent_here": False,
    }

if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_PRE4_LIBRARY_ONLY_NO_ROOT")
