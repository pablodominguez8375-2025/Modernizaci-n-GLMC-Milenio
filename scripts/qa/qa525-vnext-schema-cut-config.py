#!/usr/bin/env python3
"""Schema-cut configuration byte gate; caller must supply independently authenticated pin.
No root authority, filesystem effects, or fallback to legacy API/WEB scope.
"""
import hashlib
import hmac
import json
import re

DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")
SOURCE = re.compile(r"[0-9a-f]{40}\Z")
FP = re.compile(r"SHA256:[A-Za-z0-9+/]{43}\Z")
BASE = frozenset(("ops_source_sha", "app_source_sha", "app_archive_sha256",
                  "plan_sha256", "admission_sha256", "policy_hash", "go_fingerprint"))
SCHEMA = frozenset(("migrator_image_digest", "baseline_history_sha256",
                    "grant_script_sha256", "postgate_sha256", "api_image_id", "web_image_id"))

class Stop(Exception):
    """Fixed diagnostic."""

def _pairs(items):
    out = {}
    for key, value in items:
        if key in out:
            raise ValueError("duplicate")
        out[key] = value
    return out

def _matches(value, pattern):
    return type(value) is str and pattern.fullmatch(value) is not None

def check_config(raw, *, expected_sha256):
    """Verify exact bytes against external pin, return detached GO pins only."""
    if type(raw) is not bytes or not 0 < len(raw) <= 65536 or not _matches(expected_sha256, DIGEST):
        raise Stop("SCHEMA_CONFIG_INPUT_INVALID")
    if not hmac.compare_digest("sha256:" + hashlib.sha256(raw).hexdigest(), expected_sha256):
        raise Stop("SCHEMA_CONFIG_PIN_MISMATCH")
    try:
        obj = json.loads(raw.decode("ascii"), object_pairs_hook=_pairs,
                         parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
        canonical = (json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + "\n").encode("ascii")
    except (ValueError, UnicodeError, RecursionError, TypeError):
        raise Stop("SCHEMA_CONFIG_JSON_INVALID") from None
    if (type(obj) is not dict or set(obj) != {"schema", "host", "environment", "purpose", "pins", "schema_pins"}
            or raw != canonical or obj["schema"] != "qa525-schema-cut-config-v1"
            or obj["host"] != "glmdch" or obj["environment"] != "QA_SYNTHETIC"
            or obj["purpose"] != "SCHEMA_API_WEB_QA_ONLY"):
        raise Stop("SCHEMA_CONFIG_SHAPE_INVALID")
    base, schema = obj["pins"], obj["schema_pins"]
    if (type(base) is not dict or set(base) != BASE or type(schema) is not dict or set(schema) != SCHEMA
            or any(not _matches(base[k], SOURCE) for k in ("ops_source_sha", "app_source_sha"))
            or not _matches(base["go_fingerprint"], FP)
            or any(not _matches(base[k], DIGEST) for k in BASE - {"ops_source_sha", "app_source_sha", "go_fingerprint"})
            or any(not _matches(schema[k], DIGEST) for k in SCHEMA)):
        raise Stop("SCHEMA_CONFIG_BINDING_INVALID")
    return {"pins": dict(base), "schema_pins": dict(schema), "may_apply": False}

if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_CONFIG_LIBRARY_ONLY_NO_GO")
