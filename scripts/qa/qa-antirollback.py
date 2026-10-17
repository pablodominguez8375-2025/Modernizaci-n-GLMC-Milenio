#!/usr/bin/env python3
"""Guard QA updates against stale commits and unapproved first boot.

Read-only. Never creates baseline state, installs, downloads or runs Docker.
The baseline must be recorded by a separate supervised acceptance process.
"""
import argparse
import json
import os
from pathlib import Path
import re
import stat
import sys
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
SHA = re.compile(r"^[0-9a-f]{40}$")
BASELINE = "/var/lib/pmgm-qa-agent/last-deployed.json"


class Blocked(Exception):
    pass


def reject_duplicates(pairs):
    result = {}
    for name, value in pairs:
        if name in result:
            raise Blocked("DUPLICATE_BASELINE_FIELD")
        result[name] = value
    return result


def baseline(path):
    path = Path(path)
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    except FileNotFoundError:
        raise Blocked("FIRST_DEPLOYMENT_REQUIRES_SUPERVISED_APPROVAL") from None
    except OSError:
        raise Blocked("BASELINE_UNAVAILABLE") from None
    with os.fdopen(fd, "rb") as stream:
        st = os.fstat(stream.fileno())
        if (not stat.S_ISREG(st.st_mode) or st.st_uid != os.geteuid() or
                (stat.S_IMODE(st.st_mode) & 0o077) or st.st_size > 2048 or
                st.st_size == 0 or st.st_nlink != 1):
            raise Blocked("BASELINE_OWNER_MODE_OR_SIZE")
        raw = stream.read(2049)
    try:
        state = json.loads(raw, object_pairs_hook=reject_duplicates)
    except (UnicodeError, ValueError, TypeError):
        raise Blocked("BASELINE_JSON_INVALID") from None
    if (not isinstance(state, dict) or set(state) !=
            {"version", "last_deployed_sha", "qa_only"} or
            type(state["version"]) is not int or state["version"] != 1 or
            state["qa_only"] is not True or
            not isinstance(state["last_deployed_sha"], str) or
            SHA.fullmatch(state["last_deployed_sha"]) is None):
        raise Blocked("BASELINE_SCHEMA_INVALID")
    return state["last_deployed_sha"]


def fetch_compare(base, head):
    url = ("https://api.github.com/repos/" + REPO +
           "/compare/" + base + "..." + head)
    request = Request(url, headers={
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "pmgm-qa-antirollback-v1",
    })
    try:
        with urlopen(request, timeout=15) as response:
            if response.geturl().split("/", 3)[:3] != ["https:", "", "api.github.com"]:
                raise Blocked("UNTRUSTED_COMPARE_REDIRECT")
            return json.load(response)
    except (HTTPError, URLError, OSError, ValueError):
        raise Blocked("GITHUB_ANCESTRY_UNAVAILABLE") from None


def evaluate(previous_sha, candidate_sha, compare=fetch_compare):
    if (not isinstance(previous_sha, str) or SHA.fullmatch(previous_sha) is None or
            not isinstance(candidate_sha, str) or SHA.fullmatch(candidate_sha) is None):
        raise Blocked("INVALID_COMMIT_SHA")
    if previous_sha == candidate_sha:
        return {"status": "ALREADY_DEPLOYED_NO_ACTION", "candidate_sha": candidate_sha,
                "scope": "NO_INSTALLATION_NO_HOST_CHANGE"}
    result = compare(previous_sha, candidate_sha)
    if (not isinstance(result, dict) or result.get("status") != "ahead" or
            type(result.get("ahead_by")) is not int or result["ahead_by"] <= 0 or
            result.get("base_commit", {}).get("sha") != previous_sha or
            result.get("merge_base_commit", {}).get("sha") != previous_sha or
            result.get("commits", [{}])[-1].get("sha") != candidate_sha):
        raise Blocked("NON_FAST_FORWARD_QA_UPDATE")
    return {"status": "FAST_FORWARD_CANDIDATE_ONLY",
            "candidate_sha": candidate_sha, "previous_deployed_sha": previous_sha,
            "scope": "NO_INSTALLATION_NO_HOST_CHANGE"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--last-deployed", default=BASELINE, type=Path)
    parser.add_argument("--candidate-sha", required=True)
    args = parser.parse_args()
    try:
        previous = baseline(args.last_deployed)
        report = evaluate(previous, args.candidate_sha)
        print(json.dumps(report, sort_keys=True))
        return 0
    except Blocked as e:
        print(json.dumps({"status": "BLOCKED_QA_ANTIROLLBACK", "code": str(e),
                          "scope": "NO_INSTALLATION_NO_HOST_CHANGE"}, sort_keys=True))
        return 1


if __name__ == "__main__":
    sys.exit(main())
