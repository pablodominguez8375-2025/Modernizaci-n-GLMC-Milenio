#!/usr/bin/env python3
"""Fail-closed, read-only QA525 host preflight on a currently signed dev.

This does not build, stage, apply, start, stop, install or deploy anything.
Historical 6ff4/424153 is accepted only if the physical host proves it.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import socket
import stat
import sys

HERE = Path(__file__).resolve().parent
POLICY_FILE = HERE / "qa525-current-change-policy.py"
PREFLIGHT_FILE = HERE / "qa525-update-readiness-0bd927b.py"
PREFLIGHT_SHA256 = "7867c8ae21f8be4ad2e60e6aa1491d90c35356e67d07954a2e80994bfb78f2ae"
SCOPE = "HOST_READ_ONLY_NO_BUILD_NO_STAGING_NO_APPLY_NO_UAT"


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def trusted_source(path):
    require(path.is_absolute(), "LOCAL_SOURCE_NOT_ABSOLUTE")
    for item in (path, *path.parents):
        try:
            info = item.lstat()
        except OSError:
            raise Blocked("LOCAL_SOURCE_MISSING") from None
        require(not stat.S_ISLNK(info.st_mode) and info.st_uid == 0 and
                not stat.S_IMODE(info.st_mode) & 0o022, "LOCAL_SOURCE_UNTRUSTED")
    require(path.is_file(), "LOCAL_SOURCE_NOT_FILE")


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def check_host(fetch, candidate, preflight):
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "HOST_NOT_AUTHORIZED")
    selected = candidate.evaluate(fetch, preflight.BASE_API)
    require(selected.get("status") == "ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY",
            "SENSITIVE_CHANGES_REQUIRE_REVIEW")
    require(selected.get("analytical_baseline_sha") == preflight.BASE_API
            and selected.get("may_apply") is False
            and selected.get("source_signed_staging_required") is True
            and selected.get("host_baseline_and_backup_verified") is False,
            "CANDIDATE_POLICY_EVIDENCE_INVALID")
    # Legacy check is read-only; its historical TARGET MUST NEVER authorise
    # an old package. It validates runtime containers, effective env,
    # identity/Master/SMTP, EF migration IDs, backup reference and health.
    with contextlib.redirect_stdout(io.StringIO()):
        preflight.check()
    require(candidate.evaluate(fetch, preflight.BASE_API) == selected,
            "SIGNED_CANDIDATE_CHANGED_DURING_HOST_PREFLIGHT")
    return {
        "status": "HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED",
        "scope": SCOPE,
        "source_sha": selected["source_sha"],
        "baseline_api_sha": preflight.BASE_API,
        "baseline_web_sha": preflight.BASE_WEB,
        "signed_stage_executed": False,
        "fresh_backup_created": False,
        "may_build": False,
        "may_apply": False,
    }


def main():
    try:
        require(sys.argv[1:] == [], "READ_ONLY_NO_OPTIONS")
        require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
                "HOST_NOT_AUTHORIZED")
        for path in (Path(__file__), POLICY_FILE, PREFLIGHT_FILE):
            trusted_source(path)
        require(digest(PREFLIGHT_FILE) == PREFLIGHT_SHA256,
                "REVIEWED_PREFLIGHT_SHA_CHANGED")
        candidate = module("qa525_host_dynamic_policy", POLICY_FILE)
        preflight = module("qa525_host_readonly_preflight", PREFLIGHT_FILE)
        decision = check_host(candidate.gate.readiness.fetch_public,
                              candidate, preflight)
        print(json.dumps(decision, sort_keys=True))
        return 0
    except Blocked as exc:
        print(json.dumps({"status": "BLOCKED_QA525_HOST_PREFLIGHT",
                          "scope": SCOPE, "code": str(exc)}, sort_keys=True))
        return 2
    except Exception:
        # Suppress sensitive Docker/identity/SMTP exceptions and payloads.
        print(json.dumps({"status": "BLOCKED_QA525_HOST_PREFLIGHT",
                          "scope": SCOPE, "code": "HOST_EVIDENCE_UNAVAILABLE"},
                         sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
