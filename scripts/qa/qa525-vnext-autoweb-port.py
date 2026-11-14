#!/usr/bin/env python3
"""QA525 auto-web minimal-authority WEB port (#1010, ChatGPT audit 6878688237 P1-5). INACTIVE.

The auto-web lane must never be able to touch the API. `ServiceSwitcher` can recreate `api`,
`web` or both (`switch_all`), so the orchestrator must NOT receive a switcher. It receives a
`WebOnlyPort`, which exposes exactly two capabilities:

* ``apply_web()``    -> recreate WEB with the plan's TARGET layers   (at most once)
* ``rollback_web()`` -> recreate WEB with the plan's BASELINE layers (at most once, only after
                        apply_web was attempted; contract §1)

Build it with the module factory ``web_only_port(plan, ...)``; the returned object's public
surface is exactly those two callables (review 5904937432 P1-3).

THREAT MODEL (independent review, 14-11): this port protects against ACCIDENTAL misuse and
composition errors inside the trusted root orchestrator. It is NOT a boundary against hostile
code running in the same Python process: such code can always reach the raw runner through
closures (`port.apply_web.__closure__` -> switcher -> `_runner.__closure__`). The hard boundary
against hostile or confused callers is the REAL runner adapter: build it as
``held_runner(capability, run, argv_guard=web_argv_guard(...))`` (#1021 root plane) so the WEB
argv allowlist is enforced where the process is actually spawned, under the 5 locks + lease.

Guarantees within that model, each tested as a mutant rejected BEFORE the real runner:
1. The plan's API layers are identical in baseline and target (auto-web never changes API).
2. Every argv handed to the real runner is byte-identical to one of the two expected WEB argv
   (`--unit=qa525-<h>-web.scope`, final service `web`); `web_argv_guard` exports the same
   allowlist for the real adapter.
3. The port object has no attribute leading to a switcher or to the raw runner (__slots__).
4. Single use: apply and rollback each grant one internal permit, consumed by the guard before
   the real runner; a switcher reached through ONE closure level has no permit, and a second
   `switch("web")` is rejected (review 5904937432 P1-1, within the threat model above).
5. Rollback is refused unless apply was attempted; after a rollback the port is spent.
6. `record_unit` and `scope_factory` only ever see the WEB unit name: an API unit is never
   recorded durably, even if a switcher is misused (independent review P2).

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


def web_argv_guard(plan, *, nonce, project, workdir, env_file):
    """Allowlist for the REAL runner adapter: accepts only the exact target/baseline WEB argv.

    Returns a callable `guard(argv) -> list` suitable for `held_runner(..., argv_guard=...)`.
    It does not do single use (the port does); it never accepts an API or compose-wide argv."""
    layers = _plan_layers(plan)
    try:
        unit = containment.unit_name(nonce, "web")
        allowed = [list(containment.systemd_run_argv(unit, project, workdir, env_file,
                                                     layers[which]["web"], "web"))
                   for which in ("target", "baseline")]
    except containment.Stop:
        raise Stop("WEB_PORT_COMMAND_INVALID") from None

    def guard(argv):
        if type(argv) is not list or argv not in allowed or argv[-1] != "web":
            raise Stop("WEB_PORT_ARGV_NOT_WEB")
        return list(argv)
    return guard


class WebOnlyPort:
    """Only `apply_web` / `rollback_web`; built by the module factory `web_only_port(...)`."""

    __slots__ = ("apply_web", "rollback_web")

    def __init__(self):
        raise Stop("WEB_PORT_USE_FACTORY")


def web_only_port(plan, *, nonce, project, workdir, env_file, runner, scope_factory,
                  record_unit, observe, settle, samples=3):
    cls = WebOnlyPort
    if not all(callable(x) for x in (runner, scope_factory, record_unit)):
        raise Stop("WEB_PORT_ADAPTER_INVALID")
    layers = _plan_layers(plan)
    try:
        unit = containment.unit_name(nonce, "web")
        expected = {which: containment.systemd_run_argv(unit, project, workdir, env_file,
                                                        layers[which]["web"], "web")
                    for which in ("target", "baseline")}
    except containment.Stop:
        raise Stop("WEB_PORT_COMMAND_INVALID") from None
    def record_web_unit(name):
        if name != unit:
            raise Stop("WEB_PORT_UNIT_NOT_WEB")
        return record_unit(name)

    def web_scope(name):
        if name != unit:
            raise Stop("WEB_PORT_UNIT_NOT_WEB")
        return scope_factory(name)

    state = {"applied": False, "rolled_back": False}
    permits = {"target": False, "baseline": False}

    def guarded(which):
        want = list(expected[which])

        def runner_web_only(argv):
            if type(argv) is not list or argv != want or argv[-1] != "web":
                raise Stop("WEB_PORT_ARGV_NOT_WEB")
            if permits[which] is not True:
                raise Stop("WEB_PORT_NO_PERMIT")
            permits[which] = False  # consumed before the real runner: never twice
            return runner(list(want))
        return runner_web_only

    def make(which):
        kwargs = dict(nonce=nonce, project=project, workdir=workdir, env_file=env_file,
                      runner=guarded(which), scope_factory=web_scope,
                      record_unit=record_web_unit, observe=observe, settle=settle, samples=samples)
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
        permits["target"] = True
        try:
            return run(forward)
        finally:
            permits["target"] = False

    def rollback_web():
        if not state["applied"]:
            raise Stop("WEB_PORT_ROLLBACK_BEFORE_APPLY")
        if state["rolled_back"]:
            raise Stop("WEB_PORT_ROLLBACK_SPENT")
        state["rolled_back"] = True
        permits["baseline"] = True
        try:
            return run(backward)
        finally:
            permits["baseline"] = False

    port = object.__new__(cls)
    object.__setattr__(port, "apply_web", apply_web)
    object.__setattr__(port, "rollback_web", rollback_web)
    return port


if __name__ == "__main__":
    raise SystemExit("QA525_AUTOWEB_PORT_LIBRARY_ONLY_NO_EXECUTION")
