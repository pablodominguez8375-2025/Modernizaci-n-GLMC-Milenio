#!/usr/bin/env python3
"""Fail-closed public GitHub readiness probe for synthetic glmdch QA.

Read-only and intentionally NOT a deployment agent. On glmdch it requires
an independently provisioned root-only PAT. It never writes host state,
launches commands, downloads artifacts, or changes Docker.
"""
import json
import os
from pathlib import Path
import re
import socket
import ssl
import stat
import sys
from urllib.error import HTTPError, URLError
from urllib.request import (HTTPRedirectHandler, HTTPSHandler, ProxyHandler,
                            Request, build_opener, urlopen)

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


# PAT is never accepted through CLI, URL, environment or user-visible logs.
QA_PAT = Path("/etc/pmgm/qa525-github-readonly.pat")
PAT_FORMAT = re.compile(r"github_pat_[A-Za-z0-9_]{20,250}\Z")


class _NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def _on_qa_host():
    return socket.gethostname().split(".", 1)[0] == "glmdch"


def _read_qa_pat():
    """Root-only on glmdch; fail closed if the token source is unavailable."""
    if not _on_qa_host():
        return None
    if os.geteuid() != 0:
        raise GateFailure("QA_PAT_REQUIRES_ROOT")
    try:
        parent = QA_PAT.parent.lstat()
        if (not stat.S_ISDIR(parent.st_mode) or parent.st_uid != 0 or
                parent.st_mode & 0o022):
            raise GateFailure("QA_PAT_DIRECTORY_UNTRUSTED")
        fd = os.open(QA_PAT, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        with os.fdopen(fd, "rb") as handle:
            info = os.fstat(handle.fileno())
            if (not stat.S_ISREG(info.st_mode) or info.st_uid != 0 or
                    info.st_gid != 0 or info.st_nlink != 1 or
                    stat.S_IMODE(info.st_mode) != 0o600 or
                    not 20 <= info.st_size <= 300):
                raise GateFailure("QA_PAT_FILE_UNTRUSTED")
            raw = handle.read(301)
            if len(raw) != info.st_size:
                raise GateFailure("QA_PAT_FILE_CHANGED")
        token = raw.decode("ascii").removesuffix("\n")
        if PAT_FORMAT.fullmatch(token) is None:
            raise GateFailure("QA_PAT_FORMAT_INVALID")
        return token
    except (OSError, UnicodeError):
        raise GateFailure("QA_PAT_FILE_UNAVAILABLE") from None


def _authenticated_api_get(url, token, *, timeout=12):
    """Only this repository's GitHub HTTPS API can receive Authorization."""
    if (type(url) is not str or not url.startswith(API + "/") or
            "@" in url or "#" in url or "\\" in url or
            any(ord(ch) < 32 or ord(ch) > 126 for ch in url)):
        raise GateFailure("INVALID_AUTHENTICATED_API_URL")
    req = Request(url, headers={
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "centenario-qa-readiness-auth-v1",
        "Authorization": "Bearer " + token,
    })
    opener = build_opener(
        ProxyHandler({}), HTTPSHandler(context=ssl.create_default_context()),
        _NoRedirect())
    with opener.open(req, timeout=timeout) as resp:
        if resp.geturl() != url:
            raise GateFailure("API_REDIRECT_FORBIDDEN")
        data = resp.read(16_777_217)
        if len(data) > 16_777_216:
            raise GateFailure("GITHUB_API_RESPONSE_TOO_LARGE")
        return json.loads(data)


def fetch_public(path):
    """Legacy public mode off-host; authenticated, fail-closed mode on QA."""
    if (type(path) is not str or not path.startswith("/") or
            "//" in path or "@" in path or "\\" in path or "#" in path or
            any(ord(ch) < 32 or ord(ch) > 126 for ch in path)):
        raise GateFailure("INVALID_API_PATH")
    token = _read_qa_pat()
    url = API + path
    try:
        if token is not None:
            return _authenticated_api_get(url, token)
        req = Request(url, headers={
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "centenario-qa-readiness-v1",
        })
        with urlopen(req, timeout=12) as resp:
            if resp.geturl().split("/", 3)[:3] != ["https:", "", "api.github.com"]:
                raise GateFailure("API_REDIRECT_FORBIDDEN")
            return json.load(resp)
    except HTTPError as exc:
        headers = exc.headers or {}
        if exc.code in (403, 429) and headers.get("X-RateLimit-Remaining") == "0":
            raise GateFailure("GITHUB_API_RATE_LIMITED") from None
        if exc.code == 429 or headers.get("Retry-After"):
            raise GateFailure("GITHUB_API_RETRY_LATER") from None
        if exc.code == 401:
            raise GateFailure("GITHUB_API_TOKEN_REJECTED") from None
        raise GateFailure("GITHUB_API_HTTP_ERROR") from None
    except (URLError, OSError, ValueError):
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


def evaluate_latest_validated(fetch):
    """Most recent fully green PUSH that is still an ancestor of live dev.

    Does not relax evaluate(): each historical candidate is checked against all
    latest attempts and its exact signed artifact. Bounded to 100 recent runs.
    This avoids starving QA while newer dev commits are still being tested.
    """
    tip = fetch("/branches/dev").get("commit", {}).get("sha")
    if not isinstance(tip, str) or not SHA.fullmatch(tip):
        raise GateFailure("DEV_SHA_MISSING")
    try:
        return evaluate(fetch)
    except GateFailure:
        pass
    runs = fetch("/actions/runs?branch=dev&event=push&status=success&per_page=100")
    candidates = {}
    for run in runs.get("workflow_runs", []):
        sha = run.get("head_sha")
        if (run.get("event") == "push" and run.get("head_branch") == "dev"
                and run.get("name") in REQUIRED and isinstance(sha, str) and SHA.fullmatch(sha)
                and type(run.get("id")) is int):
            candidates[sha] = max(candidates.get(sha, 0), run["id"])
    for sha in sorted(candidates, key=candidates.get, reverse=True):
        if sha == tip:
            continue
        comparison = fetch("/compare/" + sha + "..." + tip)
        if (comparison.get("status") != "ahead"
                or comparison.get("merge_base_commit", {}).get("sha") != sha):
            continue
        def pinned(path):
            return {"commit": {"sha": sha}} if path == "/branches/dev" else fetch(path)
        try:
            candidate = evaluate(pinned)
        except GateFailure:
            continue
        if fetch("/branches/dev").get("commit", {}).get("sha") != tip:
            raise GateFailure("DEV_CHANGED_DURING_VALIDATED_SELECTION")
        return candidate
    raise GateFailure("NO_RECENT_VALIDATED_DEV_ANCESTOR")


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
