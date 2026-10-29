#!/usr/bin/env python3
"""QA525: root-local supervised cut match, NO approval to deploy.

Prevents a human's approval for an old SHA, digest, or action from being
treated as approval for another cut. This is only one necessary check:
a local JSON file does NOT prove an independent review or permit apply.
"""
import json
import os
from pathlib import Path
import re
import socket
import stat
import sys
import time

APPROVAL = Path("/etc/pmgm/qa525-supervised-cut.json")
SCOPE = "LOCAL_SUPERVISION_MATCH_ONLY_NO_APPLY"
SHA = re.compile(r"^[a-f0-9]{40}$")
DIGEST = re.compile(r"^sha256:[a-f0-9]{64}$")
NONCE = re.compile(r"^[a-f0-9]{64}$")
ACTION = "first-qa-api-web-supervised"
ENVIRONMENT = "QA_SYNTHETIC"
FIELDS = frozenset(("target_sha", "release_asset_sha256", "scope",
                    "environment", "action", "nonce", "issued_at",
                    "expires_at", "operator_reviewed"))


class Blocked(Exception):
    pass


def require(cond, code):
    if not cond:
        raise Blocked(code)


def validate(approval, source, prepared, now):
    """Pure fail-closed matching of three records; never authorizes apply."""
    require(type(approval) is dict and set(approval) == FIELDS,
            "LOCAL_APPROVAL_FIELDS_INVALID")
    require(type(source) is dict and type(prepared) is dict,
            "SOURCE_OR_PREPARED_INVALID")
    sha, digest = source.get("source_sha"), source.get("release_asset_sha256")
    require(type(sha) is str and SHA.fullmatch(sha)
            and type(digest) is str and DIGEST.fullmatch(digest)
            and source.get("status") == "READY_FOR_SIGNATURE_VERIFICATION_ONLY"
            and source.get("release_tag") == "qa-signed-" + sha,
            "SOURCE_NOT_SIGNED_METADATA")
    require(prepared.get("status") ==
            "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT"
            and prepared.get("source_sha") == sha
            and prepared.get("source_asset_sha256") == digest
            and prepared.get("images_built") is True
            and prepared.get("services_unchanged") is True
            and prepared.get("may_apply") is False,
            "PREPARED_SHA_OR_DIGEST_MISMATCH")
    require(approval.get("target_sha") == sha
            and approval.get("release_asset_sha256") == digest
            and approval.get("scope") == SCOPE
            and approval.get("environment") == ENVIRONMENT
            and approval.get("action") == ACTION
            and approval.get("operator_reviewed") is True
            and type(approval.get("nonce")) is str
            and NONCE.fullmatch(approval["nonce"]),
            "SUPERVISION_SCOPE_OR_CUT_CHANGED")
    issued, expires = approval.get("issued_at"), approval.get("expires_at")
    require(type(now) is int and type(issued) is int and type(expires) is int
            and 0 <= now - issued <= 1800
            and issued < expires <= issued + 1800
            and now < expires,
            "SUPERVISION_EXPIRED_OR_INVALID")
    return {
        "status": "LOCAL_SUPERVISED_CUT_MATCH_NO_DEPLOYMENT_AUTHORITY",
        "scope": SCOPE,
        "source_sha": sha,
        "release_asset_sha256": digest,
        "operator_reviewed_claimed_locally": True,
        "may_apply": False,
        "signature_verified_by_this_tool": False,
        "host_backup_verified_by_this_tool": False,
        "single_use_consumed_by_this_tool": False,
        "sensitive_change_exception_granted": False,
    }


def read_root_approval():
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "HOST_NOT_AUTHORIZED")
    for part in (APPROVAL, *APPROVAL.parents):
        try:
            st = part.lstat()
        except OSError:
            raise Blocked("LOCAL_APPROVAL_MISSING") from None
        require(not stat.S_ISLNK(st.st_mode) and st.st_uid == 0
                and not stat.S_IMODE(st.st_mode) & 0o022,
                "APPROVAL_PATH_UNTRUSTED")
    fd = os.open(APPROVAL, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, "rb") as f:
        st = os.fstat(f.fileno())
        require(stat.S_ISREG(st.st_mode) and st.st_uid == 0
                and stat.S_IMODE(st.st_mode) == 0o600 and st.st_nlink == 1
                and 1 <= st.st_size <= 4096,
                "APPROVAL_FILE_INVALID")
        content = f.read(4097)
        require(len(content) == st.st_size, "APPROVAL_FILE_CHANGED")
    try:
        return json.loads(content)
    except (ValueError, TypeError):
        raise Blocked("APPROVAL_JSON_INVALID") from None


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    try:
        require(args == ["--check"], "ONLY_READONLY_CHECK_SUPPORTED")
        # This command intentionally has no way to load source/prepared JSON
        # from user-provided paths or accept --apply. The future operator MUST
        # obtain live source+prepared independently under the exclusive lock.
        read_root_approval()
        print(json.dumps({"status": "LOCAL_APPROVAL_FILE_PRESENT_NOT_APPLY_AUTHORITY",
                          "scope": SCOPE,
                          "may_apply": False}, sort_keys=True))
        return 0
    except Blocked as exc:
        print(json.dumps({"status": "BLOCKED_QA525_SUPERVISION",
                          "scope": SCOPE, "code": str(exc),
                          "may_apply": False}, sort_keys=True))
        return 2
    except Exception:
        print(json.dumps({"status": "BLOCKED_QA525_SUPERVISION",
                          "scope": SCOPE, "code": "LOCAL_SUPERVISION_UNAVAILABLE",
                          "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
