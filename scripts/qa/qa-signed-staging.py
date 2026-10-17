#!/usr/bin/env python3
"""Stage one attested synthetic QA installable without executing its contents.

This deliberately does NOT start Docker, install services, trust GitHub API
metadata alone, or change /opt/centenario/app or /etc/pmgm/srv01.env.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import zipfile

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
WORKFLOW = REPO + "/.github/workflows/qa-silo-rebuild.yml"
SHA = re.compile(r"^[0-9a-f]{40}$")
DIGEST = re.compile(r"^sha256:([0-9a-f]{64})$")
MANIFEST_LINE = re.compile(r"^([0-9a-f]{64})  (\./[^\n]+)$")
STORAGE_MARK = "STORAGE=SIGNED_REBUILT_SILO_ARCHIVE_SYNTHETIC_QA"
MAX_UNCOMPRESSED = 2 * 1024**3
MAX_FILE = 1024**3


class Blocked(Exception):
    pass


def sha256_file(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def checked_names(archive):
    seen = set()
    total = 0
    result = []
    for item in archive.infolist():
        raw = item.filename
        path = PurePosixPath(raw)
        if (not raw or raw.startswith("/") or "\\" in raw or
                any(part in ("", ".", "..") for part in raw.rstrip("/").split("/")) or
                path.is_absolute() or str(path) != raw.rstrip("/") or
                len(raw) > 512 or "\x00" in raw):
            raise Blocked("UNSAFE_ARCHIVE_MEMBER")
        unix_mode = (item.external_attr >> 16)
        if stat.S_ISLNK(unix_mode):
            raise Blocked("ARCHIVE_SYMLINK_FORBIDDEN")
        if not (item.is_dir() or not stat.S_IFMT(unix_mode) or stat.S_ISREG(unix_mode)):
            raise Blocked("UNSAFE_ARCHIVE_FILE_TYPE")
        key = raw.rstrip("/")
        if key in seen:
            raise Blocked("DUPLICATE_ARCHIVE_MEMBER")
        seen.add(key)
        total += item.file_size
        if item.file_size > MAX_FILE or total > MAX_UNCOMPRESSED:
            raise Blocked("ARCHIVE_SIZE_LIMIT")
        if item.file_size and item.compress_size == 0:
            raise Blocked("INVALID_ARCHIVE_COMPRESSION")
        result.append(item)
    return result


def safe_extract(archive, where, *, allowed=None):
    infos = checked_names(archive)
    if allowed is not None:
        present = {info.filename for info in infos if not info.is_dir()}
        if present != allowed:
            raise Blocked("UNEXPECTED_OUTER_ARTIFACT_CONTENT")
    paths = []
    for info in infos:
        target = where.joinpath(*PurePosixPath(info.filename).parts)
        if info.is_dir():
            target.mkdir(mode=0o700, parents=True, exist_ok=True)
            continue
        target.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
        fd = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        try:
            with os.fdopen(fd, "wb") as destination, archive.open(info) as source:
                shutil.copyfileobj(source, destination, 1024 * 1024)
        except Exception:
            target.unlink(missing_ok=True)
            raise
        paths.append(target)
    return paths


def verify_manifest(package, source_sha):
    info_path = package / "BUILD-INFO.txt"
    manifest_path = package / "MANIFEST.sha256"
    if not info_path.is_file() or not manifest_path.is_file():
        raise Blocked("PACKAGE_BUILD_INFO_OR_MANIFEST_MISSING")
    lines = info_path.read_text(encoding="utf-8").splitlines()
    if lines.count("SOURCE_SHA=" + source_sha) != 1 or lines.count(STORAGE_MARK) != 1:
        raise Blocked("PACKAGE_SOURCE_OR_SILO_BINDING_MISMATCH")
    manifest = manifest_path.read_text(encoding="utf-8")
    if len(manifest) > 5_000_000:
        raise Blocked("OVERSIZE_MANIFEST")
    expected = {}
    for line in manifest.splitlines():
        matched = MANIFEST_LINE.fullmatch(line)
        if not matched:
            raise Blocked("NONCANONICAL_MANIFEST")
        digest, relative = matched.groups()
        path = relative[2:]
        if (path == "MANIFEST.sha256" or
                path in expected or
                any(segment in ("", ".", "..") for segment in path.split("/"))):
            raise Blocked("INVALID_MANIFEST_PATH")
        expected[path] = digest
    files = {p.relative_to(package).as_posix(): p for p in package.rglob("*") if p.is_file()}
    files.pop("MANIFEST.sha256", None)
    if not expected or set(expected) != set(files):
        raise Blocked("MANIFEST_FILE_SET_MISMATCH")
    for path, expected_hash in expected.items():
        if sha256_file(files[path]) != expected_hash:
            raise Blocked("MANIFEST_CHECKSUM_MISMATCH")
    required = ("storage-bundle/project-provenance.sigstore.json",
                "storage-bundle/rebuild-report.json",
                "storage-bundle/server-fixed.openvex.json",
                "storage-bundle/server-cve-triage.json",
                "storage-bundle/silo-qa-review-only.tar")
    if not all((package / name).is_file() for name in required):
        raise Blocked("SIGNED_SILO_EVIDENCE_MISSING")


def stage(artifact, expected_sha256, source_sha, output):
    if not SHA.fullmatch(source_sha):
        raise Blocked("INVALID_SOURCE_SHA")
    parsed = DIGEST.fullmatch(expected_sha256)
    if parsed is None:
        raise Blocked("INVALID_ARTIFACT_DIGEST")
    artifact = Path(artifact)
    if not artifact.is_file() or artifact.is_symlink():
        raise Blocked("ARTIFACT_NOT_A_REGULAR_FILE")
    if sha256_file(artifact) != parsed.group(1):
        raise Blocked("ARTIFACT_DIGEST_MISMATCH")
    output = Path(output)
    if output.exists() or output.is_symlink():
        raise Blocked("STAGING_TARGET_MUST_NOT_EXIST")
    if not output.parent.is_dir() or output.parent.is_symlink():
        raise Blocked("STAGING_PARENT_INVALID")
    archive_name = "Proyecto-Centenario-QA-srv01-" + source_sha[:12] + ".zip"
    bundle_name = "installable-provenance.sigstore.json"
    verification_name = archive_name + ".verification.json"
    os.umask(0o077)
    with tempfile.TemporaryDirectory(prefix=".pmgm-signed-stage-", dir=output.parent) as tmp:
        tmp = Path(tmp)
        envelope = tmp / "envelope"
        envelope.mkdir(mode=0o700)
        with zipfile.ZipFile(artifact) as outer:
            safe_extract(outer, envelope,
                         allowed={archive_name, bundle_name, verification_name})
        signed = envelope / archive_name
        bundle = envelope / bundle_name
        command = ["gh", "attestation", "verify", str(signed),
                   "--repo", REPO, "--bundle", str(bundle),
                   "--signer-workflow", WORKFLOW,
                   "--source-digest", source_sha, "--deny-self-hosted-runners",
                   "--format", "json"]
        try:
            subprocess.run(command, check=True, stdout=subprocess.DEVNULL,
                           stderr=subprocess.DEVNULL, timeout=120)
        except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired):
            raise Blocked("INSTALLABLE_ATTESTATION_FAILED") from None
        destination = tmp / "package"
        destination.mkdir(mode=0o700)
        with zipfile.ZipFile(signed) as inner:
            safe_extract(inner, destination)
        packages = list(destination.iterdir())
        if len(packages) != 1 or packages[0].name != archive_name[:-4] or not packages[0].is_dir():
            raise Blocked("UNEXPECTED_PACKAGE_ROOT")
        verify_manifest(packages[0], source_sha)
        # Rename a fully verified staging folder only; no installer is run.
        os.replace(destination / packages[0].name, output)
    return {"status": "SIGNED_CODE_STAGED_NOT_INSTALLED",
            "source_sha": source_sha,
            "scope": "NO_SILO_RUNTIME_OR_HOST_DEPLOYMENT_CERTIFICATION"}


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--artifact", required=True, type=Path)
    ap.add_argument("--artifact-sha256", required=True)
    ap.add_argument("--source-sha", required=True)
    ap.add_argument("--output", required=True, type=Path)
    args = ap.parse_args()
    try:
        print(json.dumps(stage(args.artifact, args.artifact_sha256, args.source_sha, args.output),
                         sort_keys=True))
        return 0
    except (Blocked, OSError, zipfile.BadZipFile, UnicodeError, ValueError) as e:
        code = str(e) if isinstance(e, Blocked) else "INVALID_ARCHIVE"
        print(json.dumps({"status": "BLOCKED_SIGNED_STAGING",
                          "scope": "NO_HOST_DEPLOYMENT",
                          "code": code}, sort_keys=True))
        return 1


if __name__ == "__main__":
    sys.exit(main())
