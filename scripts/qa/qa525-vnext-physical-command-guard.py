#!/usr/bin/env python3
"""QA525 physical command allowlist contract. INACTIVE; no host executor.

Only the 4 canonical systemd scope / docker compose argv forms for one
authenticated flight may reach an INJECTED process callback. This module
authenticates nothing: the root-owned code, Docker, systemd-run, Python,
Compose files, OPS/APP signatures, Sponsor consent and G6 MUST be verified
by an independent signed pre-root entrypoint before using ANY injected effect.

The injected callbacks must prove a durable intent, durable scope manifest,
unexpired external authority and pinned binaries/files BEFORE each call.
Their provenance is NOT proven here. No CLI, imports that spawn processes,
fallback shell, automatic retries or default executor exists.
"""
import re

STATUS = "QA525_PHYSICAL_COMMAND_GUARD_INACTIVE_NO_PHYSICAL_AUTHORITY"
SERVICES = ("api", "web")
SIDES = ("target", "baseline")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")


class Stop(Exception):
    """Fixed codes only; never copy command, exception or credential text."""


class CommandGuard:
    """Single-use per command, caller-injected authority under HELD B7 plane."""

    def __init__(self, *, containment, plan_mod, plan, boundary, record,
                 project, workdir, env_file, run_effect, scope_factory,
                 require_intent, require_scope_recorded,
                 verify_pins, verify_approval):
        effects = (run_effect, scope_factory, require_intent,
                   require_scope_recorded, verify_pins, verify_approval)
        if any(not callable(f) for f in effects):
            raise Stop("COMMANDGUARD_EFFECT_REQUIRED")
        if (not callable(getattr(containment, "systemd_run_argv", None))
                or not callable(getattr(containment, "unit_name", None))
                or not callable(getattr(plan_mod, "validate", None))
                or not callable(getattr(boundary, "require_current_record", None))):
            raise Stop("COMMANDGUARD_DEPENDENCY_INVALID")
        if type(plan) is not dict:
            raise Stop("COMMANDGUARD_PLAN_INVALID")
        try:
            # validate() hashes EVERY baseline/target field and the stage chain.
            stage = plan_mod.validate(plan)
        except Exception:
            raise Stop("COMMANDGUARD_PLAN_INVALID") from None
        if stage != "APPLYING":
            raise Stop("COMMANDGUARD_PLAN_STAGE_INVALID")
        if (type(plan.get("nonce")) is not str
                or type(plan.get("admission_sha256")) is not str
                or not DIGEST.fullmatch(plan["admission_sha256"])
                or type(plan.get("baseline_sha256")) is not str
                or not DIGEST.fullmatch(plan["baseline_sha256"])
                or type(plan.get("target_sha256")) is not str
                or not DIGEST.fullmatch(plan["target_sha256"])):
            raise Stop("COMMANDGUARD_PLAN_INVALID")
        try:
            if (boundary.require_current_record(record) is not True
                    or type(record) is not dict
                    or record.get("baseline_sha256") != plan["baseline_sha256"]
                    or getattr(boundary, "_nonce", None) != plan["nonce"]
                    or getattr(boundary, "_admission_sha256", None)
                    != plan["admission_sha256"]):
                raise Stop("COMMANDGUARD_FLIGHT_UNBOUND")
        except Stop:
            raise
        except Exception:
            raise Stop("COMMANDGUARD_FLIGHT_UNBOUND") from None
        self._boundary, self._record = boundary, record
        self._execute = run_effect
        self._scope = scope_factory
        self._intent = require_intent
        self._scope_recorded = require_scope_recorded
        self._verify_pins = verify_pins
        self._verify_approval = verify_approval
        self._attempted = set()
        allowed = {}
        units = {}
        try:
            for service in SERVICES:
                unit = containment.unit_name(plan["nonce"], service)
                units[service] = unit
                for side in SIDES:
                    layers = plan[side]["layers"][service]
                    if type(layers) is not list:
                        raise Stop("COMMANDGUARD_LAYER_INVALID")
                    argv = containment.systemd_run_argv(
                        unit, project, workdir, env_file, layers, service)
                    if type(argv) is not list or any(type(v) is not str for v in argv):
                        raise Stop("COMMANDGUARD_ARGV_INVALID")
                    command = tuple(argv)
                    if command in allowed:
                        raise Stop("COMMANDGUARD_DUPLICATE_COMMAND")
                    allowed[command] = (side, service, unit)
        except Stop:
            raise
        except Exception:
            raise Stop("COMMANDGUARD_PLAN_UNEXECUTABLE") from None
        if len(allowed) != 4:
            raise Stop("COMMANDGUARD_FOUR_COMMANDS_REQUIRED")
        self._allow = allowed
        self._units = units

    def command_for(self, side, service):
        """Return a fresh list for a single canonical case, not an authority."""
        if side not in SIDES or service not in SERVICES:
            raise Stop("COMMANDGUARD_SELECTION_INVALID")
        for command, case in self._allow.items():
            if case[0] == side and case[1] == service:
                return list(command)
        raise Stop("COMMANDGUARD_SELECTION_INVALID")

    def _require_held(self):
        try:
            if self._boundary.require_current_record(self._record) is not True:
                raise Stop("COMMANDGUARD_PLANE_LOST")
        except Exception:
            raise Stop("COMMANDGUARD_PLANE_LOST") from None

    def _scope_state(self, unit, expected):
        try:
            scope = self._scope(unit)
            if (getattr(scope, "unit", None) != unit
                    or scope.state() not in expected):
                raise Stop("COMMANDGUARD_CGROUP_UNSAFE")
        except Exception:
            raise Stop("COMMANDGUARD_CGROUP_UNSAFE") from None

    def _all_scopes_quiescent(self):
        """Both leased API/WEB units must be inactive, even during rollback.

        Checking only the unit about to run could launch WEB rollback while
        an unresolved API cgroup is still active. Unreadable is always STOP.
        """
        for service in SERVICES:
            self._scope_state(self._units[service], ("ABSENT", "EMPTY"))

    def __call__(self, argv):
        if type(argv) is not list or not all(type(x) is str for x in argv):
            raise Stop("COMMANDGUARD_ARGV_REFUSED")
        command = tuple(argv)
        if command not in self._allow:
            raise Stop("COMMANDGUARD_ARGV_REFUSED")
        if command in self._attempted:
            raise Stop("COMMANDGUARD_RETRY_REFUSED")
        side, service, unit = self._allow[command]
        stage = ("ROLLING_BACK" if side == "baseline"
                 else "API_SWITCHING" if service == "api" else "WEB_SWITCHING")
        # Burn this command's one-use slot even on failure. It might mutate
        # the Docker daemon after an ambiguous nonzero or timeout.
        self._attempted.add(command)
        self._require_held()
        try:
            if (self._verify_approval(stage) is not True
                    or self._verify_pins(list(command)) is not True
                    or self._intent(stage) is not True
                    or self._scope_recorded(unit) is not True):
                raise Stop("COMMANDGUARD_PRECONDITION_DENIED")
        except Exception:
            raise Stop("COMMANDGUARD_PRECONDITION_DENIED") from None
        self._require_held()
        self._all_scopes_quiescent()
        self._require_held()
        try:
            code = self._execute(list(command))
        except Exception:
            raise Stop("COMMANDGUARD_CHILD_UNCERTAIN") from None
        self._require_held()
        self._all_scopes_quiescent()
        self._require_held()
        if type(code) is not int:
            raise Stop("COMMANDGUARD_CHILD_UNCERTAIN")
        return code

    @property
    def status(self):
        return {"status": STATUS, "allowed_commands": len(self._allow),
                "attempts": len(self._attempted),
                "signature_verified_here": False, "sponsor_approved_here": False,
                "may_install": False, "may_apply": False}


if __name__ == "__main__":
    raise SystemExit("QA525_COMMAND_GUARD_LIBRARY_ONLY_NOT_RUNNABLE")
