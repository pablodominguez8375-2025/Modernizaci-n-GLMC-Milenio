#!/usr/bin/env python3
"""Pure, fail-closed QA525 API+WEB reversible-transaction receipt contract.

This validator operates on synthetic/externally supplied JSON only. It NEVER
authorizes deployment: formatted receipts are NOT evidence of host ownership,
completed backups, actual Sigstore verification, or tested recovery.

A separate, audited host operator must reproduce every claimed measurement.
No Docker, network, SSH, files or secrets are accessed except bounded stdin.
"""
import json
import re
import sys

SHA = re.compile(r"^[0-9a-f]{40}$")
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
IMAGES = re.compile(r"^sha256:[0-9a-f]{64}$")
CONTAINER_IDS = re.compile(r"^[0-9a-f]{64}$")
SERVICES = frozenset(("api", "web", "keycloak", "postgres", "minio",
                      "gateway", "clamav"))
DATABASES = frozenset(("pmgm", "pmgm_keycloak"))
SCOPE = "OFFLINE_RECEIPT_CONTRACT_ONLY_NO_HOST_ATTESTATION_NO_DEPLOYMENT"


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def mapping(item, code):
    require(type(item) is dict, code)
    return item


def sha(item):
    return type(item) is str and SHA.fullmatch(item) is not None


def digest(item):
    return type(item) is str and DIGEST.fullmatch(item) is not None


def image(item):
    return type(item) is str and IMAGES.fullmatch(item) is not None


def evaluate(document):
    data = mapping(document, "DOCUMENT_INVALID")
    require(set(data) == {"source", "policy", "preflight", "staging",
                          "prepared", "baseline", "backups", "rollback"},
            "DOCUMENT_FIELDS_INVALID")
    source = mapping(data["source"], "SOURCE_INVALID")
    target = source.get("source_sha")
    require(sha(target), "SOURCE_SHA_INVALID")
    require(source.get("status") == "READY_FOR_SIGNATURE_VERIFICATION_ONLY"
            and source.get("release_tag") == "qa-signed-" + target
            and digest(source.get("release_asset_sha256")),
            "SOURCE_RELEASE_NOT_VERIFIED")

    policy = mapping(data["policy"], "CHANGE_POLICY_INVALID")
    baseline_api = policy.get("analytical_baseline_sha")
    require(policy.get("status") == "ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY"
            and policy.get("source_sha") == target
            and sha(baseline_api) and baseline_api != target
            and policy.get("reasons") == []
            and policy.get("may_apply") is False,
            "SENSITIVE_OR_UNREVIEWED_CHANGE")

    preflight = mapping(data["preflight"], "PREFLIGHT_INVALID")
    base_web = preflight.get("baseline_web_sha")
    require(preflight.get("status") == "HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED"
            and preflight.get("source_sha") == target
            and preflight.get("baseline_api_sha") == baseline_api
            and sha(base_web)
            and preflight.get("may_build") is False
            and preflight.get("may_apply") is False
            and preflight.get("fresh_backup_created") is False,
            "HOST_PREFLIGHT_RECEIPT_INVALID")

    staged = mapping(data["staging"], "STAGING_INVALID")
    require(staged.get("status") == "SIGNED_SOURCE_AND_HOST_RECHECKED_NOT_PREPARED"
            and staged.get("source_sha") == target
            and staged.get("release_asset_sha256") ==
            source["release_asset_sha256"]
            and staged.get("host_checked_twice") is True
            and staged.get("signed_stage_executed") is True
            and staged.get("may_apply") is False
            and staged.get("fresh_backup_created") is False,
            "SIGNED_STAGING_RECEIPT_INVALID")

    prepared = mapping(data["prepared"], "PREPARED_INVALID")
    require(prepared.get("status") ==
            "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT"
            and prepared.get("source_sha") == target
            and prepared.get("source_asset_sha256") ==
            source["release_asset_sha256"]
            and prepared.get("images_built") is True
            and prepared.get("services_unchanged") is True
            and prepared.get("may_apply") is False,
            "PREPARED_RECEIPT_INVALID")
    api_image, web_image = prepared.get("api_image_id"), prepared.get("web_image_id")
    require(image(api_image) and image(web_image) and api_image != web_image,
            "PREPARED_IMAGE_IDENTITIES_INVALID")

    runtime = mapping(data["baseline"], "BASELINE_INVALID")
    require(set(runtime) == SERVICES, "SEVEN_SERVICE_BASELINE_INCOMPLETE")
    for name, record in runtime.items():
        v = mapping(record, "SERVICE_RECEIPT_INVALID")
        require(type(v.get("container_fingerprint")) is str
                and CONTAINER_IDS.fullmatch(v["container_fingerprint"])
                and image(v.get("image_id"))
                and v.get("running") is True, "SERVICE_RECEIPT_INVALID")
    require(runtime["api"]["image_id"] != api_image
            and runtime["web"]["image_id"] != web_image,
            "BASELINE_REPLACED_BEFORE_TRANSACTION")

    backups = mapping(data["backups"], "BACKUPS_INVALID")
    require(set(backups) == DATABASES, "BOTH_DATABASES_NOT_BACKED_UP")
    for name, record in backups.items():
        v = mapping(record, "BACKUP_RECEIPT_INVALID")
        require(digest(v.get("sha256")) and type(v.get("size")) is int
                and v["size"] > 100 and
                v.get("pg_restore_list_verified") is True
                and v.get("offhost_custody_verified") is True
                and v.get("restore_drill_verified") is True,
                "BACKUP_OR_RESTORE_EVIDENCE_MISSING")

    rollback = mapping(data["rollback"], "ROLLBACK_INVALID")
    require(set(rollback) == {"api_restored_in_drill", "web_restored_in_drill",
                              "identity_preserved_in_drill",
                              "migrations_unchanged_in_drill",
                              "auxiliary_services_unchanged_in_drill",
                              "old_image_ids_retained"},
            "ROLLBACK_EVIDENCE_INCOMPLETE")
    require(all(v is True for v in rollback.values()),
            "ROLLBACK_DRILL_NOT_COMPLETE")

    # Even perfect JSON cannot attest that a real host was examined or that
    # backups exist. Only a separately audited, physical operator may decide.
    return {
        "status": "STRUCTURAL_CONTRACT_PASS_PHYSICAL_REVIEW_REQUIRED",
        "scope": SCOPE,
        "source_sha": target,
        "baseline_api_sha": baseline_api,
        "baseline_web_sha": base_web,
        "database_receipt_count": len(backups),
        "service_receipt_count": len(runtime),
        "may_build": False,
        "may_apply": False,
        "host_attested": False,
        "backup_verified_by_this_tool": False,
        "rollback_executed_by_this_tool": False,
    }


def main():
    try:
        if sys.argv[1:]:
            raise Blocked("NO_OPTIONS_ALLOWED")
        payload = sys.stdin.buffer.read(65537)
        require(len(payload) <= 65536, "INPUT_TOO_LARGE")
        result = evaluate(json.loads(payload))
        print(json.dumps(result, sort_keys=True))
        return 0
    except (Blocked, ValueError, UnicodeDecodeError, TypeError, KeyError):
        print(json.dumps({"status": "BLOCKED_QA525_TRANSACTION_CONTRACT",
                          "scope": SCOPE, "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
