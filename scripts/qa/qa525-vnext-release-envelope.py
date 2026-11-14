#!/usr/bin/env python3
"""QA525 vNext release envelope: schema, builder and PURE checker. INACTIVE.

No I/O, no network, no signature: authenticity (Sigstore attestation of these exact bytes and
the Sponsor policy) belongs to the PRE-ROOT chain. This module only proves the bytes are
canonical, bound to the host pins, a strict descendant interval, fresh, monotonic, and that the
category equals what the HOST-PINNED classifier computes again. It never authorizes an apply.
"""
from __future__ import annotations

import datetime as dt
import hashlib
import hmac
import json
import re

SCHEMA = "qa525-release-envelope-v1"
STATUS = "ENVELOPE_CHECKED_NOT_AUTHORIZED_NO_APPLY"
MAX_BYTES = 262144
MAX_VALIDITY_S = 86400
SHA = re.compile(r"[0-9a-f]{40}\Z")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")
ISO = re.compile(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ\Z")
FIELDS = frozenset({
    "schema", "host", "environment", "installed_sha", "candidate_sha", "merge_base_sha",
    "ahead_by", "comparison_complete", "files", "classifier_version", "classifier_sha256",
    "category", "reasons", "web_image_digest", "rollback_web_image_digest", "sequence",
    "not_before", "expires_at", "signer_workflow", "source_repository_id"})


class Stop(Exception):
    """Static diagnostic code only."""


def _unique(pairs):
    out = {}
    for key, val in pairs:
        if key in out:
            raise Stop("ENVELOPE_DUPLICATE_KEY")
        out[key] = val
    return out


def canonical(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":"), ensure_ascii=True,
                       allow_nan=False) + "\n").encode("ascii")


def build(*, installed_sha, candidate_sha, merge_base_sha, ahead_by, comparison_complete, files,
          classifier, classifier_sha256, web_image_digest, rollback_web_image_digest, sequence,
          not_before, expires_at, signer_workflow, source_repository_id,
          host="glmdch", environment="QA_GLMDCH_ONLY"):
    """CI-side builder. `classifier` is the classifier module; the host re-runs its own copy."""
    verdict = classifier.classify(files, complete=comparison_complete)
    return canonical({
        "schema": SCHEMA, "host": host, "environment": environment,
        "installed_sha": installed_sha, "candidate_sha": candidate_sha,
        "merge_base_sha": merge_base_sha, "ahead_by": ahead_by,
        "comparison_complete": comparison_complete, "files": files,
        "classifier_version": verdict["classifier_version"], "classifier_sha256": classifier_sha256,
        "category": verdict["category"], "reasons": verdict["reasons"],
        "web_image_digest": web_image_digest, "rollback_web_image_digest": rollback_web_image_digest,
        "sequence": sequence, "not_before": not_before, "expires_at": expires_at,
        "signer_workflow": signer_workflow, "source_repository_id": source_repository_id})


def _when(text):
    if type(text) is not str or ISO.fullmatch(text) is None:
        raise Stop("ENVELOPE_TIME_INVALID")
    return dt.datetime.strptime(text, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=dt.timezone.utc)


def _equal(have, want, code):
    if type(have) is not str or type(want) is not str or not hmac.compare_digest(have, want):
        raise Stop(code)


def check(raw, *, pins, classifier, now, last_sequence):
    """pins: installed_sha, classifier_sha256, signer_workflow, source_repository_id (host-owned)."""
    if type(raw) is not bytes or not 0 < len(raw) <= MAX_BYTES:
        raise Stop("ENVELOPE_BYTES_INVALID")
    try:
        env = json.loads(raw.decode("ascii"), object_pairs_hook=_unique)
    except (ValueError, UnicodeError):
        raise Stop("ENVELOPE_JSON_INVALID") from None
    if type(env) is not dict or set(env) != FIELDS or canonical(env) != raw:
        raise Stop("ENVELOPE_NOT_CANONICAL_OR_FIELDS")
    if env["schema"] != SCHEMA or env["host"] != "glmdch" or env["environment"] != "QA_GLMDCH_ONLY":
        raise Stop("ENVELOPE_SCOPE")
    for key in ("installed_sha", "candidate_sha", "merge_base_sha"):
        if type(env[key]) is not str or SHA.fullmatch(env[key]) is None:
            raise Stop("ENVELOPE_SHA_INVALID")
    for key in ("web_image_digest", "rollback_web_image_digest", "classifier_sha256"):
        if type(env[key]) is not str or DIGEST.fullmatch(env[key]) is None:
            raise Stop("ENVELOPE_DIGEST_INVALID")
    _equal(env["installed_sha"], pins["installed_sha"], "ENVELOPE_NOT_BASED_ON_INSTALLED")
    _equal(env["merge_base_sha"], env["installed_sha"], "ENVELOPE_NOT_DESCENDANT")
    if env["candidate_sha"] == env["installed_sha"]:
        raise Stop("ENVELOPE_NOT_DESCENDANT")
    if type(env["ahead_by"]) is not int or env["ahead_by"] < 1 or env["comparison_complete"] is not True:
        raise Stop("ENVELOPE_COMPARISON_INCOMPLETE")
    _equal(env["classifier_sha256"], pins["classifier_sha256"], "ENVELOPE_CLASSIFIER_PIN")
    _equal(env["signer_workflow"], pins["signer_workflow"], "ENVELOPE_WORKFLOW_PIN")
    _equal(env["source_repository_id"], pins["source_repository_id"], "ENVELOPE_REPOSITORY_PIN")
    verdict = classifier.classify(env["files"], complete=env["comparison_complete"])
    if (env["classifier_version"] != verdict["classifier_version"]
            or env["category"] != verdict["category"] or env["reasons"] != verdict["reasons"]):
        raise Stop("ENVELOPE_CLASSIFICATION_MISMATCH")
    if env["category"] == classifier.AUTO and env["web_image_digest"] == env["rollback_web_image_digest"]:
        raise Stop("ENVELOPE_IMAGE_NOT_NEW")
    if type(env["sequence"]) is not int or type(last_sequence) is not int \
            or env["sequence"] <= last_sequence or env["sequence"] < 1:
        raise Stop("ENVELOPE_SEQUENCE_REPLAY")
    start, end = _when(env["not_before"]), _when(env["expires_at"])
    if not 0 < (end - start).total_seconds() <= MAX_VALIDITY_S or not start <= now < end:
        raise Stop("ENVELOPE_NOT_IN_WINDOW")
    return {"status": STATUS, "category": env["category"], "reasons": env["reasons"],
            "sequence": env["sequence"], "candidate_sha": env["candidate_sha"],
            "web_image_digest": env["web_image_digest"]}


if __name__ == "__main__":
    raise SystemExit("QA525_RELEASE_ENVELOPE_LIBRARY_ONLY_NO_EXECUTION")
