#!/usr/bin/env python3
"""QA525 vNext authenticated cache and offline admission (never APPLY)."""
from __future__ import annotations

import argparse
import calendar
import datetime as dt
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import socket
import ssl
import stat
import sys
import tempfile
import time
import zipfile
from urllib.error import HTTPError, URLError
from urllib.request import HTTPRedirectHandler, HTTPSHandler, ProxyHandler, Request, build_opener

SHA = re.compile(r"^[a-f0-9]{40}$")
DIGEST = re.compile(r"^sha256:[a-f0-9]{64}$")
NONCE = re.compile(r"^[A-Za-z0-9_-]{24,96}$")
G6_ARCHIVE = re.compile(r"^centenario-qa-g6-CIFRADO-([0-9]{8}T[0-9]{6})Z\.tar\.gz$")
G6_RESTORE = re.compile(r"^isolated-restore-[0-9]{8}T[0-9]{6}Z\.report\.json$")
DRIVE_ID = re.compile(r"^[A-Za-z0-9_-]{20,100}$")
G6_REVIEW_NAME = re.compile(r"^qa525-g6-reviewed-[0-9]{8}T[0-9]{6}Z\.json$")

HERE = Path(__file__).resolve().parent
READINESS_MODULE = HERE / "qa-agent-readiness.py"
STAGING_MODULE = HERE / "qa-signed-staging.py"
OPS_RECEIPT = Path("/etc/pmgm/qa525-vnext-ops.json")
DEFAULT_CACHE_ROOT = Path("/var/lib/pmgm/qa525-cache")
DEFAULT_STATE_DIR = Path("/var/lib/pmgm/qa525-state")
G6_REVIEW_ROOT = Path("/etc/pmgm")
G6_ARCHIVE_ROOT = Path("/home/soporte")
G6_RECEIPT_PATH = Path("/opt/centenario/qa-g6/archive.receipt.json")
G6_REPORT_ROOT = Path("/opt/centenario/qa-g6")
MAX_ARTIFACT = 4 * 1024 * 1024 * 1024
MAX_JSON = 1024 * 1024
MIN_G6_MARGIN = 30 * 60
MAX_APPROVAL_LIFETIME = 30 * 60
MAX_ADMISSION_LIFETIME = 30 * 60
G6_LIFETIME = 48 * 60 * 60
POLICY = {"action": "ADMIT_OFFLINE_APPLY", "environment": "QA_SYNTHETIC",
          "host": "glmdch", "scope": "API_WEB_QA_ONLY", "network_allowed": False,
          "nonce_consumed_at": "APPLYING"}
POLICY_HASH = "sha256:" + hashlib.sha256(
    json.dumps(POLICY, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


class Stop(Exception):
    pass


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def now_utc() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


def parse_time(value: str) -> dt.datetime:
    if not isinstance(value, str) or not value.endswith("Z"):
        raise Stop("TIME_INVALID")
    try:
        parsed = dt.datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError:
        raise Stop("TIME_INVALID") from None
    if parsed.utcoffset() != dt.timedelta(0):
        raise Stop("TIME_INVALID")
    return parsed


def format_time(value: dt.datetime) -> str:
    return value.astimezone(dt.timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def require_root(code: str) -> None:
    if os.geteuid() != 0:
        raise Stop(code)


def require_trusted_ancestors(path: Path) -> None:
    """Reject symlinked, non-root-owned or writable ancestors before root I/O."""
    if not path.is_absolute() or ".." in path.parts:
        raise Stop("UNTRUSTED_PATH")
    for ancestor in path.parents:
        try:
            st = ancestor.lstat()
        except OSError:
            raise Stop("UNTRUSTED_ANCESTOR_MISSING") from None
        if (not stat.S_ISDIR(st.st_mode) or st.st_uid != 0 or
                stat.S_IMODE(st.st_mode) & 0o022):
            raise Stop("UNTRUSTED_ANCESTOR")


def require_private_dir(path: Path, *, exact: bool = False) -> None:
    require_trusted_ancestors(path)
    try:
        info = path.lstat()
    except OSError:
        raise Stop("DIRECTORY_UNAVAILABLE") from None
    mode = stat.S_IMODE(info.st_mode)
    if (not stat.S_ISDIR(info.st_mode) or info.st_uid != os.geteuid() or
            info.st_gid != os.getegid() or mode & 0o022 or (exact and mode != 0o700)):
        raise Stop("DIRECTORY_UNTRUSTED")


def _trusted_fd(path: Path, *, mode: int, max_size: int | None = None) -> tuple[int, os.stat_result]:
    require_trusted_ancestors(path)
    try:
        parent = path.parent.lstat()
        if (not stat.S_ISDIR(parent.st_mode) or parent.st_uid != os.geteuid() or
                parent.st_gid != os.getegid() or parent.st_mode & 0o022):
            raise Stop("PARENT_UNTRUSTED")
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        info = os.fstat(fd)
        if (not stat.S_ISREG(info.st_mode) or info.st_uid != os.geteuid() or
                info.st_gid != os.getegid() or info.st_nlink != 1 or
                stat.S_IMODE(info.st_mode) != mode):
            os.close(fd)
            raise Stop("FILE_UNTRUSTED")
        if max_size is not None and info.st_size > max_size:
            os.close(fd)
            raise Stop("FILE_TOO_LARGE")
        return fd, info
    except Stop:
        raise
    except OSError:
        raise Stop("FILE_UNAVAILABLE") from None


def _same_file(before: os.stat_result, after: os.stat_result) -> bool:
    return (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns,
            before.st_ctime_ns) == (after.st_dev, after.st_ino, after.st_size,
                                    after.st_mtime_ns, after.st_ctime_ns)


def trusted_file(path: Path, *, mode: int = 0o600, max_size: int | None = None) -> bytes:
    fd, before = _trusted_fd(path, mode=mode, max_size=max_size)
    try:
        with os.fdopen(fd, "rb") as handle:
            data = handle.read((max_size + 1) if max_size is not None else -1)
            after = os.fstat(handle.fileno())
        if not _same_file(before, after) or len(data) != before.st_size:
            raise Stop("FILE_CHANGED")
        return data
    except Stop:
        raise
    except OSError:
        raise Stop("FILE_UNAVAILABLE") from None


def trusted_digest(path: Path, *, mode: int = 0o600,
                   max_size: int | None = None) -> tuple[str, dict]:
    """Hash a potentially multi-GiB file in bounded memory using one descriptor."""
    fd, before = _trusted_fd(path, mode=mode, max_size=max_size)
    digest = hashlib.sha256()
    try:
        with os.fdopen(fd, "rb") as handle:
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
            after = os.fstat(handle.fileno())
        if not _same_file(before, after):
            raise Stop("FILE_CHANGED")
    except Stop:
        raise
    except OSError:
        raise Stop("FILE_UNAVAILABLE") from None
    identity = {"device": before.st_dev, "inode": before.st_ino, "size": before.st_size,
                "mtime_ns": before.st_mtime_ns}
    return "sha256:" + digest.hexdigest(), identity


def signed_installable_digest(outer_path: Path, app_sha: str) -> str:
    """Bind the attested internal ZIP to the external Actions ZIP, without network.

    The Sigstore verification in staging verifies exactly this inner member.
    This helper does NOT independently authenticate a Sigstore signature: it
    pins the inner member and rechecks its bytes from the saved outer artifact.
    """
    if type(app_sha) is not str or SHA.fullmatch(app_sha) is None:
        raise Stop("SIGNED_MEMBER_APP_SHA_INVALID")
    member = "Proyecto-Centenario-QA-srv01-" + app_sha[:12] + ".zip"
    allowed = {member, "installable-provenance.sigstore.json", member + ".verification.json"}
    fd, before = _trusted_fd(outer_path, mode=0o600, max_size=MAX_ARTIFACT)
    try:
        with os.fdopen(fd, "rb") as handle:
            with zipfile.ZipFile(handle, "r") as outer:
                names = outer.namelist()
                if len(names) != 3 or set(names) != allowed:
                    raise Stop("SIGNED_MEMBER_OUTER_SET_INVALID")
                info = outer.getinfo(member)
                # The inner ZIP is a file, not a symlink or a decompression bomb.
                if (info.is_dir() or info.file_size <= 0 or
                        info.file_size > 2 * 1024 * 1024 * 1024 or
                        info.compress_size == 0 or
                        stat.S_ISLNK(info.external_attr >> 16)):
                    raise Stop("SIGNED_MEMBER_SIZE_OR_TYPE_INVALID")
                digest = hashlib.sha256()
                total = 0
                with outer.open(info, "r") as inner:
                    for chunk in iter(lambda: inner.read(1024 * 1024), b""):
                        total += len(chunk)
                        if total > info.file_size:
                            raise Stop("SIGNED_MEMBER_SIZE_CHANGED")
                        digest.update(chunk)
                if total != info.file_size:
                    raise Stop("SIGNED_MEMBER_SIZE_CHANGED")
            after = os.fstat(handle.fileno())
        if not _same_file(before, after):
            raise Stop("SIGNED_MEMBER_OUTER_CHANGED")
        return "sha256:" + digest.hexdigest()
    except Stop:
        raise
    except (OSError, ValueError, KeyError, zipfile.BadZipFile, RuntimeError):
        raise Stop("SIGNED_MEMBER_INVALID") from None


def load_json_trusted(path: Path) -> dict:
    raw = trusted_file(path, max_size=MAX_JSON)
    try:
        value = json.loads(raw)
    except (UnicodeDecodeError, json.JSONDecodeError):
        raise Stop("JSON_INVALID") from None
    if type(value) is not dict:
        raise Stop("JSON_INVALID")
    return value


def _fsync_dir(path: Path) -> None:
    descriptor = os.open(path, os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def atomic_write(path: Path, payload: bytes) -> None:
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    os.chmod(path.parent, 0o700)
    fd, temporary = tempfile.mkstemp(prefix="." + path.name + ".", dir=path.parent)
    try:
        os.fchmod(fd, 0o600)
        with os.fdopen(fd, "wb") as handle:
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
        _fsync_dir(path.parent)
    finally:
        try:
            os.unlink(temporary)
        except FileNotFoundError:
            pass


def exclusive_write(path: Path, payload: bytes) -> None:
    """Reserve the admission name atomically; a partial crash remains fail-closed."""
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    except FileExistsError:
        raise Stop("NONCE_ALREADY_ADMITTED") from None
    except OSError:
        raise Stop("ADMISSION_CREATE_FAILED") from None
    try:
        with os.fdopen(fd, "wb") as handle:
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())
        _fsync_dir(path.parent)
    except Exception:
        raise Stop("ADMISSION_WRITE_FAILED") from None


def _load_verified_module_bytes(name: str, path: Path, source: bytes):
    """Execute exactly the bytes verified from the pinned, trusted descriptor.

    Never re-open a module path through importlib's loader after hashing it.
    """
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise Stop("OPS_MODULE_IMPORT")
    module = importlib.util.module_from_spec(spec)
    try:
        exec(compile(source, str(path), "exec"), module.__dict__)
    except Exception:
        raise Stop("OPS_MODULE_EXECUTION_FAILED") from None
    return module


def load_verified_ops() -> tuple[dict, object, object]:
    """Trust fixed OPS bytes pinned by a root-owned signed-source receipt."""
    receipt = load_json_trusted(OPS_RECEIPT)
    required = {"schema", "status", "ops_source_sha", "program_sha256",
                "readiness_sha256", "staging_sha256", "policy_hash"}
    if (set(receipt) != required or receipt.get("schema") != "qa525-ops-v2" or
            receipt.get("status") != "OPS_SIGNED_VERIFIED" or
            not isinstance(receipt.get("ops_source_sha"), str) or
            not SHA.fullmatch(receipt["ops_source_sha"]) or
            receipt.get("policy_hash") != POLICY_HASH):
        raise Stop("OPS_RECEIPT_INVALID")
    verified = {}
    for path, field, mode in ((Path(__file__).resolve(), "program_sha256", 0o755),
                              (READINESS_MODULE, "readiness_sha256", 0o644),
                              (STAGING_MODULE, "staging_sha256", 0o644)):
        source = trusted_file(path, mode=mode, max_size=5 * 1024 * 1024)
        actual = "sha256:" + hashlib.sha256(source).hexdigest()
        if actual != receipt.get(field):
            raise Stop("OPS_SOURCE_HASH_MISMATCH")
        verified[field] = source
    return (receipt,
            _load_verified_module_bytes("qa525_readiness_v2", READINESS_MODULE,
                                        verified["readiness_sha256"]),
            _load_verified_module_bytes("qa525_staging_v2", STAGING_MODULE,
                                        verified["staging_sha256"]))


def download_artifact(url: str, token: str, destination: Path) -> None:
    prefix = "https://api.github.com/repos/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/artifacts/"
    if not url.startswith(prefix) or not url.endswith("/zip"):
        raise Stop("ARTIFACT_URL_INVALID")
    request = Request(url, headers={"Accept": "application/vnd.github+json",
        "Authorization": "Bearer " + token, "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "centenario-qa525-sync-v2"})
    opener = build_opener(ProxyHandler({}), HTTPSHandler(context=ssl.create_default_context()), NoRedirect())
    try:
        opener.open(request, timeout=20)
        raise Stop("ARTIFACT_REDIRECT_MISSING")
    except HTTPError as exc:
        if exc.code not in (302, 303, 307):
            raise Stop("ARTIFACT_API_HTTP") from None
        location = exc.headers.get("Location", "")
    if not re.fullmatch(r"https://[A-Za-z0-9.-]+\.blob\.core\.windows\.net/[\x21-\x7e]+", location):
        raise Stop("ARTIFACT_REDIRECT_UNTRUSTED")
    blob = build_opener(ProxyHandler({}), HTTPSHandler(context=ssl.create_default_context()), NoRedirect())
    try:
        response = blob.open(Request(location, headers={"User-Agent": "centenario-qa525-sync-v2"}), timeout=60)
        if response.geturl() != location:
            raise Stop("ARTIFACT_BLOB_REDIRECT")
        declared = response.headers.get("Content-Length")
        if declared is not None and (not declared.isdigit() or int(declared) > MAX_ARTIFACT):
            raise Stop("ARTIFACT_TOO_LARGE")
        fd = os.open(destination, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "wb") as handle:
            total = 0
            while True:
                chunk = response.read(1024 * 1024)
                if not chunk:
                    break
                total += len(chunk)
                if total > MAX_ARTIFACT:
                    raise Stop("ARTIFACT_TOO_LARGE")
                handle.write(chunk)
            handle.flush()
            os.fsync(handle.fileno())
    except Stop:
        raise
    except (HTTPError, URLError, OSError):
        raise Stop("ARTIFACT_DOWNLOAD_FAILED") from None


def _cache_lock(cache_root: Path):
    fd = os.open(cache_root / ".sync.lock", os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
    os.fchmod(fd, 0o600)
    fcntl.flock(fd, fcntl.LOCK_EX)
    return fd


def _rotate_expired(final_target: Path, current: dt.datetime) -> None:
    if not (final_target.exists() or final_target.is_symlink()):
        return
    try:
        require_private_dir(final_target, exact=True)
        expiry = parse_time(load_json_trusted(final_target / "cache.json").get("cache_expires_at"))
    except Stop:
        raise Stop("CACHE_EXISTING_INVALID") from None
    if expiry > current:
        raise Stop("CACHE_ALREADY_VALID")
    rotated = final_target.parent / ("expired-" + final_target.name + "-" +
                                     current.strftime("%Y%m%dT%H%M%SZ"))
    if rotated.exists() or rotated.is_symlink():
        raise Stop("CACHE_ROTATION_COLLISION")
    os.rename(final_target, rotated)
    _fsync_dir(final_target.parent)


def sync(args) -> dict:
    require_root("SYNC_REQUIRES_ROOT_GLMDCH")
    if socket.gethostname().split(".", 1)[0] != "glmdch":
        raise Stop("SYNC_REQUIRES_ROOT_GLMDCH")
    ops, readiness, staging = load_verified_ops()
    token = readiness._read_qa_pat()
    if token is None:
        raise Stop("PAT_REQUIRED_NO_ANONYMOUS_FALLBACK")
    result = readiness.evaluate_latest_validated(readiness.fetch_public)
    app_sha, artifact_digest, artifact_id = (result.get("source_sha"),
                                                result.get("artifact_sha256"),
                                                result.get("artifact_id"))
    if not isinstance(app_sha, str) or not SHA.fullmatch(app_sha):
        raise Stop("APP_SHA_INVALID")
    if not isinstance(artifact_digest, str) or not DIGEST.fullmatch(artifact_digest):
        raise Stop("ARTIFACT_DIGEST_INVALID")
    if type(artifact_id) is not int or artifact_id <= 0:
        raise Stop("ARTIFACT_ID_INVALID")
    args.cache_root.mkdir(mode=0o700, parents=True, exist_ok=True)
    require_private_dir(args.cache_root, exact=True)
    lock = _cache_lock(args.cache_root)
    try:
        current = now_utc()
        final_target = args.cache_root / app_sha
        _rotate_expired(final_target, current)
        target = Path(tempfile.mkdtemp(prefix="." + app_sha + ".sync-", dir=args.cache_root))
        os.chmod(target, 0o700)
        temporary = target / ".artifact.zip.partial"
        try:
            download_artifact(readiness.API + "/actions/artifacts/%d/zip" % artifact_id, token, temporary)
            actual, artifact_identity = trusted_digest(temporary, max_size=MAX_ARTIFACT)
            if actual != artifact_digest:
                raise Stop("ARTIFACT_DIGEST_MISMATCH")
            artifact = target / "artifact.zip"
            os.replace(temporary, artifact)
            attestation = target / "installable-provenance.sigstore.json"
            try:
                staged = staging.stage(artifact, actual, app_sha, target / "package",
                                       tokenless=True, attestation_output=attestation)
            except Exception:
                raise Stop("SIGNED_STAGING_FAILED") from None
            if (staged.get("status") != "SIGNED_CODE_STAGED_NOT_INSTALLED" or
                    staged.get("source_sha") != app_sha):
                raise Stop("SIGNED_STAGING_INVALID")
            # gh already verified this inner ZIP's Sigstore attestation at SYNC.
            signed_member_sha = signed_installable_digest(artifact, app_sha)
            manifest_sha, manifest_identity = trusted_digest(target / "package" / "MANIFEST.sha256")
            bundle_sha, bundle_identity = trusted_digest(attestation)
            synced = now_utc()
            receipt = {"schema": "qa525-cache-v2", "status": "SYNCED_ATTESTED",
                "ops_source_sha": ops["ops_source_sha"], "policy_hash": POLICY_HASH,
                "app_source_sha": app_sha, "attestation_source_sha": app_sha,
                "attestation_signer_workflow": staging.WORKFLOW, "artifact_id": artifact_id,
                "artifact_sha256": actual, "artifact_identity": artifact_identity,
                "signed_installable_sha256": signed_member_sha,
                "attestation_bundle_sha256": bundle_sha,
                "attestation_bundle_identity": bundle_identity,
                "manifest_sha256": manifest_sha, "manifest_identity": manifest_identity,
                "silo_workflow_run": result["silo_workflow_run"], "gates": result["gates"],
                "synced_at": format_time(synced),
                "cache_expires_at": format_time(synced + dt.timedelta(hours=args.ttl_hours)),
                "network_allowed_after_sync": False}
            atomic_write(target / "cache.json", (json.dumps(receipt, sort_keys=True) + "\n").encode())
            os.rename(target, final_target)
            _fsync_dir(args.cache_root)
            return receipt
        except Exception:
            shutil.rmtree(target, ignore_errors=True)
            raise
    finally:
        os.close(lock)


def _validate_identity(recorded: object, actual: dict) -> bool:
    return type(recorded) is dict and all(recorded.get(key) == value for key, value in actual.items())


def _g6_expiry(g6: dict, current: dt.datetime, margin: int) -> dt.datetime:
    """Check the same local G6 evidence as the supervised launcher, offline.

    The off-host Drive attestation remains a trusted reviewed claim, not
    cryptographic proof of disaster recovery. Fail closed if any local
    archive, receipt, or isolated-restore evidence is absent or altered.
    """
    required = {"g6_archive_name", "g6_archive_sha256", "g6_drive_file_id",
                "g6_offhost_sha256_verified", "g6_restore_report_name"}
    if type(g6) is not dict or set(g6) not in (required, required | {"reviewed_at"}):
        raise Stop("G6_FIELDS_INVALID")
    if "reviewed_at" in g6:
        reviewed = g6["reviewed_at"]
        if type(reviewed) is not int or reviewed > int(current.timestamp()) or reviewed < 0:
            raise Stop("G6_REVIEW_TIMESTAMP_INVALID")
    name, recorded = g6["g6_archive_name"], g6["g6_archive_sha256"]
    match = G6_ARCHIVE.fullmatch(name) if type(name) is str else None
    if (match is None or type(recorded) is not str or
            re.fullmatch(r"[a-f0-9]{64}", recorded) is None or
            type(g6["g6_drive_file_id"]) is not str or
            DRIVE_ID.fullmatch(g6["g6_drive_file_id"]) is None or
            type(g6["g6_restore_report_name"]) is not str or
            G6_RESTORE.fullmatch(g6["g6_restore_report_name"]) is None):
        raise Stop("G6_REFERENCE_INVALID")
    if g6["g6_offhost_sha256_verified"] is not True:
        raise Stop("G6_NOT_VERIFIED")
    try:
        archive_epoch = calendar.timegm(time.strptime(match.group(1), "%Y%m%dT%H%M%S"))
    except ValueError:
        raise Stop("G6_REFERENCE_INVALID") from None
    age = int(current.timestamp()) - archive_epoch
    if not 0 <= age < G6_LIFETIME:
        raise Stop("G6_CHECKPOINT_TOO_OLD")
    expiry = dt.datetime.fromtimestamp(archive_epoch + G6_LIFETIME, dt.timezone.utc)
    if (expiry - current).total_seconds() < margin:
        raise Stop("G6_MARGIN_LOW")

    archive = G6_ARCHIVE_ROOT / name
    try:
        fd = os.open(archive, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        with os.fdopen(fd, "rb") as stream:
            before = os.fstat(stream.fileno())
            if not stat.S_ISREG(before.st_mode) or before.st_size <= 0 or before.st_nlink != 1:
                raise Stop("G6_LOCAL_ARCHIVE_INVALID")
            checksum = hashlib.sha256()
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                checksum.update(chunk)
            after = os.fstat(stream.fileno())
        if not _same_file(before, after):
            raise Stop("G6_LOCAL_ARCHIVE_CHANGED")
    except Stop:
        raise
    except OSError:
        raise Stop("G6_LOCAL_ARCHIVE_MISSING") from None
    if checksum.hexdigest() != recorded:
        raise Stop("G6_LOCAL_ARCHIVE_HASH_CHANGED")

    receipt = load_json_trusted(G6_RECEIPT_PATH)
    if (receipt.get("archive_name") != name or receipt.get("sha256") != recorded or
            receipt.get("status") != "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING"):
        raise Stop("G6_LOCAL_RECEIPT_MISMATCH")
    restore = load_json_trusted(G6_REPORT_ROOT / g6["g6_restore_report_name"])
    tables = restore.get("postgres_tables_by_database")
    if (restore.get("result") != "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY" or
            restore.get("archive_filename") != name or
            restore.get("archive_sha256") != recorded or
            type(tables) is not dict or set(tables) != {"pmgm", "pmgm_keycloak"} or
            not all(type(v) is int and v > 0 for v in tables.values()) or
            type(restore.get("silo_objects_verified")) is not int or
            restore.get("production_touched") is not False):
        raise Stop("G6_RESTORATION_UNVERIFIED")
    return expiry


def _approval_expiry(approval: dict, *, nonce: str, ops_sha: str, app_sha: str,
                     artifact_id: int, artifact_sha: str, current: dt.datetime) -> dt.datetime:
    expected_keys = {"status", "nonce", "ops_source_sha", "app_source_sha", "artifact_id",
                     "artifact_sha256", "scope", "environment", "action", "host",
                     "issued_at", "expires_at"}
    expected = {"status": "APPROVED", "nonce": nonce, "ops_source_sha": ops_sha,
        "app_source_sha": app_sha, "artifact_id": artifact_id, "artifact_sha256": artifact_sha,
        "scope": POLICY["scope"], "environment": POLICY["environment"],
        "action": POLICY["action"], "host": POLICY["host"]}
    if set(approval) != expected_keys or any(approval.get(k) != v for k, v in expected.items()):
        raise Stop("APPROVAL_BINDING_INVALID")
    issued, expiry = parse_time(approval.get("issued_at")), parse_time(approval.get("expires_at"))
    lifetime, age = (expiry - issued).total_seconds(), (current - issued).total_seconds()
    if not 0 < lifetime <= MAX_APPROVAL_LIFETIME or not 0 <= age <= MAX_APPROVAL_LIFETIME:
        raise Stop("APPROVAL_LIFETIME_INVALID")
    if expiry <= current:
        raise Stop("APPROVAL_EXPIRED")
    return expiry


def admit(args) -> dict:
    require_root("ADMISSION_REQUIRES_ROOT")
    if not SHA.fullmatch(args.app_sha) or not NONCE.fullmatch(args.nonce):
        raise Stop("IDENTITY_INVALID")
    # G6 is a versioned root-owned acta, not a caller-chosen JSON elsewhere.
    if (not args.g6_receipt.is_absolute() or args.g6_receipt.parent != G6_REVIEW_ROOT or
            G6_REVIEW_NAME.fullmatch(args.g6_receipt.name) is None):
        raise Stop("G6_REVIEW_PATH_UNTRUSTED")
    ops, _, staging = load_verified_ops()
    cache_dir = args.cache_root / args.app_sha
    try:
        require_private_dir(args.cache_root, exact=True)
        require_private_dir(cache_dir, exact=True)
        require_private_dir(cache_dir / "package", exact=True)
    except Stop:
        raise Stop("CACHE_DIRECTORY_UNTRUSTED") from None
    receipt = load_json_trusted(cache_dir / "cache.json")
    expected = {"schema": "qa525-cache-v2", "status": "SYNCED_ATTESTED",
        "app_source_sha": args.app_sha, "attestation_source_sha": args.app_sha,
        "ops_source_sha": ops["ops_source_sha"], "policy_hash": POLICY_HASH,
        "network_allowed_after_sync": False, "attestation_signer_workflow": staging.WORKFLOW}
    if any(receipt.get(key) != value for key, value in expected.items()):
        raise Stop("CACHE_BINDING_INVALID")
    if type(receipt.get("artifact_id")) is not int or receipt["artifact_id"] <= 0:
        raise Stop("CACHE_BINDING_INVALID")
    artifact_sha, artifact_identity = trusted_digest(cache_dir / "artifact.zip", max_size=MAX_ARTIFACT)
    signed_member_sha = signed_installable_digest(cache_dir / "artifact.zip", args.app_sha)
    manifest_sha, manifest_identity = trusted_digest(cache_dir / "package" / "MANIFEST.sha256")
    bundle_sha, bundle_identity = trusted_digest(cache_dir / "installable-provenance.sigstore.json")
    bindings = (("artifact_sha256", artifact_sha),
                ("signed_installable_sha256", signed_member_sha),
                ("manifest_sha256", manifest_sha),
                ("attestation_bundle_sha256", bundle_sha))
    identities = (("artifact_identity", artifact_identity), ("manifest_identity", manifest_identity),
                  ("attestation_bundle_identity", bundle_identity))
    if any(receipt.get(field) != value for field, value in bindings) or any(
            not _validate_identity(receipt.get(field), value) for field, value in identities):
        raise Stop("CACHE_ATTESTATION_BINDING_CHANGED")
    try:
        staging.verify_manifest(cache_dir / "package", args.app_sha)
    except Exception:
        raise Stop("CACHE_PACKAGE_INVALID") from None
    if (trusted_digest(cache_dir / "package" / "MANIFEST.sha256")[0] != manifest_sha or
            trusted_digest(cache_dir / "installable-provenance.sigstore.json")[0] != bundle_sha):
        raise Stop("CACHE_PACKAGE_CHANGED")
    current = now_utc()
    cache_expiry = parse_time(receipt.get("cache_expires_at"))
    if cache_expiry <= current:
        raise Stop("CACHE_EXPIRED")
    g6_expiry = _g6_expiry(load_json_trusted(args.g6_receipt), current, args.g6_margin_seconds)
    approval_expiry = _approval_expiry(load_json_trusted(args.approval), nonce=args.nonce,
        ops_sha=ops["ops_source_sha"], app_sha=args.app_sha, artifact_id=receipt["artifact_id"],
        artifact_sha=artifact_sha, current=current)
    valid_until = min(cache_expiry, approval_expiry,
                      g6_expiry - dt.timedelta(seconds=args.g6_margin_seconds),
                      current + dt.timedelta(seconds=MAX_ADMISSION_LIFETIME))
    if valid_until <= current:
        raise Stop("ADMISSION_WINDOW_EMPTY")
    admitted = {"schema": "qa525-admission-v2", "status": "ADMITTED",
        "ops_source_sha": ops["ops_source_sha"], "app_source_sha": args.app_sha,
        "artifact_id": receipt["artifact_id"], "artifact_sha256": artifact_sha,
        "signed_installable_sha256": signed_member_sha,
        "artifact_identity": artifact_identity, "manifest_sha256": manifest_sha,
        "attestation_bundle_sha256": bundle_sha, "policy_hash": POLICY_HASH,
        "nonce": args.nonce, "nonce_consumed": False, "nonce_consume_transition": "APPLYING",
        "admitted_at": format_time(current), "valid_until": format_time(valid_until),
        "g6_expires_at": format_time(g6_expiry), "network_allowed": False}
    try:
        args.state_dir.mkdir(mode=0o700, parents=True, exist_ok=True)
    except OSError:
        raise Stop("STATE_DIRECTORY_UNAVAILABLE") from None
    require_private_dir(args.state_dir, exact=True)
    output = args.state_dir / ("admission-" + args.nonce + ".json")
    exclusive_write(output, (json.dumps(admitted, sort_keys=True) + "\n").encode())
    return admitted


def postcheck(args) -> dict:
    require_root("POSTCHECK_REQUIRES_ROOT")
    if not SHA.fullmatch(args.app_sha):
        raise Stop("APP_SHA_INVALID")
    journal, launcher, runtime = (load_json_trusted(args.journal),
                                  load_json_trusted(args.launcher_status),
                                  load_json_trusted(args.runtime))
    if (journal.get("status") != "APPLIED" or journal.get("source_sha") != args.app_sha or
            type(journal.get("new_images")) is not dict):
        raise Stop("JOURNAL_NOT_APPLIED")
    images = journal["new_images"]
    for service in ("api", "web"):
        if not isinstance(images.get(service), str) or not DIGEST.fullmatch(images[service]):
            raise Stop("JOURNAL_IMAGE_INVALID")
    next_path = Path(str(args.journal) + ".next")
    if next_path.exists() or next_path.is_symlink():
        raise Stop("JOURNAL_NEXT_PRESENT")
    if (launcher.get("status") != "QA525_UPDATE_STATUS_READ_ONLY" or
            launcher.get("services_running") is not True or launcher.get("may_apply") is not False or
            launcher.get("archive_reconciliation_required") is not False):
        raise Stop("LAUNCHER_NOT_CLOSED")
    prepared_exists = launcher.get("prepared_exists") is True
    if prepared_exists:
        prepared = load_json_trusted(args.prepared)
        if (prepared.get("source_sha") != args.app_sha or
                prepared.get("status") != "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT" or
                prepared.get("may_apply") is not False or
                prepared.get("services_unchanged") is not True or
                any(prepared.get(service + "_image_id") != images[service]
                    for service in ("api", "web"))):
            raise Stop("PREPARED_NOT_BOUND_TO_APPLIED")
    elif args.prepared.exists() or args.prepared.is_symlink():
        raise Stop("PREPARED_STATUS_DRIFT")
    services = runtime.get("services")
    if type(services) is not dict:
        raise Stop("RUNTIME_INVALID")
    for service in ("api", "web"):
        value = services.get(service)
        if (type(value) is not dict or value.get("status") != "running" or
                value.get("revision") != args.app_sha or value.get("image") != images[service]):
            raise Stop("RUNTIME_MISMATCH_" + service.upper())
    return {"schema": "qa525-postcheck-v2", "status": "PASS_APPLIED",
            "app_source_sha": args.app_sha, "prepared_retained": prepared_exists, "may_apply": False}


def g6_margin_seconds(value: str) -> int:
    """Bounded parser: no massive enumeration of 170,000 allowed choices."""
    try:
        number = int(value, 10)
    except (TypeError, ValueError):
        raise argparse.ArgumentTypeError("G6_MARGIN_NOT_INTEGER") from None
    if not MIN_G6_MARGIN <= number < G6_LIFETIME:
        raise argparse.ArgumentTypeError("G6_MARGIN_OUT_OF_RANGE")
    return number


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser()
    sub = root.add_subparsers(dest="command", required=True)
    sync_parser = sub.add_parser("sync")
    sync_parser.add_argument("--cache-root", type=Path, default=DEFAULT_CACHE_ROOT)
    sync_parser.add_argument("--ttl-hours", type=int, default=6, choices=range(1, 25))
    admission = sub.add_parser("admit")
    admission.add_argument("--app-sha", required=True)
    admission.add_argument("--nonce", required=True)
    admission.add_argument("--cache-root", type=Path, default=DEFAULT_CACHE_ROOT)
    admission.add_argument("--g6-receipt", type=Path, required=True)
    admission.add_argument("--approval", type=Path, required=True)
    admission.add_argument("--state-dir", type=Path, default=DEFAULT_STATE_DIR)
    admission.add_argument("--g6-margin-seconds", type=g6_margin_seconds,
                           default=MIN_G6_MARGIN)
    check = sub.add_parser("postcheck")
    check.add_argument("--app-sha", required=True)
    check.add_argument("--journal", type=Path, required=True)
    check.add_argument("--prepared", type=Path, required=True)
    check.add_argument("--launcher-status", type=Path, required=True)
    check.add_argument("--runtime", type=Path, required=True)
    return root


def main(argv=None) -> int:
    args = parser().parse_args(argv)
    try:
        result = sync(args) if args.command == "sync" else admit(args) if args.command == "admit" else postcheck(args)
        print(json.dumps(result, sort_keys=True))
        return 0
    except Stop as exc:
        print(json.dumps({"status": "STOP", "code": str(exc)}, sort_keys=True))
        return 2
    except Exception:
        print(json.dumps({"status": "STOP", "code": "INTERNAL_FAIL_CLOSED"}, sort_keys=True))
        return 2


if __name__ == "__main__":
    sys.exit(main())
