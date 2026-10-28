#!/usr/bin/env python3
"""Read-only, fail-closed HEAD+gates+signed-release metadata gate for QA525.

This program DOES NOT authenticate an asset's Sigstore signature: a later
signed-staging step must download, hash and verify it independently. It cannot
deploy, build, change accounts, invoke Docker, or access local secrets.
"""
import importlib.util
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location(
    "qa525_original_readiness", ROOT / "qa-agent-readiness.py")
readiness = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(readiness)

PUBLISH_WORKFLOW = "Publish signed synthetic QA envelope"
RESULT_SCOPE = "NO_HOST_ACCESS_SIGNATURE_NOT_VERIFIED_NO_DEPLOYMENT_NO_UAT"


class Blocked(Exception):
    """Fixed, non-sensitive reason code."""


def require(value, reason):
    if not value:
        raise Blocked(reason)


def evaluate(fetch):
    # Reuse the established 5-workflow selector and the unique signed Silo
    # artifact, rather than maintaining a weaker duplicate of that logic.
    selected = readiness.evaluate(fetch)
    sha = selected["source_sha"]
    expected_tag = "qa-signed-" + sha
    runs = fetch("/actions/runs?head_sha=" + sha + "&per_page=100")
    workflow_runs = runs.get("workflow_runs")
    require(isinstance(workflow_runs, list), "PUBLISH_WORKFLOW_INVENTORY_INVALID")
    publisher = [r for r in workflow_runs if isinstance(r, dict)
                 and r.get("name") == PUBLISH_WORKFLOW
                 and r.get("head_sha") == sha
                 and r.get("head_branch") == "dev" and r.get("event") == "push"]
    require(publisher, "PUBLISH_WORKFLOW_MISSING")
    require(all(type(r.get("id")) is int and r["id"] > 0 and
                type(r.get("run_attempt", 1)) is int and
                r.get("run_attempt", 1) > 0 for r in publisher),
            "PUBLISH_WORKFLOW_AMBIGUOUS")
    latest = max(publisher, key=lambda r: (r["id"], r.get("run_attempt", 1)))
    require(latest.get("status") == "completed" and
            latest.get("conclusion") == "success", "PUBLISH_WORKFLOW_NOT_SUCCESS")

    release = fetch("/releases/tags/" + expected_tag)
    require(isinstance(release, dict) and
            release.get("tag_name") == expected_tag and
            release.get("target_commitish") == sha and
            release.get("draft") is False and
            release.get("prerelease") is True,
            "RELEASE_SOURCE_OR_STATUS_MISMATCH")

    assets = release.get("assets")
    require(isinstance(assets, list), "RELEASE_ASSETS_INVALID")
    name = expected_tag + ".zip"
    matches = [a for a in assets if isinstance(a, dict) and a.get("name") == name]
    require(len(assets) == len(matches) == 1, "RELEASE_ASSET_NOT_UNIQUE")
    asset = matches[0]
    require(type(asset.get("id")) is int and asset["id"] > 0 and
            type(asset.get("size")) is int and asset["size"] > 0 and
            asset.get("digest") == selected["artifact_sha256"],
            "RELEASE_ASSET_DIGEST_OR_SIZE_MISMATCH")

    # A branch movement between API calls invalidates the decision. A future
    # host-side staging/apply must RECHECK, never reuse an old response.
    current = fetch("/branches/dev").get("commit", {}).get("sha")
    require(current == sha, "DEV_CHANGED_DURING_GATE")

    return {
        "status": "READY_FOR_SIGNATURE_VERIFICATION_ONLY",
        "scope": RESULT_SCOPE,
        "source_sha": sha,
        "publish_workflow_run": latest["id"],
        "silo_workflow_run": selected["silo_workflow_run"],
        "release_tag": expected_tag,
        "release_asset_id": asset["id"],
        "release_asset_sha256": asset["digest"],
        "release_asset_size": asset["size"],
    }


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    if args:
        print(json.dumps({"status": "BLOCKED_QA525_CURRENT_RELEASE",
                          "scope": RESULT_SCOPE, "code": "NO_ARGUMENTS_ALLOWED"},
                         sort_keys=True))
        return 2
    try:
        result = evaluate(readiness.fetch_public)
        print(json.dumps(result, sort_keys=True))
        return 0
    except (Blocked, readiness.GateFailure) as exc:
        print(json.dumps({"status": "BLOCKED_QA525_CURRENT_RELEASE",
                          "scope": RESULT_SCOPE, "code": str(exc)},
                         sort_keys=True))
        return 1
    except (TypeError, AttributeError, KeyError, ValueError):
        print(json.dumps({"status": "BLOCKED_QA525_CURRENT_RELEASE",
                          "scope": RESULT_SCOPE, "code": "INVALID_REMOTE_INVENTORY"},
                         sort_keys=True))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
