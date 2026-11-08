#!/usr/bin/env python3
"""QA525 G6 v2 evidence coherence only. INACTIVE, NEVER physical GO.

This pure module accepts already-read bytes and caller-supplied pins. It cannot
authenticate the origin of the pins, a Restic repository, a password, Drive,
Sigstore, a real restore, Sponsor approval, or the physical host. The trusted
pre-root bootstrap must independently supply those facts under a held lease.
Never use this consistency result to permit root execution or APPLY.
"""
from __future__ import annotations

import datetime as dt
import hashlib
import hmac
import json
import re

STATUS = "G6_V2_EVIDENCE_COHERENT_UNAUTHENTICATED_NO_PHYSICAL_GO"
MAX_BYTES = 65536
DIGEST = re.compile(r"sha256:[a-f0-9]{64}\Z")
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
EPOCH = re.compile(r"[a-f0-9]{32}\Z")
DRIVE_ID = re.compile(r"[A-Za-z0-9_-]{15,128}\Z")
ARCHIVE = re.compile(r"centenario-qa-g6-v2-CIFRADO-(\d{8}T\d{6}Z)\.tar\.gz\Z")
RESTORE = re.compile(r"isolated-restore-v2-(\d{8}T\d{6}Z)\.report\.json\Z")
PINS = frozenset({"review_sha256", "receipt_sha256", "restore_sha256"})
REVIEW_FIELDS = frozenset({
    "schema", "g6_archive_name", "g6_archive_sha256", "g6_restore_report_name",
    "g6_offhost_sha256_verified", "g6_drive_file_id", "restic_repo_id",
    "key_epoch",
})
RECEIPT_FIELDS = frozenset({
    "schema", "archive_name", "sha256", "status", "restic_repo_id", "key_epoch",
})
RESTORE_FIELDS = frozenset({
    "schema", "result", "archive_filename", "archive_sha256", "restic_repo_id",
    "key_epoch", "postgres_tables_by_database", "silo_objects_verified",
    "production_touched",
})


class Stop(Exception):
    """Fixed error codes only; never echo untrusted evidence."""


def _unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise Stop("G6_V2_DUPLICATE_JSON_KEY")
        result[key] = value
    return result


def _sha(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def _read(raw, pin, name):
    if type(raw) is not bytes or not 0 < len(raw) <= MAX_BYTES:
        raise Stop("G6_V2_" + name + "_BYTES_INVALID")
    if type(pin) is not str or DIGEST.fullmatch(pin) is None:
        raise Stop("G6_V2_" + name + "_PIN_INVALID")
    if not hmac.compare_digest(_sha(raw), pin):
        raise Stop("G6_V2_" + name + "_PIN_MISMATCH")
    try:
        record = json.loads(raw.decode("utf-8"), object_pairs_hook=_unique,
                            parse_constant=lambda _: (_ for _ in ()).throw(
                                Stop("G6_V2_NONFINITE_JSON")))
    except Stop:
        raise
    except (ValueError, UnicodeError, RecursionError, TypeError):
        raise Stop("G6_V2_" + name + "_JSON_INVALID") from None
    if type(record) is not dict:
        raise Stop("G6_V2_" + name + "_JSON_INVALID")
    canonical = (json.dumps(record, sort_keys=True, separators=(",", ":"),
                            ensure_ascii=True, allow_nan=False) + "\n").encode("ascii")
    if not hmac.compare_digest(raw, canonical):
        raise Stop("G6_V2_" + name + "_NOT_CANONICAL")
    return record


def _clock(value):
    if type(value) is not dt.datetime or value.tzinfo is None:
        raise Stop("G6_V2_CLOCK_INVALID")
    try:
        if value.utcoffset() is None:
            raise ValueError("no offset")
        return value.astimezone(dt.timezone.utc)
    except (ValueError, OverflowError):
        raise Stop("G6_V2_CLOCK_INVALID") from None


def _stamp(value, pattern, code):
    match = pattern.fullmatch(value) if type(value) is str else None
    if match is None:
        raise Stop(code)
    try:
        when = dt.datetime.strptime(match.group(1), "%Y%m%dT%H%M%SZ")
    except ValueError:
        raise Stop(code) from None
    return when.replace(tzinfo=dt.timezone.utc)


def inspect(*, review_bytes, receipt_bytes, restore_bytes, pins,
            observed_archive_sha256, downloaded_offhost_sha256,
            expected_restic_repo_id, expected_key_epoch, now,
            admission_expires_at):
    """Check v2 evidence consistency, NOT authenticity, approval or safety.

    All hashes, repo IDs and key epochs are externally supplied assertions.
    The caller MUST actually hash the encrypted archive and downloaded offhost
    copy, verify the newly initialized repository / new key, authenticate Drive
    and isolated restore, and bind Sponsor GO and APP/OPS signatures separately.
    """
    if type(pins) is not dict or set(pins) != PINS:
        raise Stop("G6_V2_PINS_INVALID")
    review = _read(review_bytes, pins["review_sha256"], "REVIEW")
    receipt = _read(receipt_bytes, pins["receipt_sha256"], "RECEIPT")
    restore = _read(restore_bytes, pins["restore_sha256"], "RESTORE")

    if (set(review) != REVIEW_FIELDS or set(receipt) != RECEIPT_FIELDS
            or set(restore) != RESTORE_FIELDS
            or review.get("schema") != "qa525-g6-v2-review"
            or receipt.get("schema") != "qa525-g6-v2-receipt"
            or restore.get("schema") != "qa525-g6-v2-restore"):
        raise Stop("G6_V2_SCHEMA_INVALID")

    if (type(expected_restic_repo_id) is not str
            or HEX64.fullmatch(expected_restic_repo_id) is None
            or type(expected_key_epoch) is not str
            or EPOCH.fullmatch(expected_key_epoch) is None):
        raise Stop("G6_V2_GENERATION_PIN_INVALID")
    for record in (review, receipt, restore):
        if (type(record["restic_repo_id"]) is not str
                or not hmac.compare_digest(record["restic_repo_id"],
                                           expected_restic_repo_id)
                or type(record["key_epoch"]) is not str
                or not hmac.compare_digest(record["key_epoch"],
                                           expected_key_epoch)):
            raise Stop("G6_V2_GENERATION_MISMATCH")

    archive_name = review["g6_archive_name"]
    start = _stamp(archive_name, ARCHIVE, "G6_V2_ARCHIVE_NAME_INVALID")
    restored_at = _stamp(review["g6_restore_report_name"], RESTORE,
                         "G6_V2_RESTORE_NAME_INVALID")
    if start != restored_at:
        raise Stop("G6_V2_CUT_MISMATCH")
    archive_sha = review["g6_archive_sha256"]
    if type(archive_sha) is not str or HEX64.fullmatch(archive_sha) is None:
        raise Stop("G6_V2_ARCHIVE_HASH_INVALID")
    expected_archive_sha = "sha256:" + archive_sha
    for observed in (observed_archive_sha256, downloaded_offhost_sha256):
        if (type(observed) is not str or DIGEST.fullmatch(observed) is None
                or not hmac.compare_digest(observed, expected_archive_sha)):
            raise Stop("G6_V2_ARCHIVE_OR_OFFHOST_HASH_MISMATCH")
    if (receipt["archive_name"] != archive_name
            or type(receipt["sha256"]) is not str
            or not hmac.compare_digest(receipt["sha256"], archive_sha)
            or receipt["status"] != "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING"
            or review["g6_offhost_sha256_verified"] is not True
            or type(review["g6_drive_file_id"]) is not str
            or DRIVE_ID.fullmatch(review["g6_drive_file_id"]) is None):
        raise Stop("G6_V2_RECEIPT_OR_DRIVE_MISMATCH")

    tables = restore["postgres_tables_by_database"]
    if (restore["result"] != "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY"
            or restore["archive_filename"] != archive_name
            or restore["archive_sha256"] != archive_sha
            or type(tables) is not dict
            or set(tables) != {"pmgm", "pmgm_keycloak"}
            or any(type(x) is not int or x <= 0 for x in tables.values())
            or type(restore["silo_objects_verified"]) is not int
            or restore["silo_objects_verified"] < 0
            or restore["production_touched"] is not False):
        raise Stop("G6_V2_RESTORE_MISMATCH")

    clock, admission_end = _clock(now), _clock(admission_expires_at)
    checkpoint_end = start + dt.timedelta(hours=48)
    if (clock < start or clock >= checkpoint_end
            or admission_end <= clock or admission_end >= checkpoint_end):
        raise Stop("G6_V2_WINDOW_EXPIRED_OR_UNCOVERED")

    return {
        "status": STATUS, "review_sha256": pins["review_sha256"],
        "receipt_sha256": pins["receipt_sha256"],
        "restore_sha256": pins["restore_sha256"],
        "archive_sha256": expected_archive_sha,
        "restic_repo_id": expected_restic_repo_id,
        "key_epoch": expected_key_epoch,
        "checkpoint_expires_at": checkpoint_end.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "may_apply": False, "may_install": False,
        "physical_archive_verified_here": False,
        "offhost_custody_authenticated_here": False,
        "restore_authenticated_here": False,
        "repository_identity_authenticated_here": False,
        "key_generation_authenticated_here": False,
        "sponsor_authorized_here": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_G6_V2_EVIDENCE_LIBRARY_ONLY_NO_EXECUTION")
