#!/usr/bin/env python3
"""Offline, read-only QA525 Plan B evidence consistency check. Never grants GO.

Input JSON must come from a separately authenticated READ_ONLY collector;
this module cannot authenticate provenance. No root, network, Docker or APPLY.
"""
import argparse
import json
import re
from pathlib import Path

SHA = re.compile(r"[0-9a-f]{40}\Z")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")
REQUIRED = frozenset({
    "schema", "source_sha", "release_digest", "last_applied_sha", "live_api_sha",
    "live_web_sha", "live_healthy", "pending_flight", "prepared_reconciled",
    "diff_complete", "diff_count", "physical_preflight_baseline_sha",
    "staged_api_digest", "staged_web_digest", "target_api_digest",
    "target_web_digest", "sender_frame", "receiver_frame", "receiver_real_test_pass",
    "g6_valid_now", "g6_restore_verified", "rollback_verified",
    "release_sigstore_verified", "release_source_sha", "scope",
})


def inspect(e):
    errors = []
    if type(e) is not dict or set(e) != REQUIRED:
        return ["EVIDENCE_SCHEMA_INCOMPLETE_OR_EXTRA"]
    if e["schema"] != "qa525-planb-evidence-v1":
        errors.append("EVIDENCE_SCHEMA_INVALID")
    for k in ("source_sha", "release_source_sha", "last_applied_sha",
              "live_api_sha", "live_web_sha", "physical_preflight_baseline_sha"):
        if type(e[k]) is not str or not SHA.fullmatch(e[k]):
            errors.append("SHA_INVALID:" + k)
    for k in ("release_digest", "staged_api_digest", "staged_web_digest",
              "target_api_digest", "target_web_digest"):
        if type(e[k]) is not str or not DIGEST.fullmatch(e[k]):
            errors.append("DIGEST_INVALID:" + k)
    if e["source_sha"] != e["release_source_sha"]:
        errors.append("TARGET_RELEASE_MISMATCH")
    if e["last_applied_sha"] != e["live_api_sha"] or e["last_applied_sha"] != e["live_web_sha"]:
        errors.append("BASELINE_NOT_APPLIED_LIVE_MATCH")
    if e["physical_preflight_baseline_sha"] != e["last_applied_sha"]:
        errors.append("STALE_PHYSICAL_PREFLIGHT")
    if e["source_sha"] == e["last_applied_sha"]:
        errors.append("TARGET_ALREADY_APPLIED")
    for k in ("live_healthy", "prepared_reconciled", "diff_complete",
              "receiver_real_test_pass", "g6_valid_now", "g6_restore_verified",
              "rollback_verified", "release_sigstore_verified"):
        if e[k] is not True:
            errors.append("GATE_FALSE:" + k)
    if e["pending_flight"] is not False:
        errors.append("FLIGHT_PENDING_OR_UNKNOWN")
    if type(e["diff_count"]) is not int or e["diff_count"] < 0 or e["diff_count"] >= 300:
        errors.append("DIFF_TRUNCATED_OR_INVALID")
    for service in ("api", "web"):
        if e["staged_" + service + "_digest"] != e["target_" + service + "_digest"]:
            errors.append("STAGED_TARGET_MISMATCH:" + service)
    if e["sender_frame"] != "QA525-BND-V1:" or e["receiver_frame"] != "QA525-BND-V1:":
        errors.append("FD_FRAME_MISMATCH")
    if e["scope"] != "API_WEB_QA_ONLY":
        errors.append("SCOPE_INVALID")
    return errors


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("evidence", type=Path)
    a = p.parse_args(argv)
    try:
        with a.evidence.open("rb") as f:
            raw = f.read(32769)
        if len(raw) > 32768:
            raise ValueError("oversized")
        failures = inspect(json.loads(raw))
    except (OSError, ValueError, UnicodeError):
        failures = ["EVIDENCE_UNREADABLE_OR_INVALID"]
    print(json.dumps({
        "status": "CONSISTENT_READ_ONLY_NO_GO" if not failures else "BLOCKED_READ_ONLY_NO_GO",
        "failures": failures, "may_apply": False,
    }, sort_keys=True))
    return 0 if not failures else 2


if __name__ == "__main__":
    raise SystemExit(main())
