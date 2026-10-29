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
SHA = re.compile(r"[a-f0-9]{40}\Z")
BASELINE = "6ff4ae0d092d4214df4ea31af484efbc969fde54"
STATE = Path("/opt/centenario/qa525-signed-prepare")
REVIEW = Path("/etc/pmgm/qa525-sensitive-reviewed.json")
DBA_REVIEW = Path("/etc/pmgm/qa525-member-history-dba-approved.json")
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
    HERE / "qa525-member-history-schema-gate.py",
    HERE / "qa525-post-db-readonly.py",
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


def approved_review(review, source, policy, now, *, schema_approved=False):
    """Review claims are necessary, never a substitute for manual QA signoff."""
    required = {"source_sha", "release_asset_sha256", "baseline_sha",
                "reviewed_sections", "sensitive_reasons",
                "no_new_ef_migrations", "runtime_migration_flags_disabled",
                "legacy_master_and_smtp_preserved", "reviewed_at",
                "g6_archive_name", "g6_archive_sha256", "g6_drive_file_id",
                "g6_offhost_sha256_verified", "g6_restore_report_name"}
    require(type(review) is dict and set(review) == required,
            "SENSITIVE_REVIEW_FIELDS_INVALID")
    require(type(source.get("source_sha")) is str and SHA.fullmatch(source["source_sha"])
            and review["source_sha"] == source["source_sha"]
            and review["release_asset_sha256"] == source["release_asset_sha256"]
            and review["baseline_sha"] == BASELINE
            and set(review["reviewed_sections"]) == REVIEWED
            and sorted(review["sensitive_reasons"]) == sorted(policy["reasons"])
            and policy["status"] == "BLOCKED_REVIEW_REQUIRED"
            and type(schema_approved) is bool
            and review["no_new_ef_migrations"] is (not schema_approved)
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
    timestamp = time.strptime(name[25:40], "%Y%m%dT%H%M%S")
    age = now - int(__import__("calendar").timegm(timestamp))
    require(0 <= age < 48 * 3600, "G6_CHECKPOINT_TOO_OLD")
    return True


def scan_migrations(policy, fetch, target, gate=None):
    """Only the two pinned PR801 schema blobs may enter DBA-reviewed path."""
    comparison = fetch("/compare/" + BASELINE + "..." + target)
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
    sensitive = {p for p in paths if "/migrations/" in p or "dbcontext" in p}
    if not sensitive:
        return "NO_SCHEMA_CHANGE"
    require(gate is not None, "EF_MIGRATION_OR_DBCONTEXT_CHANGE_REQUIRES_SEPARATE_PLAN")
    # These Git blob SHA values are independently pinned in the reviewed
    # pure PR801 contract, and are checked against the exact signed HEAD.
    blobs = {}
    for path in (gate.MIGRATION_PATH, gate.CONTEXT_PATH):
        detail = fetch("/contents/" + path + "?ref=" + target)
        require(type(detail) is dict and type(detail.get("sha")) is str,
                "MIGRATION_SOURCE_BLOB_UNAVAILABLE")
        blobs[path] = detail["sha"]
    try:
        gate.reviewed_diff(comparison["files"], blobs)
    except gate.Blocked:
        raise Blocked("UNKNOWN_OR_UNREVIEWED_EF_CHANGE") from None
    return "PR801_REVIEWED_ADDITIVE_NEEDS_DBA"


def validate_post_db_dba(review, source, now, docker, gate, probe):
    """Read-only live 99 + exact root DBA acta + G6 post-migration checkpoint.

    Proof of the DB schema remains distinct from approval to deploy. This
    function never runs the EF migrator or modifies PostgreSQL.
    """
    dba = private_json(DBA_REVIEW, maximum=8192)
    fields = {"source_sha", "release_asset_sha256", "baseline_sha",
              "migration_id", "migration_blob_sha", "dbcontext_blob_sha",
              "migration_completed_at", "approved_at",
              "dba_independently_reviewed", "additive_up_only",
              "previous_api_forward_compatible", "post_db_g6_archive_name",
              "post_db_g6_archive_sha256", "post_db_g6_restore_report_name"}
    require(type(dba) is dict and set(dba) == fields,
            "DBA_REVIEW_FIELDS_INVALID")
    completed, approved = dba["migration_completed_at"], dba["approved_at"]
    require(dba["source_sha"] == source["source_sha"]
            and dba["release_asset_sha256"] == source["release_asset_sha256"]
            and dba["baseline_sha"] == BASELINE
            and dba["migration_id"] == gate.MIGRATION_ID
            and dba["migration_blob_sha"] == gate.MIGRATION_BLOB
            and dba["dbcontext_blob_sha"] == gate.CONTEXT_BLOB
            and dba["dba_independently_reviewed"] is True
            and dba["additive_up_only"] is True
            and dba["previous_api_forward_compatible"] is True
            and type(completed) is int and type(approved) is int
            and 0 < completed <= approved <= now
            and now - approved <= 1800
            and dba["post_db_g6_archive_name"] == review["g6_archive_name"]
            and dba["post_db_g6_archive_sha256"] == review["g6_archive_sha256"]
            and dba["post_db_g6_restore_report_name"] == review["g6_restore_report_name"],
            "DBA_REVIEW_OUTDATED_OR_NOT_APPROVED")
    # Check the approved snapshot was actually created after DBA migration.
    try:
        archive_time = __import__("calendar").timegm(
            time.strptime(review["g6_archive_name"][25:40], "%Y%m%dT%H%M%S"))
    except (ValueError, TypeError, IndexError):
        raise Blocked("POST_DB_G6_TIMESTAMP_INVALID") from None
    require(archive_time >= completed, "G6_PRECEDES_DBA_MIGRATION")
    validate_g6(review, now)
    receipt = private_json(Path("/opt/centenario/qa-api-refresh/installed.json"))
    try:
        state = probe.inspect(receipt, docker, gate)
    except (probe.Blocked, gate.Blocked):
        raise Blocked("POST_DB_SCHEMA_LIVE_READONLY_NOT_VERIFIED") from None
    require(state.get("status") == probe.POST_DB_PASS_STATUS
            and state.get("history_count") == 99
            and state.get("may_apply") is False,
            "POST_DB_SCHEMA_LIVE_READONLY_NOT_VERIFIED")
    return True

def disable_redirects():
    class NoRedirect(HTTPRedirectHandler):
        def redirect_request(self, req, fp, code, msg, headers, newurl):
            return None
    return build_opener(HTTPHandler(), HTTPSHandler(), NoRedirect())


def health(expected_web_sha):
    """HTTPS smoke PLUS public WEB version identity; validates rollback too."""
    if type(expected_web_sha) is not str or not SHA.fullmatch(expected_web_sha):
        return False
    opener = disable_redirects()
    origin = "https://sistema.granlogiamixta.cl"
    try:
        for path in (*HEALTH_PATHS, "/qa-version.json"):
            req = Request(origin + path,
                          headers={"User-Agent": "PMGM-QA-supervised-smoke",
                                   "Cache-Control": "no-cache"})
            with opener.open(req, timeout=12) as res:
                if res.status != 200 or res.geturl() != origin + path:
                    return False
                # The homepage can be larger than a JSON version marker.
                # Only the machine-readable marker has a strict 2 KiB cap.
                if path == "/qa-version.json":
                    body = res.read(2049)
                    if len(body) > 2048:
                        return False
                    marker = json.loads(body)
                    if (type(marker) is not dict or
                            marker.get("environment") != "QA_SYNTHETIC" or
                            marker.get("source_sha") != expected_web_sha):
                        return False
        return True
    except (URLError, OSError, TimeoutError, ValueError, TypeError, UnicodeError):
        return False



def prepare_reviewed(build, approval, signed, adapter, source, approve, review):
    """Build both images from a newly verified Sigstore release; no deployment.

    An explicit root-owned sensitive review and a fresh supervised approval
    are necessary; the old unattended change policy is NOT treated as safe.
    """
    target = source["source_sha"]
    receipt_path = STATE / "prepared.json"
    require(not receipt_path.exists() and not receipt_path.is_symlink(),
            "PREPARE_ALREADY_EXISTS")
    require(approve.get("target_sha") == target and
            approve.get("release_asset_sha256") == source["release_asset_sha256"]
            and approve.get("scope") == approval.SCOPE
            and approve.get("action") == approval.ACTION
            and approve.get("environment") == approval.ENVIRONMENT
            and approve.get("operator_reviewed") is True,
            "PREPARE_SUPERVISION_NOT_MATCHED")
    now = int(time.time())
    require(type(approve.get("issued_at")) is int
            and type(approve.get("expires_at")) is int
            and 0 <= now - approve["issued_at"] <= 1800
            and now < approve["expires_at"] <= approve["issued_at"] + 1800,
            "PREPARE_SUPERVISION_EXPIRED")
    before = build.snapshot(adapter.docker)
    require(all(x["running"] is True for x in before.values()),
            "PREPARE_BASELINE_NOT_RUNNING")
    with tempfile.TemporaryDirectory(prefix=".qa525-reviewed-build-", dir=STATE) as tmp:
        package = Path(tmp) / "verified"
        attestation = signed.verify_current(package)
        require(attestation["source_sha"] == target
                and attestation["release_asset_sha256"] ==
                source["release_asset_sha256"], "PREPARE_SIGNATURE_MISMATCH")
        def verify(path, sha):
            if sha != target or path != package:
                return False
            try:
                signed.staging.verify_manifest(path, sha)
                return signed.gate.evaluate(signed.gate.readiness.fetch_public) == source
            except Exception:
                return False
        require(verify(package, target), "PREPARE_SIGNATURE_OR_DEV_DRIFT")
        images = build.build_images(package, target, verify, adapter.docker,
                                   build.builder.prepare_web)
        require(verify(package, target) and
                build.snapshot(adapter.docker) == before
                and validate_g6(review, int(time.time())),
                "PREPARE_target_HOST_OR_BACKUP_CHANGED")
        prepared = {
            "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
            "scope": build.SCOPE, "source_sha": target,
            "source_asset_sha256": source["release_asset_sha256"],
            **images, "baseline": before, "images_built": True,
            "services_unchanged": True, "may_apply": False,
            "fresh_backup_created": False, "signed_stage_ephemeral": True,
            "actual_rollback_drill_completed": False,
        }
        approval.validate(approve, source, prepared, int(time.time()))
    require(not receipt_path.exists() and not receipt_path.is_symlink(),
            "PREPARED_RECEIPT_CREATED_DURING_BUILD")
    fd = os.open(receipt_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "w") as output:
        json.dump(prepared, output, sort_keys=True)
        output.flush()
        os.fsync(output.fileno())
    return {"status": "PREPARED_REVIEWED_API_WEB_NO_DEPLOYMENT",
            "source_sha": target, "services_unchanged": True,
            "may_apply": False}



def expected_compose_chain(*, name, target, original_files,
                           previous_id, previous_image, current_id,
                           current_image, candidate_image, state=STATE):
    """Exact Docker config_files chain after additive image-only override.

    The old untouched container must still have its original layers.
    Recreated services may have only the trusted QA525 appended override.
    """
    require(name in ("api", "web") and type(original_files) is tuple
            and type(previous_id) is str and type(current_id) is str
            and re.fullmatch(r"[a-f0-9]{64}", previous_id)
            and re.fullmatch(r"[a-f0-9]{64}", current_id)
            and type(target) is str and SHA.fullmatch(target)
            and all(type(x) is str for x in original_files),
            "COMPOSE_CHAIN_INPUT_INVALID")
    require(all(type(x) is str and re.fullmatch(r"sha256:[a-f0-9]{64}", x)
                for x in (previous_image, current_image, candidate_image)),
            "COMPOSE_CHAIN_IMAGE_INVALID")
    if current_id == previous_id:
        require(current_image == previous_image, "COMPOSE_CONTAINER_IMAGE_CHANGED_WITHOUT_RECREATE")
        return list(original_files)
    require(current_image in (previous_image, candidate_image),
            "COMPOSE_UNEXPECTED_IMAGE")
    return list(original_files) + [str(Path(state) / (
        "qa525-" + name + "-" + target + "-" + current_image[7:] + ".json"))]



def retain_rollback_images(before, target, docker):
    """Tag BOTH old OCI IDs under the existing QA locks before any recreate.

    A retained tag makes an old image resist ordinary dangling-image pruning.
    This does not protect against an administrator explicitly force-deleting it.
    Tags are SHA+image-specific and must never be silently reassigned.
    """
    require(type(target) is str and SHA.fullmatch(target) and callable(docker),
            "ROLLBACK_RETAIN_INPUT_INVALID")
    tags = {}
    for name in ("api", "web"):
        previous = before.get(name)
        require(type(previous) is dict, "ROLLBACK_OLD_IMAGE_MISSING")
        old = previous.get("image")
        require(type(old) is str and re.fullmatch(r"sha256:[a-f0-9]{64}", old),
                "ROLLBACK_OLD_IMAGE_INVALID")
        tag = "pmgm-qa525-rollback:" + name + "-" + target[:12] + "-" + old[7:19]
        require(docker(["image", "inspect", "--format", "{{.Id}}", old]) == old,
                "ROLLBACK_IMAGE_NOT_PRESENT")
        try:
            existing = docker(["image", "inspect", "--format", "{{.Id}}", tag])
        except Exception:
            existing = None
        require(existing is None or existing == old, "ROLLBACK_TAG_COLLISION")
        if existing is None:
            docker(["image", "tag", old, tag])
        require(docker(["image", "inspect", "--format", "{{.Id}}", tag]) == old,
                "ROLLBACK_TAG_NOT_PINNED")
        tags[name] = tag
    return tags


def checked_compose_env_file(path):
    """Verify physically approved --env-file contains no variable assignments."""
    p = trusted(path)
    st = p.stat()
    require(stat.S_IMODE(st.st_mode) == 0o600 and 0 < st.st_size <= 65536
            and st.st_nlink == 1, "COMPOSE_ENV_FILE_UNTRUSTED")
    content = p.read_bytes()
    require(len(content) == st.st_size and b"\x00" not in content,
            "COMPOSE_ENV_FILE_CHANGED")
    try:
        decoded = content.decode("utf-8")
    except UnicodeDecodeError:
        raise Blocked("COMPOSE_ENV_FILE_INVALID") from None
    require(all(not line.strip() or line.lstrip().startswith("#")
                for line in decoded.splitlines()), "COMPOSE_ENV_FILE_NOT_EMPTY")
    return hashlib.sha256(content).hexdigest()


def validate_live_ef_history(docker):
    """Read-only EF migration set against installed API baseline receipt."""
    receipt = private_json(Path("/opt/centenario/qa-api-refresh/installed.json"))
    expected = receipt.get("database_migrations")
    require(type(expected) is list and len(expected) == 98 and
            all(type(x) is str and re.fullmatch(r"[A-Za-z0-9_]+", x)
                for x in expected) and len(set(expected)) == len(expected),
            "EF_BASELINE_MIGRATIONS_INVALID")
    query = ('BEGIN READ ONLY; SELECT "MigrationId" FROM '
             'public."__EFMigrationsHistory" ORDER BY 1; COMMIT;')
    raw = docker(["exec", "centenario-qa-lab2-postgres-1",
                  "sh", "-c",
                  'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"',
                  "qa525-readonly", query])
    actual = raw.splitlines()
    require(len(actual) == len(expected) and len(set(actual)) == len(actual)
            and set(actual) == set(expected), "EF_MIGRATION_HISTORY_DRIFT")
    return True


def main():
    mode = sys.argv[1:]
    require(mode in (["--prepare-reviewed"], ["--apply-supervised"]),
            "ONLY_EXPLICIT_SUPERVISION_ALLOWED")
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
    schema_gate = load("qa525_pr801_schema_review", MODULES[6])
    post_db_probe = load("qa525_pr801_post_db_probe", MODULES[7])
    with build.exclusive():
        review = private_json(REVIEW, maximum=8192)
        source = signed.gate.evaluate(signed.gate.readiness.fetch_public)
        target = source["source_sha"]
        require(type(target) is str and SHA.fullmatch(target),
                "SIGNATURE_RELEASE_TARGET_DRIFT")
        comparison = policy.evaluate(signed.gate.readiness.fetch_public, BASELINE)
        schema_state = scan_migrations(policy, signed.gate.readiness.fetch_public,
                                       target, schema_gate)
        known_schema = schema_state == "PR801_REVIEWED_ADDITIVE_NEEDS_DBA"
        now = int(time.time())
        if known_schema:
            validate_post_db_dba(review, source, now, adapter.docker,
                                 schema_gate, post_db_probe)
        approved_review(review, source, comparison, now,
                        schema_approved=known_schema)
        validate_g6(review, now)
        approve = approval.read_root_approval()
        if mode == ["--prepare-reviewed"]:
            return prepare_reviewed(build, approval, signed, adapter,
                                    source, approve, review)
        # Only after a separate, audited and signed build may we apply.
        prepared = private_json(STATE / "prepared.json")
        approved = approval.validate(approve, source, prepared, now)
        require(approved["status"] == "LOCAL_SUPERVISED_CUT_MATCH_NO_DEPLOYMENT_AUTHORITY",
                "SUPERVISOR_CUT_MISMATCH")
        validate_g6(review, now)
        # Re-download exact public release and REVALIDATE Sigstore signature on
        # the physical host; prepared.json flags alone are never authority.
        with tempfile.TemporaryDirectory(prefix=".qa525-supervised-signed-", dir=STATE) as d:
            result = signed.verify_current(Path(d) / "signed")
            require(result["source_sha"] == target and
                    result["release_asset_sha256"] == source["release_asset_sha256"],
                    "PHYSICAL_SIGNATURE_REVERIFICATION_FAILED")
        for name in core.TARGETS:
            image = prepared.get(name + "_image_id")
            require(type(image) is str and core.IMAGE.fullmatch(image)
                    and adapter.docker(["image", "inspect", "--format", "{{.Id}}", image]) == image
                    and adapter.docker(["image", "inspect", "--format",
                         '{{index .Config.Labels "org.opencontainers.image.revision"}}',
                         image]) == target,
                    "PREPARED_IMAGE_INVALID")
        # A new API image must not silently change live identity, mail or
        # migration environment. Never log or write Config.Env values.
        def runtime_env(name):
            data = json.loads(adapter.docker([
                "inspect", "--format", "{{json .Config.Env}}",
                "centenario-qa-lab2-" + name + "-1"]))
            require(type(data) is list and all(type(x) is str and "=" in x
                    for x in data), "RUNTIME_ENV_SHAPE_INVALID")
            settings = dict(x.split("=", 1) for x in data)
            if name == "api":
                require(settings.get("Database__ApplyMigrationsOnStartup", "false").lower()
                        == "false" and
                        settings.get("Database__RunMigrationsAndExit", "false").lower()
                        == "false", "API_MIGRATION_FLAGS_ENABLED")
            return hashlib.sha256(json.dumps(sorted(data)).encode()).hexdigest()
        env_before = {name: runtime_env(name) for name in ("api", "web")}
        # Compare live read-only EF history against the installed baseline.
        # No migration, SQL write or migrator container is permitted.
        if known_schema:
            require(validate_post_db_dba(review, source, int(time.time()),
                                         adapter.docker, schema_gate, post_db_probe),
                    "POST_DBA_EF_PREFLIGHT_FAILED")
        else:
            require(validate_live_ef_history(adapter.docker),
                    "EF_MIGRATION_PREFLIGHT_FAILED")
        # Exact baseline and all approved Compose layers are checked at runtime.
        original = adapter.metadata()
        require(set(original) == set(core.TARGETS + core.DEPENDENCIES + core.ONESHOTS),
                "UNEXPECTED_QA_SERVICE_SET")
        layers = core.normalized_layers(
            {name: (original[name][7] or "").split(",") for name in core.TARGETS},
            WORKDIR)
        require(all(original[name][6] == str(WORKDIR) for name in core.TARGETS),
                "COMPOSE_WORKING_DIR_CHANGED")
        # Pin every ORIGINAL Compose file byte-for-byte. Other QA processes
        # holding distinct locks must not change identity or route definitions.
        envfile_digest = checked_compose_env_file(core.ENV_FILE)
        compose_digests = {}
        for filename in set(layers["api"] + layers["web"]):
            with trusted(filename).open("rb") as stream:
                compose_digests[filename] = hashlib.file_digest(stream, "sha256").hexdigest()
        new = {name: prepared[name + "_image_id"] for name in core.TARGETS}
        def snapshot():
            require(checked_compose_env_file(core.ENV_FILE) == envfile_digest,
                    "COMPOSE_ENV_FILE_DRIFT")
            # Verify source files again both before and after recreation; the
            # additive image overrides must not replace the qa754 identity.
            for filename, digest in compose_digests.items():
                with trusted(filename).open("rb") as stream:
                    require(hashlib.file_digest(stream, "sha256").hexdigest() == digest,
                            "COMPOSE_OR_IDENTITY_FILE_CHANGED")
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
                    actual_files = (row[7] or "").split(",")
                    allowed_files = expected_compose_chain(
                        name=name, target=target, original_files=layers[name],
                        previous_id=original[name][0],
                        previous_image=original[name][1],
                        current_id=row[0], current_image=row[1],
                        candidate_image=new[name])
                    if len(allowed_files) > len(layers[name]):
                        overlay = trusted(allowed_files[-1])
                        expected_payload = (json.dumps(
                            {"services": {name: {"image": row[1]}}},
                            sort_keys=True) + "\n")
                        require(overlay.read_text() == expected_payload,
                                "COMPOSE_OVERRIDE_TAMPERED")
                    require(actual_files == allowed_files,
                            "COMPOSE_LAYER_ORDER_CHANGED")
                    require(runtime_env(name) == env_before[name],
                            "API_OR_WEB_IDENTITY_ENV_CHANGED")
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
        current_source = copy.deepcopy(source)
        # Public /qa-version.json must identify exact signed WEB SHA, not just
        # a healthy nginx. On rollback it must match the old image source.
        previous_web_sha = adapter.docker([
            "image", "inspect", "--format",
            '{{index .Config.Labels "org.opencontainers.image.revision"}}',
            before["web"]["image"]])
        require(type(previous_web_sha) is str and SHA.fullmatch(previous_web_sha),
                "ROLLBACK_WEB_PROVENANCE_NOT_PINNED")
        def versioned_health():
            live = adapter.metadata().get("web")
            if live is None or live[2] != "running":
                return False
            actual = live[1]
            expected = (target if actual == new["web"] else
                        previous_web_sha if actual == before["web"]["image"] else None)
            return health(expected)
        # Unique nonce file: consuming locally is deliberately irreversible.
        used = STATE / ("approval-consumed-" + approve["nonce"])
        require(not used.exists() and not used.is_symlink(),
                "SUPERVISION_ALREADY_CONSUMED")
        def evidence(phase, sha, old, candidate):
            if sha != target or candidate != new:
                return False
            try:
                fresh = signed.gate.evaluate(signed.gate.readiness.fetch_public)
                return (fresh == current_source and
                        approval.validate(approve, fresh, prepared, int(time.time()))
                        ["status"] == "LOCAL_SUPERVISED_CUT_MATCH_NO_DEPLOYMENT_AUTHORITY"
                        and approved_review(review, fresh, comparison,
                                            int(time.time()),
                                            schema_approved=known_schema) is None
                        and (not known_schema or validate_post_db_dba(
                            review, fresh, int(time.time()), adapter.docker,
                            schema_gate, post_db_probe))
                        and validate_g6(review, int(time.time())))
            except Exception:
                return False
        require(evidence("pre", target, before, new) is True,
                "SUPERVISED_EVIDENCE_DRIFTED")
        # Pin both prior images while the same exclusive locks are held.
        # This is image metadata only; no containers are changed yet.
        rollback_tags = retain_rollback_images(before, target, adapter.docker)
        require(set(rollback_tags) == set(core.TARGETS),
                "ROLLBACK_IMAGES_NOT_RETAINED")
        fd = os.open(used, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "w") as stream:
            stream.write(target + "\n")
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
        return core.transact(before=before, new=new, source_sha=target,
                             layers=layers, workdir=str(WORKDIR), state_dir=STATE,
                             run=lambda args: adapter.docker(args, cwd=str(WORKDIR), timeout=240),
                             live_snapshot=snapshot, evidence=evidence,
                             health=versioned_health, audit=audit)


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
