#!/usr/bin/env python3
"""QA525: stage current signed SHA and BUILD API+WEB images, never deploy.

This is deliberately fail-closed and NOT an installer. It is available only
under an explicit --prepare on glmdch/root after audited installation of the
local code, root-owned state directory and physical preflight. Sensitive
identity/Program/schema/Compose changes remain blocked by change policy.
"""
import contextlib
import fcntl
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import socket
import stat
import sys
import tempfile

OPS = Path(__file__).resolve().parent
QA = OPS.parent
STATE = Path("/opt/centenario/qa525-signed-prepare")
PROJECT = "centenario-qa-lab2"
SCOPE = "BUILD_ONLY_SIGNED_API_WEB_NO_CONTAINER_CHANGES_NO_APPLY"
IMAGE = re.compile(r"sha256:[0-9a-f]{64}\Z")
SHA = re.compile(r"[0-9a-f]{40}\Z")
SERVICES = ("api", "web", "keycloak", "postgres", "minio", "gateway", "clamav")
SOURCE_CHAIN = (
    OPS / "qa525-current-image-preparation.py",
    OPS / "qa525-update.py",
    OPS / "qa525-supervised-dual-entrypoint.py",
    OPS / "qa525-current-host-staging.py",
    OPS / "qa525-current-host-preflight.py",
    OPS / "qa525-current-change-policy.py",
    OPS / "qa525-host-baseline-6ff4-readonly.py",
    QA / "qa525-stage-current-release.py",
    QA / "qa525-current-release-gate.py",
    QA / "qa-agent-readiness.py",
    QA / "qa-public-release-download.py",
    QA / "qa-signed-staging.py",
    QA / "qa-lite-web-build.py",
    QA / "qa-lite-compose-adapter.py",
)
LOCKS = (
    Path("/opt/centenario/qa-api-refresh/operation.lock"),
    Path("/opt/centenario/qa-auto-lite/web-update.lock"),
    Path("/run/lock/pmgm-master-qa-604.lock"),
    Path("/run/lock/pmgm-qa754-web-hotfix.lock"),
    Path("/run/lock/pmgm-qa754-api-activation.lock"),
)


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def trusted(path, directory=False):
    path = Path(path)
    require(path.is_absolute(), "TRUST_PATH_NOT_ABSOLUTE")
    for segment in (path, *path.parents):
        try:
            st = segment.lstat()
        except OSError:
            raise Blocked("TRUST_PATH_MISSING") from None
        require(not stat.S_ISLNK(st.st_mode) and st.st_uid == 0
                and not st.st_mode & 0o022, "TRUST_PATH_UNSAFE")
    require(path.is_dir() if directory else path.is_file(), "TRUST_PATH_TYPE")
    return path


def trusted_state():
    trusted(STATE, directory=True)
    require(stat.S_IMODE(STATE.stat().st_mode) == 0o700, "STATE_MODE_NOT_PRIVATE")
    return STATE


def trusted_lock_parent(path):
    """Only /run/lock may be root-owned sticky instead of root-only.
    
    The lock itself is still opened O_NOFOLLOW, verified root/0600 and
    exclusively flocked. All other lock directories must be root-controlled.
    """
    path = Path(path)
    if path == Path("/run/lock"):
        trusted(path.parent, directory=True)
        try:
            st = path.lstat()
        except OSError:
            raise Blocked("LOCK_DIRECTORY_MISSING") from None
        require(stat.S_ISDIR(st.st_mode) and not stat.S_ISLNK(st.st_mode)
                and st.st_uid == 0
                and stat.S_IMODE(st.st_mode) in (0o700, 0o755, 0o1777),
                "LOCK_DIRECTORY_NOT_ROOT_STICKY")
        return
    trusted(path, directory=True)


def bootstrap_import_trust():
    if os.geteuid() == 0 and socket.gethostname() == "glmdch":
        for filename in SOURCE_CHAIN:
            trusted(filename)


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


bootstrap_import_trust()
stage = load("qa525_dynamic_staging", OPS / "qa525-current-host-staging.py")
builder = load("qa525_dynamic_web_builder", QA / "qa-lite-web-build.py")


def snapshot(run):
    """Extract only public Docker IDs and image hashes; never log Config.Env."""
    result = {}
    for name in SERVICES:
        rows = json.loads(run(["inspect", PROJECT + "-" + name + "-1"]))
        require(type(rows) is list and len(rows) == 1, "DOCKER_SNAPSHOT_INVALID")
        c = rows[0]
        config = c.get("Config", {})
        labels = config.get("Labels", {})
        cid, image_id = c.get("Id"), c.get("Image")
        require(type(cid) is str and re.fullmatch(r"[0-9a-f]{64}", cid)
                and type(image_id) is str and IMAGE.fullmatch(image_id)
                and c.get("State", {}).get("Status") == "running"
                and labels.get("com.docker.compose.project") == PROJECT
                and labels.get("com.docker.compose.service") == name,
                "DOCKER_BASELINE_INVALID")
        result[name] = {"container_fingerprint": cid, "image_id": image_id,
                        "running": True}
    return result


def signed_source_guard(package, target):
    """Recheck manifest, live signed SHA and physical host, not JSON booleans."""
    try:
        stage.signed.staging.verify_manifest(package, target)
        report = stage.validated_host_check()
    except Exception:
        return False
    return (report.get("status") == "HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED"
            and report.get("source_sha") == target
            and report.get("may_apply") is False)


def build_api(package, sha, verify, run):
    require(verify(package, sha) is True, "SOURCE_NOT_REVERIFIED")
    context = package / "backend"
    recipe = context / "Dockerfile"
    require(context.is_dir() and recipe.is_file() and not recipe.is_symlink(),
            "SIGNED_API_BUILD_CONTEXT_INVALID")
    with tempfile.TemporaryDirectory(prefix=".api-image-id-", dir=package.parent) as tmp:
        iid = Path(tmp) / "image-id"
        run(["build", "--pull=false", "--iidfile", str(iid),
             "--label", "org.opencontainers.image.revision=" + sha,
             "--file", str(recipe), str(context)], timeout=1200)
        image_id = iid.read_text().strip()
    require(type(image_id) is str and IMAGE.fullmatch(image_id),
            "BUILT_API_IMAGE_INVALID")
    require(run(["image", "inspect", "--format", "{{.Id}}", image_id]) == image_id
            and run(["image", "inspect", "--format",
                     '{{index .Config.Labels "org.opencontainers.image.revision"}}',
                     image_id]) == sha, "BUILT_API_IDENTITY_INVALID")
    require(verify(package, sha) is True, "SOURCE_CHANGED_DURING_API_BUILD")
    return image_id


def build_images(package, target, verify, run, prepare_web):
    """Pure orchestration seam; production passes pinned trusted dependencies."""
    require(type(target) is str and SHA.fullmatch(target),
            "TARGET_NOT_CURRENT_SHA")
    api_image = build_api(package, target, verify, run)
    require(verify(package, target) is True, "SOURCE_CHANGED_BEFORE_WEB_BUILD")
    web = prepare_web(package, target, verify)
    require(type(web) is dict
            and web.get("status") == "LOCAL_WEB_IMAGE_PREPARED_NOT_DEPLOYED"
            and web.get("source_sha") == target
            and type(web.get("web_image_id")) is str
            and IMAGE.fullmatch(web["web_image_id"])
            and api_image != web["web_image_id"], "WEB_PREPARATION_INVALID")
    require(verify(package, target) is True, "SOURCE_CHANGED_AFTER_IMAGE_BUILD")
    return {"api_image_id": api_image, "web_image_id": web["web_image_id"]}


@contextlib.contextmanager
def exclusive():
    with contextlib.ExitStack() as resources:
        for lockfile in LOCKS:
            trusted_lock_parent(lockfile.parent)
            fd = os.open(lockfile, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
            resources.callback(os.close, fd)
            st = os.fstat(fd)
            require(stat.S_ISREG(st.st_mode) and st.st_uid == 0
                    and stat.S_IMODE(st.st_mode) == 0o600,
                    "UPDATE_LOCK_UNTRUSTED")
            try:
                fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise Blocked("ANOTHER_QA_OPERATOR_RUNNING") from None
        yield


PENDING = "prepared-successor-archive-pending.json"  # own marker: never confused with the legacy expired-archive one
LEGACY_PENDING = ("expired-prepared-archive-pending.json", "pending-dual-reconciliation.json")
ACTAS = (Path("/etc/pmgm/qa525-sensitive-reviewed.json"),
         Path("/etc/pmgm/qa525-single-operator-postdb-approved.json"),
         Path("/etc/pmgm/qa525-supervised-cut.json"))


def _fsync_dir(path):
    fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def _digest(path):
    import hashlib
    with open(path, "rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def applied_predecessor(state, update, entry_mod, live, health, now_sha):
    """READ-ONLY. The existing prepared.json must be the legitimate, APPLIED, LIVE and HEALTHY predecessor of a NEW
    target (qa525-update.previous_prepared_is_applied + every journal APPLIED); anything else is a STOP with nothing
    moved. Returns the predecessor record (or None when there is no prepared.json)."""
    require(not any(os.path.lexists(state / n) for n in (PENDING, *LEGACY_PENDING)),
            "PREPARE_ARCHIVE_RECONCILIATION_REQUIRED")
    receipt = state / "prepared.json"
    if not (receipt.exists() or receipt.is_symlink()):
        return None
    previous = entry_mod.private_json(receipt)
    require(type(previous) is dict and type(previous.get("source_sha")) is str
            and SHA.fullmatch(previous["source_sha"]), "PREPARE_PREDECESSOR_INVALID")
    for journal_path in state.glob("dual-*.json"):
        require(entry_mod.private_json(journal_path).get("status") == "APPLIED",
                "PREPARE_JOURNAL_REQUIRES_RECONCILIATION")
    journal = entry_mod.private_json(state / ("dual-" + previous["source_sha"] + ".json"))
    try:
        update.previous_prepared_is_applied(previous, journal, live)
    except Exception:
        raise Blocked("PREPARE_PREDECESSOR_NOT_APPLIED_AND_LIVE") from None
    require(now_sha is None or previous["source_sha"] != now_sha, "PREPARE_TARGET_ALREADY_APPLIED")
    require(health(previous["source_sha"]) is True, "PREPARE_PREDECESSOR_HEALTH_UNVERIFIED")
    return previous


class _Noop:
    def __call__(self, label):
        return None


def _read_marker(state):
    marker = state / PENDING
    require(marker.is_file() and not marker.is_symlink(), "PREPARE_ARCHIVE_MARKER_INVALID")
    data = json.loads(marker.read_text())
    require(type(data) is dict and data.get("status") == "ARCHIVE_INCOMPLETE_NO_DEPLOYMENT"
            and type(data.get("items")) is list and type(data.get("archive_name")) is str
            and re.fullmatch(r"review-archive-[0-9a-f]{24}", data["archive_name"])
            and type(data.get("new_receipt_sha256")) is str, "PREPARE_ARCHIVE_MARKER_INVALID")
    for item in data["items"]:
        require(type(item) is dict and type(item.get("origin")) is str and type(item.get("sha256")) is str
                and item["origin"].startswith("/") and Path(item["origin"]).name == item.get("name"),
                "PREPARE_ARCHIVE_MARKER_INVALID")
    return data


def recover_prepare_archive(state, fsync_dir=_fsync_dir):
    """Deterministic, supervised recovery of an interrupted successor archive (marker ARCHIVE_INCOMPLETE_NO_DEPLOYMENT).
    The plan is fully computed from the marker and the physical state BEFORE anything moves; any ambiguity is a STOP
    that changes nothing. Nothing is ever deleted except our own aside file, empty archive dir and the marker.
      PUBLISHED  (new receipt in place, byte-identical to the marker digest): roll FORWARD, old receipt+actas stay archived.
      otherwise  (old receipt still, or all of it back at origin): roll BACK, every file returns byte-for-byte."""
    marker = _read_marker(state)
    archive = state / marker["archive_name"]
    receipt, aside = state / "prepared.json", state / "prepared.json.next"
    require(archive.is_dir() and not archive.is_symlink(), "PREPARE_ARCHIVE_RECOVERY_UNSAFE")
    items = marker["items"]
    published = (receipt.is_file() and not receipt.is_symlink() and not aside.exists()
                 and _digest(receipt) == marker["new_receipt_sha256"])
    plan = []
    for item in items:
        origin, saved = Path(item["origin"]), archive / item["name"]
        at_origin = origin.is_file() and not origin.is_symlink()
        archived = saved.is_file() and not saved.is_symlink()
        if published:
            require(archived and (item["name"] == "prepared.json" or not os.path.lexists(origin)),
                    "PREPARE_ARCHIVE_RECOVERY_UNSAFE")
            require(_digest(saved) == item["sha256"], "PREPARE_ARCHIVE_RECOVERY_UNSAFE")
        elif at_origin and not archived:
            require(_digest(origin) == item["sha256"], "PREPARE_ARCHIVE_RECOVERY_UNSAFE")
        elif archived and not os.path.lexists(origin):
            require(_digest(saved) == item["sha256"], "PREPARE_ARCHIVE_RECOVERY_UNSAFE")
            plan.append((saved, origin))
        else:
            raise Blocked("PREPARE_ARCHIVE_RECOVERY_UNSAFE")
    if aside.exists() or aside.is_symlink():
        require(aside.is_file() and not aside.is_symlink() and _digest(aside) == marker["new_receipt_sha256"],
                "PREPARE_ARCHIVE_RECOVERY_UNSAFE")
    result = archive / "applied-predecessor-archive.json"
    if published:
        status = "ROLLED_FORWARD"
        if not os.path.lexists(result):
            fd = os.open(result, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
            with os.fdopen(fd, "w") as stream:
                json.dump({"status": "APPLIED_PREDECESSOR_ARCHIVED_NO_DEPLOYMENT", "recovered": True,
                           "files_sha256": {i["name"]: i["sha256"] for i in items}}, stream, sort_keys=True)
                stream.flush()
                os.fsync(stream.fileno())
    else:
        status = "ROLLED_BACK"
        for saved, origin in reversed(plan):
            saved.rename(origin)
        if aside.exists():
            aside.unlink()
        if os.path.lexists(result):
            result.unlink()
        archive.rmdir()
    for directory in {state, archive if archive.exists() else state, *[Path(i["origin"]).parent for i in items]}:
        fsync_dir(directory)
    (state / PENDING).unlink()
    fsync_dir(state)
    return status


def install_prepared_archiving_predecessor(state, previous, prepared, actas=ACTAS, now=None, fsync_dir=_fsync_dir,
                                           revalidate=None, hook=_Noop()):
    """Write-ahead marker -> aside receipt -> rename (never delete) receipt + actas into review-archive-<hex24>
    -> revalidate (drift/TOCTOU) -> ONE rename publishes the new receipt -> fsync -> marker unlink (commit).
    Normal failure BEFORE publication rolls back through recover_prepare_archive(); a hard crash (BaseException) or a
    failure AFTER publication leaves the marker (fail-closed) and recover_prepare_archive() resolves it deterministically."""
    receipt, aside, marker = state / "prepared.json", state / "prepared.json.next", state / PENDING
    paths = [receipt] + [p for p in actas if p.exists() or p.is_symlink()]
    devices = {state.stat().st_dev}
    hashes = {}
    for p in paths:
        require(p.is_file() and not p.is_symlink() and not p.parent.is_symlink(), "PREPARE_ACTA_UNTRUSTED")
        devices.add(p.parent.stat().st_dev)
        hashes[p.name] = _digest(p)
    for p in paths[1:]:  # an acta that names a target must belong to THIS predecessor: never archive a live authorization
        target = json.loads(p.read_text()).get("target_sha")
        require(target is None or target == previous["source_sha"], "PREPARE_ACTA_NOT_PREDECESSOR")
    require(len(devices) == 1, "PREPARE_ARCHIVE_CROSS_DEVICE")  # rename must stay atomic: refuse BEFORE any change
    new_bytes = json.dumps(prepared, sort_keys=True).encode()
    import hashlib
    new_sha = hashlib.sha256(new_bytes).hexdigest()
    archive = state / ("review-archive-" + secrets.token_hex(12))
    archive.mkdir(mode=0o700)
    published = False
    try:
        fd = os.open(marker, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "w") as stream:
            json.dump({"status": "ARCHIVE_INCOMPLETE_NO_DEPLOYMENT", "source_sha": previous["source_sha"],
                       "source_asset_sha256": previous.get("source_asset_sha256"),
                       "archive_name": archive.name, "new_source_sha": prepared["source_sha"],
                       "new_receipt_sha256": new_sha, "reason": "APPLIED_PREDECESSOR_REPLACED_BY_NEW_PREPARE",
                       "items": [{"name": p.name, "origin": str(p), "sha256": hashes[p.name]} for p in paths]},
                      stream, sort_keys=True)
            stream.flush()
            os.fsync(stream.fileno())
        fsync_dir(state)
        hook("marker")
        fd = os.open(aside, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "wb") as stream:
            stream.write(new_bytes)
            stream.flush()
            os.fsync(stream.fileno())
        hook("aside")
        for index, p in enumerate(paths):
            p.rename(archive / p.name)
            hook("moved-%d" % index)
        for p in paths:  # TOCTOU: the bytes that were archived are the bytes that were hashed
            require(_digest(archive / p.name) == hashes[p.name], "PREPARE_ACTA_CHANGED_DURING_ARCHIVE")
        fd = os.open(archive / "applied-predecessor-archive.json",
                     os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "w") as stream:
            json.dump({"status": "APPLIED_PREDECESSOR_ARCHIVED_NO_DEPLOYMENT",
                       "predecessor_source_sha": previous["source_sha"], "files_sha256": hashes,
                       "new_source_sha": prepared["source_sha"], "archived_at": now}, stream, sort_keys=True)
            stream.flush()
            os.fsync(stream.fileno())
        hook("result")
        if revalidate is not None:
            revalidate()  # Docker running/health/baseline drift last check, immediately before publication
        hook("revalidated")
        aside.rename(receipt)  # the only publication step: atomic
        published = True
        hook("published")
        for directory in {archive, state, *[p.parent for p in paths]}:
            fsync_dir(directory)
        hook("synced")
        marker.unlink()  # commit boundary
        fsync_dir(state)
    except Exception:
        if published:
            raise Blocked("PREPARE_ARCHIVE_PUBLISHED_RECOVERY_REQUIRED") from None
        try:
            recover_prepare_archive(state, fsync_dir)  # same deterministic code as the supervised recovery
        except BaseException:
            pass  # marker stays: nothing lost, further prepare/update blocked until recovered
        raise


def prepare():
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "HOST_NOT_AUTHORIZED")
    bootstrap_import_trust()
    trusted_state()
    require(not any(os.path.lexists(STATE / n) for n in (PENDING, *LEGACY_PENDING)),
            "PREPARE_ARCHIVE_RECONCILIATION_REQUIRED")
    with exclusive():
        before = snapshot(builder.transport.docker)
        previous = None
        if (STATE / "prepared.json").exists() or (STATE / "prepared.json").is_symlink():
            update = load("qa525_update_for_prepare", OPS / "qa525-update.py")
            entry_mod = update.load_entry()
            live = {n: [before[n]["container_fingerprint"], before[n]["image_id"], "running"] for n in ("api", "web")}
            previous = applied_predecessor(STATE, update, entry_mod, live, entry_mod.health, None)
        with tempfile.TemporaryDirectory(prefix=".qa525-source-", dir=STATE) as tmp:
            package = Path(tmp) / "verified-source"
            receipt = stage.stage_current(package)
            require(receipt.get("status") ==
                    "SIGNED_SOURCE_AND_HOST_RECHECKED_NOT_PREPARED"
                    and receipt.get("may_apply") is False
                    and receipt.get("host_checked_twice") is True,
                    "SOURCE_SIGNED_STAGE_INVALID")
            sha = receipt["source_sha"]
            require(signed_source_guard(package, sha), "SOURCE_NOT_REVERIFIED")
            images = build_images(package, sha, signed_source_guard,
                                  builder.transport.docker, builder.prepare_web)
            require(signed_source_guard(package, sha), "HOST_OR_SOURCE_CHANGED")
            require(snapshot(builder.transport.docker) == before,
                    "RUNNING_SERVICE_CHANGED_DURING_BUILD")
            require(before["api"]["image_id"] != images["api_image_id"]
                    and before["web"]["image_id"] != images["web_image_id"],
                    "PREPARED_IMAGE_ALREADY_ACTIVE")
            prepared = {
                "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
                "scope": SCOPE,
                "source_sha": sha,
                "source_asset_sha256": receipt["release_asset_sha256"],
                **images,
                "baseline": before,
                "images_built": True,
                "services_unchanged": True,
                "may_apply": False,
                "fresh_backup_created": False,
                "signed_stage_ephemeral": True,
                "actual_rollback_drill_completed": False,
            }
        if previous is not None:
            # signed release + host already re-verified above: only now archive the APPLIED predecessor
            require(previous["source_sha"] != prepared["source_sha"], "PREPARE_TARGET_ALREADY_APPLIED")
            def revalidate():
                require(snapshot(builder.transport.docker) == before, "PREPARE_RUNTIME_DRIFT_BEFORE_COMMIT")
                require(entry_mod.health(previous["source_sha"]) is True, "PREPARE_PREDECESSOR_HEALTH_UNVERIFIED")
                require(entry_mod.private_json(STATE / "prepared.json") == previous, "PREPARE_PREDECESSOR_CHANGED")
            install_prepared_archiving_predecessor(STATE, previous, prepared, now=int(__import__("time").time()),
                                                   revalidate=revalidate)
        else:
            flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW
            fd = os.open(STATE / "prepared.json", flags, 0o600)
            with os.fdopen(fd, "w") as stream:
                json.dump(prepared, stream, sort_keys=True)
                stream.flush()
                os.fsync(stream.fileno())
        return {
            "status": prepared["status"], "scope": SCOPE,
            "source_sha": prepared["source_sha"],
            "api_web_image_ids_recorded": True,
            "services_unchanged": True,
            "may_apply": False,
            "fresh_backup_created": False,
            "rollback_drill_completed": False,
        }


def recover():
    """Explicit supervised recovery: only resolves prepared-successor-archive-pending.json, nothing else."""
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch", "HOST_NOT_AUTHORIZED")
    bootstrap_import_trust()
    trusted_state()
    with exclusive():
        status = recover_prepare_archive(STATE)
    return {"status": "PREPARE_ARCHIVE_" + status, "scope": SCOPE, "may_apply": False}


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    try:
        require(args in (["--prepare"], ["--recover-prepare-archive"]), "ONLY_EXPLICIT_PREPARE_SUPPORTED")
        print(json.dumps(prepare() if args == ["--prepare"] else recover(), sort_keys=True))
        return 0
    except Blocked as exc:
        print(json.dumps({"status": "BLOCKED_QA525_IMAGE_PREPARATION",
                          "code": str(exc), "scope": SCOPE,
                          "may_apply": False}, sort_keys=True))
        return 2
    except Exception:
        print(json.dumps({"status": "BLOCKED_QA525_IMAGE_PREPARATION",
                          "code": "PREPARATION_NOT_VERIFIED",
                          "scope": SCOPE, "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
