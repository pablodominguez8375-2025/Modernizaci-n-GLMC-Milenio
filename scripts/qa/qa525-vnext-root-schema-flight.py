#!/usr/bin/env python3
"""QA525 schema cut API/WEB flight adapter. PRE-4 verified sibling library, never CLI.

The signed schema GO has already spent its nonce and holds the outer RootPlane.
This adapter reuses the reviewed API/WEB host and held-flight bridge with the
same lock class and module instances. It never manufactures an API_WEB_QA_ONLY
GO. The held flight's own journal must report APPLIED, and the public health
and served SHA are read again before the schema executor may record APP_APPLIED.
"""
import re

SIBLING_MODULES = {
    "root_locks": "qa525-vnext-root-locks.py",
    "apiweb_child": "qa525-vnext-root-apiweb-child.py",
    "apiweb_host": "qa525-vnext-root-apiweb-host.py",
    "apiweb_effects": "qa525-vnext-root-apiweb-effects.py",
    "posix": "qa525-vnext-posix-port.py",
    "bridge": "qa525-vnext-admission-bridge.py",
    "boundary": "qa525-vnext-b7-root-boundary.py",
    "b7": "qa525-vnext-b7-held-readonly.py",
    "observer": "qa525-vnext-observer.py",
    "scopes": "qa525-vnext-scope-store.py",
    "switch": "qa525-vnext-switch.py",
    "flight": "qa525-vnext-flight.py",
    "g6_anchor": "qa525-vnext-g6-anchor.py",
    "run_held": "qa525-vnext-held-flight-bridge.py",
    "containment": "qa525-vnext-containment.py",
    "host_adapters": "qa525-vnext-host-adapters.py",
    "state_reader": "qa525-vnext-state-reader.py",
    "health": "qa525-vnext-health.py",
    "plan": "qa525-vnext-baseline-plan.py",
    "physical_runner": "qa525-vnext-held-physical-runner.py",
}
CORE = ("root_locks", "bridge", "boundary", "b7", "observer", "scopes",
        "switch", "flight", "g6_anchor", "run_held")
HOST = ("root_locks", "boundary", "scopes", "containment", "host_adapters",
        "state_reader", "health", "plan", "b7", "physical_runner")
SOURCE = re.compile(r"[0-9a-f]{40}\Z")
_DEPS = {}


class Stop(Exception):
    """Fixed diagnostic only."""


def bind_dependencies(mods):
    if type(mods) is not dict or set(mods) != set(SIBLING_MODULES):
        raise Stop("SCHEMA_FLIGHT_BINDING_INVALID")
    _DEPS.clear()
    _DEPS.update(mods)


def make(*, archive_bytes, claims, authority, plane):
    if (set(_DEPS) != set(SIBLING_MODULES)
            or not isinstance(getattr(_DEPS["root_locks"], "RootPlane", None), type)):
        raise Stop("SCHEMA_FLIGHT_BINDING_INVALID")
    if (type(archive_bytes) is not bytes or type(claims) is not dict
            or claims.get("purpose") != "SCHEMA_API_WEB_QA_ONLY"
            or type(authority) is not dict or type(plane) is not _DEPS["root_locks"].RootPlane
            or not callable(getattr(plane, "assert_held", None))
            or not callable(getattr(_DEPS.get("apiweb_host"), "build", None))
            or not callable(getattr(_DEPS.get("run_held"), "run_held", None))
            or not callable(getattr(_DEPS.get("health"), "build", None))
            or not callable(getattr(_DEPS.get("apiweb_child"), "Reentrant", None))
            or not callable(getattr(_DEPS.get("posix"), "PosixPort", None))
            or type(authority.get("target")) is not dict
            or type(authority["target"].get("web_sha")) is not str
            or SOURCE.fullmatch(authority["target"]["web_sha"]) is None):
        raise Stop("SCHEMA_FLIGHT_BINDING_INVALID")
    plane.assert_held()
    modules = {key: _DEPS[key] for key in CORE}
    host_modules = {key: _DEPS[key] for key in HOST}
    host, effects = _DEPS["apiweb_host"], _DEPS["apiweb_effects"]
    port = _DEPS["posix"].PosixPort()
    target = authority["target"]["web_sha"]

    def flight():
        plane.assert_held()
        try:
            built = host.build(archive_bytes, dict(claims), dict(authority), host_modules, effects)
            if type(built) is not dict or set(built) != {"g6_evidence", "inputs"}:
                raise Stop("SCHEMA_FLIGHT_HOST_INVALID")
            inputs = dict(built["inputs"])
            if "port" in inputs:
                raise Stop("SCHEMA_FLIGHT_HOST_INVALID")
            inputs["port"] = port
            result = modules["run_held"].run_held(
                root_locks=_DEPS["apiweb_child"].Reentrant(_DEPS["root_locks"], lambda: plane),
                bridge=modules["bridge"], boundary=modules["boundary"], b7=modules["b7"],
                observer=modules["observer"], scopes=modules["scopes"], switch=modules["switch"],
                flight=modules["flight"], g6_anchor=modules["g6_anchor"],
                g6_evidence=dict(built["g6_evidence"]), **inputs)
            plane.assert_held()
            outcome = result.get("terminal_outcome") if type(result) is dict else None
            if outcome != "APPLIED":
                return {"journal": "FAILED", "health": False, "served_sha": ""}
            check, served = _DEPS["health"].build(effects.https_fetch())
            healthy, revision = check(), served()
            plane.assert_held()
            return {"journal": "APPLIED", "health": healthy is True,
                    "served_sha": revision if type(revision) is str else ""}
        except Stop:
            raise
        except Exception:
            raise Stop("SCHEMA_FLIGHT_STOP") from None
    return flight


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_FLIGHT_LIBRARY_ONLY_NO_ROOT_NO_GO")
