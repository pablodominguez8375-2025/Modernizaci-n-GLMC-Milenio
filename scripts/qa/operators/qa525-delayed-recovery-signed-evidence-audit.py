#!/usr/bin/env python3
"""QA525 unprivileged dual-Sigstore evidence review for delayed launcher recovery.

Verifies separate *local* signed release envelopes A (original cut) and B
(recovery code) using the existing exact-GitHub-source Sigstore auditor,
then reconciles the signed identity with the original receipt and a
short-lived, independently scoped REVIEW approval.

It never issues a root admission, imports signed code, installs a launcher,
reads glmdch host state or authorizes recovery. No privileged entry point.
"""
import hashlib
import importlib.util
import os
from pathlib import Path
import re

SOURCE = re.compile(r"[0-9a-f]{40}\Z")
HASH = re.compile(r"[0-9a-f]{64}\Z")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")
CONTRACT_REL = "scripts/qa/operators/qa525-launcher-replacement-contract.py"
ROOT_EXEC_REL = "scripts/qa/operators/qa525-launcher-supervised-root-install.py"


class Blocked(Exception):
    """Non-secret, stable refusal."""


def require(ok, reason):
    if not ok:
        raise Blocked(reason)


def load_local(name):
    source = Path(__file__).with_name(name)
    spec = importlib.util.spec_from_file_location(
        "qa525_delayed_audit_" + name.replace("-", "_").replace(".", "_"),
        source)
    require(spec is not None and spec.loader is not None and
            source.is_file() and not source.is_symlink(),
            "DELAYED_LOCAL_VERIFIER_MISSING")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def validate_report(report, sha, digest):
    require(type(report) is dict and
            report.get("status") == "SIGNED_INSTALLER_AUDITED_UNPRIVILEGED_NO_INSTALL" and
            report.get("source_sha") == sha and
            report.get("release_asset_sha256") == "sha256:" + digest and
            type(report.get("manifest_sha256")) is str and
            HASH.fullmatch(report["manifest_sha256"]) is not None and
            report.get("bootstrap_admission_created") is False and
            report.get("may_install") is False and
            report.get("may_apply") is False,
            "DELAYED_ATTESTED_ENVELOPE_REPORT_MISMATCH")
    hashes = report.get("privileged_sources_sha256")
    require(type(hashes) is dict and
            all(type(hashes.get(k)) is str and
                HASH.fullmatch(hashes[k]) is not None
                for k in (ROOT_EXEC_REL, CONTRACT_REL)),
            "DELAYED_SIGNED_PRIVILEGED_MEMBERS_MISSING")
    return report


def review_two_signed_envelopes(cut_zip, cut_source_sha, cut_zip_sha256,
                                recovery_zip, recovery_source_sha,
                                recovery_zip_sha256, committed, approval, *,
                                live_sha256, backup_sha256, pending, now,
                                consumed_nonces=(), audit_fn=None):
    """Review A/B only; no host-backed truth, no new certificate or credentials.

    audit_fn injection is for unprivileged hermetic tests; production callers
    must omit it to use GitHub/Sigstore verification of BOTH complete ZIPs.
    """
    require(os.geteuid() != 0, "DELAYED_EVIDENCE_AUDIT_ROOT_FORBIDDEN")
    require(type(now) is int and now > 0 and type(pending) is bool and
            all(type(s) is str and SOURCE.fullmatch(s) is not None
                for s in (cut_source_sha, recovery_source_sha)) and
            all(type(s) is str and HASH.fullmatch(s) is not None
                for s in (cut_zip_sha256, recovery_zip_sha256)),
            "DELAYED_EVIDENCE_IDENTITY_INVALID")
    require(type(committed) is dict and
            committed.get("source_sha") == cut_source_sha and
            committed.get("release_asset_sha256") == "sha256:" + cut_zip_sha256,
            "DELAYED_ORIGINAL_RELEASE_NOT_BOUND_TO_RECEIPT")
    policy_path = Path(__file__).with_name(
        "qa525-launcher-replacement-contract.py")
    require(policy_path.is_file() and not policy_path.is_symlink(),
            "DELAYED_LOCAL_CONTRACT_MISSING")
    pre = policy_path.read_bytes()
    local_sha256 = hashlib.sha256(pre).hexdigest()
    test_override = audit_fn is not None
    if audit_fn is None:
        verifier = load_local("qa525-attested-installer-audit.py")
        audit_fn = verifier.audit
    try:
        a = validate_report(
            audit_fn(Path(cut_zip), cut_source_sha, cut_zip_sha256),
            cut_source_sha, cut_zip_sha256)
        b = validate_report(
            audit_fn(Path(recovery_zip), recovery_source_sha,
                     recovery_zip_sha256),
            recovery_source_sha, recovery_zip_sha256)
    except Blocked:
        raise
    except Exception:
        raise Blocked("DELAYED_ATTESTED_ENVELOPE_VERIFICATION_FAILED") from None
    require(b["privileged_sources_sha256"][CONTRACT_REL] == local_sha256 and
            hashlib.sha256(policy_path.read_bytes()).hexdigest() == local_sha256,
            "DELAYED_LOCAL_CONTRACT_NOT_IN_SIGNED_RECOVERY_RELEASE")
    require(a["source_sha"] == committed["source_sha"] and
            a["release_asset_sha256"] == committed["release_asset_sha256"],
            "DELAYED_ORIGINAL_CUT_SIGNED_IDENTITY_MISMATCH")
    # Import local policy ONLY after the signed release B binds its exact
    # bytes. Never execute unverified local candidate Python even unprivileged.
    policy = load_local("qa525-launcher-replacement-contract.py")
    require(hashlib.sha256(policy_path.read_bytes()).hexdigest() == local_sha256,
            "DELAYED_LOCAL_CONTRACT_CHANGED")
    claim = {
        "purpose": "QA525_DELAYED_RECOVERY_REVIEW_UNTRUSTED",
        "source_sha": recovery_source_sha,
        "release_asset_sha256": "sha256:" + recovery_zip_sha256,
        "manifest_sha256": b["manifest_sha256"],
        "executor_sha256": b["privileged_sources_sha256"][ROOT_EXEC_REL],
        "issued_at": now,
        "expires_at": now + 900,
    }
    try:
        plan = policy.assess_delayed_recovery_plan(
            committed, claim, approval,
            live_sha256=live_sha256, backup_sha256=backup_sha256,
            pending=pending, now=now, consumed_nonces=consumed_nonces)
    except policy.Blocked:
        raise Blocked("DELAYED_DUAL_PROVENANCE_REVIEW_REJECTED") from None
    require(plan["claims_authenticated"] is False and
            plan["may_recover"] is False and
            plan["may_install"] is False and plan["may_apply"] is False,
            "DELAYED_EVIDENCE_AUDIT_MUST_NEVER_AUTHORIZE")
    return {
        "status": ("SYNTHETIC_DUAL_ENVELOPES_REVIEW_NO_AUTHENTICATION"
                   if test_override else
                   "DUAL_SIGNED_ENVELOPES_AUDITED_UNPRIVILEGED_NO_RECOVERY"),
        "cut_source_sha": a["source_sha"],
        "cut_release_asset_sha256": a["release_asset_sha256"],
        "recovery_source_sha": b["source_sha"],
        "recovery_release_asset_sha256": b["release_asset_sha256"],
        "recovery_manifest_sha256": b["manifest_sha256"],
        "recovery_executor_sha256": claim["executor_sha256"],
        "review_status": plan["status"],
        "cut_receipt_authenticated_by_audit": False,
        "approval_authenticated_by_audit": False,
        "release_envelopes_sigstore_checked_unprivileged": not test_override,
        "bootstrap_admission_created": False,
        "may_install": False, "may_apply": False, "may_recover": False,
        "physical_host_touched": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_DELAYED_EVIDENCE_LIBRARY_ONLY_NO_HOST_ENTRYPOINT")
