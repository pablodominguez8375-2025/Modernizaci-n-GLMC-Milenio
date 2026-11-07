#!/usr/bin/env python3
"""QA525 vNext: OPS receipt signature contract. INACTIVE library, never a GO.

Today ``qa525-vnext-cache.py::load_verified_ops`` trusts a root-owned receipt
(``OPS_SIGNED_VERIFIED``) without proving WHO signed it.  This module defines the
missing check: a detached Ed25519 SSH signature (``ssh-keygen -Y``) over the exact
receipt bytes, made by a dedicated OPS key whose SHA256 fingerprint the Sponsor
pins out of band, and which must differ from every APP (Sigstore/GitHub) identity.

It imports no process, file or network API.  The actual ``ssh-keygen`` run is an
injected ``verifier`` port.  It does not decide who holds the OPS private key
(Sponsor decision), does not authenticate the pinned fingerprint's provenance, and
returns ``may_apply=False`` always.
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import json
import re

NAMESPACE = "qa525-ops-v2@pmgm"
PRINCIPAL = "qa525-ops"
SSH_KEYGEN = "/usr/bin/ssh-keygen"
MAX_RECEIPT = 65536
MAX_SIGNATURE = 4096
FINGERPRINT = re.compile(r"SHA256:[A-Za-z0-9+/]{43}\Z")
SIGNER_LINE = re.compile(
    r'qa525-ops namespaces="qa525-ops-v2@pmgm" ssh-ed25519 ([A-Za-z0-9+/]{68})\n?\Z')
BLOB_PREFIX = b"\x00\x00\x00\x0bssh-ed25519\x00\x00\x00\x20"
SIG_BEGIN = b"-----BEGIN SSH SIGNATURE-----\n"
SIG_END = b"-----END SSH SIGNATURE-----\n"
SHA40 = re.compile(r"[a-f0-9]{40}\Z")
DIGEST = re.compile(r"sha256:[a-f0-9]{64}\Z")
RECEIPT_FIELDS = frozenset({"schema", "status", "ops_source_sha", "program_sha256",
                            "readiness_sha256", "staging_sha256", "policy_hash"})
# v3 = v2 + the digest of the APP-attested archive the OPS digests were built from.
# It exists so a gate can prove OPS-signed bytes and APP-attested bytes are the SAME
# release (closes #928 P1-A: two individually valid signatures from different
# releases must not combine).
RECEIPT_FIELDS_V3 = RECEIPT_FIELDS | {"app_archive_sha256"}
PATH = re.compile(r"/[A-Za-z0-9._/-]{1,200}\Z")


class Stop(Exception):
    """Fixed codes only; never include receipt, key, signature or paths."""


def _unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise Stop("OPSSIG_RECEIPT_INVALID")
        result[key] = value
    return result


def fingerprint_of(allowed_signers):
    """Fingerprint of the single pinned OPS key; reject anything else."""
    if type(allowed_signers) is not bytes or not 0 < len(allowed_signers) <= 1024:
        raise Stop("OPSSIG_SIGNERS_INVALID")
    try:
        text = allowed_signers.decode("ascii")
    except UnicodeDecodeError:
        raise Stop("OPSSIG_SIGNERS_INVALID") from None
    match = SIGNER_LINE.fullmatch(text)
    if match is None:
        raise Stop("OPSSIG_SIGNERS_INVALID")
    try:
        blob = base64.b64decode(match.group(1), validate=True)
    except ValueError:
        raise Stop("OPSSIG_SIGNERS_INVALID") from None
    if len(blob) != len(BLOB_PREFIX) + 32 or not blob.startswith(BLOB_PREFIX):
        raise Stop("OPSSIG_SIGNERS_INVALID")
    digest = base64.b64encode(hashlib.sha256(blob).digest()).decode().rstrip("=")
    return "SHA256:" + digest


def ssh_keygen_argv(allowed_signers_path, signature_path):
    """Fixed argv for the future runner; the receipt is passed on stdin."""
    for value in (allowed_signers_path, signature_path):
        if type(value) is not str or PATH.fullmatch(value) is None or ".." in value.split("/"):
            raise Stop("OPSSIG_PATH_INVALID")
    return (SSH_KEYGEN, "-Y", "verify", "-f", allowed_signers_path,
            "-I", PRINCIPAL, "-n", NAMESPACE, "-s", signature_path)


def verify_ops_receipt(*, receipt, signature, allowed_signers, ops_fingerprint,
                       app_fingerprints, policy_hash, verifier,
                       expected_app_archive_sha256=None):
    """Return the parsed receipt only if the pinned OPS key signed its exact bytes
    AND its seven fields have the exact shape ``load_verified_ops`` consumes.

    ``expected_app_archive_sha256`` (default ``None``) selects the schema:
    ``None`` keeps the v2 contract unchanged (exactly seven fields).  A digest
    demands a v3 receipt (eight fields) whose ``app_archive_sha256`` equals it, so
    a v2 receipt can never be downgraded into a bound check and a v3 receipt is
    never accepted by a caller that did not ask for the binding."""
    if (type(receipt) is not bytes or not 0 < len(receipt) <= MAX_RECEIPT
            or type(signature) is not bytes or not 0 < len(signature) <= MAX_SIGNATURE
            or not signature.startswith(SIG_BEGIN) or not signature.endswith(SIG_END)
            or type(ops_fingerprint) is not str or FINGERPRINT.fullmatch(ops_fingerprint) is None
            or type(app_fingerprints) is not frozenset or not app_fingerprints
            or any(type(x) is not str or FINGERPRINT.fullmatch(x) is None
                   for x in app_fingerprints)
            or type(policy_hash) is not str or DIGEST.fullmatch(policy_hash) is None
            or not callable(verifier)
            or (expected_app_archive_sha256 is not None
                and (type(expected_app_archive_sha256) is not str
                     or DIGEST.fullmatch(expected_app_archive_sha256) is None))):
        raise Stop("OPSSIG_INPUT_INVALID")
    actual = fingerprint_of(allowed_signers)
    if not hmac.compare_digest(actual, ops_fingerprint):
        raise Stop("OPSSIG_FINGERPRINT_MISMATCH")
    if ops_fingerprint in app_fingerprints:
        raise Stop("OPSSIG_APP_IDENTITY_REUSED")
    try:
        code, out = verifier(allowed_signers, signature, PRINCIPAL, NAMESPACE, receipt)
    except Exception:
        raise Stop("OPSSIG_VERIFIER_FAILED") from None
    expected = ('Good "%s" signature for %s with ED25519 key %s\n'
                % (NAMESPACE, PRINCIPAL, ops_fingerprint)).encode()
    if type(code) is not int or code != 0 or type(out) is not bytes or out != expected:
        raise Stop("OPSSIG_NOT_VERIFIED")
    try:
        doc = json.loads(
            receipt.decode("utf-8"), object_pairs_hook=_unique,
            parse_constant=lambda _: (_ for _ in ()).throw(Stop("OPSSIG_RECEIPT_INVALID")))
    except Stop:
        raise
    except (UnicodeDecodeError, ValueError, RecursionError, TypeError):
        raise Stop("OPSSIG_RECEIPT_INVALID") from None
    bound = expected_app_archive_sha256 is not None
    if (type(doc) is not dict or set(doc) != (RECEIPT_FIELDS_V3 if bound else RECEIPT_FIELDS)
            or doc["schema"] != ("qa525-ops-v3" if bound else "qa525-ops-v2")
            or doc["status"] != "OPS_SIGNED_VERIFIED"
            or type(doc["ops_source_sha"]) is not str or not SHA40.fullmatch(doc["ops_source_sha"])
            or any(type(doc[k]) is not str or not DIGEST.fullmatch(doc[k]) for k in (
                "program_sha256", "readiness_sha256", "staging_sha256"))
            or doc["policy_hash"] != policy_hash):
        raise Stop("OPSSIG_RECEIPT_INVALID")
    if bound and (type(doc["app_archive_sha256"]) is not str
                  or not DIGEST.fullmatch(doc["app_archive_sha256"])):
        raise Stop("OPSSIG_RECEIPT_INVALID")
    if bound and not hmac.compare_digest(doc["app_archive_sha256"],
                                         expected_app_archive_sha256):
        raise Stop("OPSSIG_APP_ARCHIVE_MISMATCH")
    return {"receipt": doc, "ops_fingerprint": ops_fingerprint,
            "receipt_sha256": "sha256:" + hashlib.sha256(receipt).hexdigest(),
            "app_archive_bound": bound,
            "signature_verified_here": True, "fingerprint_provenance_verified_here": False,
            "app_identity_verified_here": False, "key_custody_verified_here": False,
            "may_apply": False, "sponsor_approved_here": False}
