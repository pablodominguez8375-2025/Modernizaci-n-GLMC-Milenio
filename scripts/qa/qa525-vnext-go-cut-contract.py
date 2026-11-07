#!/usr/bin/env python3
"""QA525: pure GO-cut consistency and freshness check; NO PHYSICAL AUTHORITY.

Verifiers and nonce journals are INJECTED PORTS. Their provenance is NOT proven
here: a test returning True is never Sponsor authority. This module MUST NOT be
wired directly to a root executable. All GO results remain may_apply=False.
"""
import datetime as dt
import hashlib
import json
import re

SHA40 = re.compile(r"[0-9a-f]{40}\Z")
SHA256 = re.compile(r"sha256:[0-9a-f]{64}\Z")
FP = re.compile(r"SHA256:[A-Za-z0-9+/]{43}\Z")
NONCE = re.compile(r"[A-Za-z0-9_-]{24,96}\Z")
UTC = re.compile(r"20[0-9]{2}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12][0-9]|3[01])T[0-2][0-9]:[0-5][0-9]:[0-5][0-9]Z\Z")
APP_FIELDS = frozenset({"source_sha", "subject_sha256", "signer_workflow", "oidc_issuer"})
PINS = frozenset({"ops_source_sha", "app_source_sha", "app_archive_sha256",
                  "plan_sha256", "admission_sha256", "policy_hash", "go_fingerprint"})
GO_FIELDS = frozenset({"schema", "nonce", "host", "environment", "scope",
                       "action", "ops_source_sha", "app_source_sha",
                       "app_archive_sha256", "plan_sha256", "admission_sha256",
                       "policy_hash", "issued_at", "expires_at"})
OPS_FIELDS = frozenset({"schema", "status", "ops_source_sha", "program_sha256",
                        "readiness_sha256", "staging_sha256", "policy_hash",
                        "app_archive_sha256"})
APP_WORKFLOW = ("pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/"
                ".github/workflows/qa-silo-rebuild.yml")
ISSUER = "https://token.actions.githubusercontent.com"
MAX_GO_AGE_SECONDS = 1800
STATUS = "QA525_GO_CUT_CONSISTENT_ONLY_UNAUTHENTICATED_NO_APPLY"


class Stop(Exception):
    """Fixed error codes; never echo signing material."""


def _strict_pairs(pairs):
    result = {}
    for key, val in pairs:
        if key in result:
            raise ValueError("duplicate key")
        result[key] = val
    return result


def _parse(raw):
    if type(raw) is not bytes or not 0 < len(raw) <= 65536:
        raise Stop("GO_CUT_INPUT_INVALID")
    try:
        value = json.loads(raw.decode("utf-8"), object_pairs_hook=_strict_pairs,
                           parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
    except (ValueError, UnicodeDecodeError, RecursionError, TypeError):
        raise Stop("GO_CUT_JSON_INVALID") from None
    if type(value) is not dict or set(value) != GO_FIELDS:
        raise Stop("GO_CUT_FIELDS_INVALID")
    # Canonical byte layout ensures Sponsor signs exactly the reviewed cut.
    expected = (json.dumps(value, sort_keys=True, separators=(",", ":"),
                           ensure_ascii=True) + "\n").encode("ascii")
    if raw != expected:
        raise Stop("GO_CUT_NOT_CANONICAL")
    return value


def _utc(raw):
    if type(raw) is not str or UTC.fullmatch(raw) is None:
        raise Stop("GO_CUT_TIME_INVALID")
    try:
        parsed = dt.datetime.strptime(raw, "%Y-%m-%dT%H:%M:%SZ")
    except ValueError:
        raise Stop("GO_CUT_TIME_INVALID") from None
    return parsed.replace(tzinfo=dt.timezone.utc)


def check_cut(*, go_bytes, go_signature_bytes, pins, ops_v3_receipt,
              app_subject, now_utc, verify_go_signature, nonce_unused):
    """Require consistent signed-GO *claims* and an exact freshness window.

    verify_go_signature(go_bytes,signature_bytes,go_fingerprint) -> bool True
    nonce_unused(nonce,admission_sha256) -> bool True

    Neither callback establishes independent provenance or atomic consumption.
    Callers must authenticate roots of trust and durable journal separately.
    """
    if (type(go_signature_bytes) is not bytes or not 0 < len(go_signature_bytes) <= 4096
            or type(pins) is not dict or set(pins) != PINS
            or type(ops_v3_receipt) is not dict
            or type(app_subject) is not dict or set(app_subject) != APP_FIELDS
            or type(now_utc) is not dt.datetime or now_utc.tzinfo is None
            or now_utc.utcoffset() != dt.timedelta(0)
            or not callable(verify_go_signature) or not callable(nonce_unused)):
        raise Stop("GO_CUT_INPUT_INVALID")
    frozen = dict(pins)
    if (any(type(frozen[k]) is not str or SHA40.fullmatch(frozen[k]) is None
            for k in ("ops_source_sha", "app_source_sha"))
            or any(type(frozen[k]) is not str or SHA256.fullmatch(frozen[k]) is None
                   for k in ("app_archive_sha256", "plan_sha256",
                             "admission_sha256", "policy_hash"))
            or type(frozen["go_fingerprint"]) is not str
            or FP.fullmatch(frozen["go_fingerprint"]) is None):
        raise Stop("GO_CUT_PIN_INVALID")
    acta = _parse(go_bytes)
    expected = {
        "schema": "qa525-go-cut-v1",
        "host": "glmdch", "environment": "QA_SYNTHETIC",
        "scope": "API_WEB_QA_ONLY", "action": "ADMIT_OFFLINE_APPLY",
    }
    for key in ("ops_source_sha", "app_source_sha", "app_archive_sha256",
                "plan_sha256", "admission_sha256", "policy_hash"):
        expected[key] = frozen[key]
    if (any(type(acta.get(k)) is not str or acta.get(k) != v
            for k, v in expected.items())
            or type(acta["nonce"]) is not str
            or NONCE.fullmatch(acta["nonce"]) is None):
        raise Stop("GO_CUT_MISMATCH")
    issued = _utc(acta["issued_at"])
    expires = _utc(acta["expires_at"])
    if not (issued <= now_utc <= expires and
            0 < (expires - issued).total_seconds() <= MAX_GO_AGE_SECONDS):
        raise Stop("GO_CUT_EXPIRED_OR_FUTURE")
    if (set(ops_v3_receipt) != OPS_FIELDS
            or ops_v3_receipt.get("schema") != "qa525-ops-v3"
            or ops_v3_receipt.get("status") != "OPS_SIGNED_VERIFIED"
            or any(type(ops_v3_receipt.get(k)) is not str or
                   SHA256.fullmatch(ops_v3_receipt[k]) is None
                   for k in ("program_sha256", "readiness_sha256", "staging_sha256"))
            or ops_v3_receipt["ops_source_sha"] != frozen["ops_source_sha"]
            or ops_v3_receipt["policy_hash"] != frozen["policy_hash"]
            or ops_v3_receipt["app_archive_sha256"] != frozen["app_archive_sha256"]):
        raise Stop("GO_CUT_OPS_V3_MISMATCH")
    if (app_subject.get("source_sha") != frozen["app_source_sha"]
            or app_subject.get("subject_sha256") != frozen["app_archive_sha256"]
            or app_subject.get("signer_workflow") != APP_WORKFLOW
            or app_subject.get("oidc_issuer") != ISSUER):
        raise Stop("GO_CUT_APP_SUBJECT_MISMATCH")
    # Callbacks see only immutable bytes (no mutable acta/pins dictionaries).
    try:
        signature_ok = verify_go_signature(
            go_bytes, go_signature_bytes, frozen["go_fingerprint"])
    except Exception:
        raise Stop("GO_CUT_SIGNATURE_DENIED") from None
    if signature_ok is not True or pins != frozen:
        raise Stop("GO_CUT_SIGNATURE_DENIED")
    try:
        unused = nonce_unused(acta["nonce"], frozen["admission_sha256"])
    except Exception:
        raise Stop("GO_CUT_NONCE_UNSAFE") from None
    if unused is not True or pins != frozen:
        raise Stop("GO_CUT_NONCE_UNSAFE")
    return {
        "status": STATUS,
        "nonce": acta["nonce"], "go_bytes_sha256": "sha256:" + hashlib.sha256(go_bytes).hexdigest(),
        "ops_source_sha": frozen["ops_source_sha"],
        "app_source_sha": frozen["app_source_sha"],
        "app_archive_sha256": frozen["app_archive_sha256"],
        "plan_sha256": frozen["plan_sha256"],
        "admission_sha256": frozen["admission_sha256"],
        "expires_at": acta["expires_at"],
        "callback_consistency_only": True,
        "go_signature_provenance_verified_here": False,
        "app_attestation_provenance_verified_here": False,
        "ops_signature_provenance_verified_here": False,
        "nonce_consumption_durable_here": False,
        "bootstrap_authenticated_here": False,
        "may_install": False, "may_apply": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_GO_CUT_LIBRARY_ONLY_NO_ROOT")
