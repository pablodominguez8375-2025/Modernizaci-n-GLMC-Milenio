#!/usr/bin/env python3
"""QA525 held RootPlane systemd-run allowlist. INACTIVE LIBRARY; NO executor.

Never treat this as signed pre-root bootstrap, physical GO or a substitute
for systemd/cgroup recovery. Signed caller MUST authenticate OPS/APP,
Sponsor consent, G6 and all pins from independent trusted sources, and inject
actual process effect. This module has NO subprocess/OS/Docker import or default
process effect. Caller retains root_flight throughout B7, API, WEB and rollback.

Allowlist has exactly four commands: api/web baseline and target, each running
Docker Compose inside its own transient systemd scope. Scope MUST already
have a durable scope-store record under same lease before injected effect.
"""
import re

STATUS = "QA525_HELD_PHYSICAL_RUNNER_CONTRACT_NOT_AUTHENTICATED_NO_GO"
DIGEST = re.compile(r"sha256:[a-f0-9]{64}\Z")
NONCE = re.compile(r"[A-Za-z0-9_-]{24,96}\Z")
PIN_KEYS = frozenset({
    "systemd_run_sha256", "docker_sha256", "python_sha256",
    "child_sha256", "compose_stack_sha256",
})
SPEC_KEYS = frozenset({
    "nonce", "project", "workdir", "env_file", "baseline_layers",
    "target_layers", "admission_sha256",
})


class Stop(Exception):
    """Fixed codes only, never leak private inputs."""


def _is_digest(value):
    return type(value) is str and DIGEST.fullmatch(value) is not None


def _layers(value):
    if type(value) is not dict or set(value) != {"api", "web"}:
        raise Stop("PHYSRUNNER_LAYERS_INVALID")
    out = {}
    for service in ("api", "web"):
        rows = value[service]
        if (type(rows) is not list or not 1 <= len(rows) <= 8
                or any(type(path) is not str for path in rows)
                or len(rows) != len(set(rows))):
            raise Stop("PHYSRUNNER_LAYERS_INVALID")
        out[service] = tuple(rows)
    return out


def bind(*, root_locks, boundary_module, containment, scopes,
         plane, boundary, report, spec, pins, verify_pins, execute):
    """Create callable runner(argv)->int for a preauthenticated root flight.

    verify_pins(expected_pins) injected by independently authenticated
    entrypoint MUST verify actual root-owned bytes, SHA and ownership every
    time. execute(argv) is ONLY source of effects, and MUST run reviewed binary.
    Shape checks and SHA strings in this helper are NOT authentication.
    """
    if (type(spec) is not dict or set(spec) != SPEC_KEYS
            or type(pins) is not dict or set(pins) != PIN_KEYS
            or any(not _is_digest(pins[name]) for name in PIN_KEYS)
            or not _is_digest(spec["admission_sha256"])
            or type(spec["nonce"]) is not str or NONCE.fullmatch(spec["nonce"]) is None):
        raise Stop("PHYSRUNNER_INPUT_INVALID")
    if not callable(verify_pins) or not callable(execute):
        raise Stop("PHYSRUNNER_EFFECT_REQUIRED")
    if (type(getattr(root_locks, "RootPlane", None)) is not type
            or type(plane) is not root_locks.RootPlane
            or type(getattr(boundary_module, "Boundary", None)) is not type
            or type(boundary) is not boundary_module.Boundary
            or boundary._plane is not plane
            or not callable(getattr(boundary, "require_current_record", None))
            or not callable(getattr(scopes, "require_recorded", None))
            or not callable(getattr(containment, "unit_name", None))
            or not callable(getattr(containment, "systemd_run_argv", None))):
        raise Stop("PHYSRUNNER_PLANE_INVALID")
    if (plane.lease.nonce != spec["nonce"]
            or boundary._admission_sha256 != spec["admission_sha256"]):
        raise Stop("PHYSRUNNER_FLIGHT_MISMATCH")
    try:
        boundary.require_current_record(report)
    except Exception:
        raise Stop("PHYSRUNNER_REPORT_INVALID") from None

    baseline = _layers(spec["baseline_layers"])
    target = _layers(spec["target_layers"])
    project = spec["project"]
    workdir = spec["workdir"]
    env_file = spec["env_file"]
    allow = {}
    # Freeze in tuples: caller changes cannot expand or alter a trusted cut.
    for layers in (baseline, target):
        for service in ("api", "web"):
            unit = containment.unit_name(spec["nonce"], service)
            command = tuple(containment.systemd_run_argv(
                unit, project, workdir, env_file, list(layers[service]), service))
            if command in allow:
                raise Stop("PHYSRUNNER_DUPLICATE_COMMAND")
            allow[command] = unit
    if len(allow) != 4:
        raise Stop("PHYSRUNNER_COMMANDS_INVALID")
    frozen_pins = {key: pins[key] for key in sorted(PIN_KEYS)}
    bound_nonce = spec["nonce"]
    bound_admission = spec["admission_sha256"]

    def held():
        try:
            if (plane.lease.nonce != bound_nonce
                    or boundary._admission_sha256 != bound_admission):
                raise Stop("PHYSRUNNER_FLIGHT_MISMATCH")
            boundary.require_current_record(report)
        except Stop:
            raise
        except Exception:
            raise Stop("PHYSRUNNER_PLANE_LOST") from None

    def runner(argv):
        if (type(argv) is not list
                or any(type(part) is not str for part in argv)
                or tuple(argv) not in allow):
            raise Stop("PHYSRUNNER_ARGV_REFUSED")
        command = tuple(argv)
        unit = allow[command]
        held()
        try:
            receipt = scopes.require_recorded(plane.lease, bound_admission, unit)
        except Exception:
            raise Stop("PHYSRUNNER_SCOPE_NOT_RECORDED") from None
        if not _is_digest(receipt):
            raise Stop("PHYSRUNNER_SCOPE_NOT_RECORDED")
        held()
        try:
            if verify_pins(dict(frozen_pins)) is not True:
                raise Stop("PHYSRUNNER_PINS_UNVERIFIED")
        except Stop:
            raise
        except Exception:
            raise Stop("PHYSRUNNER_PINS_UNVERIFIED") from None
        held()
        try:
            code = execute(list(command))
        except Exception:
            raise Stop("PHYSRUNNER_EXECUTION_FAILED") from None
        # A lost lock or changed on-disk pin after a process is ambiguous:
        # upstream journal/flight must reconcile/rollback, never re-issue.
        held()
        try:
            if verify_pins(dict(frozen_pins)) is not True:
                raise Stop("PHYSRUNNER_PINS_CHANGED")
        except Stop:
            raise
        except Exception:
            raise Stop("PHYSRUNNER_PINS_CHANGED") from None
        if type(code) is not int:
            raise Stop("PHYSRUNNER_RESULT_INVALID")
        return code

    return runner


if __name__ == "__main__":
    raise SystemExit("QA525_PHYSICAL_RUNNER_LIBRARY_ONLY_NO_HOST_EXECUTION")
