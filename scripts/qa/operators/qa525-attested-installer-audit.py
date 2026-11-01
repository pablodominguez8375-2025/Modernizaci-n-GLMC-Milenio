#!/usr/bin/env python3
"""UNPRIVILEGED audit of an independently GitHub-attested QA525 installer.

Reads a local signed release envelope; validates its SHA, GitHub signer and
source, then checks EVERY nested member against the signed SHA-256 manifest.
Never imports signed code, writes /etc or /opt, runs sudo or creates an
admission packet. An audit PASS is NOT approval or a trusted root bootstrap.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import tempfile
import zipfile

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
WORKFLOW = REPO + "/.github/workflows/qa-silo-rebuild.yml"
HEX40 = re.compile(r"[a-f0-9]{40}\Z")
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
MANIFEST_LINE = re.compile(r"([a-f0-9]{64})  \./([^\n]+)\Z")
MAX_ENVELOPE_BYTES = 512 * 1024 * 1024
MAX_MEMBER_BYTES = 1024 * 1024 * 1024
MAX_TOTAL_BYTES = 2 * 1024 * 1024 * 1024
MAX_MANIFEST_BYTES = 5_000_000
BOOTSTRAP_SCRIPT = "scripts/qa/operators/qa525-signed-root-bootstrap-prep.py"
SOURCES = (
    "scripts/qa/operators/qa525-launcher-supervised-root-install.py",
    "scripts/qa/operators/qa525-launcher-signed-preflight.py",
    "scripts/qa/operators/qa525-launcher-upgrade-inspect.py",
    "scripts/qa/operators/qa525-launcher-root-approval.py",
    "scripts/qa/operators/qa525-launcher-replacement-contract.py",
    "scripts/qa/operators/qa525-launcher-pinned-template.py",
    "scripts/qa/operators/qa525-launcher-retained-source-inspect.py",
    "scripts/qa/qa525-stage-current-release.py",
    "scripts/qa/qa525-current-release-gate.py",
    "scripts/qa/qa-agent-readiness.py",
    "scripts/qa/qa-public-release-download.py",
    "scripts/qa/qa-signed-staging.py",
)


class Blocked(Exception):
    """Non-secret stable failure code."""


def require(ok, why):
    if not ok:
        raise Blocked(why)


def sha256_stream(reader, *, maximum):
    digest = hashlib.sha256()
    size = 0
    while True:
        block = reader.read(1024 * 1024)
        if not block:
            break
        size += len(block)
        require(size <= maximum, "EXCESSIVE_MEMBER_SIZE")
        digest.update(block)
    return digest.hexdigest(), size


def members(archive, *, limit=MAX_TOTAL_BYTES):
    """Validate all paths and unix file types before any archive read."""
    seen = set()
    result = {}
    size = 0
    for info in archive.infolist():
        name = info.filename
        is_directory = info.is_dir()
        body = name[:-1] if is_directory else name
        parts = body.split("/")
        require(bool(body) and len(name) <= 512 and name.isascii()
                and not any(ord(c) < 32 or ord(c) == 127 for c in name)
                and not name.startswith("/") and "\\" not in name
                and all(p not in ("", ".", "..") for p in parts)
                and name not in seen, "UNSAFE_OR_DUPLICATE_ZIP_PATH")
        seen.add(name)
        unix_type = stat.S_IFMT(info.external_attr >> 16)
        require(unix_type in (0, stat.S_IFDIR if is_directory else stat.S_IFREG),
                "UNSAFE_ZIP_MEMBER_TYPE")
        require(0 <= info.file_size <= MAX_MEMBER_BYTES and
                (info.compress_size > 0 or info.file_size == 0),
                "UNSAFE_ZIP_MEMBER_SIZE")
        size += info.file_size
        require(size <= limit, "EXCESSIVE_ARCHIVE_SIZE")
        if not is_directory:
            require(body not in result, "DUPLICATE_ZIP_FILE")
            result[body] = info
    return result


def copy_exact(zf, info, to, *, limit):
    with zf.open(info) as src, to.open("xb") as dst:
        digest = hashlib.sha256()
        total = 0
        while True:
            block = src.read(1024 * 1024)
            if not block:
                break
            total += len(block)
            require(total <= limit, "EXCESSIVE_ENVELOPE_MEMBER")
            dst.write(block)
            digest.update(block)
    require(total == info.file_size, "CHANGED_ZIP_MEMBER_SIZE")
    return digest.hexdigest()


def audit(path, sha, digest, *, runner=subprocess.run):
    # Do not allow an accidentally sudo-invoked tool to parse untrusted ZIPs.
    require(os.geteuid() != 0, "ROOT_FORBIDDEN_FOR_AUDIT")
    require(HEX40.fullmatch(sha) is not None and HEX64.fullmatch(digest) is not None,
            "INVALID_EXPECTED_SOURCE_OR_DIGEST")
    path = Path(path)
    require(path.is_file() and not path.is_symlink() and
            0 < path.stat().st_size <= MAX_ENVELOPE_BYTES,
            "UNTRUSTED_ENVELOPE_PATH")
    with path.open("rb") as stream:
        observed, total = sha256_stream(stream, maximum=MAX_ENVELOPE_BYTES)
    require(total == path.stat().st_size and observed == digest,
            "ENVELOPE_SHA256_MISMATCH")
    inner_name = f"Proyecto-Centenario-QA-srv01-{sha[:12]}.zip"
    bundle_name = "installable-provenance.sigstore.json"
    verification_name = inner_name + ".verification.json"
    with tempfile.TemporaryDirectory(prefix="qa525-attested-audit-") as temp:
        tmp = Path(temp)
        with zipfile.ZipFile(path) as envelope:
            outer = members(envelope, limit=MAX_ENVELOPE_BYTES)
            require(set(outer) == {inner_name, bundle_name, verification_name},
                    "UNEXPECTED_ENVELOPE_MEMBERS")
            signed = tmp / inner_name
            bundle = tmp / bundle_name
            copy_exact(envelope, outer[inner_name], signed,
                       limit=MAX_ENVELOPE_BYTES)
            copy_exact(envelope, outer[bundle_name], bundle,
                       limit=MAX_MANIFEST_BYTES)
            # The third JSON is metadata only: not a source of signer trust.
            require(outer[verification_name].file_size <= MAX_MANIFEST_BYTES,
                    "OVERSIZE_ENVELOPE_METADATA")
        command = ["gh", "attestation", "verify", str(signed),
                   "--repo", REPO, "--bundle", str(bundle),
                   "--signer-workflow", WORKFLOW,
                   "--source-digest", sha, "--deny-self-hosted-runners",
                   "--format", "json"]
        try:
            runner(command, check=True, stdout=subprocess.DEVNULL,
                   stderr=subprocess.DEVNULL, timeout=120)
        except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired):
            raise Blocked("GITHUB_SIGSTORE_ATTESTATION_FAILED") from None
        package_root = inner_name[:-4]
        marker = package_root + "/"
        with zipfile.ZipFile(signed) as inner:
            all_files = members(inner)
            require(all(k.startswith(marker) for k in all_files),
                    "SIGNED_ARCHIVE_PACKAGE_ROOT_INVALID")
            paths = {key[len(marker):]: info for key, info in all_files.items()}
            require("MANIFEST.sha256" in paths and "BUILD-INFO.txt" in paths,
                    "SIGNED_MANIFEST_OR_BUILD_INFO_ABSENT")
            with inner.open(paths["BUILD-INFO.txt"]) as build:
                raw_build = build.read(MAX_MANIFEST_BYTES + 1)
            require(len(raw_build) <= MAX_MANIFEST_BYTES and
                    raw_build.splitlines().count(("SOURCE_SHA=" + sha).encode()) == 1 and
                    raw_build.splitlines().count(
                        b"STORAGE=SIGNED_REBUILT_SILO_ARCHIVE_SYNTHETIC_QA") == 1,
                    "SIGNED_SOURCE_BUILD_INFO_MISMATCH")
            with inner.open(paths["MANIFEST.sha256"]) as stream:
                manifest = stream.read(MAX_MANIFEST_BYTES + 1)
            require(len(manifest) <= MAX_MANIFEST_BYTES,
                    "OVERSIZE_SIGNED_MANIFEST")
            try:
                lines = manifest.decode("utf-8").splitlines()
            except UnicodeError:
                raise Blocked("SIGNED_MANIFEST_INVALID_ENCODING") from None
            expected = {}
            for line in lines:
                hit = MANIFEST_LINE.fullmatch(line)
                require(hit is not None, "NONCANONICAL_SIGNED_MANIFEST")
                file_path = hit.group(2)
                require(file_path != "MANIFEST.sha256" and
                        all(segment not in ("", ".", "..")
                            for segment in file_path.split("/")) and
                        file_path not in expected,
                        "INVALID_SIGNED_MANIFEST_MEMBER")
                expected[file_path] = hit.group(1)
            require(bool(expected) and
                    set(expected) == (set(paths) - {"MANIFEST.sha256"}),
                    "SIGNED_MANIFEST_FILE_SET_MISMATCH")
            require(set(SOURCES).issubset(expected) and
                    BOOTSTRAP_SCRIPT in expected,
                    "SIGNED_INSTALLER_OR_HELPERS_ABSENT")
            for key, value in expected.items():
                with inner.open(paths[key]) as stream:
                    actual, size = sha256_stream(stream,
                                                 maximum=MAX_MEMBER_BYTES)
                require(size == paths[key].file_size and actual == value,
                        "SIGNED_MEMBER_SHA256_MISMATCH")
            return {
                "status": "SIGNED_INSTALLER_AUDITED_UNPRIVILEGED_NO_INSTALL",
                "source_sha": sha,
                "release_asset_sha256": "sha256:" + digest,
                "manifest_sha256": hashlib.sha256(manifest).hexdigest(),
                "privileged_sources_sha256": {k: expected[k] for k in SOURCES},
                "bootstrap_sha256": expected[BOOTSTRAP_SCRIPT],
                "total_signed_members": len(expected),
                "bootstrap_admission_created": False,
                "may_install": False,
                "may_apply": False,
            }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--release-zip", type=Path, required=True)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--expected-sha256", required=True)
    args = parser.parse_args(argv)
    try:
        print(json.dumps(audit(args.release_zip, args.source_sha,
                               args.expected_sha256), sort_keys=True))
        return 0
    except (Blocked, OSError, ValueError, zipfile.BadZipFile,
            UnicodeError, RuntimeError):
        print(json.dumps({"status": "BLOCKED_SIGNED_INSTALLER_AUDIT",
                          "may_install": False, "may_apply": False},
                         sort_keys=True))
        return 2


if __name__ == "__main__":
    sys.exit(main())
