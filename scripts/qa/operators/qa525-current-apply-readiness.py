#!/usr/bin/env python3
"""QA525 supervised API+WEB readiness: host evidence, NEVER apply.

This is an inspection gate, not an installation or deployment. It checks the
root-owned prepared receipt, signed-release metadata, current host preflight,
both image IDs and running seven-service snapshot. Backups, Sigstore replay,
rollback testing and approval are deliberately NOT asserted by this probe.
"""
import importlib.util
import json
import os
from pathlib import Path
import re
import socket
import stat
import sys

HERE = Path(__file__).resolve().parent
QA = HERE.parent
PREPARE = HERE / "qa525-current-image-preparation.py"
STATE = Path("/opt/centenario/qa525-signed-prepare")
RECEIPT = STATE / "prepared.json"
SHA = re.compile(r"^[0-9a-f]{40}$")
IMAGE = re.compile(r"^sha256:[0-9a-f]{64}$")
SCOPE = "HOST_READINESS_ONLY_NO_APPLY_NO_BACKUP_NO_UAT"

# Explicit dependency allowlist, inspected before executing any local Python.
SOURCE_CHAIN = (
    Path(__file__),
    PREPARE,
    HERE / "qa525-current-host-staging.py",
    HERE / "qa525-current-host-preflight.py",
    HERE / "qa525-current-change-policy.py",
    HERE / "qa525-host-baseline-6ff4-readonly.py",
    QA / "qa525-stage-current-release.py",
    QA / "qa525-current-release-gate.py",
    QA / "qa-agent-readiness.py",
    QA / "qa-public-release-download.py",
    QA / "qa-signed-staging.py",
    QA / "qa-lite-web-build.py",
    QA / "qa-lite-compose-adapter.py",
)


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def trusted(path, directory=False):
    require(path.is_absolute(), "UNTRUSTED_SOURCE_PATH")
    for item in (path, *path.parents):
        try:
            v = item.lstat()
        except OSError:
            raise Blocked("MISSING_TRUSTED_SOURCE") from None
        require(not stat.S_ISLNK(v.st_mode)
                and v.st_uid == 0
                and not stat.S_IMODE(v.st_mode) & 0o022,
                "UNTRUSTED_SOURCE_OR_PARENT")
    require(path.is_dir() if directory else path.is_file(), "UNTRUSTED_SOURCE_TYPE")


def private_receipt(path):
    trusted(path)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, "rb") as fh:
        st = os.fstat(fh.fileno())
        require(stat.S_ISREG(st.st_mode)
                and st.st_uid == 0
                and stat.S_IMODE(st.st_mode) == 0o600
                and st.st_nlink == 1
                and 1 <= st.st_size <= 65536,
                "PRIVATE_PREPARED_RECEIPT_UNTRUSTED")
        data = fh.read(65537)
        require(len(data) == st.st_size, "PREPARED_RECEIPT_CHANGED")
    try:
        return json.loads(data)
    except (TypeError, ValueError):
        raise Blocked("PREPARED_RECEIPT_INVALID_JSON") from None


def image_identity(docker, image_id, target):
    require(type(image_id) is str and IMAGE.fullmatch(image_id),
            "PREPARED_IMAGE_ID_INVALID")
    actual = docker(["image", "inspect", "--format", "{{.Id}}", image_id])
    label = docker(["image", "inspect", "--format",
                    '{{index .Config.Labels "org.opencontainers.image.revision"}}',
                    image_id])
    require(actual == image_id and label == target,
            "PREPARED_IMAGE_IDENTITY_CHANGED")


def evaluate(receipt, before, after, host, source, checked_images):
    """Pure consistency gate; no JSON field may authorize deployment."""
    require(type(receipt) is dict and type(before) is dict
            and type(after) is dict and type(host) is dict
            and type(source) is dict, "EVIDENCE_INVALID")
    target = source.get("source_sha")
    require(type(target) is str and SHA.fullmatch(target)
            and source.get("status") == "READY_FOR_SIGNATURE_VERIFICATION_ONLY"
            and source.get("release_tag") == "qa-signed-" + target
            and type(source.get("release_asset_sha256")) is str
            and IMAGE.fullmatch(source["release_asset_sha256"]),
            "CURRENT_SIGNED_RELEASE_METADATA_INVALID")
    require(host.get("status") == "HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED"
            and host.get("source_sha") == target
            and host.get("may_apply") is False
            and host.get("may_build") is False
            and host.get("fresh_backup_created") is False,
            "HOST_PREFLIGHT_NOT_ELIGIBLE")
    require(receipt.get("status") ==
            "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT"
            and receipt.get("scope") ==
            "BUILD_ONLY_SIGNED_API_WEB_NO_CONTAINER_CHANGES_NO_APPLY"
            and receipt.get("source_sha") == target
            and receipt.get("source_asset_sha256") ==
            source["release_asset_sha256"]
            and receipt.get("images_built") is True
            and receipt.get("services_unchanged") is True
            and receipt.get("may_apply") is False
            and receipt.get("fresh_backup_created") is False
            and receipt.get("signed_stage_ephemeral") is True
            and receipt.get("actual_rollback_drill_completed") is False,
            "PREPARED_RECEIPT_NOT_ACCEPTABLE")
    require(set(before) == {"api", "web", "keycloak", "postgres",
                            "minio", "gateway", "clamav"}
            and before == after and receipt.get("baseline") == before,
            "PHYSICAL_SERVICES_CHANGED")
    images = (receipt.get("api_image_id"), receipt.get("web_image_id"))
    require(all(type(img) is str and IMAGE.fullmatch(img) for img in images)
            and images[0] != images[1]
            and images[0] != before["api"]["image_id"]
            and images[1] != before["web"]["image_id"],
            "CANDIDATE_IMAGES_INVALID")
    require(checked_images == images, "PHYSICAL_IMAGE_VERIFICATION_MISSING")
    return {
        "status": "PREPARED_IMAGES_MATCH_LIVE_QA_BACKUP_AND_ROLLBACK_PENDING",
        "scope": SCOPE,
        "source_sha": target,
        "release_asset_sha256": source["release_asset_sha256"],
        "seven_services_unchanged": True,
        "candidate_images_present": True,
        "signature_reverified_by_this_probe": False,
        "backup_offhost_verified_by_this_probe": False,
        "rollback_drill_verified_by_this_probe": False,
        "may_apply": False,
    }


def load_after_trust():
    for path in SOURCE_CHAIN:
        trusted(path)
    spec = importlib.util.spec_from_file_location("qa525_trusted_prepare", PREPARE)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def inspect():
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "HOST_NOT_AUTHORIZED")
    require(not RECEIPT.is_symlink() and RECEIPT.exists(), "PREPARED_RECEIPT_MISSING")
    helper = load_after_trust()
    trusted(STATE, directory=True)
    require(stat.S_IMODE(STATE.stat().st_mode) == 0o700,
            "PRIVATE_STATE_MODE_INVALID")
    # Metadata/release and live host checks may refuse when Program/Identity
    # changed. This never overrides the SENSITIVE_CHANGES_REQUIRE_REVIEW policy.
    host1 = helper.stage.validated_host_check()
    require(host1.get("status") == "HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED",
            "HOST_NOT_ELIGIBLE")
    source1 = helper.stage.signed.gate.evaluate(
        helper.stage.signed.gate.readiness.fetch_public)
    before = helper.snapshot(helper.builder.transport.docker)
    receipt = private_receipt(RECEIPT)
    checked = []
    for key in ("api_image_id", "web_image_id"):
        image_id = receipt.get(key)
        image_identity(helper.builder.transport.docker, image_id,
                       source1["source_sha"])
        checked.append(image_id)
    after = helper.snapshot(helper.builder.transport.docker)
    host2 = helper.stage.validated_host_check()
    source2 = helper.stage.signed.gate.evaluate(
        helper.stage.signed.gate.readiness.fetch_public)
    require(host1 == host2 and source1 == source2,
            "HOST_OR_CURRENT_SOURCE_CHANGED_DURING_INSPECTION")
    return evaluate(receipt, before, after, host2, source2, tuple(checked))


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    try:
        require(args == ["--inspect"], "ONLY_INSPECTION_ALLOWED")
        print(json.dumps(inspect(), sort_keys=True))
        return 0
    except Blocked as exc:
        print(json.dumps({"status": "BLOCKED_QA525_APPLY_READINESS",
                          "code": str(exc), "scope": SCOPE,
                          "may_apply": False}, sort_keys=True))
        return 2
    except Exception:
        print(json.dumps({"status": "BLOCKED_QA525_APPLY_READINESS",
                          "code": "HOST_EVIDENCE_NOT_VERIFIED", "scope": SCOPE,
                          "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
