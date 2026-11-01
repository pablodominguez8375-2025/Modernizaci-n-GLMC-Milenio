#!/usr/bin/env python3
"""QA525 launcher-only root0600 approval admission. No installer or CLI.

This reader is only a precondition for the future signed launcher installer.
Reading an operator-authored JSON file is NOT a signature, deploy approval,
nonce consumption, host reconciliation or a root-owned installation.
"""
import json
import os
from pathlib import Path
import re
import socket
import stat
import time

APPROVAL = Path("/etc/pmgm/qa525-launcher-replace.json")
SCOPE = "QA525_LAUNCHER_ONLY_NO_DOCKER_NO_IDENTITY_NO_MIGRATION"
ACTION = "replace-root-launcher-supervised"
ENVIRONMENT = "QA_GLMDCH_ONLY"
OPERATOR = "soporte@glmdch"
SOURCE_SHA = re.compile(r"[0-9a-f]{40}")
SHA256 = re.compile(r"[0-9a-f]{64}")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}")
FIELDS = frozenset((
    "source_sha", "release_asset_sha256", "old_launcher_sha256",
    "candidate_operator_sha256", "scope", "action", "environment",
    "operator", "approved", "issued_at", "expires_at", "nonce",
))
MAX_BYTES = 4096


class Blocked(Exception):
    """Stable non-sensitive validation error code."""


def require(ok, code):
    if not ok:
        raise Blocked(code)


def valid(value, pattern):
    return type(value) is str and pattern.fullmatch(value) is not None


def validate(approval, now):
    """Check the exact launcher-only act, never authorise filesystem writes."""
    require(type(approval) is dict and set(approval) == FIELDS,
            "LAUNCHER_APPROVAL_FIELDS_INVALID")
    require(type(now) is int, "LAUNCHER_APPROVAL_CLOCK_INVALID")
    require(valid(approval["source_sha"], SOURCE_SHA)
            and valid(approval["release_asset_sha256"], DIGEST)
            and valid(approval["old_launcher_sha256"], SHA256)
            and valid(approval["candidate_operator_sha256"], SHA256)
            and valid(approval["nonce"], SHA256),
            "LAUNCHER_APPROVAL_IDENTITY_INVALID")
    require(approval["scope"] == SCOPE and approval["action"] == ACTION
            and approval["environment"] == ENVIRONMENT
            and approval["operator"] == OPERATOR
            and approval["approved"] is True,
            "LAUNCHER_APPROVAL_SCOPE_INVALID")
    issued, expires = approval["issued_at"], approval["expires_at"]
    require(type(issued) is int and type(expires) is int
            and issued <= now < expires and 0 < expires - issued <= 900,
            "LAUNCHER_APPROVAL_EXPIRED_OR_INVALID")
    return {
        "status": "LOCAL_LAUNCHER_APPROVAL_SHAPE_MATCH_NO_INSTALL",
        "scope": SCOPE,
        "may_install": False,
        "may_apply": False,
        "signed_release_verified_by_reader": False,
        "nonce_consumed_by_reader": False,
    }


def trusted_chain(path):
    """Never follow links; require root ownership and safe ancestors."""
    require(isinstance(path, Path) and path.is_absolute(),
            "LAUNCHER_APPROVAL_PATH_INVALID")
    for n, item in enumerate((path, *path.parents)):
        try:
            info = item.lstat()
        except OSError:
            raise Blocked("LAUNCHER_APPROVAL_PATH_MISSING") from None
        require(not stat.S_ISLNK(info.st_mode) and info.st_uid == 0
                and not (info.st_mode & 0o022),
                "LAUNCHER_APPROVAL_PATH_UNTRUSTED")
        if n == 0:
            require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1
                    and stat.S_IMODE(info.st_mode) == 0o600
                    and 1 <= info.st_size <= MAX_BYTES,
                    "LAUNCHER_APPROVAL_FILE_INVALID")
        else:
            require(stat.S_ISDIR(info.st_mode),
                    "LAUNCHER_APPROVAL_PARENT_NOT_DIRECTORY")
    return path.lstat()


def no_duplicate_fields(pairs):
    result = {}
    for name, value in pairs:
        require(name not in result, "LAUNCHER_APPROVAL_DUPLICATE_FIELD")
        result[name] = value
    return result


def read_root_approval():
    """Read only /etc/pmgm/qa525-launcher-replace.json (no override/CLI).

    Caller must independently obtain current Sigstore-verified preflight, a
    five-lock exclusive critical section, host state and nonce/journal checks.
    This function performs none of those operations.
    """
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "LAUNCHER_APPROVAL_ONLY_ROOT_GLMDCH")
    before = trusted_chain(APPROVAL)
    fd = os.open(APPROVAL, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        current = os.fstat(fd)
        require(stat.S_ISREG(current.st_mode) and current.st_uid == 0
                and stat.S_IMODE(current.st_mode) == 0o600
                and current.st_nlink == 1
                and current.st_dev == before.st_dev
                and current.st_ino == before.st_ino
                and current.st_size == before.st_size
                and 1 <= current.st_size <= MAX_BYTES,
                "LAUNCHER_APPROVAL_CHANGED_DURING_OPEN")
        with os.fdopen(fd, "rb") as stream:
            fd = -1
            raw = stream.read(MAX_BYTES + 1)
    finally:
        if fd >= 0:
            os.close(fd)
    require(len(raw) == current.st_size and len(raw) <= MAX_BYTES,
            "LAUNCHER_APPROVAL_CHANGED_DURING_READ")
    try:
        approval = json.loads(raw.decode("utf-8"),
                              object_pairs_hook=no_duplicate_fields)
    except (UnicodeError, ValueError, TypeError):
        raise Blocked("LAUNCHER_APPROVAL_JSON_INVALID") from None
    validate(approval, int(time.time()))
    return approval


if __name__ == "__main__":
    raise SystemExit("LAUNCHER_APPROVAL_LIBRARY_ONLY_NO_INSTALL_ENTRYPOINT")
