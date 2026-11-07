#!/usr/bin/env python3
"""QA525 B7: capture a non-authoritative read-only baseline under HELD root locks.

INACTIVE, LIBRARY ONLY. Reuses the existing five-lock RootPlane + journal lease,
but NEVER creates a host port, executes a process, writes a file or authorizes
an application. The caller must inject a reviewed port, journal and inspector.

The inspector's shape/pin are structurally checked while all five locks and
the lease are held, with a second lock assertion after inspection. This is a
preflight bridge, NOT a signed physical entrypoint: the supplied inspector,
baseline pin, journal, locks and host identity remain UNAUTHENTICATED here.
"""
import hashlib
import hmac
import importlib.util
from pathlib import Path
import re

HERE = Path(__file__).resolve().parent
SHA256 = re.compile(r"sha256:[a-f0-9]{64}\Z")
STATUS = "B7_HELD_READONLY_PREFLIGHT_NO_HOST_AUTHORITY"
SHAPE_STATUS = "QA525_VNEXT_READONLY_SHAPE_STABLE_NOT_AUTHORIZED"
SHAPE_FIELDS = frozenset({
    "status", "baseline_sha256", "observation_sha256",
    "running_services_expected", "exited_zero_oneshots_verified",
    "api_compose_layers", "web_compose_layers", "samples",
    "sourced_from_authenticated_host", "sponsor_approved_here",
    "g6_verified_here", "may_apply", "may_install",
})


class Stop(Exception):
    """Stable diagnostic codes without caller content."""


def _load(name, path):
    spec = importlib.util.spec_from_file_location(name, HERE / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


root_locks = _load("qa525_b7_held_root_locks", "qa525-vnext-root-locks.py")


def _held(plane):
    """Re-check the held root-plane and its lease; no truthy shortcuts."""
    state = plane.assert_held()
    if (type(state) is not dict or state.get("legacy_locks_held") != 5
            or state.get("root_lease_held") is not True
            or state.get("may_apply") is not False
            or state.get("may_install") is not False):
        raise Stop("B7_PLANE_NOT_HELD")


def _shape(report, baseline_sha256):
    """Structural inspection only; NOT authenticated Docker or pin evidence."""
    if type(report) is not dict or set(report) != SHAPE_FIELDS:
        raise Stop("B7_SHAPE_FIELDS_INVALID")
    if (report["status"] != SHAPE_STATUS
            or type(report["baseline_sha256"]) is not str
            or SHA256.fullmatch(report["baseline_sha256"]) is None
            or not hmac.compare_digest(report["baseline_sha256"], baseline_sha256)
            or type(report["observation_sha256"]) is not str
            or SHA256.fullmatch(report["observation_sha256"]) is None
            or type(report["running_services_expected"]) is not int
            or report["running_services_expected"] != 7
            or type(report["exited_zero_oneshots_verified"]) is not int
            or report["exited_zero_oneshots_verified"] != 3
            or type(report["api_compose_layers"]) is not int
            or not 6 <= report["api_compose_layers"] <= 64
            or type(report["web_compose_layers"]) is not int
            or not 5 <= report["web_compose_layers"] <= 64
            or type(report["samples"]) is not int
            or not 2 <= report["samples"] <= 5
            or any(report[field] is not False for field in (
                "sourced_from_authenticated_host", "sponsor_approved_here",
                "g6_verified_here", "may_apply", "may_install"))):
        raise Stop("B7_SHAPE_MISMATCH")


def held_readonly(*, port, journal, root, admission, admission_sha256,
                  now, inspector, expected_baseline_sha256):
    """Capture once with the same five legacy locks and root journal lease.

    No port/journal/inspector/expected pin is created or authenticated here.
    A future independent pre-root bootstrap must establish the origin and
    byte-integrity of all of them. This bridge cannot be wired as an APPLY gate.
    """
    if (not callable(inspector) or type(expected_baseline_sha256) is not str
            or SHA256.fullmatch(expected_baseline_sha256) is None):
        raise Stop("B7_INPUT_INVALID")
    try:
        with root_locks.root_flight(port, journal, root, admission,
                                    admission_sha256, now) as plane:
            _held(plane)
            try:
                report = inspector()
            except Exception:
                raise Stop("B7_INSPECTION_FAILED") from None
            _held(plane)
            _shape(report, expected_baseline_sha256)
            _held(plane)  # release is only after the report has been checked
            return {
                "status": STATUS,
                "baseline_sha256": expected_baseline_sha256,
                "observation_sha256": report["observation_sha256"],
                "running_services_verified_structurally": 7,
                "oneshot_exits_verified_structurally": 3,
                "root_locks_held_during_inspection": 5,
                "journal_lease_held_during_inspection": True,
                "may_apply": False,
                "may_install": False,
                "signature_verified_here": False,
                "host_identity_authenticated_here": False,
                "pin_authenticated_here": False,
                "g6_verified_here": False,
                "sponsor_approved_here": False,
            }
    except Stop:
        raise
    except Exception:
        # Includes root_locks.Stop, bad cleanup and unknown external exceptions.
        # No partial result can be accepted as success.
        raise Stop("B7_ROOT_PLANE_DENIED") from None


if __name__ == "__main__":
    raise SystemExit("QA525_B7_HELD_READONLY_LIBRARY_ONLY_NO_APPLY")
