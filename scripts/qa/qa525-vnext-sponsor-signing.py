#!/usr/bin/env python3
"""QA525 Sponsor signing sheet builder. INACTIVE library, NEVER a GO, NEVER signs.

It only builds the exact CANONICAL BYTES the Sponsor will sign with his own OPS key
on his phone (``ssh-keygen -Y sign``), and a human review card with a short code to
compare on two devices. It holds no key, runs no process, touches no file or network.

Two documents are produced, both consumed unchanged by the existing verifiers:
  * OPS receipt v3  -> ``qa525-vnext-ops-signature.py::verify_ops_receipt`` (v3 mode)
  * GO cut v1       -> ``qa525-vnext-go-cut-contract.py::check_cut``
Everything it returns says ``may_apply=False``: a well-formed document is not
authority, a signature made elsewhere is not verified here, and the pinned
fingerprint's provenance is the Sponsor's out-of-band responsibility (Bitwarden).
"""
from __future__ import annotations

import base64
import datetime as dt
import hashlib
import json
import re

OPS_NAMESPACE = "qa525-ops-v2@pmgm"
GO_NAMESPACE = "qa525-go-cut-v1@pmgm"      # PROPOSED; the GO verifier must adopt it
PRINCIPAL = "qa525-ops"
NAMESPACES = frozenset({OPS_NAMESPACE, GO_NAMESPACE})
SHA40 = re.compile(r"[0-9a-f]{40}\Z")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")
FP = re.compile(r"SHA256:[A-Za-z0-9+/]{43}\Z")
NONCE = re.compile(r"[A-Za-z0-9_-]{24,96}\Z")
UTC = re.compile(r"20[0-9]{2}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12][0-9]|3[01])"
                 r"T[0-2][0-9]:[0-5][0-9]:[0-5][0-9]Z\Z")
PUB = re.compile(r"ssh-ed25519 ([A-Za-z0-9+/]{68})(?: [!-~]{1,64})?\n?\Z")
BLOB_PREFIX = b"\x00\x00\x00\x0bssh-ed25519\x00\x00\x00\x20"
MAX_GO_AGE_SECONDS = 1800
OPS_FIELDS = ("schema", "status", "ops_source_sha", "program_sha256",
              "readiness_sha256", "staging_sha256", "policy_hash", "app_archive_sha256")
GO_FIELDS = ("schema", "nonce", "host", "environment", "scope", "action",
             "ops_source_sha", "app_source_sha", "app_archive_sha256", "plan_sha256",
             "admission_sha256", "policy_hash", "issued_at", "expires_at")
GO_CONSTANTS = {"schema": "qa525-go-cut-v1", "host": "glmdch",
                "environment": "QA_SYNTHETIC", "scope": "API_WEB_QA_ONLY",
                "action": "ADMIT_OFFLINE_APPLY"}
# Launcher-only needs a DIFFERENT signed document, action and 15-min TTL.
# These exact values match qa525-launcher-root-approval.py (shape-only).
LAUNCHER_GO_FIELDS = GO_FIELDS + (
    "operator", "release_asset_sha256", "old_launcher_sha256",
    "candidate_operator_sha256",
)
LAUNCHER_GO_CONSTANTS = {
    "schema": "qa525-go-launcher-v1", "host": "glmdch",
    "environment": "QA_GLMDCH_ONLY",
    "scope": "QA525_LAUNCHER_ONLY_NO_DOCKER_NO_IDENTITY_NO_MIGRATION",
    "action": "replace-root-launcher-supervised",
    "operator": "soporte@glmdch",
}
HEX64 = re.compile(r"[0-9a-f]{64}\Z")
MAX_LAUNCHER_AGE_SECONDS = 900


class Stop(Exception):
    """Fixed codes only; never echo inputs."""


def _canonical(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":"),
                       ensure_ascii=True, allow_nan=False) + "\n").encode("ascii")


def _need(value, pattern, code):
    if type(value) is not str or pattern.fullmatch(value) is None:
        raise Stop(code)
    return value


def build_ops_v3_receipt(*, ops_source_sha, program_sha256, readiness_sha256,
                         staging_sha256, policy_hash, app_archive_sha256):
    """Exact bytes of the v3 receipt the OPS key signs (eight fields, canonical)."""
    record = {"schema": "qa525-ops-v3", "status": "OPS_SIGNED_VERIFIED",
              "ops_source_sha": _need(ops_source_sha, SHA40, "SIGNSHEET_INPUT_INVALID")}
    for name, value in (("program_sha256", program_sha256),
                        ("readiness_sha256", readiness_sha256),
                        ("staging_sha256", staging_sha256), ("policy_hash", policy_hash),
                        ("app_archive_sha256", app_archive_sha256)):
        record[name] = _need(value, DIGEST, "SIGNSHEET_INPUT_INVALID")
    return _canonical(record)


def new_nonce(entropy):
    """43-char URL-safe nonce from 32 caller-supplied random bytes (never generated here)."""
    if type(entropy) is not bytes or len(entropy) != 32 or len(set(entropy)) < 8:
        raise Stop("SIGNSHEET_NONCE_ENTROPY_INVALID")
    return base64.urlsafe_b64encode(entropy).decode("ascii").rstrip("=")


def new_launcher_nonce(entropy):
    """64 lowercase hexadecimal chars for the legacy root-approval nonce field."""
    if type(entropy) is not bytes or len(entropy) != 32 or len(set(entropy)) < 8:
        raise Stop("SIGNSHEET_NONCE_ENTROPY_INVALID")
    return entropy.hex()


def _utc(text):
    _need(text, UTC, "SIGNSHEET_TIME_INVALID")
    try:
        return dt.datetime.strptime(text, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=dt.timezone.utc)
    except ValueError:
        raise Stop("SIGNSHEET_TIME_INVALID") from None


def build_go_cut(*, nonce, ops_source_sha, app_source_sha, app_archive_sha256,
                 plan_sha256, admission_sha256, policy_hash, issued_at, expires_at,
                 purpose="API_WEB_QA_ONLY", release_asset_sha256=None,
                 old_launcher_sha256=None, candidate_operator_sha256=None):
    """Canonical GO bytes, explicitly distinct for launcher and API/WEB.

    Both are NOT approval. The launcher scope has a 900-second window and
    directly binds the same hashes the existing root approval checks.
    """
    launcher = purpose == "LAUNCHER_ONLY"
    if type(purpose) is not str or purpose not in ("API_WEB_QA_ONLY", "LAUNCHER_ONLY"):
        raise Stop("SIGNSHEET_PURPOSE_INVALID")
    extra = (release_asset_sha256, old_launcher_sha256, candidate_operator_sha256)
    if not launcher and any(v is not None for v in extra):
        raise Stop("SIGNSHEET_PURPOSE_INVALID")
    record = dict(LAUNCHER_GO_CONSTANTS if launcher else GO_CONSTANTS)
    record["nonce"] = _need(nonce, HEX64 if launcher else NONCE,
                            "SIGNSHEET_INPUT_INVALID")
    if launcher:
        record["release_asset_sha256"] = _need(
            release_asset_sha256, DIGEST, "SIGNSHEET_INPUT_INVALID")
        record["old_launcher_sha256"] = _need(
            old_launcher_sha256, HEX64, "SIGNSHEET_INPUT_INVALID")
        record["candidate_operator_sha256"] = _need(
            candidate_operator_sha256, HEX64, "SIGNSHEET_INPUT_INVALID")
    record["ops_source_sha"] = _need(ops_source_sha, SHA40, "SIGNSHEET_INPUT_INVALID")
    record["app_source_sha"] = _need(app_source_sha, SHA40, "SIGNSHEET_INPUT_INVALID")
    for name, value in (("app_archive_sha256", app_archive_sha256),
                        ("plan_sha256", plan_sha256), ("admission_sha256", admission_sha256),
                        ("policy_hash", policy_hash)):
        record[name] = _need(value, DIGEST, "SIGNSHEET_INPUT_INVALID")
    issued, expires = _utc(issued_at), _utc(expires_at)
    if not 0 < (expires - issued).total_seconds() <= (
            MAX_LAUNCHER_AGE_SECONDS if launcher else MAX_GO_AGE_SECONDS):
        raise Stop("SIGNSHEET_WINDOW_INVALID")
    record["issued_at"], record["expires_at"] = issued_at, expires_at
    return _canonical(record)


def short_code(raw):
    """16 hex of SHA-256 in 4 groups: compare it on two devices before signing."""
    if type(raw) is not bytes or not 0 < len(raw) <= 65536:
        raise Stop("SIGNSHEET_INPUT_INVALID")
    hexa = hashlib.sha256(raw).hexdigest()[:16]
    return "-".join(hexa[i:i + 4] for i in range(0, 16, 4))


def review_card(raw):
    """Read-only summary of canonical bytes: field lines + short code. Not an approval."""
    if type(raw) is not bytes or not 0 < len(raw) <= 65536:
        raise Stop("SIGNSHEET_INPUT_INVALID")
    try:
        doc = json.loads(raw.decode("utf-8"), parse_constant=lambda _: (_ for _ in ()).throw(
            ValueError()))
    except (ValueError, UnicodeError, RecursionError):
        raise Stop("SIGNSHEET_DOCUMENT_INVALID") from None
    if type(doc) is not dict or raw != _canonical(doc):
        raise Stop("SIGNSHEET_NOT_CANONICAL")
    schema = doc.get("schema")
    if schema == "qa525-ops-v3":
        order, namespace = OPS_FIELDS, OPS_NAMESPACE
    elif schema == "qa525-go-cut-v1":
        order, namespace = GO_FIELDS, GO_NAMESPACE
    elif schema == "qa525-go-launcher-v1":
        order, namespace = LAUNCHER_GO_FIELDS, GO_NAMESPACE
        if (doc.get("scope") != LAUNCHER_GO_CONSTANTS["scope"]
                or doc.get("action") != LAUNCHER_GO_CONSTANTS["action"]
                or doc.get("operator") != LAUNCHER_GO_CONSTANTS["operator"]):
            raise Stop("SIGNSHEET_DOCUMENT_INVALID")
    else:
        raise Stop("SIGNSHEET_DOCUMENT_INVALID")
    if set(doc) != set(order):
        raise Stop("SIGNSHEET_DOCUMENT_INVALID")
    return {"kind": schema, "namespace": namespace, "code": short_code(raw),
            "lines": ["%s: %s" % (name, doc[name]) for name in order],
            "sha256": "sha256:" + hashlib.sha256(raw).hexdigest(),
            "signed_here": False, "approval_here": False, "may_apply": False}


def fingerprint_of_public_key(pub_text):
    """SHA256 fingerprint of an ssh-ed25519 public key line (what Bitwarden pins)."""
    if type(pub_text) is not str or len(pub_text) > 512:
        raise Stop("SIGNSHEET_KEY_INVALID")
    match = PUB.fullmatch(pub_text)
    if match is None:
        raise Stop("SIGNSHEET_KEY_INVALID")
    try:
        blob = base64.b64decode(match.group(1), validate=True)
    except ValueError:
        raise Stop("SIGNSHEET_KEY_INVALID") from None
    if len(blob) != len(BLOB_PREFIX) + 32 or not blob.startswith(BLOB_PREFIX):
        raise Stop("SIGNSHEET_KEY_INVALID")
    return "SHA256:" + base64.b64encode(hashlib.sha256(blob).digest()).decode().rstrip("=")


def allowed_signers_line(pub_text, namespace, pinned_fingerprint):
    """``allowed_signers`` line for one namespace; refuses a key that is not the pin."""
    if type(namespace) is not str or namespace not in NAMESPACES:
        raise Stop("SIGNSHEET_NAMESPACE_INVALID")
    _need(pinned_fingerprint, FP, "SIGNSHEET_KEY_INVALID")
    if fingerprint_of_public_key(pub_text) != pinned_fingerprint:
        raise Stop("SIGNSHEET_FINGERPRINT_MISMATCH")
    blob = PUB.fullmatch(pub_text).group(1)
    return '%s namespaces="%s" ssh-ed25519 %s\n' % (PRINCIPAL, namespace, blob)


if __name__ == "__main__":
    raise SystemExit("QA525_SPONSOR_SIGNING_LIBRARY_ONLY_NO_SIGNING")
