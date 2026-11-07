#!/usr/bin/env python3
"""QA525 B4 fail-closed OPS/APP bridge — INACTIVE; never a root bootstrap.

The two injected verifiers MUST be independently authenticated by a trusted
UNPRIVILEGED entrypoint before the first root byte. In particular, a 'True'
from a fake verifier or a fingerprint copied from GitHub is NOT authority.
Use existing OPS SSH Ed25519 contract (#902) and APP Sigstore audited release.
This module has no process, filesystem, network, Docker or host access.
"""
import hashlib
import re

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
WORKFLOW = REPO + "/.github/workflows/qa-silo-rebuild.yml"
ISSUER = "https://token.actions.githubusercontent.com"
STATUS = "QA525_DUAL_GATE_CONTRACT_ONLY_PRE_ROOT_AUTHORITY_NOT_ESTABLISHED"
SHA40 = re.compile(r"[a-f0-9]{40}\Z")
SHA256 = re.compile(r"sha256:[a-f0-9]{64}\Z")
FP = re.compile(r"SHA256:[A-Za-z0-9+/]{43}\Z")
FIELDS = frozenset({
    "ops_source_sha", "app_source_sha", "ops_fingerprint",
    "policy_hash", "program_sha256", "readiness_sha256", "staging_sha256",
    "app_archive_sha256", "app_bundle_sha256",
    "repository", "app_signer_workflow", "app_oidc_issuer",
})
RECEIPT_FIELDS = frozenset({
    "schema", "status", "ops_source_sha", "program_sha256",
    "readiness_sha256", "staging_sha256", "policy_hash",
    "app_archive_sha256",
})


class Stop(Exception):
    """Only fixed error codes; no unsigned details or secret text."""


def _digest(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def verify_dual(*, receipt_bytes, signature_bytes, allowed_signers_bytes,
                app_bundle_bytes, pins, verify_ops, verify_app):
    """Evaluate TWO independent verification ports against one immutable cut.

    verify_ops(receipt, signature, allowed_signers, fingerprint, policy_hash,
               app_archive_sha256) MUST call OPS signature verifier with
               expected_app_archive_sha256 (strict v3, NEVER v2 fallback).
    verify_app(bundle, challenge) must invoke a ROOTLESS actual Sigstore
       verifier with expected repository/workflow/issuer/source/digest.
    Neither callback's truthfulness/provenance can be proven by this module.

    This is not a permission for Python root, launcher installation or APPLY.
    """
    if (type(pins) is not dict or set(pins) != FIELDS
            or any(type(x) is not bytes or len(x) == 0
                   for x in (receipt_bytes, signature_bytes, allowed_signers_bytes,
                             app_bundle_bytes))
            or len(receipt_bytes) > 65536 or len(signature_bytes) > 4096
            or len(allowed_signers_bytes) > 1024
            or len(app_bundle_bytes) > 1_000_000
            or not callable(verify_ops) or not callable(verify_app)):
        raise Stop("DUAL_INPUT_INVALID")
    frozen = dict(pins)
    # OPS source is the operational-code commit; APP source is the attested
    # application commit. They MAY be equal. Independent *signers* and an
    # authenticated cross-artifact binding are required for any future GO.
    if (any(type(frozen[k]) is not str or SHA40.fullmatch(frozen[k]) is None
            for k in ("ops_source_sha", "app_source_sha"))
            or type(frozen["ops_fingerprint"]) is not str
            or FP.fullmatch(frozen["ops_fingerprint"]) is None
            or any(type(frozen[k]) is not str or SHA256.fullmatch(frozen[k]) is None
                   for k in ("policy_hash", "program_sha256", "readiness_sha256",
                             "staging_sha256", "app_archive_sha256",
                             "app_bundle_sha256"))
            or frozen["repository"] != REPO
            or frozen["app_signer_workflow"] != WORKFLOW
            or frozen["app_oidc_issuer"] != ISSUER):
        raise Stop("DUAL_TRUST_PINS_INVALID")
    if _digest(app_bundle_bytes) != frozen["app_bundle_sha256"]:
        raise Stop("DUAL_APP_BUNDLE_HASH_MISMATCH")

    try:
        ops = verify_ops(
            receipt_bytes, signature_bytes, allowed_signers_bytes,
            frozen["ops_fingerprint"], frozen["policy_hash"],
            frozen["app_archive_sha256"])
    except Exception:
        raise Stop("DUAL_OPS_SIGNATURE_DENIED") from None
    if (type(ops) is not dict
            or ops.get("signature_verified_here") is not True
            or ops.get("app_archive_bound") is not True
            or ops.get("may_apply") is not False
            or ops.get("fingerprint_provenance_verified_here") is not False
            or ops.get("app_identity_verified_here") is not False
            or ops.get("ops_fingerprint") != frozen["ops_fingerprint"]
            or ops.get("receipt_sha256") != _digest(receipt_bytes)):
        raise Stop("DUAL_OPS_RESULT_UNTRUSTED")
    receipt = ops.get("receipt")
    if (type(receipt) is not dict or set(receipt) != RECEIPT_FIELDS
            or receipt.get("schema") != "qa525-ops-v3"
            or receipt.get("status") != "OPS_SIGNED_VERIFIED"
            or any(receipt.get(key) != frozen[key] for key in (
                "ops_source_sha", "program_sha256", "readiness_sha256",
                "staging_sha256", "policy_hash", "app_archive_sha256"))):
        raise Stop("DUAL_OPS_RECEIPT_MISMATCH")

    challenge = {
        "repository": frozen["repository"],
        "signer_workflow": frozen["app_signer_workflow"],
        "oidc_issuer": frozen["app_oidc_issuer"],
        "source_sha": frozen["app_source_sha"],
        "archive_sha256": frozen["app_archive_sha256"],
        "bundle_sha256": frozen["app_bundle_sha256"],
    }
    held = dict(challenge)
    try:
        valid = verify_app(app_bundle_bytes, challenge)
    except Exception:
        raise Stop("DUAL_APP_ATTESTATION_DENIED") from None
    if valid is not True or challenge != held or pins != frozen:
        raise Stop("DUAL_APP_ATTESTATION_DENIED")
    return {
        "status": STATUS,
        "ops_source_sha": frozen["ops_source_sha"],
        "app_source_sha": frozen["app_source_sha"],
        "ops_receipt_sha256": _digest(receipt_bytes),
        "app_bundle_sha256": frozen["app_bundle_sha256"],
        "two_verifier_ports_accepted": True,
        "ops_v3_app_archive_challenge_matched_by_ports": True,
        "ops_fingerprint_provenance_verified_here": False,
        "app_attestation_provenance_verified_here": False,
        "sponsor_approval_verified_here": False,
        "ops_app_signed_artifacts_bound_here": False,
        "signed_bootstrap_verified_here": False,
        "may_install": False,
        "may_apply": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_DUAL_GATE_LIBRARY_ONLY_NO_ROOT")
