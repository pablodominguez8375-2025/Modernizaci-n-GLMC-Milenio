#!/usr/bin/env python3
"""QA525 frozen BUILD_ONLY review contract, offline and fail-closed.

This module is NOT a host entrypoint. It cannot stage, build, apply or grant GO.
It binds a single already-signed release to an APPLIED+LIVE+HEALTHY predecessor.
External callers must independently authenticate the release and technical review.
"""
import re

SOURCE = "da274d651d8e9e056525413406c0aab62131504f"
DIGEST = "sha256:f869f3e12e048d70aaecf5e4e2f5b348d6378cf02aaa9fc0e5ee3243460f2e82"
PREDECESSOR = "6eed4f00b7917da3cca36bbda70e16d16328648d"
SCOPE = "FROZEN_BUILD_ONLY_REVIEW_NO_HOST_AUTHORITY"
SHA = re.compile(r"[0-9a-f]{40}\Z")
# These are QA-only changes flagged by the historical filename heuristic.
# They are NOT silently waived: each requires authenticated technical review.
QA_ONLY_REVIEW_PATHS = frozenset((
    "infrastructure/silo/Dockerfile.qa-rebuild",
    "scripts/qa/qa525-vnext-root-authenticity.py",
    "tests/test_qa525_pat_auth.py",
    "tests/test_qa525_vnext_root_authenticity.py",
))


class Blocked(Exception):
    pass


def require(value, code):
    if not value:
        raise Blocked(code)


def review_contract(source, asset_digest, baseline, diff, reason_for,
                    *, release_authenticated=False, review_authenticated=False):
    """Pure decision; NEVER interprets caller-supplied booleans as proof itself.

    The calling host adapter must verify Sigstore/manifest and an independently
    authenticated, cut- and digest-bound technical review BEFORE supplying True.
    Use alongside successor-baseline.resolve_baseline on physical host evidence;
    no JSON state alone grants authority.
    """
    require(source == SOURCE and type(source) is str and SHA.fullmatch(source),
            "FROZEN_SOURCE_MISMATCH")
    require(asset_digest == DIGEST, "FROZEN_DIGEST_MISMATCH")
    require(baseline == PREDECESSOR, "APPLIED_BASELINE_MISMATCH")
    require(release_authenticated is True, "SIGNED_RELEASE_NOT_AUTHENTICATED")
    require(isinstance(diff, dict) and diff.get("status") == "ahead"
            and diff.get("merge_base_commit", {}).get("sha") == baseline
            and type(diff.get("ahead_by")) is int and 0 < diff["ahead_by"] < 250
            and diff.get("behind_by") == 0, "DIFF_NOT_ANCESTRAL")
    files = diff.get("files")
    require(isinstance(files, list) and 0 < len(files) < 300,
            "DIFF_MISSING_OR_TRUNCATED")
    seen, review_paths = set(), set()
    for item in files:
        require(isinstance(item, dict), "DIFF_FILE_INVALID")
        path = item.get("filename")
        require(type(path) is str and path and len(path) <= 512
                and not path.startswith("/") and "\\" not in path
                and not any(seg in ("", ".", "..") for seg in path.split("/"))
                and path not in seen, "DIFF_PATH_UNTRUSTED")
        seen.add(path)
        require(item.get("status") in (
            "added", "modified", "removed", "changed", "copied", "renamed"),
            "DIFF_FILE_STATUS_INVALID")
        old = item.get("previous_filename")
        paths = [path]
        if item.get("status") == "renamed":
            require(type(old) is str and old and old not in seen,
                    "DIFF_RENAME_INVALID")
            paths.append(old)
        for name in paths:
            reason = reason_for(name)
            if reason:
                require(name in QA_ONLY_REVIEW_PATHS,
                        "RUNTIME_OR_UNREVIEWED_SENSITIVE_CHANGE")
                review_paths.add(name)
    require(review_paths == QA_ONLY_REVIEW_PATHS, "QA_ONLY_REVIEW_SCOPE_CHANGED")
    require(review_authenticated is True,
            "QA_ONLY_TECHNICAL_REVIEW_NOT_AUTHENTICATED")
    # This module makes no statements about Docker, EF, G6, or host authority.
    # The signed host builder must independently verify those controls.
    return {"status": "FROZEN_DIFF_REVIEW_ELIGIBLE_NO_BUILD_AUTHORITY",
            "source_sha": SOURCE, "release_asset_sha256": DIGEST,
            "baseline_sha": PREDECESSOR, "changed_files": len(files),
            "reviewed_exception_paths": sorted(review_paths),
            "scope": SCOPE, "may_build": False, "may_apply": False}
