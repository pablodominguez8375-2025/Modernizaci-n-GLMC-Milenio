#!/usr/bin/env python3
"""QA #525: pure transaction coordinator; no Docker, network, secrets or CLI.

A future, separately reviewed local adapter may implement the interface.
Never interpret a JSON field saying 'signed' as actual cryptographic evidence.
"""
from dataclasses import dataclass
from datetime import datetime, timezone, timedelta
import re

HEX40 = re.compile(r"^[a-f0-9]{40}$")
HEX64 = re.compile(r"^[a-f0-9]{64}$")
PROJECT = "centenario-qa-lab2"
MAX_BACKUP_AGE = timedelta(hours=48)


class Blocked(Exception):
    """Only static, non-sensitive error codes."""


def check(ok, code):
    if not ok:
        raise Blocked(code)


@dataclass(frozen=True)
class WebReleasePlan:
    project: str
    previous_sha: str | None  # None only for installer-controlled first bootstrap
    candidate_sha: str
    previous_image_id: str
    candidate_image_id: str
    backup_sha256: str
    action: str = "WEB_ONLY_NO_DB_NO_SILO"


def make_plan(*, policy, installed, candidate, backup, runtime, now):
    """Accept normalized results of independent trusted verifiers, not raw user JSON.

    This plans a WEB-only update, and cannot bypass the complete signed-release
    gate or verify GitHub/Sigstore independently. The future adapter must check
    all evidence again immediately before making any change.
    """
    check(isinstance(policy, dict) and policy == {
        "mode": "QA-AUTO-LIGERO", "approved": True, "host": "glmdch",
        "project": PROJECT, "branch": "dev", "qa_only": True,
        "allowed_services": ["web"], "allow_db_migrations": False,
        "allow_storage_changes": False,
    }, "SCOPE_NOT_APPROVED")
    check(isinstance(installed, dict) and
          isinstance(installed.get("source_sha"), str) and
          HEX40.fullmatch(installed["source_sha"]) and
          isinstance(installed.get("web_image_id"), str) and
          re.fullmatch(r"sha256:[a-f0-9]{64}", installed["web_image_id"]),
          "PHYSICAL_BASELINE_UNVERIFIED")
    check(isinstance(candidate, dict) and
          candidate.get("state") == "VERIFIED_SIGNED_RELEASE_FROM_DEV" and
          candidate.get("complete_release_gate") is True and
          candidate.get("fast_forward_from_sha") == installed["source_sha"] and
          isinstance(candidate.get("source_sha"), str) and
          HEX40.fullmatch(candidate["source_sha"]) and
          candidate["source_sha"] != installed["source_sha"] and
          isinstance(candidate.get("web_image_id"), str) and
          re.fullmatch(r"sha256:[a-f0-9]{64}", candidate["web_image_id"]) and
          candidate.get("image_attestation_verified") is True,
          "CANDIDATE_PROVENANCE_UNVERIFIED")
    check(isinstance(now, datetime) and now.tzinfo is not None,
          "CHECK_TIME_INVALID")
    now = now.astimezone(timezone.utc)
    check(isinstance(backup, dict) and
          isinstance(backup.get("checkpoint_at"), datetime) and
          backup["checkpoint_at"].tzinfo is not None and
          timedelta(0) <= now - backup["checkpoint_at"].astimezone(timezone.utc)
          <= MAX_BACKUP_AGE and
          backup.get("offhost_ciphertext_sha256_matches") is True and
          backup.get("pg_both_and_globals") is True and
          backup.get("silo_objects") is True and
          backup.get("restic_integrity") is True and
          backup.get("isolated_restore_procedure_tested") is True and
          isinstance(backup.get("sha256"), str) and
          HEX64.fullmatch(backup["sha256"]),
          "CURRENT_OFFHOST_BACKUP_UNVERIFIED")
    check(isinstance(runtime, dict) and
          runtime.get("hostname") == "glmdch" and
          runtime.get("compose_project") == PROJECT and
          runtime.get("running_web_image_id") == installed["web_image_id"] and
          runtime.get("postgres_volume_preserved") is True and
          runtime.get("silo_volume_preserved") is True and
          runtime.get("keycloak_database_preserved") is True and
          runtime.get("rollback_image_pinned") is True and
          runtime.get("migration_on_startup_disabled") is True and
          runtime.get("network_unchanged") is True,
          "HOST_OR_ROLLBACK_PREFLIGHT_FAILED")
    return WebReleasePlan(
        project=PROJECT,
        previous_sha=installed["source_sha"],
        candidate_sha=candidate["source_sha"],
        previous_image_id=installed["web_image_id"],
        candidate_image_id=candidate["web_image_id"],
        backup_sha256=backup["sha256"],
    )


def coordinate(plan, adapter):
    """Drive an injected adapter in a reversible transaction.

    This module contains NO adapter that could control Docker or the server.
    Adapter must be separately reviewed, locally installed and allowlisted.
    """
    check(isinstance(plan, WebReleasePlan) and plan.project == PROJECT,
          "INVALID_PLAN")
    check(adapter.reverify(plan) is True, "PREFLIGHT_CHANGED")
    # Mark mutation BEFORE calling the adapter: a failing call can partially
    # change a container. Errors must never be interpreted as successful apply.
    changed = False
    try:
        changed = True
        adapter.activate_web(plan)
        check(adapter.verify_running_web(plan.candidate_image_id) is True,
              "CANDIDATE_NOT_RUNNING")
        check(adapter.verify_untouched_dependencies() is True,
              "DEPENDENCY_CHANGED")
        check(adapter.verify_public_health() is True,
              "HEALTH_NOT_READY")
        check(adapter.verify_untouched_dependencies() is True,
              "DEPENDENCY_CHANGED")
        return {"status": "WEB_HEALTHY_PENDING_AUTHENTICATED_QA",
                "source_sha": plan.candidate_sha,
                "scope": plan.action}
    except Exception:
        if changed:
            try:
                adapter.restore_previous_web(plan)
                check(adapter.verify_running_web(plan.previous_image_id) is True,
                      "ROLLBACK_IMAGE_NOT_RUNNING")
                check(adapter.verify_untouched_dependencies() is True,
                      "ROLLBACK_DEPENDENCY_CHANGED")
                check(adapter.verify_public_health() is True,
                      "ROLLBACK_PUBLIC_HEALTH_FAILED")
            except Exception:
                raise Blocked("ROLLBACK_FAILED_MANUAL_RECOVERY") from None
        raise Blocked("APPLY_FAILED_ROLLBACK_VERIFIED") from None
