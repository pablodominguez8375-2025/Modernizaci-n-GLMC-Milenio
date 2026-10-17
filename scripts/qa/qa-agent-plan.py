#!/usr/bin/env python3
"""Determine whether a verified QA candidate may be acquired, without changes.

No SSH, Docker, token, install, download, storage mutation or network outside
read-only GitHub public API. A missing supervised first-boot baseline blocks.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent


class Blocked(Exception):
    pass


def load_module(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def plan(baseline_reader, readiness_selector, compare):
    # A supervised physical QA deployment must create this state. Its absence
    # blocks even fetching the current dev candidate.
    prior = baseline_reader()
    if not isinstance(prior, str):
        raise Blocked("NO_TRUSTED_DEPLOYMENT_BASELINE")
    selected = readiness_selector()
    if (not isinstance(selected, dict) or
            selected.get("status") != "READY_FOR_SIGNED_STAGING_ONLY" or
            selected.get("scope") != "NO_HOST_ACCESS_NO_DEPLOYMENT_NO_UAT"):
        raise Blocked("DEV_NOT_READY_FOR_STAGING")
    candidate = selected.get("source_sha")
    artifact = selected.get("artifact_id")
    digest = selected.get("artifact_sha256")
    import re
    if (not isinstance(candidate, str) or
            re.fullmatch(r"[a-f0-9]{40}", candidate) is None or
            not isinstance(artifact, int) or isinstance(artifact, bool) or artifact <= 0 or
            not isinstance(digest, str) or
            re.fullmatch(r"sha256:[a-f0-9]{64}", digest) is None):
        raise Blocked("INVALID_CANDIDATE_METADATA")
    decision = compare(prior, candidate)
    if not isinstance(decision, dict):
        raise Blocked("INVALID_ANCESTRY_DECISION")
    action = decision.get("status")
    if action == "ALREADY_DEPLOYED_NO_ACTION":
        return {"status": "QA_UP_TO_DATE_NO_ACTION", "source_sha": candidate,
                "scope": "READ_ONLY_NO_HOST_DEPLOYMENT"}
    if action != "FAST_FORWARD_CANDIDATE_ONLY":
        raise Blocked("NON_FAST_FORWARD_UPDATE_FORBIDDEN")
    if (decision.get("previous_deployed_sha") != prior or
            decision.get("candidate_sha") != candidate):
        raise Blocked("ANTIROLLBACK_RESPONSE_MISMATCH")
    return {"status": "QA_SIGNED_CANDIDATE_ELIGIBLE_FOR_STAGING_ONLY",
            "source_sha": candidate, "artifact_id": artifact,
            "artifact_sha256": digest, "scope": "READ_ONLY_NO_HOST_DEPLOYMENT"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--last-deployed", type=Path, required=True)
    opts = parser.parse_args()
    try:
        antirollback = load_module("qa_antirollback", "qa-antirollback.py")
        readiness = load_module("qa_agent_readiness", "qa-agent-readiness.py")
        result = plan(
            lambda: antirollback.baseline(opts.last_deployed),
            lambda: readiness.evaluate(readiness.fetch_public),
            antirollback.evaluate,
        )
        print(json.dumps(result, sort_keys=True))
        return 0
    except Exception as err:
        # Keep errors stable: external content and filesystem details are never
        # printed in journal, GitHub Issues or Drive.
        from_runtime = {"FIRST_DEPLOYMENT_REQUIRES_SUPERVISED_APPROVAL",
                        "BASELINE_OWNER_MODE_OR_SIZE", "BASELINE_UNAVAILABLE",
                        "BASELINE_SCHEMA_INVALID", "BASELINE_JSON_INVALID",
                        "DUPLICATE_BASELINE_FIELD", "NON_FAST_FORWARD_QA_UPDATE",
                        "GITHUB_ANCESTRY_UNAVAILABLE",
                        "REQUIRED_DEV_WORKFLOW_MISSING",
                        "REQUIRED_DEV_WORKFLOW_NOT_SUCCESS",
                        "SIGNED_SILO_ARTIFACT_NOT_UNIQUE",
                        "SIGNED_SILO_ARTIFACT_METADATA_INVALID",
                        "GITHUB_API_UNAVAILABLE"}
        own = {"NO_TRUSTED_DEPLOYMENT_BASELINE", "DEV_NOT_READY_FOR_STAGING",
               "INVALID_CANDIDATE_METADATA", "INVALID_ANCESTRY_DECISION",
               "NON_FAST_FORWARD_UPDATE_FORBIDDEN", "ANTIROLLBACK_RESPONSE_MISMATCH"}
        code = str(err) if str(err) in from_runtime | own else "QA_PLANNER_UNAVAILABLE"
        print(json.dumps({"status": "BLOCKED_QA_AUTOMATION",
                          "code": code, "scope": "READ_ONLY_NO_HOST_DEPLOYMENT"},
                         sort_keys=True))
        return 1


if __name__ == "__main__":
    sys.exit(main())
