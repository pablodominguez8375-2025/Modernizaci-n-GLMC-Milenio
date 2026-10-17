#!/usr/bin/env python3
"""Fail-closed public GitHub readiness probe for synthetic glmdch QA.

Read-only and intentionally NOT a deployment agent: no secrets, downloads,
shell commands, Docker access, host changes or tokens are required here.
"""
import json
import re
import sys
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
API = "https://api.github.com/repos/" + REPO
REQUIRED = (
    "PMGM CI",
    "QA Silo supply and recovery",
    "Proyecto Centenario QA srv01 Installable",
    "QA canonical HTTPS and OIDC",
    "PMGM Showcase Demo",
)
ARTIFACT_PREFIX = "qa-glmdch-signed-storage-"
SHA = re.compile(r"^[a-f0-9]{40}$")
DIGEST = re.compile(r"^sha256:[a-f0-9]{64}$")


class GateFailure(Exception):
    """Intentionally holds only public, fixed diagnostic codes."""


def fetch_public(path):
    if not path.startswith("/") or "//" in path or "@" in path:
        raise GateFailure("INVALID_API_PATH")
    req = Request(API + path, headers={
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "centenario-qa-readiness-v1",
    })
    try:
        with urlopen(req, timeout=12) as resp:
            if resp.geturl().split("/", 3)[:3] != ["https:", "", "api.github.com"]:
                raise GateFailure("API_REDIRECT_FORBIDDEN")
            return json.load(resp)
    except (HTTPError, URLError, OSError, ValueError):
        raise GateFailure("GITHUB_API_UNAVAILABLE") from None


def evaluate(fetch):
    branch = fetch("/branches/dev")
    sha = branch.get("commit", {}).get("sha")
    if not isinstance(sha, str) or not SHA.fullmatch(sha):
        raise GateFailure("DEV_SHA_MISSING")
    runs = fetch("/actions/runs?head_sha=" + sha + "&per_page=100")
    selected = {}
    for run in runs.get("workflow_runs", []):
        name = run.get("name")
        if name not in REQUIRED or run.get("head_sha") != sha:
            continue
        if run.get("event") != "push" or run.get("head_branch") != "dev":
            continue
        identifier = run.get("id")
        attempt = run.get("run_attempt", 1)
        if not isinstance(identifier, int) or not isinstance(attempt, int):
            continue
        prev = selected.get(name)
        if prev is None or (identifier, attempt) > (prev["id"], prev.get("run_attempt", 1)):
            selected[name] = run
    if set(selected) != set(REQUIRED):
        raise GateFailure("REQUIRED_DEV_WORKFLOW_MISSING")
    if any(run.get("status") != "completed" or run.get("conclusion") != "success"
           for run in selected.values()):
        raise GateFailure("REQUIRED_DEV_WORKFLOW_NOT_SUCCESS")
    silo_run = selected["QA Silo supply and recovery"]["id"]
    data = fetch("/actions/runs/" + str(silo_run) + "/artifacts?per_page=100")
    candidates = [a for a in data.get("artifacts", [])
                  if a.get("name") == ARTIFACT_PREFIX + sha and a.get("expired") is False]
    if len(candidates) != 1:
        raise GateFailure("SIGNED_SILO_ARTIFACT_NOT_UNIQUE")
    art = candidates[0]
    if (not isinstance(art.get("id"), int) or
            not isinstance(art.get("size_in_bytes"), int) or art["size_in_bytes"] <= 0 or
            not isinstance(art.get("digest"), str) or not DIGEST.fullmatch(art["digest"])):
        raise GateFailure("SIGNED_SILO_ARTIFACT_METADATA_INVALID")
    return {
        "status": "READY_FOR_SIGNED_STAGING_ONLY",
        "scope": "NO_HOST_ACCESS_NO_DEPLOYMENT_NO_UAT",
        "source_sha": sha,
        "silo_workflow_run": silo_run,
        "artifact_id": art["id"],
        "artifact_sha256": art["digest"],
        "gates": list(REQUIRED),
    }


def main():
    try:
        print(json.dumps(evaluate(fetch_public), sort_keys=True))
        return 0
    except GateFailure as exc:
        print(json.dumps({"status": "BLOCKED_QA_AGENT_READINESS",
                          "scope": "NO_HOST_ACCESS_NO_DEPLOYMENT_NO_UAT",
                          "code": str(exc)}, sort_keys=True))
        return 1


if __name__ == "__main__":
    sys.exit(main())
