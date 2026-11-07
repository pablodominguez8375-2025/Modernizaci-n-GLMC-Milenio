#!/usr/bin/env python3
"""QA525 B7 physical RootPlane boundary. INACTIVE, no runner, no host I/O.

This module establishes an in-process provenance boundary for a single held
RootPlane. It does NOT authenticate the Sponsor, APP, source bytes, filesystem,
journal, G6 or physical host. The *separately signed* pre-root entrypoint must
construct root_locks and its plane, bind authenticated nonce/admission inputs,
then keep that same context open through any later authorized operation.

A B7 report is evidence of inspection ONLY; never an APPLY capability.
"""
import hashlib
import json
import re

NONCE = re.compile(r"[A-Za-z0-9_-]{24,96}\Z")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")
STATUS = "QA525_B7_ROOT_BOUND_OBSERVATION_ONLY_NO_APPLY"
B7_REQUIRED = frozenset({
    "status", "baseline_sha256", "observation_sha256",
    "running_services_verified_structurally",
    "oneshot_exits_verified_structurally",
    "root_locks_held_during_inspection",
    "journal_lease_held_during_inspection",
    "may_apply", "may_install", "signature_verified_here",
    "host_identity_authenticated_here", "pin_authenticated_here",
    "g6_verified_here", "sponsor_approved_here",
})
OUTPUT_FIELDS = frozenset({
    "status", "flight_context_sha256", "baseline_sha256",
    "observation_sha256", "may_apply", "may_install",
    "authentication_verified_here", "g6_verified_here",
    "sponsor_approved_here",
})


class Stop(Exception):
    """Fixed codes only; never return private input content."""


def _valid_digest(value):
    return type(value) is str and DIGEST.fullmatch(value) is not None


def _fingerprint(nonce, admission_sha256, baseline_sha256, observation_sha256):
    value = {
        "schema": "qa525-b7-root-boundary-v1",
        "nonce": nonce,
        "admission_sha256": admission_sha256,
        "baseline_sha256": baseline_sha256,
        "observation_sha256": observation_sha256,
    }
    data = json.dumps(value, sort_keys=True, separators=(",", ":")).encode("ascii")
    return "sha256:" + hashlib.sha256(data).hexdigest()


class Boundary:
    """One boundary instance per single caller-owned root_flight context.

    Strict *class identity* deliberately rejects a duck-typed fake plane.
    A new import of root-locks yields a different class; the caller MUST
    pass the exact module instance that created this plane. This is a local
    anti-confusion check, not proof that Python/source/root are authentic.
    """

    def __init__(self, *, root_locks, plane, nonce, admission_sha256):
        root_plane_cls = getattr(root_locks, "RootPlane", None)
        if not isinstance(root_plane_cls, type) or type(plane) is not root_plane_cls:
            raise Stop("ROOTBOUNDARY_NOT_REAL_PLANE")
        if (type(nonce) is not str or NONCE.fullmatch(nonce) is None
                or not _valid_digest(admission_sha256)):
            raise Stop("ROOTBOUNDARY_INPUT_INVALID")
        self._plane = plane
        self._root_locks = root_locks
        self._nonce = nonce
        self._admission_sha256 = admission_sha256
        self._last_record = None
        self._check()

    def _check(self):
        try:
            state = self._plane.assert_held()
        except Exception:
            raise Stop("ROOTBOUNDARY_PLANE_LOST") from None
        try:
            # Journal Lease.nonce is set on begin and belongs to THIS lease.
            # An externally supplied nonce is not sufficient: compare both.
            lease_nonce = self._plane.lease.nonce
        except Exception:
            raise Stop("ROOTBOUNDARY_LEASE_NONCE_MISSING") from None
        if type(lease_nonce) is not str or lease_nonce != self._nonce:
            raise Stop("ROOTBOUNDARY_LEASE_NONCE_MISMATCH")
        if (type(state) is not dict
                or state.get("status") != self._root_locks.STATUS
                or type(state.get("legacy_locks_held")) is not int
                or state["legacy_locks_held"] != 5
                or state.get("root_lease_held") is not True
                or any(state.get(k) is not False for k in (
                    "may_apply", "may_install", "signature_verified_here",
                    "physical_runtime_verified", "legacy_entrypoint_disabled_verified"))):
            raise Stop("ROOTBOUNDARY_PLANE_INVALID")

    def inspect_once(self, *, b7, inspector, baseline_sha256):
        """Call the existing B7 inspector with our EXACT still-held plane.

        No physical host effect exists in this library. A real signed caller
        may inject a read-only inspector only after authenticating its origin.
        """
        if self._last_record is not None:
            raise Stop("ROOTBOUNDARY_ALREADY_INSPECTED")
        if (not callable(getattr(b7, "inspect_inside_held_plane", None))
                or not callable(inspector) or not _valid_digest(baseline_sha256)):
            raise Stop("ROOTBOUNDARY_INSPECT_INPUT_INVALID")
        self._check()
        try:
            report = b7.inspect_inside_held_plane(
                plane=self._plane, inspector=inspector,
                expected_baseline_sha256=baseline_sha256)
        except Exception:
            raise Stop("ROOTBOUNDARY_INSPECTION_STOP") from None
        self._check()
        if (type(report) is not dict or set(report) != B7_REQUIRED
                or report["status"] != getattr(b7, "STATUS", None)
                or report["baseline_sha256"] != baseline_sha256
                or not _valid_digest(report["observation_sha256"])
                or report["running_services_verified_structurally"] != 7
                or report["oneshot_exits_verified_structurally"] != 3
                or report["root_locks_held_during_inspection"] != 5
                or report["journal_lease_held_during_inspection"] is not True
                or any(report[k] is not False for k in (
                    "may_apply", "may_install", "signature_verified_here",
                    "host_identity_authenticated_here", "pin_authenticated_here",
                    "g6_verified_here", "sponsor_approved_here"))):
            raise Stop("ROOTBOUNDARY_REPORT_INVALID")
        record = {
            "status": STATUS,
            "flight_context_sha256": _fingerprint(
                self._nonce, self._admission_sha256,
                baseline_sha256, report["observation_sha256"]),
            "baseline_sha256": baseline_sha256,
            "observation_sha256": report["observation_sha256"],
            "may_apply": False,
            "may_install": False,
            "authentication_verified_here": False,
            "g6_verified_here": False,
            "sponsor_approved_here": False,
        }
        self._check()
        self._last_record = record
        return record

    def require_current_record(self, record):
        """Reject stale, copied, foreign, mutated and post-unlock reports.

        Object identity is intentionally in-process: serialized records cannot
        be replayed as a capability. This is NOT sufficient to authorize APPLY.
        """
        self._check()
        if (record is not self._last_record or type(record) is not dict
                or set(record) != OUTPUT_FIELDS or record["status"] != STATUS
                or any(record[k] is not False for k in (
                    "may_apply", "may_install", "authentication_verified_here",
                    "g6_verified_here", "sponsor_approved_here"))
                or not _valid_digest(record["baseline_sha256"])
                or not _valid_digest(record["observation_sha256"])
                or record["flight_context_sha256"] != _fingerprint(
                    self._nonce, self._admission_sha256,
                    record["baseline_sha256"], record["observation_sha256"])):
            raise Stop("ROOTBOUNDARY_RECORD_STALE")
        self._check()
        return True


if __name__ == "__main__":
    raise SystemExit("QA525_B7_ROOT_BOUNDARY_LIBRARY_ONLY_NO_PHYSICAL_EXECUTION")
