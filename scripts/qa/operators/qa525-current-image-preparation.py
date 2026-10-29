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
            trusted(lockfile.parent, directory=True)
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


def prepare():
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "HOST_NOT_AUTHORIZED")
    bootstrap_import_trust()
    trusted_state()
    require(not (STATE / "prepared.json").exists()
            and not (STATE / "prepared.json").is_symlink(),
            "PREPARE_ALREADY_EXISTS")
    with exclusive():
        before = snapshot(builder.transport.docker)
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


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    try:
        require(args == ["--prepare"], "ONLY_EXPLICIT_PREPARE_SUPPORTED")
        print(json.dumps(prepare(), sort_keys=True))
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
