#!/usr/bin/env python3
"""QA525 vNext: authenticated SYNC/CACHE and offline ADMISSION.

This program never applies a release.  ``sync`` is the only command allowed to
use the network; ``admit`` consumes a root-owned immutable cache and emits a
short-lived admission receipt for a separate, offline apply engine.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import socket
import stat
import sys
import tempfile
from urllib.error import HTTPError, URLError
from urllib.request import HTTPRedirectHandler, HTTPSHandler, ProxyHandler, Request, build_opener
import ssl


SHA = re.compile(r"^[a-f0-9]{40}$")
DIGEST = re.compile(r"^sha256:[a-f0-9]{64}$")
NONCE = re.compile(r"^[A-Za-z0-9_-]{24,96}$")
DEFAULT_CACHE_ROOT = Path("/var/lib/pmgm/qa525-cache")
DEFAULT_PAT = Path("/etc/pmgm/qa525-github-readonly.pat")
MAX_ARTIFACT = 4 * 1024 * 1024 * 1024
MIN_G6_MARGIN = 30 * 60


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
    return parsed


def format_time(value: dt.datetime) -> str:
    return value.astimezone(dt.timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return "sha256:" + digest.hexdigest()


def require_root(code: str) -> None:
    if os.geteuid() != 0:
        raise Stop(code)


def require_private_dir(path: Path, *, exact: bool = False, owner: int | None = None,
                        group: int | None = None) -> None:
    if owner is None:
        owner = os.geteuid()
    if group is None:
        group = os.getegid()
    try:
        info = path.lstat()
    except OSError:
        raise Stop("DIRECTORY_UNAVAILABLE") from None
    mode = stat.S_IMODE(info.st_mode)
    if (not stat.S_ISDIR(info.st_mode) or info.st_uid != owner or info.st_gid != group or
            mode & 0o022 or (exact and mode != 0o700)):
        raise Stop("DIRECTORY_UNTRUSTED")


def trusted_file(path: Path, *, owner: int | None = None, group: int | None = None,
                 mode: int = 0o600, max_size: int | None = None) -> bytes:
    if owner is None:
        owner = os.geteuid()
    if group is None:
        group = os.getegid()
    try:
        parent = path.parent.lstat()
        if (not stat.S_ISDIR(parent.st_mode) or parent.st_uid != owner or
                parent.st_gid != group or parent.st_mode & 0o022):
            raise Stop("PARENT_UNTRUSTED")
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        with os.fdopen(fd, "rb") as handle:
            before = os.fstat(handle.fileno())
            if (not stat.S_ISREG(before.st_mode) or before.st_uid != owner or
                    before.st_gid != group or before.st_nlink != 1 or
                    stat.S_IMODE(before.st_mode) != mode):
                raise Stop("FILE_UNTRUSTED")
            if max_size is not None and before.st_size > max_size:
                raise Stop("FILE_TOO_LARGE")
            data = handle.read((max_size + 1) if max_size is not None else -1)
            after = os.fstat(handle.fileno())
            if (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) != (
                    after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns):
                raise Stop("FILE_CHANGED")
            if len(data) != before.st_size:
                raise Stop("FILE_CHANGED")
            return data
    except OSError:
        raise Stop("FILE_UNAVAILABLE") from None


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
        directory = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(directory)
        finally:
            os.close(directory)
    finally:
        try:
            os.unlink(temporary)
        except FileNotFoundError:
            pass


def load_readiness(path: Path):
    spec = importlib.util.spec_from_file_location("qa525_readiness", path)
    if spec is None or spec.loader is None:
        raise Stop("READINESS_IMPORT")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def download_artifact(url: str, token: str, destination: Path) -> None:
    prefix = "https://api.github.com/repos/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/artifacts/"
    if not url.startswith(prefix) or not url.endswith("/zip"):
        raise Stop("ARTIFACT_URL_INVALID")
    request = Request(url, headers={
        "Accept": "application/vnd.github+json",
        "Authorization": "Bearer " + token,
        "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "centenario-qa525-sync-v2",
    })
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
        with destination.open("xb") as handle:
            os.chmod(destination, 0o600)
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
    except (HTTPError, URLError, OSError):
        raise Stop("ARTIFACT_DOWNLOAD_FAILED") from None


def sync(args) -> dict:
    require_root("SYNC_REQUIRES_ROOT_GLM DCH".replace(" ", ""))
    if socket.gethostname().split(".", 1)[0] != "glmdch":
        raise Stop("SYNC_REQUIRES_ROOT_GLMDCH")
    readiness = load_readiness(args.readiness)
    token = readiness._read_qa_pat()
    if token is None:
        raise Stop("PAT_REQUIRED_NO_ANONYMOUS_FALLBACK")
    result = readiness.evaluate_latest_validated(readiness.fetch_public)
    app_sha = result.get("source_sha")
    artifact_digest = result.get("artifact_sha256")
    artifact_id = result.get("artifact_id")
    if not isinstance(app_sha, str) or not SHA.fullmatch(app_sha):
        raise Stop("APP_SHA_INVALID")
    if not isinstance(artifact_digest, str) or not DIGEST.fullmatch(artifact_digest):
        raise Stop("ARTIFACT_DIGEST_INVALID")
    if type(artifact_id) is not int:
        raise Stop("ARTIFACT_ID_INVALID")
    args.cache_root.mkdir(mode=0o700, parents=True, exist_ok=True)
    try:
        require_private_dir(args.cache_root, exact=True)
    except Stop:
        raise Stop("CACHE_ROOT_UNTRUSTED") from None
    final_target = args.cache_root / app_sha
    if final_target.exists() or final_target.is_symlink():
        raise Stop("CACHE_ALREADY_EXISTS")
    target = Path(tempfile.mkdtemp(prefix="." + app_sha + ".sync-", dir=args.cache_root))
    os.chmod(target, 0o700)
    try:
        require_private_dir(target, exact=True)
    except Stop:
        raise Stop("CACHE_DIR_UNTRUSTED") from None
    temporary = target / ".artifact.zip.partial"
    try:
        download_artifact(readiness.API + "/actions/artifacts/%d/zip" % artifact_id, token, temporary)
        actual = sha256_file(temporary)
        if actual != artifact_digest:
            raise Stop("ARTIFACT_DIGEST_MISMATCH")
        os.replace(temporary, target / "artifact.zip")
        staging = load_readiness(args.staging)
        try:
            staged = staging.stage(target / "artifact.zip", actual, app_sha,
                                   target / "package", tokenless=True)
        except Exception:
            raise Stop("SIGNED_STAGING_FAILED") from None
        if staged.get("status") != "SIGNED_CODE_STAGED_NOT_INSTALLED":
            raise Stop("SIGNED_STAGING_INVALID")
        synced = now_utc()
        receipt = {
            "schema": "qa525-cache-v2",
            "status": "SYNCED_ATTESTED",
            "ops_source_sha": args.ops_sha,
            "app_source_sha": app_sha,
            "artifact_id": artifact_id,
            "artifact_sha256": actual,
            "silo_workflow_run": result["silo_workflow_run"],
            "gates": result["gates"],
            "synced_at": format_time(synced),
            "cache_expires_at": format_time(synced + dt.timedelta(hours=args.ttl_hours)),
            "network_allowed_after_sync": False,
        }
        atomic_write(target / "cache.json", (json.dumps(receipt, sort_keys=True) + "\n").encode())
        os.rename(target, final_target)
        directory = os.open(args.cache_root, os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(directory)
        finally:
            os.close(directory)
        return receipt
    except Exception:
        shutil.rmtree(target, ignore_errors=True)
        raise


def load_json_trusted(path: Path) -> dict:
    raw = trusted_file(path, max_size=1024 * 1024)
    try:
        value = json.loads(raw)
    except (UnicodeDecodeError, json.JSONDecodeError):
        raise Stop("JSON_INVALID") from None
    if type(value) is not dict:
        raise Stop("JSON_INVALID")
    return value


def admit(args) -> dict:
    require_root("ADMISSION_REQUIRES_ROOT")
    if not SHA.fullmatch(args.app_sha) or not SHA.fullmatch(args.ops_sha) or not NONCE.fullmatch(args.nonce):
        raise Stop("IDENTITY_INVALID")
    cache_dir = args.cache_root / args.app_sha
    try:
        require_private_dir(args.cache_root, exact=True)
        require_private_dir(cache_dir, exact=True)
        require_private_dir(cache_dir / "package", exact=True)
    except Stop:
        raise Stop("CACHE_DIRECTORY_UNTRUSTED") from None
    receipt = load_json_trusted(cache_dir / "cache.json")
    artifact = trusted_file(cache_dir / "artifact.zip", max_size=MAX_ARTIFACT)
    del artifact
    expected = {
        "schema": "qa525-cache-v2", "status": "SYNCED_ATTESTED",
        "app_source_sha": args.app_sha, "ops_source_sha": args.ops_sha,
        "network_allowed_after_sync": False,
    }
    if any(receipt.get(key) != value for key, value in expected.items()):
        raise Stop("CACHE_BINDING_INVALID")
    if sha256_file(cache_dir / "artifact.zip") != receipt.get("artifact_sha256"):
        raise Stop("CACHE_ARTIFACT_CHANGED")
    staging = load_readiness(args.staging)
    try:
        staging.verify_manifest(cache_dir / "package", args.app_sha)
    except Exception:
        raise Stop("CACHE_PACKAGE_INVALID") from None
    current = now_utc()
    if parse_time(receipt.get("cache_expires_at")) <= current:
        raise Stop("CACHE_EXPIRED")
    g6 = load_json_trusted(args.g6_receipt)
    if g6.get("g6_offhost_sha256_verified") is not True:
        raise Stop("G6_NOT_VERIFIED")
    g6_expiry = parse_time(g6.get("expires_at"))
    if (g6_expiry - current).total_seconds() < args.g6_margin_seconds:
        raise Stop("G6_MARGIN_LOW")
    approval = load_json_trusted(args.approval)
    if (approval.get("status") != "APPROVED" or approval.get("nonce") != args.nonce or
            approval.get("app_source_sha") != args.app_sha or
            approval.get("ops_source_sha") != args.ops_sha):
        raise Stop("APPROVAL_BINDING_INVALID")
    if parse_time(approval.get("expires_at")) <= current:
        raise Stop("APPROVAL_EXPIRED")
    nonce_marker = args.state_dir / ("nonce-consumed-" + args.nonce + ".json")
    if nonce_marker.exists() or nonce_marker.is_symlink():
        raise Stop("NONCE_REPLAY")
    admitted = {
        "schema": "qa525-admission-v2", "status": "ADMITTED",
        "ops_source_sha": args.ops_sha, "app_source_sha": args.app_sha,
        "artifact_sha256": receipt["artifact_sha256"], "nonce": args.nonce,
        "admitted_at": format_time(current), "g6_expires_at": format_time(g6_expiry),
        "network_allowed": False,
    }
    if args.output.parent != args.state_dir:
        raise Stop("OUTPUT_OUTSIDE_STATE_DIR")
    if args.state_dir.exists() or args.state_dir.is_symlink():
        require_private_dir(args.state_dir, exact=True)
    else:
        args.state_dir.mkdir(mode=0o700, parents=True)
        require_private_dir(args.state_dir, exact=True)
    atomic_write(args.output, (json.dumps(admitted, sort_keys=True) + "\n").encode())
    atomic_write(nonce_marker, (json.dumps({"status": "CONSUMED", "nonce": args.nonce,
                                           "app_source_sha": args.app_sha}, sort_keys=True) + "\n").encode())
    return admitted


def postcheck(args) -> dict:
    """Validate APPLIED state without requiring prepared.json to be absent."""
    require_root("POSTCHECK_REQUIRES_ROOT")
    if not SHA.fullmatch(args.app_sha):
        raise Stop("APP_SHA_INVALID")
    journal = load_json_trusted(args.journal)
    launcher = load_json_trusted(args.launcher_status)
    runtime = load_json_trusted(args.runtime)
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
            launcher.get("services_running") is not True or
            launcher.get("may_apply") is not False or
            launcher.get("archive_reconciliation_required") is not False):
        raise Stop("LAUNCHER_NOT_CLOSED")
    prepared_exists = launcher.get("prepared_exists") is True
    if prepared_exists:
        prepared = load_json_trusted(args.prepared)
        bound_sha = prepared.get("source_sha", prepared.get("app_source_sha"))
        if bound_sha != args.app_sha:
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
    return {
        "schema": "qa525-postcheck-v2", "status": "PASS_APPLIED",
        "app_source_sha": args.app_sha,
        "prepared_retained": prepared_exists,
        "may_apply": False,
    }


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser()
    sub = root.add_subparsers(dest="command", required=True)
    s = sub.add_parser("sync")
    s.add_argument("--ops-sha", required=True, type=lambda v: v if SHA.fullmatch(v) else (_ for _ in ()).throw(argparse.ArgumentTypeError("sha")))
    s.add_argument("--readiness", type=Path, default=Path(__file__).with_name("qa-agent-readiness.py"))
    s.add_argument("--staging", type=Path, default=Path(__file__).with_name("qa-signed-staging.py"))
    s.add_argument("--cache-root", type=Path, default=DEFAULT_CACHE_ROOT)
    s.add_argument("--ttl-hours", type=int, default=6, choices=range(1, 25))
    a = sub.add_parser("admit")
    a.add_argument("--ops-sha", required=True)
    a.add_argument("--app-sha", required=True)
    a.add_argument("--nonce", required=True)
    a.add_argument("--cache-root", type=Path, default=DEFAULT_CACHE_ROOT)
    a.add_argument("--g6-receipt", type=Path, required=True)
    a.add_argument("--approval", type=Path, required=True)
    a.add_argument("--staging", type=Path, default=Path(__file__).with_name("qa-signed-staging.py"))
    a.add_argument("--state-dir", type=Path, required=True)
    a.add_argument("--output", type=Path, required=True)
    a.add_argument("--g6-margin-seconds", type=int, default=MIN_G6_MARGIN)
    p = sub.add_parser("postcheck")
    p.add_argument("--app-sha", required=True)
    p.add_argument("--journal", type=Path, required=True)
    p.add_argument("--prepared", type=Path, required=True)
    p.add_argument("--launcher-status", type=Path, required=True)
    p.add_argument("--runtime", type=Path, required=True)
    return root


def main(argv=None) -> int:
    args = parser().parse_args(argv)
    try:
        if args.command == "sync":
            result = sync(args)
        elif args.command == "admit":
            result = admit(args)
        else:
            result = postcheck(args)
        print(json.dumps(result, sort_keys=True))
        return 0
    except Stop as exc:
        print(json.dumps({"status": "STOP", "code": str(exc)}, sort_keys=True))
        return 2


if __name__ == "__main__":
    sys.exit(main())
