#!/usr/bin/env python3
"""Stage current, attested synthetic QA installable; never deploy or execute it.

The remote metadata gate, tokenless public downloader and Sigstore verifier
remain independent fail-closed boundaries. This orchestrator only creates a
private local directory containing the verified package after rechecking dev.
"""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import stat
import sys
import tempfile

ROOT = Path(__file__).resolve().parent
SCOPE = "LOCAL_STAGING_ONLY_NO_DOCKER_NO_HOST_DEPLOYMENT_NO_UAT"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / filename)
    item = importlib.util.module_from_spec(spec)
    sys.modules[name] = item
    spec.loader.exec_module(item)
    return item


gate = load("qa525_stage_gate", "qa525-current-release-gate.py")
download = load("qa525_stage_release_download", "qa-public-release-download.py")
staging = load("qa525_stage_signature", "qa-signed-staging.py")


class Blocked(Exception):
    """Stable non-sensitive error code."""


def require(value, code):
    if not value:
        raise Blocked(code)


def trusted_destination(output):
    output = Path(output)
    require(output.is_absolute() and output.name not in (".", ".."),
            "OUTPUT_MUST_BE_ABSOLUTE")
    parent = output.parent
    try:
        st = parent.lstat()
    except OSError:
        raise Blocked("PRIVATE_STAGING_PARENT_MISSING") from None
    require(stat.S_ISDIR(st.st_mode) and not parent.is_symlink() and
            st.st_uid == os.geteuid() and stat.S_IMODE(st.st_mode) == 0o700,
            "PRIVATE_STAGING_PARENT_UNTRUSTED")
    require(not output.exists() and not output.is_symlink(),
            "DESTINATION_ALREADY_EXISTS")
    return output


def verify_current(output, *, selection=None):
    output = trusted_destination(output)
    # Internal callers can supply the full gate they JUST evaluated in this
    # operation. Not a CLI option or persisted receipt. Signature verification
    # and the independent, full post-download gate below are never skipped.
    first = gate.evaluate(gate.readiness.fetch_public) if selection is None else dict(selection)
    require(first.get("status") == "READY_FOR_SIGNATURE_VERIFICATION_ONLY" and
            first.get("scope") == gate.RESULT_SCOPE,
            "CURRENT_RELEASE_NOT_APPROVED")
    sha = first["source_sha"]
    expected = first["release_asset_sha256"]
    selection = {
        "status": "READY_FOR_SIGNED_STAGING_ONLY",
        "source_sha": sha,
        "artifact_sha256": expected,
    }
    # All temporary files (including the envelope) are removed on error.
    with tempfile.TemporaryDirectory(prefix=".qa525-signed-", dir=output.parent) as tmp:
        workspace = Path(tmp)
        archive = workspace / "release.zip"
        downloaded = download.download(selection, archive)
        require(downloaded.get("source_sha") == sha and
                downloaded.get("sha256") == expected,
                "PUBLIC_DOWNLOAD_RECEIPT_INVALID")
        package = workspace / "verified-package"
        receipt = staging.stage(archive, expected, sha, package, tokenless=True)
        require(receipt.get("status") == "SIGNED_CODE_STAGED_NOT_INSTALLED" and
                receipt.get("source_sha") == sha,
                "SIGNED_STAGING_RECEIPT_INVALID")
        # A second full gate invalidates the staged candidate if dev, any
        # workflow result, artifact or release moved during the download.
        second = (gate.evaluate(gate.readiness.fetch_public) if selection is None else
                  gate.evaluate(gate.readiness.fetch_public, expected_sha=sha))
        require(second == first, "CURRENT_RELEASE_CHANGED_DURING_STAGING")
        require(not output.exists() and not output.is_symlink(),
                "DESTINATION_CHANGED_DURING_STAGING")
        os.replace(package, output)
    return {
        "status": "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED",
        "scope": SCOPE,
        "source_sha": sha,
        "release_tag": first["release_tag"],
        "release_asset_sha256": expected,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    try:
        args = parser.parse_args(argv)
        print(json.dumps(verify_current(args.output), sort_keys=True))
        return 0
    except (Blocked, gate.Blocked, gate.readiness.GateFailure,
            download.Blocked, staging.Blocked) as exc:
        print(json.dumps({"status": "BLOCKED_QA525_SIGNED_STAGING",
                          "scope": SCOPE, "code": str(exc)}, sort_keys=True))
        return 1
    except (OSError, ValueError, KeyError, TypeError, AttributeError):
        print(json.dumps({"status": "BLOCKED_QA525_SIGNED_STAGING",
                          "scope": SCOPE, "code": "VERIFICATION_OR_STAGING_FAILED"},
                         sort_keys=True))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
