#!/usr/bin/env python3
"""QA525 B7: capture a non-authoritative read-only baseline under HELD root locks.

INACTIVE, LIBRARY ONLY. Reuses the existing five-lock RootPlane + journal
lease. It has no direct file/process writes and never authorizes an APPLY, BUT
the injected journal.begin MAY persist state when connected to a real journal.
Never invoke as a physical "read-only preflight" without an explicit host GO.
The caller must inject a reviewed port, journal and inspector.

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

try:
    HERE = Path(__file__).resolve().parent
except NameError:  # PRE-4 Verified exec has no __file__: siblings are INJECTED by bind_dependencies, never read from disk
    HERE = None
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


root_locks = _load("qa525_b7_held_root_locks", "qa525-vnext-root-locks.py") if HERE is not None else None

SIBLING_MODULES = {"root_locks": "qa525-vnext-root-locks.py"}
_BOUND = []


def bind_dependencies(mapping):
    """PRE-4 ONLY: receive the exact sibling modules (one verified instance per file, shared by the whole flight)."""
    if (HERE is not None or _BOUND or type(mapping) is not dict or set(mapping) != set(SIBLING_MODULES)
            or any(type(m).__name__ != "module" or type(m).__module__ != "builtins" for m in mapping.values())):
        raise Stop("DEPENDENCY_BINDING_INVALID")
    globals().update(mapping)
    _BOUND.append(True)


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


def inspect_inside_held_plane(*, plane, inspector, expected_baseline_sha256):
    """Inspect with a caller-owned RootPlane that remains held after return.

    This is the composable, still-INACTIVE part of B7 for the future single
    signed root entrypoint. The caller MUST own the entire root_flight context
    and preserve it across inspection AND any independently authorized flight.
    A returned report is NEVER an APPLY permission. A caller-supplied plane,
    inspector or pin is NOT authenticated by this function.
    """
    if (not callable(inspector) or type(expected_baseline_sha256) is not str
            or SHA256.fullmatch(expected_baseline_sha256) is None):
        raise Stop("B7_INPUT_INVALID")
    _held(plane)
    try:
        report = inspector()
    except Exception:
        raise Stop("B7_INSPECTION_FAILED") from None
    _held(plane)
    _shape(report, expected_baseline_sha256)
    _held(plane)
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


def held_readonly(*, port, journal, root, admission, admission_sha256,
                  now, inspector, expected_baseline_sha256):
    """Historical convenience wrapper: releases ALL five locks on return.

    Unlike inspect_inside_held_plane(), this wrapper closes root_flight
    before yielding its report, so it MUST NEVER be used as a post-unlock
    preflight for a subsequent APPLY. Its journal may persist nonce/lease.
    """
    if (not callable(inspector) or type(expected_baseline_sha256) is not str
            or SHA256.fullmatch(expected_baseline_sha256) is None):
        raise Stop("B7_INPUT_INVALID")
    try:
        with root_locks.root_flight(port, journal, root, admission,
                                    admission_sha256, now) as plane:
            return inspect_inside_held_plane(
                plane=plane, inspector=inspector,
                expected_baseline_sha256=expected_baseline_sha256)
    except Stop:
        raise
    except Exception:
        # Includes root_locks.Stop, bad cleanup and unknown external exceptions.
        # No partial result can be accepted as success.
        raise Stop("B7_ROOT_PLANE_DENIED") from None


if __name__ == "__main__":
    raise SystemExit("QA525_B7_HELD_READONLY_LIBRARY_ONLY_NO_APPLY")
