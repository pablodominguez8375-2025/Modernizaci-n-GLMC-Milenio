#!/usr/bin/env python3
"""Verify a signed replacement launcher candidate, WITHOUT installing it.

Issue #822 phase 2: root/glmdch only, inspect existing launcher, reuse the
project's independent exact-dev + GitHub Sigstore release verification, check
the candidate's signed manifest, then return an ephemeral non-authorizing plan.
Never modifies /usr/local/sbin, Compose, API, WEB, identity or G6.
"""
import argparse
import ast
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import socket
import stat
import sys
import tempfile

HERE = Path(__file__).resolve().parent
QA_DIR = HERE.parent
STATE = Path("/opt/centenario/qa525-signed-prepare")
SOURCE_NAMES = (
    HERE / "qa525-launcher-signed-preflight.py",
    HERE / "qa525-launcher-upgrade-inspect.py",
    QA_DIR / "qa525-stage-current-release.py",
    QA_DIR / "qa525-current-release-gate.py",
    QA_DIR / "qa-agent-readiness.py",
    QA_DIR / "qa-public-release-download.py",
    QA_DIR / "qa-signed-staging.py",
)
CANDIDATE = Path("scripts/qa/operators/qa525-update.py")
REQUIRED_OPERATOR_CONTRACT = (
    "--archive-expired-prepared",
    "--inspect-pending-archive",
    "RECOVERY_ARCHIVE_PARTIAL_REQUIRES_RECONCILIATION",
    "--status",
    "--apply-prepared",
)
SCOPE = "SIGNED_EXACT_DEV_PREFLIGHT_ONLY_NO_LAUNCHER_REPLACEMENT_NO_DEPLOY"


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def trusted_file(path):
    """Reject writable ancestors/links; verify immutable root-owned source."""
    require(path.is_absolute(), "PREFLIGHT_SOURCE_NOT_ABSOLUTE")
    for i, item in enumerate((path, *path.parents)):
        try:
            st = item.lstat()
        except OSError:
            raise Blocked("PREFLIGHT_TRUSTED_SOURCE_MISSING") from None
        require(st.st_uid == 0 and not stat.S_ISLNK(st.st_mode)
                and not (st.st_mode & 0o022),
                "PREFLIGHT_TRUSTED_SOURCE_UNSAFE")
        require((stat.S_ISREG(st.st_mode) and st.st_nlink == 1) if i == 0
                else stat.S_ISDIR(st.st_mode),
                "PREFLIGHT_SOURCE_TYPE_UNSAFE")
    return True


def load(name, source):
    spec = importlib.util.spec_from_file_location(name, source)
    require(spec is not None and spec.loader is not None,
            "PREFLIGHT_IMPORT_SPEC_INVALID")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def stable_hash(path, maximum=2_000_000):
    """Read candidate without following symlinks and reject archive hardlinks."""
    try:
        before = path.lstat()
    except OSError:
        raise Blocked("PREFLIGHT_CANDIDATE_MISSING") from None
    require(stat.S_ISREG(before.st_mode) and before.st_nlink == 1
            and before.st_uid == 0 and not (before.st_mode & 0o022)
            and 0 < before.st_size <= maximum,
            "PREFLIGHT_CANDIDATE_UNTRUSTED")
    handle = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        st = os.fstat(handle)
        require(st.st_dev == before.st_dev and st.st_ino == before.st_ino
                and st.st_size == before.st_size and st.st_nlink == 1
                and st.st_uid == 0 and stat.S_ISREG(st.st_mode),
                "PREFLIGHT_CANDIDATE_CHANGED")
        with os.fdopen(handle, "rb") as stream:
            handle = -1
            data = stream.read(maximum + 1)
    finally:
        if handle >= 0:
            os.close(handle)
    require(len(data) == before.st_size and len(data) <= maximum,
            "PREFLIGHT_CANDIDATE_SIZE_CHANGED")
    return data



def validate_operator_ast(data):
    """Syntax-only gate: comments/strings cannot impersonate recovery logic.

    This augments the signed manifest and hermetic tests; it is deliberately
    NOT a security proof of the operator's behavior, nor execution approval.
    """
    try:
        tree = ast.parse(data.decode("utf-8"), filename="signed-qa525-update.py")
    except (UnicodeDecodeError, SyntaxError, ValueError):
        raise Blocked("PREFLIGHT_OPERATOR_PYTHON_INVALID") from None
    functions = [node for node in tree.body if isinstance(node,
                 (ast.FunctionDef, ast.AsyncFunctionDef))]
    names = [node.name for node in functions]
    needed = ("main", "archive_expired_prepared", "inspect_pending_archive",
              "require_reconciled_archive", "install_review_packet")
    require(all(names.count(name) == 1 for name in needed),
            "PREFLIGHT_REQUIRED_RECOVERY_CONTRACT_MISSING")
    # Arguments must be registered on the exact mutual-exclusive CLI parser.
    main = next(node for node in functions if node.name == "main")
    flags = []
    for node in ast.walk(main):
        if (isinstance(node, ast.Call)
                and isinstance(node.func, ast.Attribute)
                and node.func.attr == "add_argument"
                and isinstance(node.func.value, ast.Name)
                and node.func.value.id == "choice"
                and node.args and isinstance(node.args[0], ast.Constant)
                and type(node.args[0].value) is str):
            flags.append(node.args[0].value)
    for flag in ("--archive-expired-prepared", "--inspect-pending-archive",
                 "--status", "--apply-prepared", "--review-and-run"):
        require(flags.count(flag) == 1,
                "PREFLIGHT_REQUIRED_RECOVERY_CONTRACT_MISSING")
    by_name = {node.name: node for node in functions}
    for name in ("install_review_packet", "archive_expired_prepared", "main"):
        guard = any(isinstance(node, ast.Call)
                    and isinstance(node.func, ast.Name)
                    and node.func.id == "require_reconciled_archive"
                    for node in ast.walk(by_name[name]))
        require(guard, "PREFLIGHT_ARCHIVE_RECONCILIATION_GUARD_MISSING")
    fail_codes = {node.value for node in ast.walk(tree)
                  if isinstance(node, ast.Constant) and type(node.value) is str}
    require("RECOVERY_ARCHIVE_PARTIAL_REQUIRES_RECONCILIATION" in fail_codes
            and "RECOVERY_ARCHIVE_COMMIT_SYNC_UNCERTAIN" in fail_codes,
            "PREFLIGHT_FAIL_CLOSED_RECOVERY_CODES_MISSING")
    return True


def private_stage_parent(path):
    require(path.is_absolute(), "PREFLIGHT_STAGE_PARENT_NOT_ABSOLUTE")
    for item in (path, *path.parents):
        st = item.lstat()
        require(st.st_uid == 0 and stat.S_ISDIR(st.st_mode)
                and not stat.S_ISLNK(st.st_mode) and not (st.st_mode & 0o022),
                "PREFLIGHT_STAGE_PARENT_UNSAFE")
    require(stat.S_IMODE(path.lstat().st_mode) == 0o700,
            "PREFLIGHT_STAGE_NOT_PRIVATE")
    return True

def signed_candidate_preflight(inspector, signed, *, stage_parent=STATE):
    """Only the built-in signed verifier may choose the candidate.

    Injectable dependencies exist solely for hermetic tests. The real CLI
    always creates both dependencies from trusted root-owned source files.
    """
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "PREFLIGHT_ONLY_GLMDCH_ROOT")
    for source in SOURCE_NAMES:
        trusted_file(source)
    private_stage_parent(stage_parent)
    old = inspector.inspect()
    require(old.get("status") == "LAUNCHER_OBSERVED_UPGRADE_NOT_AUTHORIZED"
            and old.get("old_launcher_template_verified") is True
            and old.get("may_apply") is False and old.get("may_install") is False,
            "PREFLIGHT_OLD_LAUNCHER_UNTRUSTED")
    old_identity = (
        old.get("current_launcher_sha256"), old.get("current_operator_sha256"),
        old.get("current_operator"), old.get("current_launcher")
    )
    require(all(type(v) is str for v in old_identity)
            and all(re.fullmatch(r"[a-f0-9]{64}", v) for v in old_identity[:2]),
            "PREFLIGHT_OLD_IDENTITY_INVALID")
    with tempfile.TemporaryDirectory(prefix=".qa525-launcher-preflight-",
                                     dir=stage_parent) as temp:
        package = Path(temp) / "signed-package"
        # This is the real chained verifier: dev gate before and after,
        # immutable release digest, GitHub Sigstore provenance, manifest.
        result = signed.verify_current(package)
        sha = result.get("source_sha")
        digest = result.get("release_asset_sha256")
        require(result.get("status") == "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED"
                and result.get("scope") == signed.SCOPE
                and type(sha) is str and re.fullmatch(r"[a-f0-9]{40}", sha)
                and type(digest) is str
                and re.fullmatch(r"sha256:[a-f0-9]{64}", digest),
                "PREFLIGHT_SIGNED_STAGE_NOT_VERIFIED")
        # Defense in depth: verify whole manifest and required operator contract
        # from the signed staged bytes, NOT from mutable GitHub raw content.
        signed.staging.verify_manifest(package, sha)
        candidate = package / CANDIDATE
        data = stable_hash(candidate)
        observed = hashlib.sha256(data).hexdigest()
        manifest_raw = stable_hash(package / "MANIFEST.sha256", maximum=5_000_000)
        try:
            manifest_lines = manifest_raw.decode("utf-8").splitlines()
        except UnicodeDecodeError:
            raise Blocked("PREFLIGHT_SIGNED_MANIFEST_ENCODING_INVALID") from None
        exact_member = "./" + CANDIDATE.as_posix()
        digest_lines = [line for line in manifest_lines
                        if line.endswith("  " + exact_member)]
        require(len(digest_lines) == 1
                and digest_lines[0] == observed + "  " + exact_member,
                "PREFLIGHT_OPERATOR_SIGNED_MANIFEST_MISMATCH")
        # Ensure both the manifest and candidate remained valid on re-read.
        signed.staging.verify_manifest(package, sha)
        validate_operator_ast(data)
        # Re-read launcher to reject concurrent replacement during download.
        current = inspector.inspect()
        require(all(current.get(k) == old[k] for k in
                    ("current_launcher_sha256", "current_operator_sha256",
                     "current_operator", "current_launcher"))
                and current.get("status") == old["status"],
                "PREFLIGHT_OLD_LAUNCHER_CHANGED")
        # All staged paths are removed on returning. This plan cannot be used
        # as a bearer capability for a later physical install.
        return {
            "status": "SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL",
            "scope": SCOPE,
            "source_sha": sha,
            "release_asset_sha256": digest,
            "candidate_operator_sha256": observed,
            "old_launcher_sha256": old_identity[0],
            "old_operator_sha256": old_identity[1],
            "sigstore_verified_in_this_operation": True,
            "signed_manifest_verified": True,
            "old_launcher_checked_twice": True,
            "replacement_applied": False,
            "may_install": False,
            "may_apply": False,
        }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--preflight", action="store_true", required=True)
    args = parser.parse_args(argv)
    try:
        require(args.preflight, "PREFLIGHT_EXPLICIT_COMMAND_REQUIRED")
        require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
                "PREFLIGHT_ONLY_GLMDCH_ROOT")
        for source in SOURCE_NAMES:
            trusted_file(source)
        inspector = load("qa525_launcher_signed_old_inspector", SOURCE_NAMES[1])
        signed = load("qa525_launcher_signed_release_verifier", SOURCE_NAMES[2])
        report = signed_candidate_preflight(inspector, signed)
        print(json.dumps(report, sort_keys=True))
        return 0
    except (Blocked, OSError, ValueError, TypeError, KeyError) as exc:
        code = str(exc) if isinstance(exc, Blocked) else "PREFLIGHT_EVIDENCE_UNAVAILABLE"
        print(json.dumps({"status": "BLOCKED_QA525_SIGNED_LAUNCHER_PREFLIGHT",
                          "scope": SCOPE, "code": code,
                          "may_install": False, "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
