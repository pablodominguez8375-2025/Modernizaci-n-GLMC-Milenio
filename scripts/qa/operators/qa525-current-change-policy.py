#!/usr/bin/env python3
"""Read-only QA525 candidate classification; not a host approval or installer.

Never allows an old green SHA. The historical API baseline is an analytical
input; the glmdch preflight must independently prove its runtime provenance.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import re
import sys

SHA = re.compile(r"^[0-9a-f]{40}$")
ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location(
    "qa525_change_gate", ROOT / "qa525-current-release-gate.py")
gate = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = gate
SPEC.loader.exec_module(gate)
SCOPE = "READ_ONLY_CHANGE_REVIEW_HOST_BASELINE_UNVERIFIED_NO_DEPLOYMENT"


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def reason_for(path):
    """Return conservative, public reason codes; never evaluate code or secrets."""
    filename = path.lower()
    if ("/migrations/" in filename or "dbcontext" in filename
            or "/entities/" in filename or "/models/" in filename):
        return "SCHEMA_OR_MODEL_CHANGE"
    if ("identity" in filename or "keycloak" in filename
            or "useraccount" in filename or "accountlifecycle" in filename
            or "accountinventory" in filename or "auth" in filename
            or "master" in filename or "systemmail" in filename
            or "accounttoken" in filename):
        return "ACCOUNT_OR_IDENTITY_CHANGE"
    if (path == "frontend/src/main.tsx"
            or path == "backend/src/PMGM.Api/Program.cs"
            or path.startswith("backend/src/PMGM.Api/Configuration/")
            or path.startswith("backend/src/PMGM.Api/Infrastructure/")
            or path.startswith("backend/src/PMGM.Api/Properties/")
            or path.startswith("backend/src/PMGM.Api/appsettings")
            or path.startswith("backend/src/PMGM.Api/Startup")):
        return "API_BOOTSTRAP_CHANGE"
    if (filename.endswith("dockerfile") or filename.endswith(".dockerignore")
            or "docker-compose" in filename or "compose." in filename
            or path.startswith(("infra/", "deploy/", "infrastructure/"))):
        return "INFRASTRUCTURE_CHANGE"
    if path.startswith("backend/src/") and not path.startswith(
            "backend/src/PMGM.Api/Modules/"):
        return "OTHER_BACKEND_RUNTIME_CHANGE"
    if path.startswith("frontend/") and not path.startswith("frontend/src/"):
        return "FRONTEND_BUILD_CONFIGURATION_CHANGE"
    return None


def classify(baseline, selected, diff):
    """No mutation. Accept only a complete, ancestrally related comparison."""
    require(type(baseline) is str and SHA.fullmatch(baseline),
            "BASELINE_SHA_INVALID")
    sha = selected.get("source_sha")
    require(selected.get("status") == "READY_FOR_SIGNATURE_VERIFICATION_ONLY"
            and selected.get("scope") == gate.RESULT_SCOPE
            and type(sha) is str and SHA.fullmatch(sha),
            "UNVERIFIED_RELEASE_SELECTION")
    require(baseline != sha, "BASELINE_EQUALS_CURRENT_DEV")
    require(isinstance(diff, dict)
            and diff.get("status") == "ahead"
            and diff.get("merge_base_commit", {}).get("sha") == baseline
            and type(diff.get("behind_by")) is int and diff["behind_by"] == 0
            and type(diff.get("ahead_by")) is int and 0 < diff["ahead_by"] < 250,
            "BASELINE_NOT_ANCESTOR_OR_DIFF_INCOMPLETE")
    files = diff.get("files")
    require(isinstance(files, list) and 0 < len(files) < 300,
            "DIFF_INCOMPLETE_OR_OVERSIZE")
    seen = set()
    warnings = set()
    for entry in files:
        require(isinstance(entry, dict), "DIFF_FILE_INVALID")
        name = entry.get("filename")
        change = entry.get("status")
        require(type(name) is str and 0 < len(name) <= 512 and
                not name.startswith("/") and "\\" not in name and
                not any(part in ("", ".", "..") for part in name.split("/")) and
                name not in seen and
                change in ("added", "removed", "modified", "renamed",
                           "copied", "changed"),
                "DIFF_FILE_UNTRUSTED")
        seen.add(name)
        cause = reason_for(name)
        if cause:
            warnings.add(cause)
        if change == "renamed":
            previous = entry.get("previous_filename")
            require(type(previous) is str and previous,
                    "RENAMED_PREVIOUS_PATH_MISSING")
            previous_reason = reason_for(previous)
            if previous_reason:
                warnings.add(previous_reason)
            # Moving code between paths changes runtime expectations.
            warnings.add("RENAMED_SOURCE_FILE")
    return {
        "status": ("BLOCKED_REVIEW_REQUIRED" if warnings
                   else "ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY"),
        "scope": SCOPE,
        "source_sha": sha,
        "analytical_baseline_sha": baseline,
        "changed_files": len(files),
        "reasons": sorted(warnings),
        "source_signed_staging_required": True,
        "host_baseline_and_backup_verified": False,
        "may_apply": False,
    }


def evaluate(fetch, baseline):
    selected = gate.evaluate(fetch)
    target = selected["source_sha"]
    diff = fetch("/compare/" + baseline + "..." + target)
    result = classify(baseline, selected, diff)
    require(gate.evaluate(fetch) == selected, "CURRENT_RELEASE_CHANGED_DURING_REVIEW")
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-sha", required=True)
    args = parser.parse_args(argv)
    try:
        decision = evaluate(gate.readiness.fetch_public, args.baseline_sha)
        print(json.dumps(decision, sort_keys=True))
        return 0 if decision["status"] == "ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY" else 2
    except (Blocked, gate.Blocked, gate.readiness.GateFailure) as exc:
        print(json.dumps({"status": "BLOCKED_QA525_CHANGE_REVIEW",
                          "scope": SCOPE, "code": str(exc)}, sort_keys=True))
        return 1
    except (KeyError, AttributeError, TypeError, ValueError):
        print(json.dumps({"status": "BLOCKED_QA525_CHANGE_REVIEW",
                          "scope": SCOPE, "code": "INVALID_REMOTE_EVIDENCE"},
                         sort_keys=True))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
