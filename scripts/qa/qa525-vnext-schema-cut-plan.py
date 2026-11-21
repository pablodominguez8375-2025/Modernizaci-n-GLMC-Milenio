#!/usr/bin/env python3
"""QA525 schema-cut plan gate. Pure library; no signature, root or host authority.

PRE-4 must authenticate the external pins and exact bytes before a root caller can
use this result. This gate rejects drift between the reviewed EF 100 baseline,
four migrations, release and immutable OCI/app image digests. It executes nothing.
"""
import hashlib
import json
import re

SOURCE = re.compile(r"[0-9a-f]{40}\Z")
SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")
MIGRATION = re.compile(r"20[0-9]{12}_[A-Za-z0-9_]+\Z")
REQUIRED = (
    "20261118150000_AddWorkshopRegistryState",
    "20261118170000_AddWorkshopRetiredGuard",
    "20261119010000_AddWorkshopStatusTransitions",
    "20261119010100_AddWorkshopStatusTransitionsAppendOnly",
)
PREVIOUS = "20261030223000_SeedSupportWorkshop00"
FIELDS = frozenset({
    "schema", "host", "environment", "app_source_sha", "app_archive_sha256",
    "migrator_image_digest", "api_image_id", "web_image_id",
    "history_ids", "pending_ids", "grant_script_sha256", "postgate_sha256",
})
PINS = frozenset(FIELDS - {"schema", "host", "environment", "history_ids", "pending_ids"}
                 | {"baseline_history_sha256", "plan_sha256"})


class Stop(Exception):
    """Fixed diagnostic only; no input echo."""


def _strict_pairs(pairs):
    value = {}
    for key, item in pairs:
        if key in value:
            raise ValueError("duplicate")
        value[key] = item
    return value


def _digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


def check_plan(raw, pins):
    """Return bounded, detached evidence; pins must come from the authenticated PRE-4 config."""
    if (type(raw) is not bytes or not 0 < len(raw) <= 65536
            or type(pins) is not dict or set(pins) != PINS
            or any(type(pins[k]) is not str or SHA.fullmatch(pins[k]) is None
                   for k in PINS - {"app_source_sha"})
            or type(pins["app_source_sha"]) is not str
            or SOURCE.fullmatch(pins["app_source_sha"]) is None):
        raise Stop("SCHEMA_PLAN_INPUT_INVALID")
    if _digest(raw) != pins["plan_sha256"]:
        raise Stop("SCHEMA_PLAN_PIN_MISMATCH")
    try:
        plan = json.loads(raw.decode("ascii"), object_pairs_hook=_strict_pairs,
                          parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
    except (ValueError, UnicodeError, RecursionError, TypeError):
        raise Stop("SCHEMA_PLAN_JSON_INVALID") from None
    if (type(plan) is not dict or set(plan) != FIELDS
            or raw != (json.dumps(plan, sort_keys=True, separators=(",", ":"),
                                  ensure_ascii=True) + "\n").encode("ascii")
            or plan["schema"] != "qa525-schema-cut-plan-v1"
            or plan["host"] != "glmdch" or plan["environment"] != "QA_SYNTHETIC"):
        raise Stop("SCHEMA_PLAN_SHAPE_INVALID")
    for key in PINS - {"plan_sha256", "baseline_history_sha256"}:
        if type(plan[key]) is not str or plan[key] != pins[key]:
            raise Stop("SCHEMA_PLAN_BINDING_MISMATCH")
    history, pending = plan["history_ids"], plan["pending_ids"]
    if (type(history) is not list or len(history) != 100
            or any(type(mid) is not str or MIGRATION.fullmatch(mid) is None for mid in history)
            or len(set(history)) != 100 or history != sorted(history)
            or PREVIOUS not in history or any(mid in history for mid in REQUIRED)
            or type(pending) is not list or pending != list(REQUIRED)):
        raise Stop("SCHEMA_PLAN_HISTORY_INVALID")
    observed = _digest("".join(mid + "\n" for mid in history).encode("ascii"))
    if observed != pins["baseline_history_sha256"]:
        raise Stop("SCHEMA_PLAN_HISTORY_PIN_MISMATCH")
    return {
        "status": "QA525_SCHEMA_PLAN_CONSISTENT_ONLY_NO_AUTHORITY",
        "plan_sha256": pins["plan_sha256"],
        "baseline_history_sha256": observed,
        "migration_count_before": 100,
        "pending_ids": REQUIRED,
        "migrator_image_digest": pins["migrator_image_digest"],
        "may_apply": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_PLAN_LIBRARY_ONLY_NO_GO")
