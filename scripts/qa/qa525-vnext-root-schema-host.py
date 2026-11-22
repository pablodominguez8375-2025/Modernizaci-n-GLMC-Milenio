#!/usr/bin/env python3
"""QA525 schema-cut HOST assembly. INACTIVE library; loaded only by the verified PRE-4 launcher, never CLI.

Interface fixed by the launcher/child (#1073):
  precheck(plan_bytes, claims, authority, schema_pins) -> True   BEFORE the nonce (shape + plan pins; else STOP, GO unspent)
  build_steps(archive_bytes, plan_bytes, claims, plane, authority, schema_pins) -> dict == executor.STEPS (callables)
`authority` is the closed apiweb section + admission/policy: only `g6` pins and `target.web_sha` are used. Everything
host-specific (Postgres container, network, env files, write roles, archive member names) is a FIXED CONSTANT of this
pinned module (glmdch only; never from the GO, the plan or the archive). Siblings are injected by the launcher through
SIBLING_MODULES/bind_dependencies; `effects` (qa525-vnext-root-schema-effects.py, NOT YET WRITTEN => launcher fails
closed) exposes `provide(authority, claims)` returning an object with runner, clock, abort_requested, read_evidence,
verifier, uat_checks (tuple; UAT credential only from a root 0600 source), archive_epoch, api_container_id.
Fixed codes only.
"""
import io
import re
import zipfile

SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")
HEX40 = re.compile(r"[0-9a-f]{40}\Z")
MEMBER = re.compile(r"[A-Za-z0-9][A-Za-z0-9_./-]{0,120}\Z")
SIBLING_MODULES = {"ports": "qa525-vnext-schema-cut-ports.py", "adapter": "qa525-vnext-schema-adapter.py",
                   "plan": "qa525-vnext-schema-cut-plan.py", "effects": "qa525-vnext-root-schema-effects.py",
                   "flight_adapter": "qa525-vnext-root-schema-flight.py",
                   "env_files": "qa525-vnext-schema-env-files.py",
                   "env_render": "qa525-vnext-schema-env-render.py"}
# glmdch constants (reviewed with this module's bytes; T0 read-only must confirm them before any GO).
FIXED = {"pg_container": "centenario-qa-lab2-postgres-1", "network": "centenario-qa-lab2_pmgm_srv01_internal",
         "migrator_env_file": "/run/qa525-schema-env/migrator.env",
         "grants_env_file": "/run/qa525-schema-env/grants.env",
         "write_roles": ("pmgm_runtime", "pmgm_app", "pmgm_keycloak"),
         "grant_script_member": "scripts/grant-pilot-db2.sh",
         "gate_sql_member": "scripts/qa/qa525-workshop-postmigration-gate.sql"}
_DEPS = {}


def bind_dependencies(mods):
    if type(mods) is not dict or set(mods) != set(SIBLING_MODULES):
        raise Stop("SCHEMA_HOST_INPUT_INVALID")
    _DEPS.clear()
    _DEPS.update(mods)
SCHEMA_PIN_KEYS = frozenset({"migrator_image_digest", "baseline_history_sha256", "grant_script_sha256",
                             "postgate_sha256", "api_image_id", "web_image_id"})
ALLOWED_ROLES = ("pmgm_runtime", "pmgm_app", "pmgm_keycloak")
G6_MAP = {"admission": "pinned_admission_sha256", "approval": "pinned_approval_sha256",
          "review": "pinned_review_sha256", "receipt": "pinned_receipt_sha256", "restore": "pinned_restore_sha256"}
EFFECTS = ("runner", "clock", "abort_requested", "read_evidence", "verifier")
MAX_MEMBER = 1 << 20
MAX_ARCHIVE_MEMBERS = 10000


class Stop(Exception):
    """Fixed code only."""


def _need(ok):
    if not ok:
        raise Stop("SCHEMA_HOST_INPUT_INVALID")


def _shape(authority, schema_pins):
    _need(type(authority) is dict and type(authority.get("g6")) is dict and type(authority.get("target")) is dict
          and type(schema_pins) is dict and set(schema_pins) == SCHEMA_PIN_KEYS
          and all(type(v) is str and SHA.fullmatch(v) for v in schema_pins.values())
          and type(authority["target"].get("web_sha")) is str and HEX40.fullmatch(authority["target"]["web_sha"])
          and type(authority.get("baseline_sha256")) is str and SHA.fullmatch(authority["baseline_sha256"])
          and all(type(authority["g6"].get(v)) is str and SHA.fullmatch(authority["g6"][v]) for v in G6_MAP.values())
          and set(_DEPS) == set(SIBLING_MODULES)
          and callable(getattr(_DEPS["effects"], "provide", None))
          and callable(getattr(_DEPS["flight_adapter"], "make", None))
          and callable(getattr(_DEPS["flight_adapter"], "precheck_schema_transition", None))
          and callable(getattr(_DEPS["flight_adapter"], "preflight", None))
          and callable(getattr(_DEPS["env_files"], "ephemeral_env_files", None))
          and callable(getattr(_DEPS["env_render"], "render", None)))


def _effects(authority, claims):
    try:
        effects = _DEPS["effects"].provide(dict(authority), dict(claims))
    except Exception:  # noqa: BLE001
        raise Stop("SCHEMA_HOST_EFFECTS_UNAVAILABLE") from None
    _need(effects is not None and all(callable(getattr(effects, n, None)) for n in EFFECTS)
          and type(getattr(effects, "uat_checks", None)) is tuple and 1 <= len(effects.uat_checks)
          and type(getattr(effects, "archive_epoch", None)) is int and not isinstance(effects.archive_epoch, bool)
          and type(getattr(effects, "api_container_id", None)) is str
          and callable(getattr(effects, "read_schema_source", None))
          and callable(getattr(effects, "verify_schema_tmpfs", None)))
    return effects


def _container(effects, pinned_id, api_image_id):
    """The container to stop is the one the AUTHENTICATED plan pins; discovery by label must agree and it must run
    the pinned API image. Any difference => STOP (never stop a container found only by name)."""
    _need(type(pinned_id) is str and effects.api_container_id == pinned_id)
    try:
        rc, out = effects.runner(("/usr/bin/docker", "inspect", "--format", "{{.Image}}", pinned_id), 60, None)
    except Exception:  # noqa: BLE001
        raise Stop("SCHEMA_HOST_CONTAINER_MISMATCH") from None
    if rc != 0 or out != (api_image_id + "\n").encode("ascii"):
        raise Stop("SCHEMA_HOST_CONTAINER_MISMATCH")


def precheck(*, plan_bytes, claims, authority, schema_pins):
    """Runs BEFORE the nonce: any doubt (missing UAT credential, effects, plan/pin drift) => STOP with the GO unspent."""
    _shape(authority, schema_pins)
    _need(type(plan_bytes) is bytes and type(claims) is dict)
    pins = dict(schema_pins, app_source_sha=claims.get("app_source_sha"),
                app_archive_sha256=claims.get("app_archive_sha256"), plan_sha256=claims.get("plan_sha256"))
    try:
        checked = _DEPS["plan"].check_plan(plan_bytes, pins)
    except Exception:  # noqa: BLE001
        raise Stop("SCHEMA_HOST_PLAN_INVALID") from None
    effects = _effects(authority, claims)
    _container(effects, checked.get("api_container_id"), schema_pins["api_image_id"])
    # Fail before nonce and before API quiesce if source or tmpfs is unsafe.
    import os
    if os.path.lexists("/run/qa525-schema-env"):
        raise Stop("SCHEMA_HOST_ENV_DIR_EXISTS")
    try:
        import json
        history = json.loads(plan_bytes.decode("ascii"))["history_ids"]
        if effects.verify_schema_tmpfs("/run") is not True:
            raise Stop("SCHEMA_HOST_ENV_NOT_TMPFS")
        rendered = _DEPS["env_render"].render(effects.read_schema_source(), history)
        if type(rendered) is not tuple or len(rendered) != 2 or any(type(x) is not bytes for x in rendered):
            raise Stop("SCHEMA_HOST_ENV_INVALID")
    except Stop:
        raise
    except Exception:
        raise Stop("SCHEMA_HOST_ENV_INVALID") from None
    try:
        _DEPS["flight_adapter"].precheck_schema_transition()
    except Exception:
        raise Stop("SCHEMA_HOST_FLIGHT_TRANSITION_UNWIRED") from None
    return True


def _member(archive_bytes, name):
    try:
        with zipfile.ZipFile(io.BytesIO(archive_bytes)) as z:
            infos = z.infolist()
            _need(len(infos) <= MAX_ARCHIVE_MEMBERS)
            hits = [i for i in infos if i.filename == name]
            _need(len(hits) == 1 and 0 < hits[0].file_size <= MAX_MEMBER and not hits[0].is_dir())
            data = z.read(hits[0])
    except Stop:
        raise
    except Exception:  # noqa: BLE001
        raise Stop("SCHEMA_HOST_ARCHIVE_INVALID") from None
    _need(len(data) <= MAX_MEMBER)
    return data


def build_steps(*, archive_bytes, plan_bytes, claims, plane, authority, schema_pins):
    _shape(authority, schema_pins)
    host = dict(FIXED, target_web_sha=authority["target"]["web_sha"])
    effects = _effects(authority, claims)
    _need(type(archive_bytes) is bytes and type(plan_bytes) is bytes and type(claims) is dict
          and callable(getattr(plane, "assert_held", None)))
    ports, adapter, plan_mod = _DEPS["ports"], _DEPS["adapter"], _DEPS["plan"]
    pins = dict(schema_pins, app_source_sha=claims.get("app_source_sha"),
                app_archive_sha256=claims.get("app_archive_sha256"), plan_sha256=claims.get("plan_sha256"))
    try:
        checked = plan_mod.check_plan(plan_bytes, pins)  # bytes == pin, canonical, history/pending/baseline bound
        _container(effects, checked["api_container_id"], schema_pins["api_image_id"])
        host["api_container_id"] = checked["api_container_id"]
        import json
        history = json.loads(plan_bytes.decode("ascii"))["history_ids"]
        expected_104 = "sha256:" + __import__("hashlib").sha256(
            "\n".join(adapter.expected_history_after(history)).encode("ascii")).hexdigest()
        params = {"network": host["network"], "pg_container": host["pg_container"],
                  "migrator_env_file": host["migrator_env_file"], "grants_env_file": host["grants_env_file"],
                  "migrator_image_id": schema_pins["migrator_image_digest"], "baseline_history_ids": history,
                  "grant_script": _member(archive_bytes, host["grant_script_member"]),
                  "gate_sql": _member(archive_bytes, host["gate_sql_member"]),
                  "grant_script_sha256": schema_pins["grant_script_sha256"],
                  "postgate_sha256": schema_pins["postgate_sha256"]}
        adapter.plan_commands(params)  # validate signed archive SQL pins BEFORE nonce, not after quiesce
        images = {"api": schema_pins["api_image_id"], "web": schema_pins["web_image_id"]}
        revisions = {"api": claims["app_source_sha"], "web": claims["app_source_sha"]}
        spec = {"pg_container": host["pg_container"], "expires_at": claims["expires_at"], "images": images,
                "revisions": revisions}
        g6 = authority["g6"]
        def b7_normal():
            return _DEPS["flight_adapter"].preflight(
                archive_bytes=archive_bytes, claims=claims, authority=authority, plane=plane)
        steps = {
            "preflight": ports.make_preflight(effects.runner, effects.clock, effects.abort_requested,
                                              dict(spec, migrator_image_id=schema_pins["migrator_image_digest"],
                                                   baseline_sha256=schema_pins["baseline_history_sha256"],
                                                   b7_baseline_sha256=authority["baseline_sha256"]),
                                              b7_inspect=b7_normal),
            "verify_backup": ports.make_verify_backup(effects.read_evidence, effects.verifier, effects.clock,
                                                      {k: g6[v] for k, v in G6_MAP.items()}, effects.archive_epoch),
            "quiesce_writes": ports.make_quiesce_writes(
                effects.runner, {"pg_container": host["pg_container"], "api_container_id": host["api_container_id"],
                                 "write_roles": tuple(host["write_roles"])}),
            "schema": None,  # wired below: env files live ONLY for the DDL/grants phase
            "revalidate": ports.make_revalidate(effects.runner, effects.clock, effects.abort_requested, spec,
                                                expected_104),
            "apply_api_web": ports.make_apply_api_web(_DEPS["flight_adapter"].make(
                archive_bytes=archive_bytes, claims=claims, authority=authority, plane=plane,
                api_container_id=checked["api_container_id"], old_api_image_id=schema_pins["api_image_id"],
                schema_runner=effects.runner), host["target_web_sha"]),
            "uat": ports.make_uat(effects.uat_checks),
        }
        def schema_with_ephemeral_env(journal):
            plane.assert_held()
            with _DEPS["env_files"].ephemeral_env_files(
                    base_dir="/run/qa525-schema-env", source_reader=effects.read_schema_source,
                    history=history, render=_DEPS["env_render"].render,
                    verify_tmpfs=effects.verify_schema_tmpfs, uid=0) as paths:
                if type(paths) is not tuple or len(paths) != 2:
                    raise Stop("SCHEMA_HOST_ENV_INVALID")
                ephemeral = dict(params, migrator_env_file=paths[0], grants_env_file=paths[1])
                result = ports.make_schema(adapter, ephemeral, effects.runner)(journal)
            plane.assert_held()
            return result
        steps["schema"] = schema_with_ephemeral_env
    except Stop:
        raise
    except Exception:  # noqa: BLE001  # includes ports/adapter/plan Stops: never echo their text
        raise Stop("SCHEMA_HOST_BUILD_FAILED") from None
    return steps


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_HOST_LIBRARY_ONLY_NO_GO")
