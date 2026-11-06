#!/usr/bin/env python3
"""QA525 vNext service switcher -- INACTIVE library, NO process execution here.

Turns the fixed containment command shape into the ``switch_api`` /
``switch_web`` / ``rollback`` callables the flight orchestrator needs, with
EVERY side effect injected:

* ``runner(argv) -> int`` is the only thing that could ever start a process.
  This module never imports subprocess/os/socket and never builds a shell
  string; ``argv`` is the list produced by ``containment.systemd_run_argv``.
* ``scope_factory(unit)`` returns a read-only scope view (e.g. CgroupScope).
* ``record_unit(unit)`` must durably record the unit name BEFORE the runner is
  called (containment rule); a failure there means nothing was started.
* ``observe()`` / ``settle()`` feed the Docker-daemon convergence gate.

Order for one service: record unit -> scope must be ABSENT/EMPTY -> runner ->
exit code 0 -> scope quiescent -> runtime settled. Any deviation is a fixed
Stop code; exception text is never echoed and nothing is retried. A caller
that gets a Stop must NOT assume the command did not run (the flight engine
treats a persisted intent as "possibly executed").
Nothing here authorizes installation; the gate, locks, G6 and Sponsor approval
belong to the future engine, and real adapters are wired only by it.
"""
import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent
STATUS = "SWITCH_RESULT_NOT_AUTHENTICATED_NOT_AUTHORIZED_FOR_PHYSICAL_APPLY"


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def _load(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


containment = _load("qa525_vnext_containment_for_switch", "qa525-vnext-containment.py")


class ServiceSwitcher:
    def __init__(self, *, nonce, project, workdir, env_file, files, runner,
                 scope_factory, record_unit, observe, settle, samples=3):
        for item in (runner, scope_factory, record_unit, observe, settle):
            if not callable(item):
                raise Stop("SWITCH_ADAPTER_INVALID")
        self._cfg = (nonce, project, workdir, env_file, list(files))
        self._runner, self._scope_factory = runner, scope_factory
        self._record_unit, self._observe, self._settle = record_unit, observe, settle
        self._samples = samples
        # Fail early (and without running anything) on any malformed input.
        for service in ("api", "web"):
            self._argv(service)

    def _argv(self, service):
        nonce, project, workdir, env_file, files = self._cfg
        try:
            unit = containment.unit_name(nonce, service)
            return unit, containment.systemd_run_argv(
                unit, project, workdir, env_file, files, service)
        except containment.Stop:
            raise Stop("SWITCH_COMMAND_INVALID") from None

    def _scope(self, unit):
        try:
            scope = self._scope_factory(unit)
            if getattr(scope, "unit", None) != unit or not hasattr(scope, "state"):
                raise Stop("SWITCH_SCOPE_INVALID")
            return scope
        except Stop:
            raise
        except Exception:
            raise Stop("SWITCH_SCOPE_INVALID") from None

    def _quiet(self, scope, unit):
        try:
            containment.require_quiescent([scope], expected_units=[unit])
        except containment.Stop as exc:
            raise Stop("SWITCH_" + str(exc)) from None

    def switch(self, service) -> dict:
        """Recreate ONE service (api|web) with this switcher's compose files."""
        if service not in ("api", "web"):
            raise Stop("SWITCH_SERVICE_INVALID")
        unit, argv = self._argv(service)
        scope = self._scope(unit)
        try:
            self._record_unit(unit)
        except Exception:
            raise Stop("SWITCH_RECORD_FAILED") from None
        self._quiet(scope, unit)  # nothing from an earlier run may still live
        try:
            code = self._runner(list(argv))
        except Exception:
            raise Stop("SWITCH_RUNNER_FAILED") from None
        if type(code) is not int or code != 0:
            raise Stop("SWITCH_COMMAND_FAILED")
        self._quiet(scope, unit)  # the child must be gone before we go on
        try:
            digest = containment.require_settled(self._observe, self._settle, self._samples)
        except containment.Stop as exc:
            raise Stop("SWITCH_" + str(exc)) from None
        return {"status": STATUS, "service": service, "unit": unit,
                "settled_sha256": digest, "may_apply": False,
                "signature_verified_here": False,
                "physical_runtime_verified": False}

    def switch_api(self):
        self.switch("api")

    def switch_web(self):
        self.switch("web")

    def switch_all(self, order=("web", "api")):
        """Rollback shape: recreate both services, web first by default."""
        if tuple(sorted(order)) != ("api", "web"):
            raise Stop("SWITCH_ORDER_INVALID")
        for service in order:
            self.switch(service)


if __name__ == "__main__":
    raise SystemExit("QA525_SWITCH_LIBRARY_ONLY_NO_EXECUTION")
