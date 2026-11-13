#!/usr/bin/env python3
"""QA525 G6 backup final status policy (pure / no host actions).

This is a review-only building block. NOT a GO, trusted collector, recovery
controller or authorization to stop MinIO. The final caller MUST separately
capture fresh, authenticated Docker/systemd state AFTER cleanup and refuse
a success result if any read fails. Do not pass secrets in evidence.
"""
from __future__ import annotations

import re

HEX64 = re.compile(r"[0-9a-f]{64}\Z")
SNAPSHOT = re.compile(r"[0-9a-f]{8,64}\Z")
ARCHIVE = re.compile(r"centenario-qa-g6-v2-CIFRADO-\d{8}T\d{6}Z\.tar\.gz\Z")

def final_status(evidence: dict) -> str:
    """Return an unambiguous result code; never infer success from missing data.

    Expected fields are populated from *fresh* trusted collector readbacks.
    No truthiness coercion for flags: strings like "false" must not pass.
    An active/unknown timer is a custody/recovery incident regardless of
    whether the backup phase itself ended successfully.
    """
    if type(evidence) is not dict:
        return "STOP_EVIDENCE_INVALID"
    if evidence.get("minio_running") is not True:
        return "STOP_MINIO_NOT_RUNNING"
    if evidence.get("minio_health") != "healthy":
        return "STOP_MINIO_NOT_HEALTHY"
    if evidence.get("watchdog_active_state") != "inactive":
        return "STOP_WATCHDOG_NOT_INACTIVE"
    if evidence.get("watchdog_sub_state") not in {"dead", "elapsed"}:
        return "STOP_WATCHDOG_NOT_SETTLED"
    if type(evidence.get("bootstrap_exit")) is not int or evidence["bootstrap_exit"] != 0:
        return "STOP_BOOTSTRAP_FAILED"
    if evidence.get("go_archived") is not True:
        return "STOP_GO_CUSTODY"
    if evidence.get("backup_markers_confirmed") is not True:
        return "STOP_BACKUP_MARKERS"
    if not isinstance(evidence.get("archive_name"), str) or not ARCHIVE.fullmatch(evidence["archive_name"]):
        return "STOP_ARCHIVE_NAME"
    for name in ("archive_sha256", "manifest_sha256"):
        value = evidence.get(name)
        if not isinstance(value, str) or not HEX64.fullmatch(value):
            return "STOP_ARTIFACT_HASH"
    sid = evidence.get("snapshot_id")
    if not isinstance(sid, str) or not SNAPSHOT.fullmatch(sid):
        return "STOP_SNAPSHOT_ID"
    return "PASS_BACKUP_FINAL_STATE"


if __name__ == "__main__":
    raise SystemExit("QA525_REVIEW_ONLY_NO_DIRECT_GO")
