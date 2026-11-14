#!/usr/bin/env python3
"""QA525 auto-web minimal-authority WEB port (#1010, ChatGPT audit 6878688237 P1-5). INACTIVE.

The auto-web lane must never be able to touch the API. `ServiceSwitcher` can recreate `api`,
`web` or both (`switch_all`), so the orchestrator must NOT receive a switcher. It receives a
`WebOnlyPort`, which exposes exactly two capabilities:

* ``apply_web()``    -> recreate WEB with the plan's TARGET layers   (at most once)
* ``rollback_web()`` -> recreate WEB with the plan's BASELINE layers (at most once, contract §1)

Guarantees, each tested as a mutant that is rejected BEFORE the real runner is reached:
1. The plan's API layers are identical in baseline and target (auto-web never changes API).
2. Every argv handed to the real runner is byte-identical to the expected WEB argv
   (`--unit=qa525-<h>-web.scope`, final service `web`, no extra verbs).
3. The port object has no attribute leading to a switcher or to the raw runner (__slots__,
   closures). Python closures stay introspectable, so guarantee 2 is the hard boundary:
   even a switcher reached by introspection cannot get an API argv past the guard.
4. After a rollback the port is spent: no second rollback and no apply after rollback.

This module starts no process. It does not authorize an apply: locks, lease, journal,
policy and envelope checks belong to the future `qa525-vnext-auto.py`.
"""
import importlib.util
from pathlib import Path

try:
    HERE = Path(__file__).resolve().parent
except NameError:  # PRE-4 Verified exec has no __file__: siblings are injected by bind_dependencies
    HERE = None
STATUS = "WEB_PORT_RESULT_NOT_AUTHORIZED_FOR_PHYSICAL_APPLY"


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


switch = _load("qa525_vnext_switch_for_autoweb_port", "qa525-vnext-switch.py") if HERE is not None else None
containment = (_load("qa525_vnext_containment_for_autoweb_port", "qa525-vnext-containment.py")
               if HERE is not None else None)

SIBLING_MODULES = {"switch": "qa525-vnext-switch.py", "containment": "qa525-vnext-containment.py"}
_BOUND = []


def bind_dependencies(mapping):
    """PRE-4 ONLY: receive the exact verified sibling modules (one instance per flight)."""
    if (HERE is not None or _BOUND or type(mapping) is not dict or set(mapping) != set(SIBLING_MODULES)
            or any(type(m).__name__ != "module" or type(m).__module__ != "builtins" for m in mapping.values())):
        raise Stop("DEPENDENCY_BINDING_INVALID")
    globals().update(mapping)
    _BOUND.append(True)


def _plan_layers(plan):
    try:
        base, target = plan["baseline"]["layers"], plan["target"]["layers"]
        out = {w: {s: list(layers[s]) for s in ("api", "web")} for w, layers in
               (("baseline", base), ("target", target))}
    except (KeyError, TypeError):
        raise Stop("WEB_PORT_PLAN_INVALID") from None
    if any(type(p) is not str for w in out.values() for s in w.values() for p in s):
        raise Stop("WEB_PORT_PLAN_INVALID")
    if out["baseline"]["api"] != out["target"]["api"]:
        raise Stop("WEB_PORT_API_LAYERS_CHANGE")
    return out


class WebOnlyPort:
    """Only `apply_web` / `rollback_web`; build with `WebOnlyPort.from_plan(...)`."""

    __slots__ = ("apply_web", "rollback_web", "__weakref__")

    def __init__(self):
        raise Stop("WEB_PORT_USE_FROM_PLAN")

    @classmethod
    def from_plan(cls, plan, *, nonce, project, workdir, env_file, runner, scope_factory,
                  record_unit, observe, settle, samples=3):
        if not callable(runner):
            raise Stop("WEB_PORT_ADAPTER_INVALID")
        layers = _plan_layers(plan)
        try:
            unit = containment.unit_name(nonce, "web")
            expected = {which: containment.systemd_run_argv(unit, project, workdir, env_file,
                                                            layers[which]["web"], "web")
                        for which in ("target", "baseline")}
        except containment.Stop:
            raise Stop("WEB_PORT_COMMAND_INVALID") from None
        state = {"applied": False, "rolled_back": False}

        def guarded(which):
            want = list(expected[which])

            def runner_web_only(argv):
                if type(argv) is not list or argv != want or argv[-1] != "web":
                    raise Stop("WEB_PORT_ARGV_NOT_WEB")
                return runner(list(want))
            return runner_web_only

        def make(which):
            kwargs = dict(nonce=nonce, project=project, workdir=workdir, env_file=env_file,
                          runner=guarded(which), scope_factory=scope_factory,
                          record_unit=record_unit, observe=observe, settle=settle, samples=samples)
            try:
                return switch.ServiceSwitcher.for_plan(plan, which, **kwargs)
            except switch.Stop:
                raise Stop("WEB_PORT_COMMAND_INVALID") from None

        forward, backward = make("target"), make("baseline")

        def run(sw):
            try:
                result = sw.switch("web")
            except switch.Stop as exc:
                raise Stop("WEB_PORT_" + str(exc)) from None
            if type(result) is not dict or result.get("service") != "web" or result.get("unit") != unit:
                raise Stop("WEB_PORT_RESULT_INVALID")
            return {"status": STATUS, "service": "web", "unit": unit,
                    "settled_sha256": result.get("settled_sha256"), "may_apply": False}

        def apply_web():
            if state["applied"] or state["rolled_back"]:
                raise Stop("WEB_PORT_APPLY_SPENT")
            state["applied"] = True  # spent before the attempt: a failed apply is never retried
            return run(forward)

        def rollback_web():
            if state["rolled_back"]:
                raise Stop("WEB_PORT_ROLLBACK_SPENT")
            state["rolled_back"] = True
            return run(backward)

        port = object.__new__(cls)
        object.__setattr__(port, "apply_web", apply_web)
        object.__setattr__(port, "rollback_web", rollback_web)
        return port


if __name__ == "__main__":
    raise SystemExit("QA525_AUTOWEB_PORT_LIBRARY_ONLY_NO_EXECUTION")
