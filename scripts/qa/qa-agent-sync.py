#!/usr/bin/env python3
"""Fail-closed, baseline-gated QA auto-staging. NEVER deploy or start services.

Only a supervised first deployment may establish the trusted baseline. The
candidate and its artifact are pinned throughout download and signed staging.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import re
import sys

HERE = Path(__file__).resolve().parent
SHA = re.compile(r"[a-f0-9]{40}\Z")
DIGEST = re.compile(r"sha256:[a-f0-9]{64}\Z")


class Blocked(Exception):
    pass


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded


def selection_tuple(value):
    if not isinstance(value, dict) or value.get("status") != "READY_FOR_SIGNED_STAGING_ONLY" or value.get("scope") != "NO_HOST_ACCESS_NO_DEPLOYMENT_NO_UAT":
        raise Blocked("SIGNED_SELECTION_INVALID")
    sha, artifact, digest = value.get("source_sha"), value.get("artifact_id"), value.get("artifact_sha256")
    if (not isinstance(sha, str) or SHA.fullmatch(sha) is None or
            type(artifact) is not int or artifact <= 0 or
            not isinstance(digest, str) or DIGEST.fullmatch(digest) is None):
        raise Blocked("SIGNED_SELECTION_INVALID")
    return sha, artifact, digest


def sync(planning, baseline_reader, selector, compare, acquire_candidate):
    """Dependency-injected for offline adversarial tests. No Docker permitted.

    acquire_candidate accepts a pinned selection callback; the existing
    qa-acquire-candidate module will read that callback twice around staging.
    """
    previous = baseline_reader()  # MUST happen before contacting GitHub or reading tokens.
    if not isinstance(previous, str) or SHA.fullmatch(previous) is None:
        raise Blocked("NO_TRUSTED_QA_BASELINE")
    decision = planning(lambda: previous, selector, compare)
    if not isinstance(decision, dict):
        raise Blocked("UNTRUSTED_PLAN")
    if decision.get("status") == "QA_UP_TO_DATE_NO_ACTION":
        if decision.get("source_sha") != previous:
            raise Blocked("PLAN_SHA_MISMATCH")
        return {"status": "QA_UP_TO_DATE_NO_ACTION", "source_sha": previous,
                "scope": "NO_DOCKER_NO_DEPLOYMENT"}
    if decision.get("status") != "QA_SIGNED_CANDIDATE_ELIGIBLE_FOR_STAGING_ONLY":
        raise Blocked("PLAN_NOT_ELIGIBLE")
    planned = (decision.get("source_sha"), decision.get("artifact_id"),
               decision.get("artifact_sha256"))
    if (not isinstance(planned[0], str) or SHA.fullmatch(planned[0]) is None or
            type(planned[1]) is not int or planned[1] <= 0 or
            not isinstance(planned[2], str) or DIGEST.fullmatch(planned[2]) is None or
            decision.get("scope") != "READ_ONLY_NO_HOST_DEPLOYMENT"):
        raise Blocked("PLAN_METADATA_INVALID")

    def pinned_selector():
        current = selector()
        if selection_tuple(current) != planned:
            raise Blocked("DEV_ARTIFACT_CHANGED_AFTER_PLAN")
        return current

    staged = acquire_candidate(pinned_selector)
    if (not isinstance(staged, dict) or
            staged.get("status") != "VERIFIED_QA_CANDIDATE_PREPARED_NOT_DEPLOYED" or
            staged.get("source_sha") != planned[0] or
            staged.get("artifact_id") != planned[1] or
            staged.get("scope") != "NO_DOCKER_NO_INSTALLATION_NO_QA_ACCEPTANCE"):
        raise Blocked("STAGING_RESULT_MISMATCH")
    if baseline_reader() != previous:
        # A candidate may remain staged, but cannot be promoted on a changed baseline.
        raise Blocked("QA_BASELINE_CHANGED_DURING_STAGING")
    return {"status": "SIGNED_QA_CANDIDATE_STAGED_NO_DEPLOYMENT",
            "source_sha": planned[0], "artifact_id": planned[1],
            "scope": "NO_DOCKER_NO_INSTALLATION_NO_QA_ACCEPTANCE"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--last-deployed", type=Path, required=True)
    parser.add_argument("--token-file", type=Path, required=True)
    parser.add_argument("--staging-root", type=Path, required=True)
    args = parser.parse_args()
    try:
        planner = module("qa_agent_plan", "qa-agent-plan.py")
        baseline = module("qa_antirollback", "qa-antirollback.py")
        readiness = module("qa_agent_readiness", "qa-agent-readiness.py")
        acquire = module("qa_acquire_candidate", "qa-acquire-candidate.py")
        downloader = module("qa_artifact_download", "qa-artifact-download.py")
        staging = module("qa_signed_staging", "qa-signed-staging.py")
        output = sync(
            planner.plan,
            lambda: baseline.baseline(args.last_deployed),
            lambda: readiness.evaluate(readiness.fetch_public),
            baseline.evaluate,
            lambda selector: acquire.acquire(args.staging_root, args.token_file, selector,
                                             downloader.download, staging.stage, downloader.read_token),
        )
        print(json.dumps(output, sort_keys=True))
        return 0
    except Exception as exc:
        # Never echo filesystem paths, external response bodies or credential data.
        known = {"NO_TRUSTED_QA_BASELINE", "UNTRUSTED_PLAN", "PLAN_SHA_MISMATCH",
                 "PLAN_NOT_ELIGIBLE", "PLAN_METADATA_INVALID", "SIGNED_SELECTION_INVALID",
                 "DEV_ARTIFACT_CHANGED_AFTER_PLAN", "STAGING_RESULT_MISMATCH",
                 "QA_BASELINE_CHANGED_DURING_STAGING",
                 "FIRST_DEPLOYMENT_REQUIRES_SUPERVISED_APPROVAL",
                 "BASELINE_OWNER_MODE_OR_SIZE", "BASELINE_UNAVAILABLE",
                 "BASELINE_SCHEMA_INVALID", "BASELINE_JSON_INVALID", "DUPLICATE_BASELINE_FIELD",
                 "NON_FAST_FORWARD_QA_UPDATE", "GITHUB_ANCESTRY_UNAVAILABLE",
                 "CANDIDATE_ALREADY_STAGED", "DEV_HEAD_OR_ARTIFACT_CHANGED_DURING_STAGING"}
        code = str(exc) if str(exc) in known else "QA_AUTOSTAGING_BLOCKED"
        print(json.dumps({"status": "BLOCKED_QA_AUTOSTAGING", "code": code,
                          "scope": "NO_DOCKER_NO_DEPLOYMENT"}, sort_keys=True))
        return 1


if __name__ == "__main__":
    sys.exit(main())
