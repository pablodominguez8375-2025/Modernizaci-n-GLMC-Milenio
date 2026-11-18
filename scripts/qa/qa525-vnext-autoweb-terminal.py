#!/usr/bin/env python3
"""Pure authenticated terminal contract for QA525 auto-web V3.2.

INACTIVE library: no filesystem, process, network or host access.  A trusted
caller injects the signature verifier.  Only the opaque capability returned by
``authenticate`` may be consumed by the multi-release journal contract.

The production signer is a dedicated local root-owned key loaded by the future
verified entrypoint.  It protects the chain from non-root tampering; it is not
a boundary against a compromised root process.  The key, signature verifier,
stage inventory and runtime measurements are external trust inputs.
"""
import datetime as dt
import hashlib
import json
import re
import weakref

SCHEMA = "pmgm-qa-auto-web-terminal-v1"
DIGEST = re.compile(r"^sha256:[a-f0-9]{64}$")
NONCE = re.compile(r"^[A-Za-z0-9_-]{24,96}$")
RELEASE = DIGEST
STATUSES = {"ACTIVE", "BLOCKED"}
FIELDS = frozenset({
    "schema", "status", "policy_id", "policy_generation", "sequence",
    "release_id", "nonce", "stage_record_sha256", "final_stage",
    "installed_before_sha256", "expected_after_sha256",
    "observed_after_sha256", "health_receipt_sha256",
    "rollback_receipt_sha256", "previous_terminal_sha256", "critical",
    "finished_at", "may_apply", "authenticated_by",
})


class Stop(Exception):
    """Static fail-closed code only."""


def _canonical(value):
    try:
        return (json.dumps(value, sort_keys=True, separators=(",", ":"),
                           ensure_ascii=True, allow_nan=False) + "\n").encode()
    except (TypeError, ValueError, RecursionError):
        raise Stop("TERMINAL_JSON_INVALID") from None


def digest(value):
    return "sha256:" + hashlib.sha256(_canonical(value)).hexdigest()


def _time(value):
    if type(value) is not str or re.fullmatch(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ", value) is None:
        raise Stop("TERMINAL_TIME_INVALID")
    try:
        return dt.datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=dt.timezone.utc)
    except ValueError:
        raise Stop("TERMINAL_TIME_INVALID") from None


def validate(record):
    """Validate exact binding and return a detached canonical copy."""
    if type(record) is not dict or set(record) != FIELDS or record.get("schema") != SCHEMA:
        raise Stop("TERMINAL_FIELDS_INVALID")
    if (record["status"] not in STATUSES
            or type(record["policy_id"]) is not str or not record["policy_id"]
            or type(record["policy_generation"]) is not int or record["policy_generation"] < 1
            or type(record["sequence"]) is not int or record["sequence"] < 1
            or type(record["release_id"]) is not str or RELEASE.fullmatch(record["release_id"]) is None
            or type(record["nonce"]) is not str or NONCE.fullmatch(record["nonce"]) is None
            or record["may_apply"] is not False
            or record["authenticated_by"] != "EXTERNAL_SIGNATURE_VERIFIER_V1"):
        raise Stop("TERMINAL_BINDING_INVALID")
    for key in ("stage_record_sha256", "installed_before_sha256",
                "expected_after_sha256", "observed_after_sha256",
                "health_receipt_sha256"):
        if type(record[key]) is not str or DIGEST.fullmatch(record[key]) is None:
            raise Stop("TERMINAL_DIGEST_INVALID")
    for key in ("rollback_receipt_sha256", "previous_terminal_sha256"):
        if record[key] is not None and (type(record[key]) is not str or DIGEST.fullmatch(record[key]) is None):
            raise Stop("TERMINAL_DIGEST_INVALID")
    _time(record["finished_at"])
    if type(record["critical"]) is not bool:
        raise Stop("TERMINAL_BINDING_INVALID")
    if record["status"] == "ACTIVE":
        if (record["final_stage"] != "VERIFYING"
                or record["expected_after_sha256"] == record["installed_before_sha256"]
                or record["observed_after_sha256"] != record["expected_after_sha256"]
                or record["rollback_receipt_sha256"] is not None
                or record["critical"] is not False):
            raise Stop("TERMINAL_ACTIVE_INVALID")
    else:
        if (record["final_stage"] != "ROLLBACK_VERIFYING"
                or record["rollback_receipt_sha256"] is None):
            raise Stop("TERMINAL_BLOCKED_INVALID")
        restored = record["observed_after_sha256"] == record["installed_before_sha256"]
        if (record["critical"] is False and not restored) or (record["critical"] is True and restored):
            raise Stop("TERMINAL_ROLLBACK_STATE_INVALID")
    return json.loads(_canonical(record))


class _AuthenticatedTerminal:
    __slots__ = ("_record", "_sha256", "__weakref__")

    def __init__(self, record):
        self._record = record
        self._sha256 = digest(record)

    def summary(self):
        return json.loads(_canonical(self._record)), self._sha256


_ISSUED = weakref.WeakSet()


def _bind_external(record, stage_binding, measurements):
    stage_fields = {"policy_id", "policy_generation", "sequence", "release_id",
                    "nonce", "record_sha256", "stage"}
    measure_fields = {"installed_before_sha256", "expected_after_sha256",
                      "observed_after_sha256", "health_receipt_sha256",
                      "rollback_receipt_sha256"}
    if type(stage_binding) is not dict or set(stage_binding) != stage_fields:
        raise Stop("TERMINAL_STAGE_BINDING_INVALID")
    if type(measurements) is not dict or set(measurements) != measure_fields:
        raise Stop("TERMINAL_MEASUREMENTS_INVALID")
    expected_stage = "VERIFYING" if record["status"] == "ACTIVE" else "ROLLBACK_VERIFYING"
    comparisons = {
        "policy_id": record["policy_id"], "policy_generation": record["policy_generation"],
        "sequence": record["sequence"], "release_id": record["release_id"],
        "nonce": record["nonce"], "record_sha256": record["stage_record_sha256"],
        "stage": expected_stage,
    }
    if stage_binding != comparisons:
        raise Stop("TERMINAL_STAGE_BINDING_MISMATCH")
    if any(measurements[key] != record[key] for key in measure_fields):
        raise Stop("TERMINAL_MEASUREMENTS_MISMATCH")


def authenticate(record, signature, verify_signature, stage_binding, measurements,
                 physical_plane, physical_plane_module):
    """Return an opaque capability only after an injected verifier says True."""
    checked = validate(record)
    _bind_external(checked, stage_binding, measurements)
    if (type(physical_plane_module).__name__ != "module"
            or type(physical_plane) is not getattr(physical_plane_module, "PhysicalPlaneCapability", None)):
        raise Stop("TERMINAL_PHYSICAL_PLANE_INVALID")
    try:
        held = physical_plane.assert_held()
    except Exception:
        raise Stop("TERMINAL_PHYSICAL_PLANE_NOT_HELD") from None
    if (type(held) is not dict
            or held.get("schema") != "pmgm-qa-auto-web-root-plane-v1"
            or held.get("legacy_locks_held") != 5
            or held.get("physical_root_plane_v1") is not True
            or held.get("may_apply") is not False):
        raise Stop("TERMINAL_PHYSICAL_PLANE_NOT_HELD")
    if not callable(verify_signature) or type(signature) is not bytes or not signature:
        raise Stop("TERMINAL_AUTH_INPUT_INVALID")
    try:
        verified = verify_signature(_canonical(checked), bytes(signature))
    except Exception:
        raise Stop("TERMINAL_SIGNATURE_INVALID") from None
    if verified is not True:
        raise Stop("TERMINAL_SIGNATURE_INVALID")
    try:
        physical_plane.assert_held()
    except Exception:
        raise Stop("TERMINAL_PHYSICAL_PLANE_NOT_HELD") from None
    capability = _AuthenticatedTerminal(checked)
    _ISSUED.add(capability)
    return capability


def require_capability(value):
    if type(value) is not _AuthenticatedTerminal or value not in _ISSUED:
        raise Stop("TERMINAL_CAPABILITY_REQUIRED")
    record, fingerprint = value.summary()
    validate(record)
    if digest(record) != fingerprint:
        raise Stop("TERMINAL_CAPABILITY_INVALID")
    return record, fingerprint


if __name__ == "__main__":
    raise SystemExit("QA525_AUTOWEB_TERMINAL_LIBRARY_ONLY_NO_EXECUTION")
