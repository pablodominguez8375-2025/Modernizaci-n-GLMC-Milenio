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
