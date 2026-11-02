#!/usr/bin/env python3
"""Pure fail-closed contract for a future supervised QA525 root launcher cut.

NO host IO, imports from runtime, signature verification, installer or Docker.
An approved plan is never an authorization to deploy; it is one input gate
for the separately signed root-owned transactional installer (Issue #822).
"""
import hashlib
import json
import re

SHA = re.compile(r"[0-9a-f]{40}")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}")
HASH = re.compile(r"[0-9a-f]{64}")
NONCE = re.compile(r"[0-9a-f]{64}")
SCOPE = "QA525_LAUNCHER_ONLY_NO_DOCKER_NO_IDENTITY_NO_MIGRATION"
ACTION = "replace-root-launcher-supervised"
ENVIRONMENT = "QA_GLM DCH_ONLY".replace(" ", "")


class Blocked(Exception):
    """Public, static error code only."""


def require(ok, code):
    if not ok:
        raise Blocked(code)


def valid_sha(value, pattern):
    return type(value) is str and pattern.fullmatch(value) is not None


def frozen_sha(value):
    """Stable hash of a plain JSON record (not a substitute for a signature)."""
    require(type(value) is dict, "REPLACE_RECEIPT_NOT_OBJECT")
    return hashlib.sha256(
        json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()
    ).hexdigest()


def assess_review(preflight, approval, *, now, pending, consumed_nonces=()):
    """Validate ephemeral signed preflight and one root approval packet.

    The real installer MUST independently confirm signature/manifest, host
    locks, root0600 approval file and live state. This function cannot
    authenticate an approval or grant an install on its own.
    """
    require(type(preflight) is dict and type(approval) is dict and type(now) is int,
            "REPLACE_CONTRACT_INPUT_INVALID")
    require(preflight.get("status") == "SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL"
            and preflight.get("sigstore_verified_in_this_operation") is True
            and preflight.get("signed_manifest_verified") is True
            and preflight.get("old_launcher_checked_twice") is True
            and preflight.get("replacement_applied") is False
            and preflight.get("may_install") is False
            and preflight.get("may_apply") is False,
            "REPLACE_SIGNED_PREFLIGHT_REQUIRED")
    source = preflight.get("source_sha")
    digest = preflight.get("release_asset_sha256")
    old_hash = preflight.get("old_launcher_sha256")
    new_hash = preflight.get("candidate_operator_sha256")
    require(valid_sha(source, SHA) and valid_sha(digest, DIGEST)
            and valid_sha(old_hash, HASH) and valid_sha(new_hash, HASH),
            "REPLACE_CANDIDATE_IDENTITY_INVALID")
    require(approval.get("source_sha") == source
            and approval.get("release_asset_sha256") == digest
            and approval.get("old_launcher_sha256") == old_hash
            and approval.get("candidate_operator_sha256") == new_hash
            and approval.get("scope") == SCOPE
            and approval.get("action") == ACTION
            and approval.get("environment") == ENVIRONMENT
            and approval.get("operator") == "soporte@glmdch"
            and approval.get("approved") is True,
            "REPLACE_EXACT_APPROVAL_REQUIRED")
    issued, expires, nonce = (approval.get("issued_at"),
                              approval.get("expires_at"),
                              approval.get("nonce"))
    require(type(issued) is int and type(expires) is int and
            issued <= now < expires and 0 < expires - issued <= 900
            and valid_sha(nonce, NONCE),
            "REPLACE_APPROVAL_EXPIRED_OR_INVALID")
    require(type(pending) is bool and pending is False,
            "REPLACE_UNFINISHED_JOURNAL_BLOCKS")
    require(type(consumed_nonces) in (tuple, list, frozenset)
            and all(valid_sha(item, NONCE) for item in consumed_nonces)
            and nonce not in consumed_nonces,
            "REPLACE_APPROVAL_NONCE_CONSUMED_OR_INVALID")
    return {
        "status": "REVIEWED_SIGNED_LAUNCHER_PLAN_NO_INSTALL",
        "scope": SCOPE,
        "source_sha": source,
        "release_asset_sha256": digest,
        "old_launcher_sha256": old_hash,
        "candidate_operator_sha256": new_hash,
        "nonce": nonce,
        "approval_expires_at": expires,
        "may_install": False,
        "may_apply": False,
        "rollback_complete": False,
    }



RECOVERY_SCOPE = "QA525_LAUNCHER_ONLY_RECOVERY_NO_DOCKER_NO_IDENTITY_NO_MIGRATION"
RECOVERY_ACTION = "rollback-root-launcher-supervised"
RECOVERY_RECEIPT_FIELDS = frozenset({
    "status", "source_sha", "release_asset_sha256",
    "old_launcher_sha256", "new_launcher_sha256",
    "candidate_operator_sha256", "nonce",
})
RECOVERY_APPROVAL_FIELDS = frozenset({
    "action", "scope", "environment", "operator", "approved",
    "cut_nonce", "nonce", "source_sha", "release_asset_sha256",
    "old_launcher_sha256", "new_launcher_sha256", "issued_at", "expires_at",
})


def assess_reversal(committed, approval, *, current_launcher_sha256,
                    backup_sha256, pending, now, consumed_nonces=()):
    """PURE precondition for a separate supervised reversal; never authorizes it.

    No filesystem access, lock, signature or operator authentication here.
    The future independently signed root entrypoint must confirm all of them.
    """
    require(type(committed) is dict and set(committed) == RECOVERY_RECEIPT_FIELDS
            and committed["status"] == "LAUNCHER_ATOMIC_SWAP_COMMITTED"
            and valid_sha(committed["source_sha"], SHA)
            and valid_sha(committed["release_asset_sha256"], DIGEST)
            and all(valid_sha(committed[k], HASH) for k in (
                "old_launcher_sha256", "new_launcher_sha256",
                "candidate_operator_sha256", "nonce"))
            and committed["old_launcher_sha256"] !=
                committed["new_launcher_sha256"],
            "RECOVERY_COMMITTED_RECEIPT_UNTRUSTED")
    require(type(pending) is bool and pending is False
            and valid_sha(current_launcher_sha256, HASH)
            and valid_sha(backup_sha256, HASH)
            and current_launcher_sha256 == committed["new_launcher_sha256"]
            and backup_sha256 == committed["old_launcher_sha256"],
            "RECOVERY_PENDING_OR_HASH_DRIFT")
    require(type(approval) is dict and set(approval) == RECOVERY_APPROVAL_FIELDS
            and approval["action"] == RECOVERY_ACTION
            and approval["scope"] == RECOVERY_SCOPE
            and approval["environment"] == ENVIRONMENT
            and approval["operator"] == "soporte@glmdch"
            and approval["approved"] is True
            and all(approval[k] == committed[k] for k in (
                "source_sha", "release_asset_sha256",
                "old_launcher_sha256", "new_launcher_sha256"))
            and approval["cut_nonce"] == committed["nonce"],
            "RECOVERY_SEPARATE_APPROVAL_REQUIRED")
    nonce = approval["nonce"]
    require(valid_sha(nonce, NONCE) and nonce != committed["nonce"]
            and type(consumed_nonces) in (tuple, list, frozenset)
            and all(valid_sha(n, NONCE) for n in consumed_nonces)
            and nonce not in consumed_nonces,
            "RECOVERY_NONCE_REPLAY_OR_INVALID")
    issued, expires = approval["issued_at"], approval["expires_at"]
    require(type(now) is int and type(issued) is int and
            type(expires) is int and issued <= now < expires
            and 0 < expires - issued <= 900,
            "RECOVERY_APPROVAL_EXPIRED")
    return {
        "status": "REVIEWED_LAUNCHER_REVERSAL_PLAN_NO_INSTALL",
        "scope": RECOVERY_SCOPE, "source_sha": committed["source_sha"],
        "cut_nonce": committed["nonce"], "recovery_nonce": nonce,
        "old_launcher_sha256": backup_sha256,
        "new_launcher_sha256": current_launcher_sha256,
        "may_install": False, "may_apply": False,
        "may_recover": False, "rollback_complete": False,
        "requires_independent_signed_root_executor": True,
    }



# Contract-only model. Neither the caller's packet nor the JSON approval is
# proof of authenticity. A distinct future *trusted root* verifier must check
# Sigstore, complete manifests, original receipts and root-owned files.
DELAYED_RECOVERY_SCOPE = "QA525_LAUNCHER_ONLY_DELAYED_RECOVERY_NO_API_WEB"
DELAYED_RECOVERY_ACTION = "review-delayed-root-launcher-reversal"
DELAYED_RECOVERY_CLAIM_FIELDS = frozenset({
    "purpose", "source_sha", "release_asset_sha256",
    "manifest_sha256", "executor_sha256", "issued_at", "expires_at",
})
DELAYED_RECOVERY_APPROVAL_FIELDS = frozenset({
    "action", "scope", "environment", "operator", "approved",
    "cut_source_sha", "cut_release_asset_sha256", "cut_nonce",
    "cut_receipt_sha256", "old_launcher_sha256", "new_launcher_sha256",
    "recovery_source_sha", "recovery_release_asset_sha256",
    "recovery_manifest_sha256", "recovery_executor_sha256",
    "nonce", "issued_at", "expires_at",
})


def assess_delayed_recovery_plan(committed, recovery_claim, approval, *,
                                 live_sha256, backup_sha256, pending, now,
                                 consumed_nonces=()):
    """Pure *review* of original-cut A and independently sourced executor B.

    No signature, IAM, filesystem, privilege or lock checks. recovery_claim is
    UNTRUSTED caller data, never a root admission. The time limit applies to a
    separate new recovery claim, not to the historical installation admission.
    A future trusted runner must authenticate both immutable source chains
    and original journal, prove live state, recheck consent, and fail closed
    after any interruption. This function is NEVER executable authorization.
    """
    require(type(committed) is dict and
            set(committed) == RECOVERY_RECEIPT_FIELDS and
            committed["status"] == "LAUNCHER_ATOMIC_SWAP_COMMITTED" and
            valid_sha(committed["source_sha"], SHA) and
            valid_sha(committed["release_asset_sha256"], DIGEST) and
            all(valid_sha(committed[k], HASH) for k in (
                "old_launcher_sha256", "new_launcher_sha256",
                "candidate_operator_sha256", "nonce")) and
            committed["old_launcher_sha256"] !=
                committed["new_launcher_sha256"],
            "DELAYED_ORIGINAL_COMMITTED_RECEIPT_INVALID")
    require(type(pending) is bool and pending is False and
            valid_sha(live_sha256, HASH) and
            valid_sha(backup_sha256, HASH) and
            live_sha256 == committed["new_launcher_sha256"] and
            backup_sha256 == committed["old_launcher_sha256"],
            "DELAYED_ORIGINAL_PENDING_OR_HASH_DRIFT")
    require(type(recovery_claim) is dict and
            set(recovery_claim) == DELAYED_RECOVERY_CLAIM_FIELDS and
            recovery_claim["purpose"] ==
                "QA525_DELAYED_RECOVERY_REVIEW_UNTRUSTED" and
            valid_sha(recovery_claim["source_sha"], SHA) and
            valid_sha(recovery_claim["release_asset_sha256"], DIGEST) and
            all(valid_sha(recovery_claim[k], HASH) for k in
                ("manifest_sha256", "executor_sha256")),
            "DELAYED_RECOVERY_SOURCE_CLAIM_INVALID")
    require(type(approval) is dict and
            set(approval) == DELAYED_RECOVERY_APPROVAL_FIELDS and
            approval["action"] == DELAYED_RECOVERY_ACTION and
            approval["scope"] == DELAYED_RECOVERY_SCOPE and
            approval["environment"] == ENVIRONMENT and
            approval["operator"] == "soporte@glmdch" and
            approval["approved"] is True and
            approval["cut_source_sha"] == committed["source_sha"] and
            approval["cut_release_asset_sha256"] ==
                committed["release_asset_sha256"] and
            approval["cut_nonce"] == committed["nonce"] and
            approval["cut_receipt_sha256"] == frozen_sha(committed) and
            all(approval[k] == committed[k] for k in
                ("old_launcher_sha256", "new_launcher_sha256")) and
            all(approval["recovery_" + k] == recovery_claim[k] for k in
                ("source_sha", "release_asset_sha256",
                 "manifest_sha256", "executor_sha256")),
            "DELAYED_DUAL_PROVENANCE_APPROVAL_REQUIRED")
    nonce = approval["nonce"]
    require(valid_sha(nonce, NONCE) and nonce != committed["nonce"] and
            type(consumed_nonces) in (tuple, list, frozenset) and
            all(valid_sha(n, NONCE) for n in consumed_nonces) and
            nonce not in consumed_nonces,
            "DELAYED_RECOVERY_NONCE_INVALID_OR_REPLAYED")
    require(type(now) is int and
            all(type(packet[k]) is int for packet in
                (approval, recovery_claim) for k in ("issued_at", "expires_at")) and
            all(packet["issued_at"] <= now < packet["expires_at"] and
                0 < packet["expires_at"] - packet["issued_at"] <= 900
                for packet in (approval, recovery_claim)),
            "DELAYED_RECOVERY_FRESH_AUTHENTICATION_CLAIM_REQUIRED")
    return {
        "status": "REVIEWED_DELAYED_DUAL_PROVENANCE_NO_EXECUTION",
        "scope": DELAYED_RECOVERY_SCOPE,
        "cut_source_sha": committed["source_sha"],
        "cut_receipt_sha256": frozen_sha(committed),
        "recovery_source_sha": recovery_claim["source_sha"],
        "recovery_release_asset_sha256":
            recovery_claim["release_asset_sha256"],
        "recovery_manifest_sha256": recovery_claim["manifest_sha256"],
        "recovery_executor_sha256": recovery_claim["executor_sha256"],
        "recovery_nonce": nonce,
        "may_install": False, "may_apply": False,
        "may_recover": False, "rollback_complete": False,
        "claims_authenticated": False,
        "requires_independent_signed_root_executor": True,
        "requires_new_recovery_bootstrap": True,
        "requires_original_cut_authentication": True,
        "requires_root_journal_and_lock_verification": True,
    }



# Interrupted original A is NOT a committed receipt. Unlike delayed rollback,
# this review binds the original durable intent (not a fabricated commit) to a
# distinct recovery B. It provides no authorization or executable operations.
PENDING_RECONCILIATION_SCOPE = (
    "QA525_LAUNCHER_ONLY_PENDING_RECONCILIATION_NO_API_WEB"
)
PENDING_RECONCILIATION_ACTION = "review-pending-root-launcher-reconciliation"
PENDING_RECONCILIATION_INTENT_FIELDS = frozenset({
    "status", "source_sha", "release_asset_sha256",
    "old_launcher_sha256", "new_launcher_sha256", "nonce",
})
PENDING_RECONCILIATION_APPROVAL_FIELDS = frozenset({
    "action", "scope", "environment", "operator", "approved",
    "cut_source_sha", "cut_release_asset_sha256", "cut_nonce",
    "cut_intent_sha256", "old_launcher_sha256", "new_launcher_sha256",
    "recovery_source_sha", "recovery_release_asset_sha256",
    "recovery_manifest_sha256", "recovery_executor_sha256",
    "nonce", "issued_at", "expires_at",
})


def assess_pending_reconciliation_plan(intent, recovery_claim, approval, *,
                                       live_sha256, backup_sha256,
                                       receipt_present, now,
                                       consumed_nonces=()):
    """PURE review for an interrupted A with durable intent and NO receipt.

    A and B must be authenticated by a future independent root verifier,
    including journal custody, bootstrap source, Sigstore and operator consent.
    The old A admission may be expired; the B claim and separate B approval
    must each be fresh. Caller records are UNTRUSTED. Never auto-recover.
    """
    require(type(intent) is dict
            and set(intent) == PENDING_RECONCILIATION_INTENT_FIELDS
            and intent["status"] == "SWAP_INTENT_NO_DEPLOYMENT"
            and valid_sha(intent["source_sha"], SHA)
            and valid_sha(intent["release_asset_sha256"], DIGEST)
            and all(valid_sha(intent[k], HASH) for k in (
                "old_launcher_sha256", "new_launcher_sha256", "nonce"))
            and intent["old_launcher_sha256"] != intent["new_launcher_sha256"],
            "PENDING_RECONCILIATION_INTENT_INVALID")
    require(type(receipt_present) is bool and receipt_present is False
            and valid_sha(live_sha256, HASH)
            and valid_sha(backup_sha256, HASH)
            and backup_sha256 == intent["old_launcher_sha256"]
            and live_sha256 in (
                intent["old_launcher_sha256"], intent["new_launcher_sha256"]),
            "PENDING_RECONCILIATION_JOURNAL_OR_LIVE_DRIFT")
    require(type(recovery_claim) is dict
            and set(recovery_claim) == DELAYED_RECOVERY_CLAIM_FIELDS
            and recovery_claim["purpose"] ==
                "QA525_DELAYED_RECOVERY_REVIEW_UNTRUSTED"
            and valid_sha(recovery_claim["source_sha"], SHA)
            and valid_sha(recovery_claim["release_asset_sha256"], DIGEST)
            and all(valid_sha(recovery_claim[k], HASH) for k in (
                "manifest_sha256", "executor_sha256")),
            "PENDING_RECONCILIATION_B_CLAIM_INVALID")
    require(recovery_claim["source_sha"] != intent["source_sha"]
            and recovery_claim["release_asset_sha256"] !=
                intent["release_asset_sha256"],
            "PENDING_RECONCILIATION_B_NOT_DISTINCT_FROM_A")
    require(type(approval) is dict
            and set(approval) == PENDING_RECONCILIATION_APPROVAL_FIELDS
            and approval["action"] == PENDING_RECONCILIATION_ACTION
            and approval["scope"] == PENDING_RECONCILIATION_SCOPE
            and approval["environment"] == ENVIRONMENT
            and approval["operator"] == "soporte@glmdch"
            and approval["approved"] is True
            and approval["cut_source_sha"] == intent["source_sha"]
            and approval["cut_release_asset_sha256"] ==
                intent["release_asset_sha256"]
            and approval["cut_nonce"] == intent["nonce"]
            and approval["cut_intent_sha256"] == frozen_sha(intent)
            and all(approval[k] == intent[k] for k in (
                "old_launcher_sha256", "new_launcher_sha256"))
            and all(approval["recovery_" + k] == recovery_claim[k]
                    for k in ("source_sha", "release_asset_sha256",
                              "manifest_sha256", "executor_sha256")),
            "PENDING_RECONCILIATION_SEPARATE_APPROVAL_REQUIRED")
    nonce = approval["nonce"]
    require(valid_sha(nonce, NONCE) and nonce != intent["nonce"]
            and type(consumed_nonces) in (tuple, list, frozenset)
            and all(valid_sha(n, NONCE) for n in consumed_nonces)
            and nonce not in consumed_nonces,
            "PENDING_RECONCILIATION_NONCE_REPLAY")
    require(type(now) is int
            and all(type(packet[k]) is int
                    for packet in (approval, recovery_claim)
                    for k in ("issued_at", "expires_at"))
            and all(packet["issued_at"] <= now < packet["expires_at"]
                    and 0 < packet["expires_at"] - packet["issued_at"] <= 900
                    for packet in (approval, recovery_claim)),
            "PENDING_RECONCILIATION_FRESH_B_REQUIRED")
    return {
        "status": "REVIEWED_PENDING_DUAL_PROVENANCE_NO_EXECUTION",
        "scope": PENDING_RECONCILIATION_SCOPE,
        "observed_phase": (
            "PENDING_BEFORE_ATOMIC_SWAP"
            if live_sha256 == intent["old_launcher_sha256"]
            else "PENDING_AFTER_ATOMIC_SWAP_NO_RECEIPT"),
        "cut_source_sha": intent["source_sha"],
        "cut_intent_sha256": frozen_sha(intent),
        "recovery_source_sha": recovery_claim["source_sha"],
        "recovery_nonce": nonce,
        "may_install": False,
        "may_apply": False,
        "may_recover": False,
        "reconciliation_complete": False,
        "claims_authenticated": False,
        "requires_independent_signed_root_executor": True,
        "requires_new_recovery_bootstrap": True,
        "requires_original_cut_authentication": True,
        "requires_root_journal_and_lock_verification": True,
    }





# Append-only B receipt for forensic verification of an original interrupted
# A. Even a valid-looking JSON record is NEVER proof of signer, custody or
# runtime permissions; root source B must authenticate those independently.
PENDING_RECONCILIATION_RECEIPT_FIELDS = frozenset({
    "status", "cut_source_sha", "cut_release_asset_sha256", "cut_nonce",
    "cut_intent_sha256", "old_launcher_sha256", "new_launcher_sha256",
    "recovery_source_sha", "recovery_release_asset_sha256",
    "recovery_manifest_sha256", "recovery_executor_sha256",
    "recovery_nonce", "recovery_approval_sha256", "observed_phase",
    "restored_launcher_sha256",
})


def assess_pending_reconciliation_receipt(intent, recovery_receipt, *,
                                          old_backup_sha256,
                                          live_launcher_sha256,
                                          original_receipt_present,
                                          recovery_intent_present):
    """Pure verification of proposed separate B receipt; never release A.

    The original intent must be preserved byte-for-byte by a real executor.
    This checks caller-supplied identities only, not file ownership, locks,
    Sigstore, fsync, or any real B authority. It must NOT be used by itself
    to permit scan_history(), a fresh installer cut or any root execution.
    """
    require(type(intent) is dict
            and set(intent) == PENDING_RECONCILIATION_INTENT_FIELDS
            and intent["status"] == "SWAP_INTENT_NO_DEPLOYMENT"
            and valid_sha(intent["source_sha"], SHA)
            and valid_sha(intent["release_asset_sha256"], DIGEST)
            and all(valid_sha(intent[k], HASH) for k in (
                "old_launcher_sha256", "new_launcher_sha256", "nonce"))
            and intent["old_launcher_sha256"] !=
                intent["new_launcher_sha256"],
            "PENDING_RECEIPT_ORIGINAL_INTENT_INVALID")
    require(type(original_receipt_present) is bool
            and original_receipt_present is False
            and type(recovery_intent_present) is bool
            and recovery_intent_present is False
            and valid_sha(old_backup_sha256, HASH)
            and valid_sha(live_launcher_sha256, HASH)
            and old_backup_sha256 == intent["old_launcher_sha256"]
            and live_launcher_sha256 == intent["old_launcher_sha256"],
            "PENDING_RECEIPT_LIVE_OR_JOURNAL_NOT_RECONCILED")
    require(type(recovery_receipt) is dict
            and set(recovery_receipt) ==
                PENDING_RECONCILIATION_RECEIPT_FIELDS
            and recovery_receipt["status"] ==
                "PENDING_LAUNCHER_RECOVERY_RECONCILIATION_COMMITTED"
            and all(recovery_receipt[k] == intent[k] for k in (
                "old_launcher_sha256", "new_launcher_sha256"))
            and recovery_receipt["cut_source_sha"] == intent["source_sha"]
            and recovery_receipt["cut_release_asset_sha256"] ==
                intent["release_asset_sha256"]
            and recovery_receipt["cut_nonce"] == intent["nonce"]
            and recovery_receipt["cut_intent_sha256"] == frozen_sha(intent)
            and valid_sha(recovery_receipt["recovery_source_sha"], SHA)
            and valid_sha(recovery_receipt["recovery_release_asset_sha256"],
                          DIGEST)
            and all(valid_sha(recovery_receipt[k], HASH) for k in (
                "recovery_manifest_sha256", "recovery_executor_sha256",
                "recovery_nonce", "recovery_approval_sha256"))
            and recovery_receipt["recovery_nonce"] != intent["nonce"]
            and recovery_receipt["recovery_source_sha"] !=
                intent["source_sha"]
            and recovery_receipt["recovery_release_asset_sha256"] !=
                intent["release_asset_sha256"]
            and recovery_receipt["restored_launcher_sha256"] ==
                intent["old_launcher_sha256"]
            and recovery_receipt["observed_phase"] in (
                "PENDING_BEFORE_ATOMIC_SWAP",
                "PENDING_AFTER_ATOMIC_SWAP_NO_RECEIPT"),
            "PENDING_RECEIPT_RECONCILIATION_PROOF_INVALID")
    return {
        "status": "REVIEWED_PENDING_RECOVERY_RECEIPT_UNTRUSTED_NO_EXECUTION",
        "scope": PENDING_RECONCILIATION_SCOPE,
        "cut_source_sha": intent["source_sha"],
        "cut_intent_sha256": frozen_sha(intent),
        "recovery_source_sha": recovery_receipt["recovery_source_sha"],
        "recovery_nonce": recovery_receipt["recovery_nonce"],
        "observed_phase": recovery_receipt["observed_phase"],
        "original_intent_must_remain_preserved": True,
        "receipt_needs_independent_root_authentication": True,
        "root_history_reconciliation_implemented": False,
        "may_install": False, "may_apply": False, "may_recover": False,
        "reconciliation_complete": False,
    }





# Interruption AFTER the A committed receipt is durable but BEFORE its
# original intent has been removed. This is NOT an uncommitted A cut.
# A future authenticated reconciler must handle it separately, never forge
# a missing receipt, and must not replay an approved cut.
COMMITTED_INTENT_RESIDUE_RECEIPT_FIELDS = frozenset({
    "status", "source_sha", "release_asset_sha256",
    "old_launcher_sha256", "new_launcher_sha256",
    "candidate_operator_sha256", "nonce",
})


def assess_committed_intent_residue(intent, committed_receipt, *,
                                    old_backup_sha256,
                                    live_launcher_sha256):
    """Pure classification of post-receipt crash residue; NO execution.

    Checks identities and SHA consistency only. Not evidence of custody,
    Sigstore, a valid root signer, durable fsync or operator approval.
    An intact committed receipt and launcher B are essential; restoring the
    old launcher here would *undo a committed cut* and must not be automatic.
    """
    require(type(intent) is dict
            and set(intent) == PENDING_RECONCILIATION_INTENT_FIELDS
            and intent.get("status") == "SWAP_INTENT_NO_DEPLOYMENT"
            and valid_sha(intent.get("source_sha"), SHA)
            and valid_sha(intent.get("release_asset_sha256"), DIGEST)
            and all(valid_sha(intent.get(k), HASH) for k in (
                "old_launcher_sha256", "new_launcher_sha256", "nonce"))
            and intent["old_launcher_sha256"] != intent["new_launcher_sha256"],
            "COMMITTED_RESIDUE_ORIGINAL_INTENT_INVALID")
    require(type(committed_receipt) is dict
            and set(committed_receipt) ==
                COMMITTED_INTENT_RESIDUE_RECEIPT_FIELDS
            and committed_receipt.get("status") ==
                "LAUNCHER_ATOMIC_SWAP_COMMITTED"
            and all(committed_receipt.get(k) == intent[k]
                    for k in intent if k != "status")
            and valid_sha(committed_receipt.get(
                "candidate_operator_sha256"), HASH),
            "COMMITTED_RESIDUE_RECEIPT_NOT_MATCHED")
    require(valid_sha(old_backup_sha256, HASH)
            and valid_sha(live_launcher_sha256, HASH)
            and old_backup_sha256 == intent["old_launcher_sha256"]
            and live_launcher_sha256 == intent["new_launcher_sha256"],
            "COMMITTED_RESIDUE_BACKUP_OR_LIVE_DRIFT")
    return {
        "status": "REVIEWED_COMMITTED_CUT_INTENT_RESIDUE_NO_EXECUTION",
        "source_sha": intent["source_sha"],
        "cut_nonce_sha256": hashlib.sha256(
            intent["nonce"].encode("ascii")).hexdigest(),
        "intent_sha256": frozen_sha(intent),
        "committed_receipt_sha256": frozen_sha(committed_receipt),
        "observed_phase": "POST_COMMITTED_RECEIPT_PRE_INTENT_CLEANUP",
        "original_committed_receipt_preserved": True,
        "requires_independent_root_custody_authentication": True,
        "requires_signed_cleanup_approval": True,
        "requires_locked_idempotent_journal_reconciliation": True,
        "may_install": False,
        "may_apply": False,
        "may_recover": False,
        "may_delete_intent": False,
        "may_rollback": False,
        "reconciliation_complete": False,
    }



def assess_interrupted_swap(*, old_sha, new_sha, live_sha, backup_sha,
                            pending, source_sha, release_asset_sha256,
                            nonce, intent=None, receipt=None):
    """Diagnose a signed-cut launcher swap without recovery authorization.

    Require exact signed source, release digest and nonce in every durable
    marker. Values are caller-observed evidence, NOT cryptographic proof.
    A future installer MUST independently re-read trusted host state,
    verify release Sigstore/manifest and require operator approval/locks.
    """
    require(all(valid_sha(v, HASH) for v in
                (old_sha, new_sha, live_sha, backup_sha)),
            "REPLACE_RECOVERY_HASH_INVALID")
    require(valid_sha(source_sha, SHA)
            and valid_sha(release_asset_sha256, DIGEST)
            and valid_sha(nonce, NONCE),
            "REPLACE_RECOVERY_SIGNED_IDENTITY_INVALID")
    require(old_sha != new_sha, "REPLACE_NO_NEW_LAUNCHER")
    require(type(pending) is bool, "REPLACE_RECOVERY_MARKER_INVALID")
    require(intent is None or type(intent) is dict,
            "REPLACE_PENDING_INTENT_INVALID")
    require(receipt is None or type(receipt) is dict,
            "REPLACE_RECOVERY_RECEIPT_INVALID")
    identity = {
        "source_sha": source_sha,
        "release_asset_sha256": release_asset_sha256,
        "nonce": nonce,
        "old_launcher_sha256": old_sha,
        "new_launcher_sha256": new_sha,
    }
    if pending:
        require(intent is not None
                and intent.get("status") == "SWAP_INTENT_NO_DEPLOYMENT"
                and all(intent.get(key) == value for key, value in identity.items()),
                "REPLACE_PENDING_INTENT_NOT_THIS_CUT")
    else:
        require(intent is None, "REPLACE_UNEXPECTED_INTENT_WITHOUT_MARKER")
    if backup_sha != old_sha:
        raise Blocked("REPLACE_BACKUP_HASH_DRIFT")
    if receipt is not None:
        require(receipt.get("status") == "LAUNCHER_ATOMIC_SWAP_COMMITTED"
                and all(receipt.get(key) == value for key, value in identity.items()),
                "REPLACE_COMMIT_RECEIPT_NOT_THIS_CUT")
    if pending:
        status = (
            "REPLACE_OLD_PRESENT_PENDING_MANUAL_RECONCILIATION" if live_sha == old_sha else
            "REPLACE_NEW_PRESENT_PENDING_MANUAL_RECONCILIATION" if live_sha == new_sha else
            "REPLACE_UNKNOWN_LAUNCHER_PENDING_MANUAL_RECONCILIATION"
        )
    elif receipt is not None and live_sha == new_sha:
        status = "REPLACE_COMMITTED_VERIFIED_NO_DEPLOYMENT"
    elif receipt is not None:
        status = "REPLACE_RECEIPT_LAUNCHER_DIVERGED_MANUAL_RECONCILIATION"
    elif live_sha == old_sha:
        status = "REPLACE_NOT_STARTED_NO_AUTHORIZATION"
    else:
        status = "REPLACE_UNACCOUNTED_CHANGE_MANUAL_RECONCILIATION"
    return {"status": status, "may_install": False, "may_apply": False,
            "may_recover_automatically": False,
            "requires_operator_reconciliation": pending or "MANUAL_RECONCILIATION" in status}
