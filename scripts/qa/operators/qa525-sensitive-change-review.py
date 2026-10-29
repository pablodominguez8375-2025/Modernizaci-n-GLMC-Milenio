#!/usr/bin/env python3
"""QA525 public signed-dev sensitive change report; NEVER grant apply.

Produces a bounded, evidence-only set of sensitive changed paths and a
review checklist. Does not inspect or approve glmdch, keys, backups or UAT.
The baseline SHA is historical/analytical until a trusted host proves it.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
POLICY = ROOT / "qa525-current-change-policy.py"
SCOPE = "PUBLIC_DIFF_ANALYSIS_ONLY_NO_HOST_NO_APPLY_NO_UAT"
SENSITIVE_REVIEW = {
    "ACCOUNT_OR_IDENTITY_CHANGE": [
        "Compare privileged Master and qa.admin protections and lifecycle policy",
        "Review Keycloak/OIDC issuer, subject mapping, role boundaries and SMTP",
        "Require authenticated synthetic QA checks before any account mutation",
    ],
    "API_BOOTSTRAP_CHANGE": [
        "Review Program.cs startup and effective environment with secrets redacted",
        "Check migrations/startup flags remain disabled during first QA activation",
    ],
    "SCHEMA_OR_MODEL_CHANGE": [
        "Compare EF migration identifiers and model changes against live QA baseline",
        "Require separate migration, restore and data compatibility approval",
    ],
    "INFRASTRUCTURE_CHANGE": [
        "Review Compose layers, image provenance, volumes and auxiliary services",
        "Require unchanged Keycloak, PostgreSQL, MinIO, gateway and ClamAV",
    ],
    "FRONTEND_BUILD_CONFIGURATION_CHANGE": [
        "Revalidate WEB build flags, OIDC authority and static asset security",
    ],
    "OTHER_BACKEND_RUNTIME_CHANGE": [
        "Review backend runtime behavior beyond module routes and permissions",
    ],
    "RENAMED_SOURCE_FILE": [
        "Confirm moved paths and previous names were independently reviewed",
    ],
}
MUST_PROVE = (
    "Signed exact-dev release and complete, untruncated GitHub comparison",
    "Independent read-only physical baseline and current Compose image IDs",
    "Manual security review of all flagged runtime/identity/schema paths",
    "Fresh encrypted off-host pmgm and pmgm_keycloak backups with restore drill",
    "Tested API+WEB dual rollback and privileged account/SMTP invariance",
    "Explicit single-use supervised deployment decision and authenticated UAT",
)


def load_policy():
    spec = importlib.util.spec_from_file_location("qa525_sensitive_review_policy", POLICY)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


policy = load_policy()


class Blocked(Exception):
    pass


def report(baseline, selected, comparison):
    # Use the *same* strict 300-file / 250-commit ancestry and rename
    # controls as the already-reviewed fail-closed policy; never duplicate a
    # weaker validator. This does not authenticate the physical host.
    decision = policy.classify(baseline, selected, comparison)
    listed = {}
    for row in comparison["files"]:
        paths = [row["filename"]]
        if row["status"] == "renamed":
            paths.append(row["previous_filename"])
        for path in paths:
            reason = policy.reason_for(path)
            if reason:
                listed.setdefault(reason, set()).add(path)
        if row["status"] == "renamed":
            listed.setdefault("RENAMED_SOURCE_FILE", set()).update(paths)
    reasons = sorted(listed)
    if decision["status"] == "BLOCKED_REVIEW_REQUIRED":
        if sorted(decision["reasons"]) != reasons:
            raise Blocked("CHANGE_POLICY_AND_REPORT_DISAGREE")
    elif reasons:
        raise Blocked("CHANGE_POLICY_AND_REPORT_DISAGREE")
    groups = []
    for reason in reasons:
        paths = sorted(listed[reason])
        groups.append({
            "reason": reason,
            "affected_path_count": len(paths),
            "affected_paths_sample": paths[:25],
            "sample_truncated": len(paths) > 25,
            "review_items": SENSITIVE_REVIEW.get(
                reason, ["Manually review affected runtime and policy paths"]),
        })
    return {
        "status": ("BLOCKED_SENSITIVE_CHANGE_SUPERVISED_REVIEW_REQUIRED"
                   if reasons else "ANALYTICAL_DIFF_NO_SENSITIVE_FLAGS_HOST_REVIEW_STILL_REQUIRED"),
        "scope": SCOPE,
        "source_sha": selected["source_sha"],
        "analytical_baseline_sha": baseline,
        "changed_files_total": decision["changed_files"],
        "changed_commit_count": comparison["ahead_by"],
        "groups": groups,
        "manual_evidence_required": list(MUST_PROVE),
        "host_baseline_verified": False,
        "physical_backup_verified": False,
        "signed_asset_download_verified": False,
        "manual_review_completed": False,
        "may_build": False,
        "may_apply": False,
    }


def live_report(baseline, fetch):
    selected = policy.gate.evaluate(fetch)
    comparison = fetch("/compare/" + baseline + "..." + selected["source_sha"])
    outcome = report(baseline, selected, comparison)
    # Second full signed gate forbids interpreting stale metadata as current.
    if policy.gate.evaluate(fetch) != selected:
        raise Blocked("SIGNED_DEV_CHANGED_DURING_REVIEW")
    return outcome


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-sha", required=True)
    args = parser.parse_args(argv)
    try:
        response = live_report(args.baseline_sha, policy.gate.readiness.fetch_public)
        print(json.dumps(response, sort_keys=True))
        return 2 if response["status"].startswith("BLOCKED") else 0
    except (Blocked, policy.Blocked, policy.gate.Blocked,
            policy.gate.readiness.GateFailure) as e:
        print(json.dumps({"status": "BLOCKED_QA525_SENSITIVE_REVIEW",
                          "scope": SCOPE,
                          "code": str(e), "may_apply": False}, sort_keys=True))
        return 2
    except Exception:
        print(json.dumps({"status": "BLOCKED_QA525_SENSITIVE_REVIEW",
                          "scope": SCOPE,
                          "code": "PUBLIC_REVIEW_EVIDENCE_UNAVAILABLE",
                          "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
