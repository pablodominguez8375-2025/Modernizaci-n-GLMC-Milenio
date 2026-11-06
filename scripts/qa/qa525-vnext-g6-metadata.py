#!/usr/bin/env python3
"""QA525 vNext: G6 metadata coherence only. INACTIVE; never authorizes APPLY.

All four SHA pins and the observed encrypted-archive hash MUST come from a
separately authenticated root bootstrap. This module does not verify their
provenance, read host files, access Drive, verify Sigstore, or restore databases.
It intentionally returns may_apply=False even when every check passes.
"""
from __future__ import annotations

import datetime as dt
import hashlib
import hmac
import json
import re

MAX_BYTES = 65536
DIGEST = re.compile(r"sha256:[a-f0-9]{64}\Z")
ARCHIVE = re.compile(r"centenario-qa-g6-CIFRADO-(\d{8}T\d{6}Z)\.tar\.gz\Z")
RESTORE = re.compile(r"isolated-restore-\d{8}T\d{6}Z\.report\.json\Z")
STATUS = "G6_METADATA_COHERENT_UNAUTHENTICATED_NO_PHYSICAL_GO"


class Stop(Exception):
    """Fixed error codes; never include source JSON, paths or secrets."""


def _unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise Stop("G6_DUPLICATE_JSON_KEY")
        result[key] = value
    return result


def _digest(value):
    return type(value) is str and DIGEST.fullmatch(value) is not None


def _read(raw, pinned, label):
    if type(raw) is not bytes or not 0 < len(raw) <= MAX_BYTES:
        raise Stop("G6_" + label + "_BYTES_INVALID")
    if not _digest(pinned):
        raise Stop("G6_" + label + "_PIN_INVALID")
    actual = "sha256:" + hashlib.sha256(raw).hexdigest()
    if not hmac.compare_digest(actual, pinned):
        raise Stop("G6_" + label + "_DIGEST_MISMATCH")
    try:
        doc = json.loads(
            raw.decode("utf-8"), object_pairs_hook=_unique,
            parse_constant=lambda _: (_ for _ in ()).throw(Stop("G6_JSON_NONFINITE")))
    except Stop:
        raise
    except (UnicodeDecodeError, ValueError, RecursionError, TypeError):
        raise Stop("G6_" + label + "_JSON_INVALID") from None
    if type(doc) is not dict:
        raise Stop("G6_" + label + "_JSON_INVALID")
    return doc


def _timestamp(name):
    match = ARCHIVE.fullmatch(name) if type(name) is str else None
    if match is None:
        raise Stop("G6_ARCHIVE_NAME_INVALID")
    try:
        return dt.datetime.strptime(match.group(1), "%Y%m%dT%H%M%SZ").replace(
            tzinfo=dt.timezone.utc)
    except ValueError:
        raise Stop("G6_ARCHIVE_NAME_INVALID") from None


def _now(value):
    if type(value) is not dt.datetime or value.tzinfo is None:
        raise Stop("G6_CLOCK_INVALID")
    try:
        return value.astimezone(dt.timezone.utc)
    except (ValueError, OverflowError):
        raise Stop("G6_CLOCK_INVALID") from None


def inspect(review_bytes, receipt_bytes, restore_bytes, *,
            pinned_review_sha256, pinned_receipt_sha256,
            pinned_restore_sha256, observed_archive_sha256, now):
    """Inspect metadata and freshness; NEVER verify signatures or host state.

    The trusted caller is responsible for checking all four supplied digests,
    reading the original encrypted archive using trusted descriptors, verifying
    off-host custody and restoration evidence, and authorizing the same held
    lock-window. A caller-controlled pin is NOT authentication.
    """
    clock = _now(now)
    if not _digest(observed_archive_sha256):
        raise Stop("G6_OBSERVED_ARCHIVE_HASH_INVALID")
    review = _read(review_bytes, pinned_review_sha256, "REVIEW")
    receipt = _read(receipt_bytes, pinned_receipt_sha256, "RECEIPT")
    restore = _read(restore_bytes, pinned_restore_sha256, "RESTORE")

    name = review.get("g6_archive_name")
    when = _timestamp(name)
    age = (clock - when).total_seconds()
    if not (0 <= age < 48 * 3600):
        raise Stop("G6_CHECKPOINT_EXPIRED_OR_FUTURE")

    recorded = review.get("g6_archive_sha256")
    if (type(recorded) is not str
            or re.fullmatch(r"[a-f0-9]{64}", recorded) is None
            or not hmac.compare_digest("sha256:" + recorded, observed_archive_sha256)):
        raise Stop("G6_ARCHIVE_DIGEST_MISMATCH")

    restore_name = review.get("g6_restore_report_name")
    if type(restore_name) is not str or RESTORE.fullmatch(restore_name) is None:
        raise Stop("G6_RESTORE_NAME_INVALID")

    # A true *claim* is required for metadata consistency, but is NOT proof
    # of independent offhost custody, upload, signature, or restoration.
    if review.get("g6_offhost_sha256_verified") is not True:
        raise Stop("G6_OFFHOST_CLAIM_MISSING")
    if (receipt.get("archive_name") != name
            or receipt.get("sha256") != recorded
            or receipt.get("status") != "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING"):
        raise Stop("G6_LOCAL_RECEIPT_MISMATCH")

    tables = restore.get("postgres_tables_by_database")
    if (restore.get("result")
            != "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY"
            or restore.get("archive_filename") != name
            or restore.get("archive_sha256") != recorded
            or type(tables) is not dict
            or set(tables) != {"pmgm", "pmgm_keycloak"}
            or any(type(v) is not int or v <= 0 for v in tables.values())
            or type(restore.get("silo_objects_verified")) is not int
            or restore.get("silo_objects_verified") < 0
            or restore.get("production_touched") is not False):
        raise Stop("G6_RESTORE_METADATA_MISMATCH")

    try:
        checkpoint_expiry = when + dt.timedelta(hours=48)
    except OverflowError:
        raise Stop("G6_CHECKPOINT_DATE_INVALID") from None

    return {
        "status": STATUS,
        "checkpoint_age_seconds": int(age),
        "checkpoint_expires_at": checkpoint_expiry.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "review_sha256": pinned_review_sha256,
        "receipt_sha256": pinned_receipt_sha256,
        "restore_sha256": pinned_restore_sha256,
        "archive_sha256": observed_archive_sha256,
        "may_apply": False,
        "may_install": False,
        "signature_verified_here": False,
        "physical_archive_verified_here": False,
        "offhost_custody_authenticated_here": False,
        "independent_restore_verified_here": False,
        "sponsor_approved_here": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_G6_METADATA_LIBRARY_ONLY_NO_EXECUTION")
