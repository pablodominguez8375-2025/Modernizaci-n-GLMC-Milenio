#!/usr/bin/env python3
"""QA525 vNext: G6 v2 metadata coherence only. INACTIVE; never authorizes APPLY.

Contract for the future ``qa525-g6-v2`` backup generation (new repository, new key,
new receipt and restore-report paths). It is a SEPARATE fail-closed validator: it
never reads the v1 receipt path and never falls back to v1. A v1 document (no
``schema`` key) is never a v2 document, and the reverse.

Every pin and every observed digest MUST come from a separately authenticated root
bootstrap. This module does not read host files, reach Drive, verify signatures or
restore databases. Claims in the documents are not facts. It returns
``may_apply=False`` even when every check passes.
"""
from __future__ import annotations

import datetime as dt
import hashlib
import hmac
import json
import re

MAX_BYTES = 65536
SCHEMA = "qa525-g6-v2"
DIGEST = re.compile(r"sha256:[a-f0-9]{64}\Z")
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
GENERATION = re.compile(r"g6v2-[0-9a-f]{8,16}\Z")
ARCHIVE = re.compile(r"centenario-qa-g6-v2-CIFRADO-(\d{8}T\d{6}Z)\.tar\.gz\Z")
RESTORE = re.compile(r"isolated-restore-v2-\d{8}T\d{6}Z\.report\.json\Z")
RECEIPT_STATUS = "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING"
RESTORE_RESULT = "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY"
STATUS = "G6_V2_METADATA_COHERENT_UNAUTHENTICATED_NO_PHYSICAL_GO"
MAX_AGE = dt.timedelta(hours=48)


class Stop(Exception):
    """Fixed error codes; never include source JSON, paths, keys or secrets."""


def _unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise Stop("G6V2_DUPLICATE_JSON_KEY")
        result[key] = value
    return result


def _digest(value):
    return type(value) is str and DIGEST.fullmatch(value) is not None


def _read(raw, pinned, label):
    if type(raw) is not bytes or not 0 < len(raw) <= MAX_BYTES:
        raise Stop("G6V2_" + label + "_BYTES_INVALID")
    if not _digest(pinned):
        raise Stop("G6V2_" + label + "_PIN_INVALID")
    actual = "sha256:" + hashlib.sha256(raw).hexdigest()
    if not hmac.compare_digest(actual, pinned):
        raise Stop("G6V2_" + label + "_DIGEST_MISMATCH")
    try:
        doc = json.loads(
            raw.decode("utf-8"), object_pairs_hook=_unique,
            parse_constant=lambda _: (_ for _ in ()).throw(Stop("G6V2_JSON_NONFINITE")))
    except Stop:
        raise
    except (UnicodeDecodeError, ValueError, RecursionError, TypeError):
        raise Stop("G6V2_" + label + "_JSON_INVALID") from None
    if type(doc) is not dict:
        raise Stop("G6V2_" + label + "_JSON_INVALID")
    if doc.get("schema") != SCHEMA:
        raise Stop("G6V2_" + label + "_SCHEMA_NOT_V2")
    return doc


def _aware(value, code):
    if type(value) is not dt.datetime or value.tzinfo is None:
        raise Stop(code)
    try:
        return value.astimezone(dt.timezone.utc)
    except (ValueError, OverflowError):
        raise Stop(code) from None


def inspect(review_bytes, receipt_bytes, restore_bytes, *,
            pinned_review_sha256, pinned_receipt_sha256, pinned_restore_sha256,
            observed_archive_sha256, observed_drive_download_sha256,
            window_end, now):
    """Check v2 metadata coherence and that the checkpoint covers the WHOLE window.

    ``window_end`` is the instant by which the last forward or rollback step could
    finish; the checkpoint must still be valid then, not only at ``now``.
    """
    clock = _aware(now, "G6V2_CLOCK_INVALID")
    end = _aware(window_end, "G6V2_WINDOW_INVALID")
    if end < clock:
        raise Stop("G6V2_WINDOW_INVALID")
    if not _digest(observed_archive_sha256) or not _digest(observed_drive_download_sha256):
        raise Stop("G6V2_OBSERVED_HASH_INVALID")
    review = _read(review_bytes, pinned_review_sha256, "REVIEW")
    receipt = _read(receipt_bytes, pinned_receipt_sha256, "RECEIPT")
    restore = _read(restore_bytes, pinned_restore_sha256, "RESTORE")

    generation = review.get("generation")
    if type(generation) is not str or GENERATION.fullmatch(generation) is None:
        raise Stop("G6V2_GENERATION_INVALID")
    for doc, label in ((review, "REVIEW"), (receipt, "RECEIPT"), (restore, "RESTORE")):
        if doc.get("generation") != generation or doc.get("key_generation") != generation:
            raise Stop("G6V2_" + label + "_GENERATION_MISMATCH")
    if restore.get("restored_with_key_generation") != generation:
        raise Stop("G6V2_RESTORE_NOT_WITH_NEW_KEY")

    name = review.get("g6_archive_name")
    match = ARCHIVE.fullmatch(name) if type(name) is str else None
    if match is None:
        raise Stop("G6V2_ARCHIVE_NAME_INVALID")
    try:
        taken = dt.datetime.strptime(match.group(1), "%Y%m%dT%H%M%SZ").replace(
            tzinfo=dt.timezone.utc)
        expires = taken + MAX_AGE
    except (ValueError, OverflowError):
        raise Stop("G6V2_ARCHIVE_NAME_INVALID") from None
    if taken > clock:
        raise Stop("G6V2_CHECKPOINT_FUTURE")
    if clock >= expires:
        raise Stop("G6V2_CHECKPOINT_EXPIRED")
    if end >= expires:
        raise Stop("G6V2_CHECKPOINT_DOES_NOT_COVER_WINDOW")

    recorded = review.get("g6_archive_sha256")
    if (type(recorded) is not str or HEX64.fullmatch(recorded) is None
            or not hmac.compare_digest("sha256:" + recorded, observed_archive_sha256)):
        raise Stop("G6V2_ARCHIVE_DIGEST_MISMATCH")
    for field in ("drive_upload_sha256", "drive_download_sha256"):
        value = review.get(field)
        if (type(value) is not str or HEX64.fullmatch(value) is None
                or not hmac.compare_digest(value, recorded)):
            raise Stop("G6V2_DRIVE_DIGEST_MISMATCH")
    if not hmac.compare_digest(observed_drive_download_sha256, observed_archive_sha256):
        raise Stop("G6V2_DRIVE_DIGEST_MISMATCH")

    restore_name = review.get("g6_restore_report_name")
    if type(restore_name) is not str or RESTORE.fullmatch(restore_name) is None:
        raise Stop("G6V2_RESTORE_NAME_INVALID")
    if review.get("g6_offhost_sha256_verified") is not True:
        raise Stop("G6V2_OFFHOST_CLAIM_MISSING")

    if (receipt.get("archive_name") != name or receipt.get("sha256") != recorded
            or receipt.get("status") != RECEIPT_STATUS):
        raise Stop("G6V2_RECEIPT_MISMATCH")

    tables = restore.get("postgres_tables_by_database")
    silo = restore.get("silo_objects_verified")
    if (restore.get("result") != RESTORE_RESULT
            or restore.get("archive_filename") != name
            or restore.get("archive_sha256") != recorded
            or type(tables) is not dict or set(tables) != {"pmgm", "pmgm_keycloak"}
            or any(type(v) is not int or v <= 0 for v in tables.values())
            or type(silo) is not int or silo < 0
            or restore.get("production_touched") is not False):
        raise Stop("G6V2_RESTORE_MISMATCH")

    return {
        "status": STATUS,
        "schema": SCHEMA,
        "generation": generation,
        "checkpoint_age_seconds": int((clock - taken).total_seconds()),
        "checkpoint_expires_at": expires.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "archive_sha256": observed_archive_sha256,
        "review_sha256": pinned_review_sha256,
        "receipt_sha256": pinned_receipt_sha256,
        "restore_sha256": pinned_restore_sha256,
        "may_apply": False,
        "may_install": False,
        "signature_verified_here": False,
        "physical_archive_verified_here": False,
        "offhost_custody_authenticated_here": False,
        "independent_restore_verified_here": False,
        "sponsor_approved_here": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_G6_V2_METADATA_LIBRARY_ONLY_NO_EXECUTION")
