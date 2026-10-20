#!/usr/bin/env python3
"""Operational local WEB-only installer. No host credentials or remote commands.

First --bootstrap must be run by the Sponsor on glmdch. Subsequent --apply uses
the authenticated installed baseline. Release metadata is never sufficient:
signature, manifest, exact dev gates, backup and public health are reverified.
"""
import argparse
from dataclasses import asdict
from datetime import datetime, timedelta, timezone
import hashlib
import importlib.util
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

ROOT = Path(__file__).resolve().parent
STATE = Path("/opt/centenario/qa-auto-lite")
FIRST_IMAGE = "sha256:25c51921f26ebac2734fc5b53fc0c9a3553fc6801d86d1fd455ae10053127696"
BACKUP = Path("/home/soporte/centenario-qa-g6-CIFRADO-20261019T175218Z.tar.gz")
BACKUP_SHA = "bed4ee196074a1919234da44c6bbd66c8a7cd16bdce70fba97ed1e34df8f9cae"
BACKUP_TIME = datetime(2026, 10, 19, 17, 52, 18, tzinfo=timezone.utc)
OFFHOST_FILE = "1Sl_qrcJDZIHEMByngwAUrCrewguWVhT1"  # independently verified in Drive


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / filename)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


adapter = module("installer_adapter", "qa-lite-compose-adapter.py")
tx = module("installer_transaction", "qa-lite-web-transaction.py")
builder = module("installer_builder", "qa-lite-web-build.py")
ready = module("installer_readiness", "qa-agent-readiness.py")
staging = module("installer_signed_staging", "qa-signed-staging.py")
public_download = module("installer_public_download", "qa-public-release-download.py")
health = module("installer_public_health", "check-public-readiness.py")


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_json(path, value):
    path = Path(path)
    with tempfile.NamedTemporaryFile(mode="w", dir=path.parent, prefix=".atomic-", delete=False) as stream:
        temporary = Path(stream.name)
        try:
            os.fchmod(stream.fileno(), 0o600)
            json.dump(value, stream, sort_keys=True)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        except Exception:
            temporary.unlink(missing_ok=True)
            raise
    os.replace(temporary, path)


def read_json(path):
    path = adapter.trusted_path(path)
    adapter.require(path.stat().st_mode & 0o777 == 0o600, "STATE_FILE_NOT_PRIVATE")
    return json.loads(path.read_bytes())


def backup_verified(now=None):
    # Pin comes from the binary independently downloaded and hashed from Drive,
    # not from a receipt supplied by the release or an unverified JSON flag.
    now = now or datetime.now(timezone.utc)
    adapter.require(timedelta(0) <= now - BACKUP_TIME <= timedelta(hours=48),
                    "OFFHOST_BACKUP_EXPIRED_RENEW_BEFORE_UPDATE")
    fd = os.open(BACKUP, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, "rb") as stream:
        info = os.fstat(stream.fileno())
        adapter.require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1,
                        "BACKUP_NOT_REGULAR")
        adapter.require(hashlib.file_digest(stream, "sha256").hexdigest() == BACKUP_SHA,
                        "BACKUP_CIPHERTEXT_CHANGED")
    return True


def public_health(expected_sha=None):
    result = health.inspect(True)
    if result.get("decision") != "PUBLIC_ENTRY_CHECKS_PASS_ONLY":
        return False
    try:
        web, _, web_body = health.request("/health/web")
        api, _, api_body = health.request("/health/ready")
        api_status = json.loads(api_body) if api == 200 else {}
        if not (web == api == 200 and web_body.strip() == b"ok"
                and api_status == {"status": "ready", "database": "postgresql"}):
            return False
        if expected_sha is not None:
            status, _, data = health.request("/qa-version.json")
            return status == 200 and json.loads(data) == {"environment": "QA_SYNTHETIC", "source_sha": expected_sha}
        return True
    except Exception:
        return False


def binding_for(bootstrap):
    # Physical Compose has resolved environment inline; an explicit EMPTY env
    # prevents accidental .env discovery. Existing JSON is never rewritten.
    builder.build_inputs()
    env = STATE / "empty.env"
    if not env.exists():
        fd = os.open(env, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        os.close(fd)
    adapter.require(adapter.trusted_path(env).read_bytes() == b"", "EMPTY_ENV_CHANGED")
    config = json.loads(builder.COMPOSE.read_bytes())
    files = {str(builder.COMPOSE): builder.COMPOSE_SHA256, str(env): digest(env)}
    for service in config["services"].values():
        adapter.require(not service.get("env_file"), "UNEXPECTED_RUNTIME_ENV_FILE")
        for volume in service.get("volumes", []):
            if isinstance(volume, dict) and volume.get("type") == "bind":
                source = adapter.trusted_path(volume["source"])
                files[str(source)] = digest(source)
    return adapter.Binding(str(builder.COMPOSE.parent), (str(builder.COMPOSE),), str(env),
                           tuple(sorted(files.items())), str(STATE), FIRST_IMAGE if bootstrap else None)


def verified_candidate(selection):
    sha, expected, _ = public_download.identity(selection)
    cache = STATE / "releases"
    cache.mkdir(mode=0o700, exist_ok=True)
    adapter.trusted_path(cache, directory=True)
    archive = cache / (sha + ".zip")
    if not archive.exists():
        for attempt in range(41):
            try:
                public_download.download(selection, archive)
                break
            except public_download.Blocked as error:
                if str(error) != "SIGNED_PUBLIC_RELEASE_NOT_READY" or attempt == 40:
                    raise
                print("QA_WAITING_FOR_VERIFIED_PUBLICATION", flush=True)
                time.sleep(30)
    adapter.require("sha256:" + digest(adapter.trusted_path(archive)) == expected,
                    "CACHED_ENVELOPE_CHANGED")
    package = cache / sha
    if not package.exists():
        staging.stage(archive, expected, sha, package, tokenless=True)

    def verify_source(current, current_sha):
        adapter.require(current == package and current_sha == sha, "SOURCE_BINDING_MISMATCH")
        # Recheck the actual cryptographic envelope, independently of local state.
        with tempfile.TemporaryDirectory(prefix=".reverify-", dir=STATE) as tmp:
            checked = Path(tmp) / "package"
            staging.stage(archive, expected, sha, checked, tokenless=True)
            adapter.require((checked / "MANIFEST.sha256").read_bytes() ==
                            (package / "MANIFEST.sha256").read_bytes(), "MANIFEST_BINDING_CHANGED")
        staging.verify_manifest(package, sha)
        return True
    return package, verify_source


def selection_still_valid(selection):
    sha = selection["source_sha"]
    tip = ready.fetch_public("/branches/dev").get("commit", {}).get("sha")
    adapter.require(isinstance(tip, str) and adapter.SHA.fullmatch(tip), "DEV_SHA_MISSING")
    if tip != sha:
        comparison = ready.fetch_public("/compare/" + sha + "..." + tip)
        adapter.require(comparison.get("status") == "ahead" and
                        comparison.get("merge_base_commit", {}).get("sha") == sha,
                        "CANDIDATE_NO_LONGER_ON_DEV")
    def pinned(path):
        return {"commit": {"sha": sha}} if path == "/branches/dev" else ready.fetch_public(path)
    adapter.require(ready.evaluate(pinned) == selection, "CANDIDATE_GATES_OR_ARTIFACT_CHANGED")
    return True


def check_ancestry(previous_sha, candidate_sha):
    if previous_sha is None:  # permitted ONLY by explicit first bootstrap path
        return True
    comparison = ready.fetch_public("/compare/" + previous_sha + "..." + candidate_sha)
    adapter.require(comparison.get("status") == "ahead" and
                    comparison.get("merge_base_commit", {}).get("sha") == previous_sha,
                    "DEV_NOT_FAST_FORWARD")
    return True


def recover(binding):
    journal = read_json(STATE / "pending.json")
    plan = tx.WebReleasePlan(**journal["plan"])
    try:
        current = adapter.metadata()["web"][1]
    except adapter.Blocked as error:
        if str(error) != "WEB_NOT_FOUND":
            raise
        current = None  # interrupted recreation may leave no web container
    adapter.require(current in (None, plan.previous_image_id, plan.candidate_image_id),
                    "UNKNOWN_IMAGE_MANUAL_RECOVERY_REQUIRED")
    runner = adapter.ComposeWebAdapter(binding, lambda p: False, lambda: public_health(plan.previous_sha))
    with runner.exclusive():
        runner.plan = plan
        runner.dependencies = journal["dependencies"]
        runner.web_boundary = tuple(journal["web_boundary"])
        runner.restore_previous_web(plan)
        adapter.require(runner.verify_running_web(plan.previous_image_id)
                        and runner.verify_untouched_dependencies() and public_health(plan.previous_sha),
                        "RECOVERY_NOT_VERIFIED")
        # If a crash followed the installed-state write, restore the old marker.
        if journal["previous_installed"] is None:
            (STATE / "installed.json").unlink(missing_ok=True)
        else:
            write_json(STATE / "installed.json", journal["previous_installed"])
        (STATE / "pending.json").unlink()
    return {"status": "PREVIOUS_WEB_RESTORED", "source_sha": plan.previous_sha}


def install(bootstrap=False):
    adapter.require(os.geteuid() == 0 and socket.gethostname() == "glmdch", "HOST_NOT_AUTHORIZED")
    os.umask(0o077)
    STATE.mkdir(mode=0o700, exist_ok=True)
    adapter.trusted_path(STATE, directory=True)
    adapter.require(not (STATE / "paused").exists(), "QA_UPDATES_PAUSED")
    adapter.require(not (STATE / "pending.json").exists(), "INTERRUPTED_UPDATE_USE_RECOVER")
    installed_path = STATE / "installed.json"
    previous = read_json(installed_path) if installed_path.exists() else None
    if previous is not None:
        adapter.require(isinstance(previous.get("source_sha"), str)
                        and adapter.SHA.fullmatch(previous["source_sha"])
                        and isinstance(previous.get("web_image_id"), str)
                        and adapter.IMAGE.fullmatch(previous["web_image_id"]),
                        "INSTALLED_BASELINE_INVALID")
    adapter.require((bootstrap and previous is None) or (not bootstrap and previous is not None),
                    "FIRST_INSTALL_REQUIRES_SUPERVISED_BOOTSTRAP")
    selection = ready.evaluate_latest_validated(ready.fetch_public)
    if previous and previous["source_sha"] == selection["source_sha"]:
        adapter.require(adapter.metadata()["web"][1] == previous["web_image_id"], "BASELINE_IMAGE_CHANGED")
        return {"status": "QA_WEB_UP_TO_DATE", "source_sha": selection["source_sha"]}
    print("QA_CHECKING_BACKUP_AND_PUBLIC_HEALTH", flush=True)
    backup_verified()
    adapter.require(public_health(), "PUBLIC_HEALTH_NOT_READY_BEFORE_UPDATE")
    binding = binding_for(bootstrap)
    old_image = FIRST_IMAGE if bootstrap else previous["web_image_id"]
    old_sha = None if bootstrap else previous["source_sha"]
    check_ancestry(old_sha, selection["source_sha"])
    print("QA_VERIFYING_SIGNED_RELEASE " + selection["source_sha"], flush=True)
    package, source_verifier = verified_candidate(selection)
    runner = adapter.ComposeWebAdapter(binding, lambda p: False, public_health)
    with runner.exclusive():
        adapter.require(adapter.metadata()["web"][1] == old_image, "PHYSICAL_IMAGE_CHANGED")
        print("QA_BUILDING_WEB_IMAGE_ONLY", flush=True)
        prepared = builder.prepare_web(package, selection["source_sha"], source_verifier)
        plan = tx.WebReleasePlan(adapter.PROJECT, old_sha, selection["source_sha"],
                                 old_image, prepared["web_image_id"], BACKUP_SHA)
        def versioned_health():
            image = adapter.metadata()["web"][1]
            expected = plan.candidate_sha if image == plan.candidate_image_id else plan.previous_sha
            return public_health(expected)
        runner.public_health = versioned_health
        checked_at = [None]
        def evidence(current_plan):
            adapter.require(current_plan == plan, "PLAN_CHANGED")
            # One fresh metadata verification immediately before apply, shared
            # across the coordinator's consecutive preflight calls for <=60s.
            # The actual signature and full manifest are checked EVERY call.
            if checked_at[0] is None or time.monotonic() - checked_at[0] > 60:
                selection_still_valid(selection)
                checked_at[0] = time.monotonic()
            return backup_verified() and check_ancestry(old_sha, plan.candidate_sha) and source_verifier(package, plan.candidate_sha)
        runner.verify_evidence = evidence
        runner.reverify(plan)
        write_json(STATE / "pending.json", {"plan": asdict(plan),
                   "dependencies": runner.dependencies, "web_boundary": runner.web_boundary,
                   "previous_installed": previous})
        try:
            print("QA_APPLYING_WEB_WITH_ROLLBACK", flush=True)
            result = tx.coordinate(plan, runner)
        except tx.Blocked as error:
            if str(error) == "APPLY_FAILED_ROLLBACK_VERIFIED":
                (STATE / "pending.json").unlink()
            raise
        write_json(installed_path, {"source_sha": plan.candidate_sha,
                   "web_image_id": plan.candidate_image_id, "backup_sha256": BACKUP_SHA,
                   "checked_at_utc": datetime.now(timezone.utc).isoformat(),
                   "previous_source_sha": old_sha, "previous_image_id": old_image})
        (STATE / "pending.json").unlink()
        return result


def enable_timer():
    adapter.require((STATE / "installed.json").exists(), "NO_INSTALLED_BASELINE")
    code = adapter.trusted_path(ROOT, directory=True)
    service = ("[Unit]\nDescription=Centenario synthetic QA WEB updates\nAfter=docker.service network-online.target\n"
               "[Service]\nType=oneshot\nUser=root\nUMask=0077\nTimeoutStartSec=1200\n"
               "ExecStart=/usr/bin/python3 " + str(code / "qa-lite-install.py") + " --apply\n"
               "NoNewPrivileges=true\nPrivateTmp=true\n"
               "UnsetEnvironment=GH_TOKEN GITHUB_TOKEN GH_ENTERPRISE_TOKEN GITHUB_ENTERPRISE_TOKEN\n")
    timer = ("[Unit]\nDescription=Check signed Centenario QA every 15 minutes\n[Timer]\n"
             "OnBootSec=2min\nOnUnitInactiveSec=15min\nRandomizedDelaySec=30\nPersistent=true\n"
             "[Install]\nWantedBy=timers.target\n")
    for filename, contents in (("pmgm-qa-lite.service", service), ("pmgm-qa-lite.timer", timer)):
        path = Path("/etc/systemd/system") / filename
        adapter.require(not path.exists() or path.read_text() == contents, "EXISTING_UNIT_DIFFERS")
        path.write_text(contents)
        path.chmod(0o644)
    subprocess.run(["/usr/bin/systemctl", "daemon-reload"], check=True)
    subprocess.run(["/usr/bin/systemctl", "enable", "--now", "pmgm-qa-lite.timer"], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--bootstrap", action="store_true")
    group.add_argument("--apply", action="store_true")
    group.add_argument("--recover", action="store_true")
    parser.add_argument("--enable-timer", action="store_true")
    args = parser.parse_args()
    try:
        if args.recover:
            adapter.require(os.geteuid() == 0 and socket.gethostname() == "glmdch", "HOST_NOT_AUTHORIZED")
            result = recover(binding_for(False))
        else:
            result = install(args.bootstrap)
            if args.enable_timer:
                enable_timer()
        print(json.dumps(result, sort_keys=True))
        return 0
    except Exception as error:
        types = (adapter.Blocked, tx.Blocked, builder.transport.Blocked,
                 ready.GateFailure, staging.Blocked, public_download.Blocked)
        code = str(error) if isinstance(error, types) and re.fullmatch(r"[A-Z0-9_]{1,100}", str(error)) else "INSTALLATION_CHECK_FAILED"
        print(json.dumps({"status": "BLOCKED_QA_WEB_UPDATE", "code": code}))
        return 1


if __name__ == "__main__":
    sys.exit(main())
