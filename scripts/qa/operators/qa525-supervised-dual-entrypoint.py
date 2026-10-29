#!/usr/bin/env python3
"""QA525 local supervised activation of prebuilt API+WEB images, no staging CLI.

Not an unattended agent: runs only as root on glmdch, holds all QA locks,
re-verifies HEAD+release/Sigstore+image identities, requires a separate
root-owned security-review attestation and a time-limited single-use human
approval. G6 report is checked locally; external Drive verification is
attested by the reviewer, not cryptographically performed by this host.
"""
from __future__ import annotations

from contextlib import contextmanager
import copy
import fcntl
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
import time
from urllib.request import build_opener, HTTPHandler, HTTPSHandler, Request, HTTPRedirectHandler
from urllib.error import URLError

HERE = Path(__file__).resolve().parent
QA_DIR = HERE.parent
SOURCE = "0bd14cb81a1c0e607e04283083f77a5ddf36b339"
BASELINE = "6ff4ae0d092d4214df4ea31af484efbc969fde54"
STATE = Path("/opt/centenario/qa525-signed-prepare")
REVIEW = Path("/etc/pmgm/qa525-sensitive-reviewed.json")
ARCHIVE_RECEIPT = Path("/opt/centenario/qa-g6/archive.receipt.json")
G6_DIR = Path("/opt/centenario/qa-g6")
WORKDIR = Path("/opt/centenario/qa-lab-firstboot/recovery-v2")
REVIEWED = frozenset({"Program.cs", "UserAccounts", "Keycloak/SMTP",
                      "EF/migrations", "Compose/identity"})
MODULES = (
    HERE / "qa525-supervised-dual-transaction.py",
    HERE / "qa525-current-image-preparation.py",
    HERE / "qa525-supervised-cut-approval.py",
    HERE / "qa525-current-change-policy.py",
    QA_DIR / "qa525-stage-current-release.py",
    QA_DIR / "qa-lite-compose-adapter.py",
)
HEALTH_PATHS = ("/", "/health/web", "/health/ready",
                "/identity/realms/pmgm/.well-known/openid-configuration")


class Blocked(Exception):
    pass


def require(value, code):
    if not value:
        raise Blocked(code)


def trusted(path, directory=False):
    p = Path(path)
    require(p.is_absolute(), "SOURCE_NOT_ABSOLUTE")
    for q in (p, *p.parents):
        try:
            st = q.lstat()
        except OSError:
            raise Blocked("ROOT_SOURCE_MISSING") from None
        require(st.st_uid == 0 and not stat.S_ISLNK(st.st_mode)
                and not st.st_mode & 0o022, "ROOT_SOURCE_UNTRUSTED")
    require(p.is_dir() if directory else p.is_file(), "ROOT_SOURCE_WRONG_TYPE")
    return p


def private_json(path, maximum=65536):
    path = trusted(path)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, "rb") as stream:
        st = os.fstat(stream.fileno())
        require(stat.S_ISREG(st.st_mode) and st.st_uid == 0 and st.st_nlink == 1
                and stat.S_IMODE(st.st_mode) == 0o600 and 0 < st.st_size <= maximum,
                "PRIVATE_EVIDENCE_UNTRUSTED")
        data = stream.read(maximum + 1)
        require(len(data) == st.st_size, "PRIVATE_EVIDENCE_CHANGED")
    try:
        return json.loads(data)
    except (ValueError, TypeError):
        raise Blocked("PRIVATE_EVIDENCE_NOT_JSON") from None


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def approved_review(review, source, policy, now):
    """Review claims are necessary, never a substitute for manual QA signoff."""
    required = {"source_sha", "release_asset_sha256", "baseline_sha",
                "reviewed_sections", "sensitive_reasons",
                "no_new_ef_migrations", "runtime_migration_flags_disabled",
                "legacy_master_and_smtp_preserved", "reviewed_at",
                "g6_archive_name", "g6_archive_sha256", "g6_drive_file_id",
                "g6_offhost_sha256_verified", "g6_restore_report_name"}
    require(type(review) is dict and set(review) == required,
            "SENSITIVE_REVIEW_FIELDS_INVALID")
    require(review["source_sha"] == SOURCE == source["source_sha"]
            and review["release_asset_sha256"] == source["release_asset_sha256"]
            and review["baseline_sha"] == BASELINE
            and set(review["reviewed_sections"]) == REVIEWED
            and sorted(review["sensitive_reasons"]) == sorted(policy["reasons"])
            and policy["status"] == "BLOCKED_REVIEW_REQUIRED"
            and review["no_new_ef_migrations"] is True
            and review["runtime_migration_flags_disabled"] is True
            and review["legacy_master_and_smtp_preserved"] is True
            and review["g6_offhost_sha256_verified"] is True,
            "SENSITIVE_REVIEW_NOT_CURRENT_OR_INCOMPLETE")
    issued = review.get("reviewed_at")
    require(type(issued) is int and 0 <= now - issued <= 1800,
            "SENSITIVE_REVIEW_TOO_OLD")
    require(type(review["g6_archive_name"]) is str
            and re.fullmatch(r"centenario-qa-g6-CIFRADO-[0-9]{8}T[0-9]{6}Z\.tar\.gz",
                             review["g6_archive_name"])
            and type(review["g6_archive_sha256"]) is str
            and re.fullmatch(r"[a-f0-9]{64}", review["g6_archive_sha256"])
            and type(review["g6_drive_file_id"]) is str
            and re.fullmatch(r"[A-Za-z0-9_-]{20,100}", review["g6_drive_file_id"])
            and type(review["g6_restore_report_name"]) is str
            and re.fullmatch(r"isolated-restore-[0-9]{8}T[0-9]{6}Z\.report\.json",
                             review["g6_restore_report_name"]),
            "G6_REVIEW_REFERENCE_INVALID")


def validate_g6(review, now):
    """Check two databases+Silo restored and local archive SHA; NOT remote Drive."""
    name = review["g6_archive_name"]
    recorded = review["g6_archive_sha256"]
    archive = Path("/home/soporte") / name
    require(archive.is_file() and not archive.is_symlink(), "G6_LOCAL_ARCHIVE_MISSING")
    require(archive.stat().st_size > 0, "G6_LOCAL_ARCHIVE_EMPTY")
    with archive.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    require(digest == recorded, "G6_LOCAL_ARCHIVE_HASH_CHANGED")
    receipt = private_json(ARCHIVE_RECEIPT)
    require(receipt.get("archive_name") == name and receipt.get("sha256") == recorded
            and receipt.get("status") == "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING",
            "G6_LOCAL_RECEIPT_MISMATCH")
    restore = private_json(G6_DIR / review["g6_restore_report_name"])
    tables = restore.get("postgres_tables_by_database")
    require(restore.get("result") ==
            "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY"
            and restore.get("archive_filename") == name
            and restore.get("archive_sha256") == recorded
            and type(tables) is dict and set(tables) == {"pmgm", "pmgm_keycloak"}
            and all(type(v) is int and v > 0 for v in tables.values())
            and type(restore.get("silo_objects_verified")) is int
            and restore.get("production_touched") is False,
            "G6_RESTORATION_UNVERIFIED")
    # G6 checkpoint maximum age is 48 hours for this QA apply.
    timestamp = time.strptime(name[22:37], "%Y%m%dT%H%M%S")
    age = now - int(__import__("calendar").timegm(timestamp))
    require(0 <= age < 48 * 3600, "G6_CHECKPOINT_TOO_OLD")
    return True


def scan_migrations(policy, fetch):
    """No EF migration/DbContext changes may be silently applied."""
    comparison = fetch("/compare/" + BASELINE + "..." + SOURCE)
    require(comparison.get("status") == "ahead"
            and type(comparison.get("files")) is list
            and 0 < len(comparison["files"]) < 300,
            "MIGRATION_DIFF_INCOMPLETE")
    paths = set()
    for row in comparison["files"]:
        require(type(row) is dict and type(row.get("filename")) is str,
                "MIGRATION_DIFF_INVALID")
        paths.add(row["filename"].lower())
        if row.get("status") == "renamed":
            require(type(row.get("previous_filename")) is str,
                    "RENAMED_MIGRATION_UNKNOWN")
            paths.add(row["previous_filename"].lower())
    require(not any("/migrations/" in p or "dbcontext" in p for p in paths),
            "EF_MIGRATION_OR_DBCONTEXT_CHANGE_REQUIRES_SEPARATE_PLAN")
    return True


def disable_redirects():
    class NoRedirect(HTTPRedirectHandler):
        def redirect_request(self, req, fp, code, msg, headers, newurl):
            return None
    return build_opener(HTTPHandler(), HTTPSHandler(), NoRedirect())


def health():
    opener = disable_redirects()
    try:
        for path in HEALTH_PATHS:
            req = Request("https://sistema.granlogiamixta.cl" + path,
                          headers={"User-Agent": "PMGM-QA-supervised-smoke"})
            with opener.open(req, timeout=12) as res:
                if res.status != 200:
                    return False
                res.read(2048)
        return True
    except (URLError, OSError, TimeoutError):
        return False


def main():
    require(sys.argv[1:] == ["--apply-supervised"],
            "ONLY_EXPLICIT_SUPERVISED_APPLY_ALLOWED")
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "ONLY_GLM DCH_ROOT_QA".replace(" ", "_"))
    for path in (Path(__file__), *MODULES):
        trusted(path)
    trusted(STATE, directory=True)
    trusted(G6_DIR, directory=True)
    require(stat.S_IMODE(STATE.stat().st_mode) == 0o700,
            "STATE_DIR_MODE_INVALID")
    core = load("qa525_supervised_dual_core", MODULES[0])
    build = load("qa525_supervised_prepared", MODULES[1])
    approval = load("qa525_supervised_local_approval", MODULES[2])
    policy = load("qa525_supervised_change_policy", MODULES[3])
    signed = load("qa525_supervised_signed_stage", MODULES[4])
    adapter = load("qa525_supervised_transport", MODULES[5])
    with build.exclusive():
        # Existing root-only prepared images were built under exact-SHA attestation.
        prepared = private_json(STATE / "prepared.json")
        review = private_json(REVIEW, maximum=8192)
        source = signed.gate.evaluate(signed.gate.readiness.fetch_public)
        require(source["source_sha"] == SOURCE, "SIGNATURE_RELEASE_TARGET_DRIFT")
        comparison = policy.evaluate(signed.gate.readiness.fetch_public, BASELINE)
        scan_migrations(policy, signed.gate.readiness.fetch_public)
        now = int(time.time())
        approved_review(review, source, comparison, now)
        approve = approval.read_root_approval()
        approved = approval.validate(approve, source, prepared, now)
        require(approved["status"] == "LOCAL_SUPERVISED_CUT_MATCH_NO_DEPLOYMENT_AUTHORITY",
                "SUPERVISOR_CUT_MISMATCH")
        validate_g6(review, now)
        # Re-download exact public release and REVALIDATE Sigstore signature on
        # the physical host; prepared.json flags alone are never authority.
        with tempfile.TemporaryDirectory(prefix=".qa525-supervised-signed-", dir=STATE) as d:
            result = signed.verify_current(Path(d) / "signed")
            require(result["source_sha"] == SOURCE and
                    result["release_asset_sha256"] == source["release_asset_sha256"],
                    "PHYSICAL_SIGNATURE_REVERIFICATION_FAILED")
        for name in core.TARGETS:
            image = prepared.get(name + "_image_id")
            require(type(image) is str and core.IMAGE.fullmatch(image)
                    and adapter.docker(["image", "inspect", "--format", "{{.Id}}", image]) == image
                    and adapter.docker(["image", "inspect", "--format",
                         '{{index .Config.Labels "org.opencontainers.image.revision"}}',
                         image]) == SOURCE,
                    "PREPARED_IMAGE_INVALID")
        # Exact baseline and all approved Compose layers are checked at runtime.
        original = adapter.metadata()
        require(set(original) == set(core.TARGETS + core.DEPENDENCIES + core.ONESHOTS),
                "UNEXPECTED_QA_SERVICE_SET")
        layers = core.normalized_layers(
            {name: (original[name][7] or "").split(",") for name in core.TARGETS},
            WORKDIR)
        def snapshot():
            records = adapter.metadata()
            require(set(records) == set(original), "SERVICE_SET_CHANGED")
            answer = {}
            for name, row in records.items():
                require(type(row[0]) is str and core.HASH.fullmatch(row[0])
                        and type(row[1]) is str and core.IMAGE.fullmatch(row[1]),
                        "INVALID_DOCKER_FINGERPRINT")
                answer[name] = {"id": row[0], "image": row[1],
                                "status": row[2]}
                if name in core.ONESHOTS:
                    answer[name]["exit_code"] = row[11]
                if name in core.TARGETS:
                    # Verify the same volumes, bindings, ports and network IDs
                    # even when container IDs must change during recreation.
                    baseline_row = original[name]
                    binding = lambda x: (x[8],
                                         {k: v.get("NetworkID") for k, v in x[9].items()},
                                         x[10])
                    require(binding(row) == binding(baseline_row),
                            "API_OR_WEB_RUNTIME_BOUNDARY_CHANGED")
            return answer
        before = snapshot()
        frozen = build.snapshot(adapter.docker)
        require(prepared.get("baseline") == frozen
                and all(before[n]["id"] == frozen[n]["container_fingerprint"]
                        and before[n]["image"] == frozen[n]["image_id"]
                        for n in core.TARGETS + core.DEPENDENCIES),
                "PREPARED_BASELINE_DRIFT")
        new = {name: prepared[name + "_image_id"] for name in core.TARGETS}
        current_source = copy.deepcopy(source)
        # Unique nonce file: consuming locally is deliberately irreversible.
        used = STATE / ("approval-consumed-" + approve["nonce"])
        require(not used.exists() and not used.is_symlink(),
                "SUPERVISION_ALREADY_CONSUMED")
        def evidence(phase, sha, old, candidate):
            if sha != SOURCE or candidate != new:
                return False
            try:
                fresh = signed.gate.evaluate(signed.gate.readiness.fetch_public)
                return (fresh == current_source and
                        approval.validate(approve, fresh, prepared, int(time.time()))
                        ["status"] == "LOCAL_SUPERVISED_CUT_MATCH_NO_DEPLOYMENT_AUTHORITY"
                        and approved_review(review, fresh, comparison, int(time.time())) is None
                        and validate_g6(review, int(time.time())))
            except Exception:
                return False
        require(evidence("pre", SOURCE, before, new) is True,
                "SUPERVISED_EVIDENCE_DRIFTED")
        fd = os.open(used, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "w") as stream:
            stream.write(SOURCE + "\n")
            stream.flush()
            os.fsync(stream.fileno())
        def audit(event, sha):
            # Never print environment, database data or Docker logs.
            path = STATE / "dual-events.log"
            fd2 = os.open(path, os.O_WRONLY | os.O_APPEND | os.O_CREAT | os.O_NOFOLLOW, 0o600)
            with os.fdopen(fd2, "w") as f:
                f.write(str(int(time.time())) + " " + event + " " + sha + "\n")
                f.flush()
                os.fsync(f.fileno())
        return core.transact(before=before, new=new, source_sha=SOURCE,
                             layers=layers, workdir=str(WORKDIR), state_dir=STATE,
                             run=lambda args: adapter.docker(args, cwd=str(WORKDIR), timeout=240),
                             live_snapshot=snapshot, evidence=evidence,
                             health=health, audit=audit)


if __name__ == "__main__":
    try:
        result = main()
        print(json.dumps(result, sort_keys=True))
    except Exception as exc:
        # Stable coarse codes, never secret-bearing process/config output.
        code = str(exc) if isinstance(exc, Blocked) else "SUPERVISED_APPLY_NOT_COMPLETED"
        print(json.dumps({"status": "BLOCKED_QA525_SUPERVISED_DUAL",
                          "code": code, "may_apply": False}, sort_keys=True))
        raise SystemExit(2)
