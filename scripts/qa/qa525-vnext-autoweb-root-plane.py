#!/usr/bin/env python3
"""Versioned physical RootPlane capability for QA525 auto-web V3.2.

Pure wrapper only.  It performs no host action and never supplies a default
runner.  Every effect revalidates the underlying physical plane first.
"""

SCHEMA = "pmgm-qa-auto-web-root-plane-v1"


class Stop(Exception):
    """Static fail-closed code only."""


def _held(plane):
    if not callable(getattr(plane, "assert_held", None)):
        raise Stop("PHYSICAL_PLANE_INVALID")
    try:
        status = plane.assert_held()
    except Exception:
        raise Stop("PHYSICAL_PLANE_NOT_HELD") from None
    if (type(status) is not dict
            or status.get("legacy_locks_held") != 5
            or status.get("root_lease_held") is not True
            or "root_lease_held_synthetically" in status
            or status.get("may_apply") is not False
            or status.get("may_install") is not False
            or status.get("signature_verified_here") is not False
            or status.get("physical_runtime_verified") is not False):
        raise Stop("PHYSICAL_PLANE_NOT_HELD")
    return status


class PhysicalPlaneCapability:
    __slots__ = ("_plane", "_module", "_closed")

    def __init__(self, plane, root_plane_module):
        if (type(root_plane_module).__name__ != "module"
                or not isinstance(getattr(root_plane_module, "LEGACY_LOCKS", None), tuple)
                or len(root_plane_module.LEGACY_LOCKS) != 5
                or type(plane) is not getattr(root_plane_module, "RootPlane", None)):
            raise Stop("PHYSICAL_PLANE_TYPE_INVALID")
        _held(plane)
        self._plane, self._module, self._closed = plane, root_plane_module, False

    def assert_held(self):
        if self._closed:
            raise Stop("PHYSICAL_CAPABILITY_CLOSED")
        _held(self._plane)
        return {
            "schema": SCHEMA,
            "legacy_locks_held": 5,
            "physical_root_plane_v1": True,
            "may_apply": False,
            "may_install": False,
        }

    def close(self):
        self._closed = True


def bind(plane, root_plane_module):
    return PhysicalPlaneCapability(plane, root_plane_module)


def held_runner(capability, run, argv_guard, *, max_effects=1):
    if (type(capability) is not PhysicalPlaneCapability
            or not callable(run) or not callable(argv_guard)
            or type(max_effects) is not int or max_effects not in (1, 2)):
        raise Stop("PHYSICAL_RUNNER_AUTHORITY_REQUIRED")
    remaining = [max_effects]

    def runner(argv):
        capability.assert_held()
        if remaining[0] <= 0:
            raise Stop("PHYSICAL_EFFECT_BUDGET_SPENT")
        try:
            command = argv_guard(argv)
        except Exception:
            raise Stop("PHYSICAL_ARGV_REJECTED") from None
        if type(command) is not list or not all(type(x) is str for x in command):
            raise Stop("PHYSICAL_ARGV_REJECTED")
        capability.assert_held()
        remaining[0] -= 1  # spent before the process: never replay an ambiguous effect
        try:
            result = run(list(command))
        except Exception:
            raise Stop("PHYSICAL_EFFECT_INDETERMINATE") from None
        if type(result) is not int:
            raise Stop("PHYSICAL_EFFECT_INDETERMINATE")
        try:
            capability.assert_held()
        except Exception:
            raise Stop("PHYSICAL_PLANE_LOST_AFTER_EFFECT") from None
        return result

    return runner


if __name__ == "__main__":
    raise SystemExit("QA525_AUTOWEB_ROOT_PLANE_LIBRARY_ONLY_NO_EXECUTION")
