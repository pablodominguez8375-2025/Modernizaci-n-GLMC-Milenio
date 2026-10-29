#!/usr/bin/env python3
"""QA525 dual-image rollback planning from sanitized host inventory.

PURE OFFLINE CONTRACT: never loads Docker, restarts services, reads secrets or
authorizes apply. Inventory JSON is UNTRUSTED until reproduced on glmdch
under exclusive locks; host labels/filenames are NOT source attestations.
"""
import json
from pathlib import Path
import re
import sys

SCOPE = "OFFLINE_API_WEB_ROLLBACK_PLAN_NO_HOST_ATTESTATION_NO_APPLY"
SERVICES = ("api", "web", "keycloak", "postgres",
            "minio", "gateway", "clamav")
ONESHOT = ("db-grants", "migrator", "minio-init")
CID = re.compile(r"[0-9a-f]{64}\Z")
IMAGE = re.compile(r"sha256:[0-9a-f]{64}\Z")
SHA = re.compile(r"[0-9a-f]{40}\Z")
FULL_SHA = re.compile(r"[0-9a-f]{64}\Z")
API_LAYER = re.compile(r"api-[0-9a-f]{40}\.json\Z")
WEB_LAYER = re.compile(r"web-[0-9a-f]{64}\.json\Z")
ENVIRONMENT = "QA_SYNTHETIC"


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def trusted_shape(path):
    """Syntax only, not host path trust or filesystem inspection."""
    require(type(path) is str and 5 < len(path) < 1024
            and not any(x in path for x in ("\n", "\r", "\x00", "\\"))
            and path.startswith("/")
            and all(x not in ("", ".", "..") for x in path.split("/")[1:]),
            "UNTRUSTED_COMPOSE_PATH")
    require(path.startswith("/opt/centenario/")
            or path.startswith("/etc/pmgm/"),
            "UNEXPECTED_COMPOSE_PATH_ROOT")
    return Path(path)


def inventory_layers(inv):
    require(type(inv) is dict
            and inv.get("status") == "PHYSICAL_METADATA_CAPTURED_NO_CHANGE_AUTHORITY"
            and inv.get("project") == "centenario-qa-lab2"
            and inv.get("scope") == "HOST_INVENTORY_READONLY_NO_SECRETS_NO_INSTALL_NO_APPLY"
            and inv.get("may_apply") is False
            and inv.get("may_prepare") is False
            and inv.get("identity_verified") is False
            and inv.get("current_git_release_identified") is False,
            "HOST_INVENTORY_CONTRACT_INVALID")
    rows = inv.get("services")
    require(type(rows) is dict and set(rows) == set(SERVICES),
            "SEVEN_SERVICE_INVENTORY_INCOMPLETE")
    work = trusted_shape(inv.get("working_dir"))
    require(work.name == "recovery-v2", "UNREVIEWED_COMPOSE_WORKDIR")
    for name in SERVICES:
        row = rows[name]
        require(type(row) is dict
                and type(row.get("container_id")) is str
                and CID.fullmatch(row["container_id"])
                and type(row.get("image_id")) is str
                and IMAGE.fullmatch(row["image_id"])
                and row.get("status") == "running"
                and type(row.get("compose_files_count")) is int
                and 1 <= row["compose_files_count"] <= 12,
                "CONTAINER_METADATA_INVALID")
    require(len({rows[k]["container_id"] for k in SERVICES}) == len(SERVICES),
            "DUPLICATE_DOCKER_CONTAINER_ID")
    require(rows["api"]["compose_files_count"] == 3
            and rows["web"]["compose_files_count"] == 2,
            "UNREVIEWED_API_WEB_COMPOSE_LAYER_COUNT")
    configs = inv.get("compose_file_digests")
    require(type(configs) is list and len(configs) == 4,
            "EXPECTED_FOUR_DISTINCT_COMPOSE_LAYERS")
    names = {}
    for pair in configs:
        require(type(pair) is dict and set(pair) == {"path", "sha256"}
                and type(pair.get("sha256")) is str
                and FULL_SHA.fullmatch(pair["sha256"]),
                "COMPOSE_DIGEST_INVALID")
        p = trusted_shape(pair["path"])
        require(str(p) not in names, "DUPLICATE_COMPOSE_LAYER")
        names[str(p)] = pair["sha256"]
    base = str(work / "compose.recovery.json")
    require(base in names, "SHARED_BASE_COMPOSE_MISSING")
    api_layers = [p for p in names if API_LAYER.fullmatch(Path(p).name)]
    web_layers = [p for p in names if WEB_LAYER.fullmatch(Path(p).name)]
    identity_layers = [p for p in names if Path(p).name == "qa754-identity-overlay.stage.json"]
    require(len(api_layers) == len(web_layers) == len(identity_layers) == 1,
            "REQUIRED_API_WEB_IDENTITY_LAYERS_MISSING")
    require(Path(api_layers[0]).parent == work.parent.parent / "qa-api-refresh",
            "UNEXPECTED_API_OVERRIDE_PARENT")
    require(Path(web_layers[0]).parent in (work, Path("/opt/centenario/qa-auto-lite")),
            "UNEXPECTED_WEB_OVERRIDE_PARENT")
    require(identity_layers[0] == "/etc/pmgm/qa754-identity-overlay.stage.json",
            "IDENTITY_OVERLAY_CHANGED")
    require(WEB_LAYER.fullmatch(Path(web_layers[0]).name).group(0)[4:68]
            == rows["web"]["image_id"][7:],
            "WEB_OVERRIDE_DOES_NOT_MATCH_IMAGE")
    require(set(names) == {base, *api_layers, *web_layers, *identity_layers},
            "UNREVIEWED_COMPOSE_EXTRA_LAYER")
    return {"base": base, "api": api_layers[0],
            "web": web_layers[0], "identity": identity_layers[0],
            "digests": names}


def plan(inventory, prepared, release):
    """Deterministic reconciliation; every successful result still DENIES apply."""
    layers = inventory_layers(inventory)
    require(type(prepared) is dict and type(release) is dict,
            "PREPARED_OR_RELEASE_INVALID")
    source = release.get("source_sha")
    asset = release.get("release_asset_sha256")
    require(type(source) is str and SHA.fullmatch(source)
            and type(asset) is str and IMAGE.fullmatch(asset)
            and release.get("status") == "READY_FOR_SIGNATURE_VERIFICATION_ONLY"
            and release.get("release_tag") == "qa-signed-" + source,
            "SIGNED_RELEASE_METADATA_INVALID")
    require(prepared.get("status") ==
            "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT"
            and prepared.get("source_sha") == source
            and prepared.get("source_asset_sha256") == asset
            and prepared.get("images_built") is True
            and prepared.get("services_unchanged") is True
            and prepared.get("may_apply") is False
            and prepared.get("fresh_backup_created") is False,
            "PREPARED_CUT_NOT_COHERENT")
    baseline = prepared.get("baseline")
    require(type(baseline) is dict and set(baseline) == set(SERVICES),
            "PREPARED_SEVEN_SERVICE_BASELINE_MISSING")
    for name in SERVICES:
        old = baseline[name]
        current = inventory["services"][name]
        require(type(old) is dict
                and old.get("container_fingerprint") == current["container_id"]
                and old.get("image_id") == current["image_id"]
                and old.get("running") is True,
                "PHYSICAL_BASELINE_DRIFT")
    api_new, web_new = prepared.get("api_image_id"), prepared.get("web_image_id")
    require(type(api_new) is str and type(web_new) is str
            and IMAGE.fullmatch(api_new) and IMAGE.fullmatch(web_new)
            and api_new != web_new
            and api_new != inventory["services"]["api"]["image_id"]
            and web_new != inventory["services"]["web"]["image_id"],
            "NEW_IMAGES_NOT_DISTINCT")
    return {
        "status": "ROLLBACK_PLAN_STRUCTURALLY_COHERENT_HOST_ATTESTATION_REQUIRED",
        "scope": SCOPE,
        "source_sha": source,
        "release_asset_sha256": asset,
        "rollback_images": {
            name: inventory["services"][name]["image_id"]
            for name in ("api", "web")
        },
        "candidate_images": {"api": api_new, "web": web_new},
        "compose_layer_sha256": {
            name: layers["digests"][path] for name, path in
            (("base", layers["base"]), ("api", layers["api"]),
             ("web", layers["web"]), ("identity", layers["identity"]))
        },
        "must_preserve_services": list(SERVICES[2:]),
        "must_preserve_completed_jobs": list(ONESHOT),
        "host_runtime_rechecked_by_this_tool": False,
        "backup_offhost_verified": False,
        "restore_drill_verified": False,
        "sigstore_download_verified": False,
        "migration_review_approved": False,
        "single_use_supervisor_approval_verified": False,
        "may_apply": False,
    }


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    try:
        require(args == [], "NO_OPTIONS_ALLOWED")
        data = sys.stdin.buffer.read(65537)
        require(0 < len(data) <= 65536, "INPUT_TOO_LARGE")
        d = json.loads(data)
        require(type(d) is dict and set(d) == {"inventory", "prepared", "release"},
                "DOCUMENT_FIELDS_INVALID")
        print(json.dumps(plan(d["inventory"], d["prepared"], d["release"]),
                         sort_keys=True))
        return 0
    except (Blocked, ValueError, TypeError, KeyError):
        print(json.dumps({"status": "BLOCKED_QA525_ROLLBACK_PLAN",
                          "scope": SCOPE, "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
