#!/usr/bin/env python3
"""QA-AUTO-LIGERO #525: pure, fail-closed deployment *eligibility* policy.

This module does NOT deploy, run Docker, read credentials, check signatures,
change files, or authorize the first installation. Inputs are NOT trusted merely
because they look like JSON; a future privileged executor must obtain them from
independently verified, read-only sources and recheck at use time.
"""
from datetime import datetime, timedelta, timezone
import re

SHA = re.compile(r"^[0-9a-f]{40}$")
DIGEST = re.compile(r"^[0-9a-f]{64}$")
PROJECT = "centenario-qa-lab2"
MAX_BACKUP_AGE = timedelta(hours=48)


class Blocked(Exception):
    """Stable, non-secret rejection code."""


def require(condition, code):
    if not condition:
        raise Blocked(code)


def parse_utc(value):
    require(isinstance(value, str) and value.endswith("Z"), "INVALID_BACKUP_TIMESTAMP")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError:
        raise Blocked("INVALID_BACKUP_TIMESTAMP") from None
    require(parsed.tzinfo is not None, "INVALID_BACKUP_TIMESTAMP")
    return parsed.astimezone(timezone.utc)


def evaluate(policy, installed, staged, ancestry, backup, runtime, now):
    """Return a non-executable plan for a future isolated executor.

    All fields must be verified independently by the future executor:
    - installed: trusted host baseline anchored to running images;
    - staged: signed artifact manifest and attestation verified on host;
    - ancestry: fresh GitHub comparison against the installed SHA;
    - backup: verified off-host ciphertext/hash, restore receipt and freshness;
    - runtime: physical container/volume/migration checks just before apply.
    """
    require(isinstance(policy, dict) and set(policy) == {
        "mode", "approved", "host", "project", "source_branch", "qa_only",
        "services", "allow_destructive_migrations", "allow_public_network_changes",
    }, "POLICY_SCHEMA_INVALID")
    require(policy == {
        "mode": "QA-AUTO-LIGERO", "approved": True, "host": "glmdch",
        "project": PROJECT, "source_branch": "dev", "qa_only": True,
        "services": ["web", "api"], "allow_destructive_migrations": False,
        "allow_public_network_changes": False,
    }, "POLICY_SCOPE_FORBIDDEN")
    require(isinstance(installed, str) and SHA.fullmatch(installed), "TRUSTED_BASELINE_MISSING")
    require(isinstance(staged, dict) and
            staged.get("status") == "SIGNED_QA_CANDIDATE_STAGED_NO_DEPLOYMENT" and
            staged.get("scope") == "NO_DOCKER_NO_INSTALLATION_NO_QA_ACCEPTANCE" and
            isinstance(staged.get("source_sha"), str) and
            SHA.fullmatch(staged["source_sha"]) and
            type(staged.get("artifact_id")) is int and staged["artifact_id"] > 0,
            "SIGNED_STAGING_UNVERIFIED")
    candidate = staged["source_sha"]
    require(candidate != installed, "ALREADY_DEPLOYED")
    require(isinstance(ancestry, dict) and
            ancestry.get("status") == "FAST_FORWARD_CANDIDATE_ONLY" and
            ancestry.get("previous_deployed_sha") == installed and
            ancestry.get("candidate_sha") == candidate,
            "FAST_FORWARD_NOT_VERIFIED")

    require(isinstance(now, datetime) and now.tzinfo is not None, "INVALID_CHECK_TIME")
    now = now.astimezone(timezone.utc)
    require(isinstance(backup, dict) and set(backup) == {
        "checkpoint_at_utc", "offhost_verified_at_utc", "sha256",
        "postgres_pmgm", "postgres_keycloak", "postgres_globals",
        "silo_current_objects", "restic_check", "recovery_procedure_tested",
        "encrypted_only", "offhost_location_verified",
    }, "BACKUP_RECEIPT_MISSING")
    begin = parse_utc(backup["checkpoint_at_utc"])
    offhost = parse_utc(backup["offhost_verified_at_utc"])
    require(begin <= offhost <= now and now - begin <= MAX_BACKUP_AGE,
            "BACKUP_NOT_RECENT_OR_OFFHOST")
    require(isinstance(backup["sha256"], str) and DIGEST.fullmatch(backup["sha256"]),
            "BACKUP_HASH_INVALID")
    for key in ("postgres_pmgm", "postgres_keycloak", "postgres_globals",
                "silo_current_objects", "restic_check", "recovery_procedure_tested",
                "encrypted_only", "offhost_location_verified"):
        require(backup[key] is True, "BACKUP_INCOMPLETE")

    require(isinstance(runtime, dict) and set(runtime) == {
        "hostname", "project", "qa_only", "current_image_sha_verified",
        "volumes_preserved", "rollback_images_pinned", "migrations_disabled",
        "schema_compatible", "network_unchanged", "public_ports_unchanged",
        "pending_password_change_after_backup",
    }, "RUNTIME_PREFLIGHT_INCOMPLETE")
    require(runtime["hostname"] == "glmdch" and runtime["project"] == PROJECT and
            runtime["qa_only"] is True and runtime["current_image_sha_verified"] is True,
            "HOST_IDENTITY_UNVERIFIED")
    require(runtime["volumes_preserved"] is True and
            runtime["rollback_images_pinned"] is True and
            runtime["migrations_disabled"] is True and
            runtime["schema_compatible"] is True,
            "ROLLBACK_OR_SCHEMA_NOT_SAFE")
    require(runtime["network_unchanged"] is True and
            runtime["public_ports_unchanged"] is True and
            runtime["pending_password_change_after_backup"] is False,
            "NETWORK_OR_BACKUP_STATE_CHANGED")
    return {"status": "ELIGIBLE_FOR_RESTRICTED_QA_EXECUTOR_REVIEW_ONLY",
            "scope": "NO_DOCKER_NO_DEPLOYMENT_NO_BASELINE_WRITE",
            "source_sha": candidate, "previous_sha": installed,
            "services": ["web", "api"], "backup_sha256": backup["sha256"]}
