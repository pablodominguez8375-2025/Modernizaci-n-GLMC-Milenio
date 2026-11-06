#!/usr/bin/env python3
"""QA525 vNext G6-to-approval byte binding. INACTIVE, NOT a physical gate.

G6 metadata, admission, policy and the approval anchor are consistency inputs
ONLY. All pins and the observed archive SHA MUST be obtained and authenticated
by a future signed root bootstrap. No filesystem, Docker, network, process,
signature, independent restore, offhost custody or Sponsor check occurs here.
"""
from __future__ import annotations

import datetime as dt
import hashlib
import hmac
import importlib.util
import json
from pathlib import Path
import re

HERE = Path(__file__).resolve().parent
DIGEST = re.compile(r"sha256:[a-f0-9]{64}\Z")
MAX_BYTES = 65536
STATUS = "G6_ANCHOR_METADATA_MATCH_NOT_AUTHENTICATED_NO_PHYSICAL_GO"


class Stop(Exception):
    """Static diagnostic codes; never echo external content."""


def _module(alias, path):
    spec = importlib.util.spec_from_file_location(alias, HERE / path)
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded


binding = _module("qa525_vnext_binding_for_g6_anchor", "qa525-vnext-trust-binding.py")
g6 = _module("qa525_vnext_g6_for_anchor", "qa525-vnext-g6-metadata.py")


def _pin(raw, pinned, label):
    if type(raw) is not bytes or not 0 < len(raw) <= MAX_BYTES:
        raise Stop("G6_ANCHOR_" + label + "_BYTES_INVALID")
    if type(pinned) is not str or DIGEST.fullmatch(pinned) is None:
        raise Stop("G6_ANCHOR_" + label + "_PIN_INVALID")
    actual = "sha256:" + hashlib.sha256(raw).hexdigest()
    if not hmac.compare_digest(pinned, actual):
        raise Stop("G6_ANCHOR_" + label + "_PIN_MISMATCH")


def _unique(pairs):
    result = {}
    for key, val in pairs:
        if key in result:
            raise Stop("G6_ANCHOR_DUPLICATE_KEY")
        result[key] = val
    return result


def _document(raw):
    try:
        obj = json.loads(
            raw.decode("utf-8"), object_pairs_hook=_unique,
            parse_constant=lambda _: (_ for _ in ()).throw(
                Stop("G6_ANCHOR_JSON_INVALID")))
    except Stop:
        raise
    except (ValueError, UnicodeDecodeError, RecursionError, TypeError):
        raise Stop("G6_ANCHOR_JSON_INVALID") from None
    if type(obj) is not dict:
        raise Stop("G6_ANCHOR_JSON_INVALID")
    return obj


def _utc(value):
    if type(value) is not str or re.fullmatch(
            r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", value) is None:
        raise Stop("G6_ANCHOR_DATE_INVALID")
    try:
        return dt.datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(
            tzinfo=dt.timezone.utc)
    except ValueError:
        raise Stop("G6_ANCHOR_DATE_INVALID") from None


def compare(*, admission_bytes, approval_bytes, review_bytes, receipt_bytes,
            restore_bytes, pinned_admission_sha256, pinned_approval_sha256,
            pinned_review_sha256, pinned_receipt_sha256, pinned_restore_sha256,
            observed_archive_sha256, trusted_policy_hash, now):
    """Check equality of externally pinned evidence; never authenticate pins.

    This is NOT a substitute for SIGSTORE/OPS/G6/Sponsor approval at the
    physical host. Pass only the exact bytes read from protected descriptors
    by a separately authenticated, trusted caller within the held lock window.
    """
    _pin(admission_bytes, pinned_admission_sha256, "ADMISSION")
    _pin(approval_bytes, pinned_approval_sha256, "APPROVAL")
    # G6 inspector validates and pins its own three source byte streams.
    try:
        reviewed = g6.inspect(
            review_bytes, receipt_bytes, restore_bytes,
            pinned_review_sha256=pinned_review_sha256,
            pinned_receipt_sha256=pinned_receipt_sha256,
            pinned_restore_sha256=pinned_restore_sha256,
            observed_archive_sha256=observed_archive_sha256, now=now)
    except Exception:
        raise Stop("G6_ANCHOR_G6_METADATA_REJECTED") from None

    try:
        bound = binding.bind(
            admission_bytes, approval_bytes,
            trusted_policy_hash=trusted_policy_hash, now=now)
    except Exception:
        raise Stop("G6_ANCHOR_BINDING_REJECTED") from None
    anchor, admission = _document(approval_bytes), _document(admission_bytes)
    if (not hmac.compare_digest(bound["admission_sha256"],
                                pinned_admission_sha256)
            or not hmac.compare_digest(bound["external_anchor_sha256"],
                                       pinned_approval_sha256)):
        raise Stop("G6_ANCHOR_BYTES_NOT_BOUND")
    if (type(anchor.get("g6_review_sha256")) is not str
            or type(reviewed.get("review_sha256")) is not str
            or not hmac.compare_digest(anchor["g6_review_sha256"],
                                       reviewed["review_sha256"])
            or not hmac.compare_digest(anchor["g6_review_sha256"],
                                       pinned_review_sha256)):
        raise Stop("G6_ANCHOR_REVIEW_MISMATCH")
    if _utc(anchor.get("g6_expires_at")) < _utc(admission.get("valid_until")):
        raise Stop("G6_ANCHOR_EXPIRES_BEFORE_ADMISSION")

    return {
        "status": STATUS,
        "admission_sha256": bound["admission_sha256"],
        "approval_sha256": bound["external_anchor_sha256"],
        "g6_review_sha256": reviewed["review_sha256"],
        "archive_sha256": reviewed["archive_sha256"],
        "nonce": bound["nonce"],
        "may_apply": False, "may_install": False,
        "signature_verified_here": False,
        "physical_archive_verified_here": False,
        "offhost_custody_authenticated_here": False,
        "independent_restore_verified_here": False,
        "sponsor_approved_here": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_G6_ANCHOR_LIBRARY_ONLY_NO_EXECUTION")
