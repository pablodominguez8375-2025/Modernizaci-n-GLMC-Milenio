#!/usr/bin/env python3
"""Validate a historical QA525 snapshot; NEVER infer deployment or authorize changes.

Offline/stdlib-only: no network, Docker, subprocess, credentials, host writes,
or GitHub/Drive API access. A PASS means only that the document has a coherent,
fail-closed shape. Current operational state ALWAYS requires fresh independent
GitHub/Drive AND physical-host evidence.
"""
import argparse
from datetime import date
import json
import os
from pathlib import Path
import re
import stat

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
SHA40 = re.compile(r"[0-9a-f]{40}\Z")
SHA64 = re.compile(r"[0-9a-f]{64}\Z")
ROOT_FIELDS = frozenset({
    "schema_version", "document_type", "observed_date", "generated_by",
    "provenance_rule", "automation_status", "environment", "github",
    "physical_qa", "backup_g6", "decision", "source_links",
    "consumption_rules",
})


class Blocked(ValueError):
    """Fail closed, without displaying potentially sensitive input."""


def require(test, reason):
    if not test:
        raise Blocked(reason)


def exact_keys(obj, expected, reason):
    require(type(obj) is dict and set(obj) == set(expected), reason)


def sha40(value):
    return type(value) is str and SHA40.fullmatch(value) is not None


def sha64(value):
    return type(value) is str and SHA64.fullmatch(value) is not None


def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "DUPLICATE_JSON_KEY")
        result[key] = value
    return result


def no_json_constant(_):
    raise Blocked("NONFINITE_JSON_NUMBER")


def read_snapshot(path):
    """Read one bounded regular-file descriptor; reject symlinks at open time.

    lstat followed by read_bytes would permit a path swap between the
    two operations. O_NOFOLLOW + fstat bind validation to the actual fd.
    """
    path = Path(path)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    with os.fdopen(fd, "rb") as stream:
        status = os.fstat(stream.fileno())
        require(stat.S_ISREG(status.st_mode) and 0 < status.st_size <= 16_384
                and status.st_nlink == 1, "NOT_A_REGULAR_SMALL_SNAPSHOT")
        data = stream.read(16_385)
    require(len(data) <= 16_384, "SNAPSHOT_TOO_LARGE")
    try:
        value = json.loads(data.decode("utf-8"), object_pairs_hook=unique_pairs,
                           parse_constant=no_json_constant)
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise Blocked("INVALID_JSON") from exc
    require(type(value) is dict, "SNAPSHOT_ROOT_NOT_OBJECT")
    return value


def verify(snapshot, *, today=None):
    """Returns non-authorizing classification; never returns deployment permission."""
    if today is None:
        today = date.today()
    require(type(today) is date, "INVALID_EVALUATION_DATE")
    exact_keys(snapshot, ROOT_FIELDS, "SNAPSHOT_SCHEMA_DRIFT")
    require(snapshot["schema_version"] == "pmgm.qa525.readonly.snapshot.v1"
            and snapshot["document_type"] ==
            "HISTORICAL_READ_ONLY_OBSERVATION_NOT_AUTHORIZATION"
            and snapshot["automation_status"] ==
            "NOT_AUTOMATED_NOT_SIGNED_NOT_DEPLOYED",
            "SNAPSHOT_MUST_REMAIN_HISTORICAL")
    require(type(snapshot["generated_by"]) is str
            and type(snapshot["provenance_rule"]) is str
            and len(snapshot["provenance_rule"]) > 20,
            "PROVENANCE_REQUIRED")
    try:
        observed = date.fromisoformat(snapshot["observed_date"])
    except (TypeError, ValueError) as exc:
        raise Blocked("OBSERVED_DATE_INVALID") from exc
    require(observed <= today, "FUTURE_SNAPSHOT_DISALLOWED")

    env = snapshot["environment"]
    exact_keys(env, {
        "name", "single_physical_host", "allowed_host_names",
        "physical_host_readback_this_snapshot",
        "production_authorized", "main_promotion_authorized",
    }, "ENVIRONMENT_SCHEMA_DRIFT")
    require(env["name"] == "QA_SYNTHETIC"
            and env["single_physical_host"] == "glmdch"
            and env["allowed_host_names"] == ["glmdch"]
            and env["physical_host_readback_this_snapshot"] is False
            and env["production_authorized"] is False
            and env["main_promotion_authorized"] is False,
            "UNSUPPORTED_QA_CLAIM")

    g = snapshot["github"]
    exact_keys(g, {"repository", "observed_dev_sha", "observed_main_sha",
                   "launcher_recovery_pr", "original_signed_release"},
               "GITHUB_SCHEMA_DRIFT")
    require(g["repository"] == REPO
            and sha40(g["observed_dev_sha"]) and sha40(g["observed_main_sha"]),
            "GITHUB_REVISION_INVALID")
    pr = g["launcher_recovery_pr"]
    exact_keys(pr, {"number", "head_sha", "state",
                    "ci_workflows_exact_head", "independent_approved_reviews",
                    "code_status"}, "PR_SCHEMA_DRIFT")
    require(type(pr["number"]) is int and pr["number"] == 838
            and sha40(pr["head_sha"])
            and pr["state"] == "OPEN_DRAFT_NOT_MERGED"
            and pr["ci_workflows_exact_head"] == "8_OF_8_COMPLETED_SUCCESS"
            and type(pr["independent_approved_reviews"]) is int
            and pr["independent_approved_reviews"] == 0
            and pr["code_status"] ==
            "SANDBOX_AND_PURE_CONTRACTS_NOT_PHYSICAL_RECOVERY",
            "ROOT_REVIEW_MUST_REMAIN_NON_AUTHORIZING")
    release = g["original_signed_release"]
    exact_keys(release, {"source_sha", "asset_sha256", "asset_bytes",
                         "contains_pr_838_changes"}, "RELEASE_SCHEMA_DRIFT")
    require(release["source_sha"] == g["observed_dev_sha"]
            and sha64(release["asset_sha256"])
            and type(release["asset_bytes"]) is int
            and release["asset_bytes"] > 0
            and release["contains_pr_838_changes"] is False,
            "SIGNED_RELEASE_CANNOT_CLAIM_PR_838")

    physical = snapshot["physical_qa"]
    exact_keys(physical, {"verification_level", "launcher", "api_web",
                          "pending_prepared", "any_fresh_application",
                          "uat", "issues_under_test"}, "PHYSICAL_SCHEMA_DRIFT")
    require(physical["verification_level"] ==
            "NOT_REINSPECTED_IN_THIS_SNAPSHOT"
            and physical["launcher"] ==
            "LAST_OPERATOR_EVIDENCE_OLD_LAUNCHER_NO_CURRENT_READBACK"
            and physical["api_web"] ==
            "LAST_OPERATOR_EVIDENCE_APPLIED_41d9c490d138419c7286445f37e7aeddf0d1b8d5_NOT_REINSPECTED"
            and physical["pending_prepared"] ==
            "LAST_OPERATOR_EVIDENCE_A184936D_PREPARED_EXPIRED_NOT_APPLIED"
            and physical["any_fresh_application"] == "NOT_CLAIMED"
            and physical["uat"] == "NOT_COMPLETED"
            and type(physical["issues_under_test"]) is list
            and all(type(v) is str for v in physical["issues_under_test"]),
            "PHYSICAL_HOST_NOT_PROVEN")

    backup = snapshot["backup_g6"]
    exact_keys(backup, {"snapshot_id", "archive_sha256",
                        "encrypted_offhost_copy", "same_host_isolated_restore",
                        "disaster_restore_other_host", "iam_restore",
                        "atomic_cross_resource_snapshot",
                        "validity_for_future_cut"}, "G6_SCHEMA_DRIFT")
    require(re.fullmatch(r"20[0-9]{6}T[0-9]{6}Z",
                         backup["snapshot_id"]) is not None
            and sha64(backup["archive_sha256"])
            and backup["encrypted_offhost_copy"] ==
            "VERIFIED_BY_PRIOR_HASH_AND_CURRENT_METADATA_ONLY"
            and backup["same_host_isolated_restore"] ==
            "PASS_PRIOR_OPERATOR_EVIDENCE_POSTGRES_2DB_GLOBALS_AND_SILO_CLEANUP"
            and backup["disaster_restore_other_host"] == "PENDING"
            and backup["iam_restore"] == "NOT_INCLUDED"
            and backup["atomic_cross_resource_snapshot"] == "NOT_PROVEN"
            and backup["validity_for_future_cut"] ==
            "REASSESS_FRESHNESS_BEFORE_APPROVAL",
            "G6_OVERCLAIM_OR_INVALID_HASH")

    decision = snapshot["decision"]
    exact_keys(decision, {"launcher_physical_cut", "root_bootstrap",
                          "api_web_physical_update", "blockers",
                          "next_allowed_action", "post_gate_sequence",
                          "restrictions"}, "DECISION_SCHEMA_DRIFT")
    require(decision["launcher_physical_cut"] == "NO_GO"
            and decision["root_bootstrap"] == "DO_NOT_EXECUTE"
            and decision["api_web_physical_update"] ==
            "ON_HOLD_SEPARATE_APPROVAL_REQUIRED"
            and type(decision["blockers"]) is list
            and len(decision["blockers"]) >= 2
            and "DO_NOT_MERGE_MAIN" in decision["restrictions"]
            and "DO_NOT_USE_SRV01" in decision["restrictions"],
            "UNAUTHORIZED_GO_CLAIM")

    links = snapshot["source_links"]
    require(type(links) is dict and len(links) >= 3
            and all(type(v) is str
                    and v.startswith("https://github.com/" + REPO + "/")
                    for v in links.values()), "UNTRUSTED_SOURCE_LINK")
    rules = snapshot["consumption_rules"]
    require(type(rules) is list and len(rules) >= 3
            and all(type(v) is str and len(v) >= 10 for v in rules),
            "NO_CONSUMPTION_RESTRICTIONS")
    age_days = (today - observed).days
    return {
        "status": "HISTORICAL_SNAPSHOT_SHAPE_VALID_NOT_LIVE_QA",
        "schema_version": snapshot["schema_version"],
        "observed_date": snapshot["observed_date"],
        "age_days": age_days,
        "same_calendar_day": age_days == 0,
        "current_git_and_drive_rechecked": False,
        "current_physical_host_verified": False,
        "may_deploy": False,
        "may_merge": False,
        "may_authorize_root": False,
        "requires_fresh_independent_evidence": True,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("snapshot", nargs="?", default="docs/qa/status-current.json")
    args = parser.parse_args(argv)
    try:
        report = verify(read_snapshot(args.snapshot))
    except (OSError, Blocked, TypeError, KeyError, AttributeError) as exc:
        code = str(exc) if isinstance(exc, Blocked) else "UNREADABLE_OR_INVALID_SNAPSHOT"
        print(json.dumps({"status": "BLOCKED_HISTORICAL_SNAPSHOT", "code": code,
                          "may_deploy": False, "may_merge": False}))
        return 2
    print(json.dumps(report, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
