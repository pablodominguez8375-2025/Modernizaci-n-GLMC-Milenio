#!/usr/bin/env python3
"""Stage only the latest publicly attested QA cut after two physical preflights.

This operator is intentionally NOT an image builder or deployer. A sensitive
API/identity/schema change is BLOCKED before download and before disk writes.
Never install on glmdch until the first execution is separately authorized.
"""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import socket
import sys
import tempfile

OPERATOR_DIR = Path(__file__).resolve().parent
QA_DIR = OPERATOR_DIR.parent
SCOPE = "SIGNED_HOST_STAGING_ONLY_NO_IMAGE_BUILD_NO_APPLY_NO_UAT"


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


host = load("qa525_combined_host_preflight", OPERATOR_DIR / "qa525-current-host-preflight.py")
signed = load("qa525_combined_signed_staging", QA_DIR / "qa525-stage-current-release.py")


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def validated_host_check():
    """No remote code runs: checked local, read-only source and policy modules."""
    for path in (Path(__file__), OPERATOR_DIR / "qa525-current-host-preflight.py",
                 OPERATOR_DIR / "qa525-current-change-policy.py",
                 OPERATOR_DIR / "qa525-update-readiness-0bd927b.py",
                 QA_DIR / "qa525-stage-current-release.py",
                 QA_DIR / "qa525-current-release-gate.py",
                 QA_DIR / "qa-agent-readiness.py",
                 QA_DIR / "qa-public-release-download.py",
                 QA_DIR / "qa-signed-staging.py"):
        host.trusted_source(path)
    require(host.digest(host.PREFLIGHT_FILE) == host.PREFLIGHT_SHA256,
            "REVIEWED_PREFLIGHT_SHA_CHANGED")
    candidate = host.module("qa525_combined_policy", host.POLICY_FILE)
    preflight = host.module("qa525_combined_preflight", host.PREFLIGHT_FILE)
    return host.check_host(candidate.gate.readiness.fetch_public,
                           candidate, preflight)


def stage_current(output):
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "HOST_NOT_AUTHORIZED")
    output = signed.trusted_destination(output)
    # Forbid metadata writes until both policy and physical baseline pass.
    first = validated_host_check()
    require(first.get("status") == "HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED"
            and first.get("may_build") is False
            and first.get("may_apply") is False,
            "HOST_PREFLIGHT_NOT_ELIGIBLE")
    with tempfile.TemporaryDirectory(prefix=".qa525-candidate-", dir=output.parent) as tmp:
        work = Path(tmp)
        staging_target = work / "signed-package"
        result = signed.verify_current(staging_target)
        require(result.get("status") == "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED"
                and result.get("scope") == signed.SCOPE
                and result.get("source_sha") == first["source_sha"],
                "SIGNED_PACKAGE_SOURCE_OR_RECEIPT_MISMATCH")
        # Recheck live release, schema/identity risk and actual host state.
        second = validated_host_check()
        require(second == first, "HOST_OR_SIGNED_DEV_CHANGED_DURING_STAGING")
        signed.trusted_destination(output)
        os.replace(staging_target, output)
    return {
        "status": "SIGNED_SOURCE_AND_HOST_RECHECKED_NOT_PREPARED",
        "scope": SCOPE,
        "source_sha": first["source_sha"],
        "release_asset_sha256": result["release_asset_sha256"],
        "host_checked_twice": True,
        "signed_stage_executed": True,
        "api_web_images_built": False,
        "fresh_backup_created": False,
        "may_build": False,
        "may_apply": False,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage", action="store_true", required=True)
    parser.add_argument("--output", required=True, type=Path)
    try:
        args = parser.parse_args(argv)
        os.umask(0o077)
        print(json.dumps(stage_current(args.output), sort_keys=True))
        return 0
    except Blocked as exc:
        print(json.dumps({"status": "BLOCKED_QA525_HOST_STAGING",
                          "scope": SCOPE, "code": str(exc)}, sort_keys=True))
        return 2
    except Exception:
        print(json.dumps({"status": "BLOCKED_QA525_HOST_STAGING",
                          "scope": SCOPE, "code": "STAGING_OR_HOST_EVIDENCE_UNAVAILABLE"},
                         sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
