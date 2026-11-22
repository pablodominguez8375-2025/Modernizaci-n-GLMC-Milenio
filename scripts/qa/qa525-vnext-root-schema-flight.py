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
    "hold": "qa525-vnext-schema-hold-stopped.py",
}
CORE = ("root_locks", "bridge", "boundary", "b7", "observer", "scopes",
        "switch", "flight", "g6_anchor", "run_held")
HOST = ("root_locks", "boundary", "scopes", "containment", "host_adapters",
        "state_reader", "health", "plan", "b7", "physical_runner")
SOURCE = re.compile(r"[0-9a-f]{40}\Z")
CID = re.compile(r"[0-9a-f]{64}\Z")
IMAGE = re.compile(r"sha256:[0-9a-f]{64}\Z")
_DEPS = {}


class Stop(Exception):
    """Fixed diagnostic only."""


def bind_dependencies(mods):
    if type(mods) is not dict or set(mods) != set(SIBLING_MODULES):
        raise Stop("SCHEMA_FLIGHT_BINDING_INVALID")
    _DEPS.clear()
    _DEPS.update(mods)


def precheck_schema_transition():
    """Verify the signed 7→6→7 profile and stop-only collaborators are bound before the GO nonce."""
    if (set(_DEPS) != set(SIBLING_MODULES)
            or not callable(getattr(_DEPS["state_reader"], "StateReader", None))
            or "SCHEMA_POSTQUIESCE" not in getattr(_DEPS["state_reader"], "PROFILES", ())
            or "SCHEMA_POSTQUIESCE" not in getattr(_DEPS["b7"], "PROFILES", ())
            or "SCHEMA_POSTQUIESCE" not in getattr(_DEPS["boundary"], "PROFILES", ())
            or not callable(getattr(_DEPS["hold"], "make_rollback_factory", None))
            or not callable(getattr(_DEPS["run_held"], "run_held", None))):
        raise Stop("SCHEMA_FLIGHT_TRANSITION_UNWIRED")
    return True


def preflight(*, archive_bytes, claims, authority, plane):
    """Read a NORMAL B7 baseline while API is still running, before quiesce."""
    modules = {key: _DEPS[key] for key in HOST}
    built = _DEPS["apiweb_host"].build(archive_bytes, dict(claims), dict(authority), modules,
                                        _DEPS["apiweb_effects"])
    report = _DEPS["b7"].inspect_inside_held_plane(
        plane=plane, inspector=built["inputs"]["inspector"],
        expected_baseline_sha256=authority["baseline_sha256"])
    return report["baseline_sha256"]


def make(*, archive_bytes, claims, authority, plane, api_container_id, old_api_image_id, schema_runner):
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
            or SOURCE.fullmatch(authority["target"]["web_sha"]) is None
            or type(api_container_id) is not str or CID.fullmatch(api_container_id) is None
            or type(old_api_image_id) is not str or IMAGE.fullmatch(old_api_image_id) is None
            or type(authority["target"].get("images")) is not dict
            or type(authority["target"]["images"].get("api")) is not str
            or IMAGE.fullmatch(authority["target"]["images"]["api"]) is None
            or old_api_image_id == authority["target"]["images"]["api"]
            or not callable(schema_runner)
            or not callable(getattr(_DEPS.get("hold"), "make_rollback_factory", None))):
        raise Stop("SCHEMA_FLIGHT_BINDING_INVALID")
    plane.assert_held()
    modules = {key: _DEPS[key] for key in CORE}
    host_modules = {key: _DEPS[key] for key in HOST}
    host, effects = _DEPS["apiweb_host"], _DEPS["apiweb_effects"]
    port = _DEPS["posix"].PosixPort()
    target = authority["target"]["web_sha"]
    target_api_image_id = authority["target"]["images"]["api"]

    def stop_api():
        plane.assert_held()
        labels = ("/usr/bin/docker", "ps", "-aq", "--no-trunc",
                  "--filter", "label=com.docker.compose.project=centenario-qa-lab2",
                  "--filter", "label=com.docker.compose.service=api")
        rc, out = schema_runner(labels, 60, None)
        if rc != 0 or type(out) is not bytes or len(out) > 256:
            raise Stop("SCHEMA_FLIGHT_API_STOP_UNVERIFIED")
        ids = out.decode("ascii").splitlines()
        if len(ids) > 2 or len(set(ids)) != len(ids) or any(CID.fullmatch(i) is None for i in ids):
            raise Stop("SCHEMA_FLIGHT_API_STOP_UNVERIFIED")
        for cid in ids:
            plane.assert_held()
            rc, image = schema_runner(("/usr/bin/docker", "inspect", "--format", "{{.Image}}", cid), 60, None)
            if rc != 0 or image not in ((old_api_image_id + "\n").encode(),
                                        (target_api_image_id + "\n").encode()):
                raise Stop("SCHEMA_FLIGHT_API_STOP_UNVERIFIED")
            rc, _ = schema_runner(("/usr/bin/docker", "stop", "--time", "30", cid), 60, None)
            if rc != 0:
                raise Stop("SCHEMA_FLIGHT_API_STOP_UNVERIFIED")
            rc, running = schema_runner(("/usr/bin/docker", "inspect", "--format", "{{.State.Running}}", cid), 60, None)
            if rc != 0 or running != b"false\n":
                raise Stop("SCHEMA_FLIGHT_API_STOP_UNVERIFIED")
        plane.assert_held()
        return True

    def flight():
        plane.assert_held()
        try:
            built = host.build(archive_bytes, dict(claims), dict(authority), host_modules, effects,
                               schema_profile=True, api_container_id=api_container_id)
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
                g6_evidence=dict(built["g6_evidence"]), b7_profile="SCHEMA_POSTQUIESCE",
                forbid_baseline_api=True,
                rollback_factory=_DEPS["hold"].make_rollback_factory(stop_api), **inputs)
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
