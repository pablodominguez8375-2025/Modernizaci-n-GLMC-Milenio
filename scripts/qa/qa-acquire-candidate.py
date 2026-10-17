#!/usr/bin/env python3
"""Prepare one fully verified dev QA candidate. Never install or start services.

Combines the already-reviewed read-only version selection, limited Actions
download and Sigstore ZIP staging. Output is private, not deployed.
"""
import argparse
import fcntl
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import stat
import sys
import tempfile

ROOT = Path(__file__).resolve().parent
SHA = re.compile(r"^[a-f0-9]{40}$")


class Blocked(Exception):
    pass


def module(name, filename):
    path = ROOT / filename
    spec = importlib.util.spec_from_file_location(name, path)
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded


def validate_root(path):
    path = Path(path)
    try:
        st = path.lstat()
        if (not path.is_absolute() or path.resolve(strict=True) != path or
                not stat.S_ISDIR(st.st_mode) or st.st_uid != os.geteuid() or
                stat.S_IMODE(st.st_mode) != 0o700):
            raise Blocked("STAGING_ROOT_OWNER_OR_MODE")
    except (OSError, RuntimeError):
        raise Blocked("STAGING_ROOT_UNAVAILABLE") from None
    return path


def validate_selection(selection):
    if (not isinstance(selection, dict) or
            selection.get("status") != "READY_FOR_SIGNED_STAGING_ONLY" or
            selection.get("scope") != "NO_HOST_ACCESS_NO_DEPLOYMENT_NO_UAT" or
            not isinstance(selection.get("source_sha"), str) or
            SHA.fullmatch(selection["source_sha"]) is None or
            type(selection.get("artifact_id")) is not int or
            selection["artifact_id"] <= 0 or
            not isinstance(selection.get("artifact_sha256"), str) or
            re.fullmatch(r"sha256:[a-f0-9]{64}", selection["artifact_sha256"]) is None):
        raise Blocked("INVALID_DEV_SELECTION")
    return (selection["source_sha"], selection["artifact_id"],
            selection["artifact_sha256"])


def acquire(stage_root, token_file, selector, downloader, stager, reader):
    stage_root = validate_root(stage_root)
    lock_path = stage_root / ".qa-candidate.lock"
    os.umask(0o077)
    fd = os.open(lock_path, os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW | os.O_CLOEXEC, 0o600)
    try:
        lock_stat = os.fstat(fd)
        if (not stat.S_ISREG(lock_stat.st_mode) or
                lock_stat.st_uid != os.geteuid() or
                stat.S_IMODE(lock_stat.st_mode) != 0o600 or
                lock_stat.st_nlink != 1):
            raise Blocked("CANDIDATE_LOCK_INVALID")
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise Blocked("CANDIDATE_ALREADY_RUNNING") from None
        first = selector()
        sha, art_id, digest = validate_selection(first)
        destination = stage_root / sha
        if destination.exists() or destination.is_symlink():
            raise Blocked("CANDIDATE_ALREADY_STAGED")
        token = reader(token_file)
        temp = Path(tempfile.mkdtemp(prefix=".pmgm-acquire-", dir=stage_root))
        try:
            zip_path = temp / "actions.zip"
            fetched = downloader(art_id, digest, token, zip_path)
            if (not isinstance(fetched, dict) or
                    fetched.get("status") != "DOWNLOADED_UNVERIFIED_NOT_INSTALLED" or
                    fetched.get("artifact_id") != art_id or
                    fetched.get("sha256") != digest):
                raise Blocked("CANDIDATE_DOWNLOAD_NOT_VERIFIED")
            candidate = temp / "signed-package"
            verified = stager(zip_path, digest, sha, candidate)
            if (not isinstance(verified, dict) or
                    verified.get("status") != "SIGNED_CODE_STAGED_NOT_INSTALLED" or
                    verified.get("source_sha") != sha or
                    not candidate.is_dir() or candidate.is_symlink()):
                raise Blocked("CANDIDATE_SIGNATURE_OR_MANIFEST_FAILED")
            # Critical TOCTOU guard: dev may have moved during download.
            second = selector()
            if validate_selection(second) != (sha, art_id, digest):
                raise Blocked("DEV_HEAD_OR_ARTIFACT_CHANGED_DURING_STAGING")
            if destination.exists() or destination.is_symlink():
                raise Blocked("CANDIDATE_STAGING_RACE")
            os.replace(candidate, destination)
            return {"status": "VERIFIED_QA_CANDIDATE_PREPARED_NOT_DEPLOYED",
                    "source_sha": sha, "artifact_id": art_id,
                    "scope": "NO_DOCKER_NO_INSTALLATION_NO_QA_ACCEPTANCE"}
        finally:
            shutil.rmtree(temp)
    finally:
        os.close(fd)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--staging-root", type=Path, required=True)
    p.add_argument("--token-file", type=Path, required=True)
    args = p.parse_args()
    try:
        readiness = module("qa_agent_readiness", "qa-agent-readiness.py")
        fetcher = module("qa_artifact_download", "qa-artifact-download.py")
        staging = module("qa_signed_staging", "qa-signed-staging.py")
        result = acquire(
            args.staging_root, args.token_file,
            lambda: readiness.evaluate(readiness.fetch_public),
            fetcher.download, staging.stage, fetcher.read_token)
        print(json.dumps(result, sort_keys=True))
        return 0
    except Exception as e:
        # No stack traces, token values or private paths in automation logs.
        allowed = {"STAGING_ROOT_OWNER_OR_MODE", "STAGING_ROOT_UNAVAILABLE",
                   "CANDIDATE_LOCK_INVALID", "CANDIDATE_ALREADY_RUNNING",
                   "INVALID_DEV_SELECTION", "CANDIDATE_ALREADY_STAGED",
                   "CANDIDATE_DOWNLOAD_NOT_VERIFIED",
                   "CANDIDATE_SIGNATURE_OR_MANIFEST_FAILED",
                   "DEV_HEAD_OR_ARTIFACT_CHANGED_DURING_STAGING",
                   "CANDIDATE_STAGING_RACE"}
        code = str(e) if isinstance(e, Blocked) and str(e) in allowed else "PREPARATION_BLOCKED"
        print(json.dumps({"status": "BLOCKED_QA_CANDIDATE_PREPARATION",
                          "code": code, "scope": "NO_HOST_DEPLOYMENT"}, sort_keys=True))
        return 1


if __name__ == "__main__":
    sys.exit(main())
