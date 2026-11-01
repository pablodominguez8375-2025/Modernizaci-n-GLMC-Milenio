#!/usr/bin/env python3
"""QA525 independent root bootstrap: authenticate, stage, issue NON-authorizing receipt.

TRUST BOUNDARY: this file is itself privileged Python. An authorized operator
MUST independently audit/authenticate it and check its pinned SHA-256 from a
trusted root command BEFORE the first Python execution. Passing its own
--trusted-bootstrap-sha256 argument cannot establish that trust on its own.

This command NEVER launches the installer, swaps a launcher, deploys a service,
reads secrets or grants installation approval. It only prepares root-owned,
Sigstore-authenticated source bytes for separately supervised use.
"""
import argparse
from contextlib import contextmanager
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import stat
import subprocess
import sys
import tempfile
import time
import zipfile

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
SIGNER = REPO + "/.github/workflows/qa-silo-rebuild.yml"
STORE = Path("/opt/centenario/qa525-root-source")
LOCK = STORE / "bootstrap.lock"
GH_BINARY = Path("/usr/bin/gh")  # checked exact system binary, never PATH lookup
ADMISSION = Path("/etc/pmgm/qa525-bootstrap-admission.json")
SHA40 = re.compile(r"[a-f0-9]{40}\Z")
SHA64 = re.compile(r"[a-f0-9]{64}\Z")
MANIFEST_LINE = re.compile(r"([a-f0-9]{64})  \./([^\n]+)\Z")
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
MAX_ZIP = 512 * 1024 * 1024
MAX_FILE = 1024 * 1024 * 1024
MAX_TOTAL = 2 * 1024 * 1024 * 1024


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def digest_file(path, *, maximum=MAX_ZIP):
    size = 0
    h = hashlib.sha256()
    with path.open("rb") as f:
        while True:
            data = f.read(1024 * 1024)
            if not data:
                break
            size += len(data)
            require(size <= maximum, "SOURCE_TOO_LARGE")
            h.update(data)
    return h.hexdigest()


def root_owned(path, *, directory=False, mode=None):
    path = Path(path)
    require(path.is_absolute(), "UNTRUSTED_BOOTSTRAP_PATH")
    for n, element in enumerate((path, *path.parents)):
        try:
            s = element.lstat()
        except OSError:
            raise Blocked("BOOTSTRAP_ROOT_PATH_MISSING") from None
        require(s.st_uid == 0 and not stat.S_ISLNK(s.st_mode)
                and not s.st_mode & 0o022,
                "BOOTSTRAP_ROOT_PATH_UNTRUSTED")
        require((stat.S_ISDIR(s.st_mode) if directory else
                 (stat.S_ISREG(s.st_mode) and s.st_nlink == 1)) if n == 0
                else stat.S_ISDIR(s.st_mode),
                "BOOTSTRAP_ROOT_PATH_TYPE_INVALID")
        if n == 0 and mode is not None:
            require(stat.S_IMODE(s.st_mode) == mode,
                    "BOOTSTRAP_ROOT_PATH_MODE_INVALID")


def write_all(fd, data):
    view = memoryview(data)
    while view:
        amount = os.write(fd, view)
        require(type(amount) is int and 0 < amount <= len(view),
                "BOOTSTRAP_SHORT_WRITE")
        view = view[amount:]


@contextmanager
def locked_bootstrap():
    """Bootstrap-only root lock: never create it, never bypass contention."""
    root_owned(STORE, directory=True, mode=0o700)
    root_owned(LOCK, mode=0o600)
    before = LOCK.lstat()
    fd = os.open(LOCK, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        current = os.fstat(fd)
        require(current.st_dev == before.st_dev and current.st_ino == before.st_ino
                and current.st_uid == 0 and current.st_nlink == 1
                and stat.S_ISREG(current.st_mode)
                and stat.S_IMODE(current.st_mode) == 0o600,
                "BOOTSTRAP_LOCK_CHANGED")
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise Blocked("BOOTSTRAP_ALREADY_RUNNING") from None
        require(LOCK.lstat().st_ino == current.st_ino
                and LOCK.lstat().st_dev == current.st_dev,
                "BOOTSTRAP_LOCK_PATH_CHANGED")
        yield
    finally:
        os.close(fd)


def zip_members(zf, *, max_total=MAX_TOTAL):
    files = {}
    names = set()
    total = 0
    for item in zf.infolist():
        name = item.filename
        simple = name[:-1] if item.is_dir() else name
        require(bool(simple) and len(name) <= 512 and name.isascii()
                and not any(ord(c) < 32 or ord(c) == 127 for c in name)
                and not name.startswith("/") and "\\" not in name
                and all(part not in ("", ".", "..") for part in simple.split("/"))
                and name not in names,
                "BOOTSTRAP_UNSAFE_ZIP_PATH")
        names.add(name)
        kind = stat.S_IFMT(item.external_attr >> 16)
        require(kind in (0, stat.S_IFDIR if item.is_dir() else stat.S_IFREG),
                "BOOTSTRAP_UNSAFE_ZIP_KIND")
        require(item.file_size <= MAX_FILE and item.file_size >= 0 and
                (item.file_size == 0 or item.compress_size > 0),
                "BOOTSTRAP_UNSAFE_ZIP_SIZE")
        total += item.file_size
        require(total <= max_total, "BOOTSTRAP_UNSAFE_ZIP_EXPANSION")
        if not item.is_dir():
            require(simple not in files, "BOOTSTRAP_DUPLICATE_ZIP_MEMBER")
            files[simple] = item
    return files


def read_member(zf, info, *, maximum):
    require(0 <= info.file_size <= maximum, "BOOTSTRAP_MEMBER_OVERSIZE")
    with zf.open(info) as stream:
        content = stream.read(maximum + 1)
    require(len(content) == info.file_size and len(content) <= maximum,
            "BOOTSTRAP_MEMBER_SIZE_CHANGED")
    return content


def verify_zip_and_prepare(envelope_path, source_sha, envelope_digest,
                           target, *, verify=subprocess.run):
    """Populate a *temporary* root-only target, never execute any member."""
    root_owned(envelope_path, mode=0o600)
    require(0 < envelope_path.stat().st_size <= MAX_ZIP and
            digest_file(envelope_path) == envelope_digest,
            "BOOTSTRAP_ENVELOPE_DIGEST_MISMATCH")
    inner_name = f"Proyecto-Centenario-QA-srv01-{source_sha[:12]}.zip"
    bundle_name = "installable-provenance.sigstore.json"
    verification_name = inner_name + ".verification.json"
    with tempfile.TemporaryDirectory(prefix=".qa525-bootstrap-download-", dir=STORE) as td:
        temp = Path(td)
        with zipfile.ZipFile(envelope_path) as zf:
            outer = zip_members(zf, max_total=MAX_ZIP)
            require(set(outer) == {inner_name, bundle_name, verification_name},
                    "BOOTSTRAP_ENVELOPE_MEMBERS_INVALID")
            inner_zip = temp / inner_name
            with zf.open(outer[inner_name]) as src, inner_zip.open("xb") as dst:
                count = 0
                while True:
                    block = src.read(1024 * 1024)
                    if not block:
                        break
                    count += len(block)
                    require(count <= MAX_ZIP, "BOOTSTRAP_ARCHIVE_TOO_LARGE")
                    dst.write(block)
                require(count == outer[inner_name].file_size,
                        "BOOTSTRAP_ARCHIVE_SIZE_CHANGED")
            bundle = temp / bundle_name
            bundle.write_bytes(read_member(zf, outer[bundle_name], maximum=5_000_000))
            require(outer[verification_name].file_size <= 5_000_000,
                    "BOOTSTRAP_VERIFICATION_METADATA_OVERSIZE")
        # A root command MUST NOT resolve gh through caller-controlled PATH.
        # Nor may caller variables inject an alternate gh config or loader.
        root_owned(GH_BINARY)
        gh_config = temp / "gh-config"
        gh_config.mkdir(mode=0o700)
        command = [str(GH_BINARY), "attestation", "verify", str(inner_zip),
                   "--repo", REPO, "--bundle", str(bundle),
                   "--signer-workflow", SIGNER, "--source-digest", source_sha,
                   "--deny-self-hosted-runners", "--format", "json"]
        try:
            verify(command, check=True, stdout=subprocess.DEVNULL,
                   stderr=subprocess.DEVNULL, timeout=120,
                   env={"PATH": "/usr/bin:/bin", "HOME": str(temp),
                        "GH_CONFIG_DIR": str(gh_config), "LC_ALL": "C"})
        except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired):
            raise Blocked("BOOTSTRAP_ATTESTATION_FAILED") from None
        with zipfile.ZipFile(inner_zip) as zf:
            members = zip_members(zf)
            prefix = f"Proyecto-Centenario-QA-srv01-{source_sha[:12]}/"
            require(all(key.startswith(prefix) for key in members),
                    "BOOTSTRAP_PACKAGE_ROOT_INVALID")
            files = {name[len(prefix):]: item for name, item in members.items()}
            require("MANIFEST.sha256" in files and "BUILD-INFO.txt" in files,
                    "BOOTSTRAP_MANIFEST_MISSING")
            build = read_member(zf, files["BUILD-INFO.txt"], maximum=5_000_000)
            require(build.splitlines().count(("SOURCE_SHA=" + source_sha).encode()) == 1
                    and build.splitlines().count(
                        b"STORAGE=SIGNED_REBUILT_SILO_ARCHIVE_SYNTHETIC_QA") == 1,
                    "BOOTSTRAP_BUILD_SOURCE_MISMATCH")
            manifest = read_member(zf, files["MANIFEST.sha256"], maximum=5_000_000)
            expected = {}
            try:
                lines = manifest.decode("utf-8").splitlines()
            except UnicodeError:
                raise Blocked("BOOTSTRAP_MANIFEST_UTF8_INVALID") from None
            for line in lines:
                hit = MANIFEST_LINE.fullmatch(line)
                require(hit is not None, "BOOTSTRAP_MANIFEST_LINE_INVALID")
                name = hit.group(2)
                require(name != "MANIFEST.sha256" and name not in expected
                        and all(part not in ("", ".", "..")
                                for part in name.split("/")),
                        "BOOTSTRAP_MANIFEST_NAME_INVALID")
                expected[name] = hit.group(1)
            require(set(expected) == (set(files) - {"MANIFEST.sha256"}),
                    "BOOTSTRAP_MANIFEST_SET_MISMATCH")
            require(set(SOURCES).issubset(expected) and
                    BOOTSTRAP_SCRIPT in expected,
                    "BOOTSTRAP_SIGNED_INSTALLER_OR_HELPERS_MISSING")
            # The bootstrap source itself MUST be one of the independently
            # signed members, not only its eventual caller-supplied hash.
            require(expected[BOOTSTRAP_SCRIPT] ==
                    digest_file(Path(__file__), maximum=200_000),
                    "BOOTSTRAP_PIN_NOT_IN_SIGNED_MANIFEST")
            target.mkdir(mode=0o700)
            for key, item in files.items():
                if key == "MANIFEST.sha256":
                    continue
                size = 0
                h = hashlib.sha256()
                parts = None
                dest = None
                if key in SOURCES:
                    parts = key.split("/")
                    dest = target.joinpath(*parts)
                    dest.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
                    fd = os.open(dest, os.O_WRONLY | os.O_CREAT | os.O_EXCL |
                                 os.O_NOFOLLOW | os.O_CLOEXEC, 0o600)
                else:
                    fd = -1
                try:
                    with zf.open(item) as stream:
                        while True:
                            block = stream.read(1024 * 1024)
                            if not block:
                                break
                            size += len(block)
                            require(size <= MAX_FILE, "BOOTSTRAP_MEMBER_EXPANSION")
                            h.update(block)
                            if fd >= 0:
                                write_all(fd, block)
                    require(size == item.file_size and
                            h.hexdigest() == expected[key],
                            "BOOTSTRAP_SIGNED_MEMBER_MISMATCH")
                    if fd >= 0:
                        os.fsync(fd)
                finally:
                    if fd >= 0:
                        os.close(fd)
            for base, dirs, _ in os.walk(target, topdown=False):
                for directory in dirs:
                    handle = os.open(Path(base) / directory, os.O_RDONLY |
                                     os.O_DIRECTORY | os.O_NOFOLLOW)
                    try:
                        os.fsync(handle)
                    finally:
                        os.close(handle)
            handle = os.open(target, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
            try:
                os.fsync(handle)
            finally:
                os.close(handle)
            return hashlib.sha256(manifest).hexdigest(), expected


def _prepare_locked(envelope, sha, expected, verifier_sha, *, now=None,
                    verify=subprocess.run):
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "BOOTSTRAP_ROOT_GLMDCH_ONLY")
    require(SHA40.fullmatch(sha) is not None and
            SHA64.fullmatch(expected) is not None and
            SHA64.fullmatch(verifier_sha) is not None,
            "BOOTSTRAP_IDENTITY_INVALID")
    root_owned(Path(__file__))
    require(digest_file(Path(__file__), maximum=200_000) == verifier_sha,
            "BOOTSTRAP_TRUSTED_VERIFIER_HASH_MISMATCH")
    root_owned(STORE, directory=True, mode=0o700)
    root_owned(ADMISSION.parent, directory=True)
    require(not ADMISSION.exists() and not ADMISSION.is_symlink(),
            "BOOTSTRAP_ADMISSION_ALREADY_EXISTS")
    target = STORE / sha
    require(not target.exists() and not target.is_symlink(),
            "BOOTSTRAP_TARGET_ALREADY_EXISTS")
    root_owned(Path(envelope), mode=0o600)
    # No installation or automatic overwriting of a previously admitted source.
    with tempfile.TemporaryDirectory(prefix=".qa525-signed-sources-", dir=STORE) as td:
        tmp_target = Path(td) / "staged-source"
        manifest_digest, hashes = verify_zip_and_prepare(
            Path(envelope), sha, expected, tmp_target, verify=verify)
        root_owned(tmp_target, directory=True, mode=0o700)
        # Recheck every privileged source after all write/fsync barriers.
        actual = {}
        for key in SOURCES:
            file = tmp_target / key
            root_owned(file, mode=0o600)
            actual[key] = digest_file(file, maximum=2_000_000)
            require(actual[key] == hashes[key], "BOOTSTRAP_STAGED_SOURCE_DRIFT")
        timestamp = int(time.time()) if now is None else now
        require(type(timestamp) is int and timestamp > 0,
                "BOOTSTRAP_CLOCK_INVALID")
        os.rename(tmp_target, target)
        fd = os.open(STORE, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
        try:
            os.fsync(fd)
        finally:
            os.close(fd)
        packet = {
            "status": "EXTERNAL_SIGSTORE_BOOTSTRAP_ADMISSION_ONLY",
            "source_sha": sha,
            "release_asset_sha256": "sha256:" + expected,
            "manifest_sha256": manifest_digest,
            "trusted_verifier_sha256": verifier_sha,
            "files": {str(target / name): value for name, value in actual.items()},
            "issued_at": timestamp,
            "expires_at": timestamp + 900,
        }
        raw = (json.dumps(packet, sort_keys=True, separators=(",", ":")) +
               "\n").encode()
        require(len(raw) <= 16_384, "BOOTSTRAP_ADMISSION_TOO_LARGE")
        fd = os.open(ADMISSION, os.O_WRONLY | os.O_CREAT | os.O_EXCL |
                     os.O_NOFOLLOW | os.O_CLOEXEC, 0o600)
        try:
            write_all(fd, raw)
            os.fsync(fd)
        finally:
            os.close(fd)
        fd = os.open(ADMISSION.parent, os.O_RDONLY | os.O_DIRECTORY |
                     os.O_NOFOLLOW)
        try:
            os.fsync(fd)
        finally:
            os.close(fd)
        return {
            "status": "QA525_SIGNED_SOURCES_PREPARED_NO_LAUNCHER_CHANGE",
            "source_sha": sha, "manifest_sha256": manifest_digest,
            "prepared_source_count": len(actual),
            "bootstrap_admission_lifetime_seconds": 900,
            "launcher_replaced": False, "api_web_changed": False,
            "may_install": False, "may_apply": False,
        }


def prepare(envelope, sha, expected, verifier_sha, *, now=None,
            verify=subprocess.run):
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "BOOTSTRAP_ROOT_GLMDCH_ONLY")
    with locked_bootstrap():
        return _prepare_locked(envelope, sha, expected, verifier_sha,
                               now=now, verify=verify)


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--verified-envelope", type=Path, required=True)
    p.add_argument("--source-sha", required=True)
    p.add_argument("--expected-zip-sha256", required=True)
    p.add_argument("--trusted-bootstrap-sha256", required=True)
    args = p.parse_args(argv)
    try:
        report = prepare(args.verified_envelope, args.source_sha,
                         args.expected_zip_sha256, args.trusted_bootstrap_sha256)
        print(json.dumps(report, sort_keys=True))
        return 0
    except (Blocked, OSError, ValueError, TypeError, RuntimeError,
            zipfile.BadZipFile):
        print(json.dumps({"status": "BLOCKED_ROOT_BOOTSTRAP",
                          "launcher_replaced": False, "may_install": False,
                          "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    sys.exit(main())
