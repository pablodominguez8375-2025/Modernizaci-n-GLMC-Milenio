#!/usr/bin/env python3
"""QA525 schema-cut HOST assembly. INACTIVE library; loaded only by the verified PRE-4 launcher, never CLI.

`build_steps(archive_bytes, plan_bytes, claims, plane, authority, schema_pins, modules, effects)` returns the exact
seven zero-argument steps (`executor.STEPS`) built from the reviewed PORTS and the adapter. NOTHING authoritative comes
from the archive except the two SQL/shell files, whose digests are pinned (`schema_pins`) and re-checked by the adapter.

authority["schema_host"] (closed, Sponsor-pinned launch configuration; extra requirement for ChatGPT's config):
  pg_container, network, migrator_env_file, grants_env_file, api_container_id (hex), write_roles (list of pmgm_*),
  target_web_sha (40 hex), grant_script_member, gate_sql_member (archive member names)
authority["g6"]: the five `pinned_*_sha256` evidence pins. modules: {"ports","adapter","plan"} (the pinned, loaded
modules). effects: runner, clock, abort_requested, read_evidence, verifier, flight, uat_checks (tuple), archive_epoch (int);
the UAT credential, if any, is inside `uat_checks` (injected from a root 0600 source); missing => STOP before the nonce.
No value from the GO, the plan or the archive can name a command, path or role. Fixed codes only.
"""
import io
import re
import zipfile

SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")
HEX40 = re.compile(r"[0-9a-f]{40}\Z")
MEMBER = re.compile(r"[A-Za-z0-9][A-Za-z0-9_./-]{0,120}\Z")
HOST_KEYS = frozenset({"pg_container", "network", "migrator_env_file", "grants_env_file", "api_container_id",
                       "write_roles", "target_web_sha", "grant_script_member", "gate_sql_member"})
SCHEMA_PIN_KEYS = frozenset({"migrator_image_digest", "baseline_history_sha256", "grant_script_sha256",
                             "postgate_sha256", "api_image_id", "web_image_id"})
ALLOWED_ROLES = ("pmgm_runtime", "pmgm_app", "pmgm_keycloak")
G6_MAP = {"admission": "pinned_admission_sha256", "approval": "pinned_approval_sha256",
          "review": "pinned_review_sha256", "receipt": "pinned_receipt_sha256", "restore": "pinned_restore_sha256"}
EFFECTS = ("runner", "clock", "abort_requested", "read_evidence", "verifier", "flight")
MAX_MEMBER = 1 << 20
MAX_ARCHIVE_MEMBERS = 10000


class Stop(Exception):
    """Fixed code only."""


def _need(ok):
    if not ok:
        raise Stop("SCHEMA_HOST_INPUT_INVALID")


def _shape(authority, schema_pins, modules, effects):
    _need(type(authority) is dict and type(authority.get("g6")) is dict and type(authority.get("schema_host")) is dict
          and type(schema_pins) is dict and set(schema_pins) == SCHEMA_PIN_KEYS
          and all(type(v) is str and SHA.fullmatch(v) for v in schema_pins.values())
          and type(modules) is dict and set(modules) == {"ports", "adapter", "plan"})
    host = authority["schema_host"]
    _need(set(host) == HOST_KEYS and type(host["write_roles"]) is list
          and tuple(sorted(host["write_roles"])) == tuple(sorted(ALLOWED_ROLES))  # exactly the Sponsor-listed roles
          and type(host["target_web_sha"]) is str and HEX40.fullmatch(host["target_web_sha"])
          and all(type(host[k]) is str and MEMBER.fullmatch(host[k]) for k in ("grant_script_member", "gate_sql_member"))
          and all(type(authority["g6"].get(v)) is str and SHA.fullmatch(authority["g6"][v]) for v in G6_MAP.values()))
    _need(effects is not None and all(callable(getattr(effects, n, None)) for n in EFFECTS)
          and type(getattr(effects, "uat_checks", None)) is tuple and 1 <= len(effects.uat_checks)
          and type(getattr(effects, "archive_epoch", None)) is int and not isinstance(effects.archive_epoch, bool))
    return host


def precheck(authority, schema_pins, modules, effects):
    """Shape-only check the launcher can run BEFORE the nonce: missing credential/port => STOP with the GO unspent."""
    _shape(authority, schema_pins, modules, effects)
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


def build_steps(*, archive_bytes, plan_bytes, claims, plane, authority, schema_pins, modules, effects):
    host = _shape(authority, schema_pins, modules, effects)
    _need(type(archive_bytes) is bytes and type(plan_bytes) is bytes and type(claims) is dict
          and callable(getattr(plane, "assert_held", None)))
    ports, adapter, plan_mod = modules["ports"], modules["adapter"], modules["plan"]
    pins = dict(schema_pins, app_source_sha=claims.get("app_source_sha"),
                app_archive_sha256=claims.get("app_archive_sha256"), plan_sha256=claims.get("plan_sha256"))
    try:
        plan_mod.check_plan(plan_bytes, pins)  # bytes == pin, canonical, history/pending/baseline bound to the GO pins
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
        images = {"api": schema_pins["api_image_id"], "web": schema_pins["web_image_id"]}
        revisions = {"api": claims["app_source_sha"], "web": claims["app_source_sha"]}
        spec = {"pg_container": host["pg_container"], "expires_at": claims["expires_at"], "images": images,
                "revisions": revisions}
        g6 = authority["g6"]
        steps = {
            "preflight": ports.make_preflight(effects.runner, effects.clock, effects.abort_requested,
                                              dict(spec, migrator_image_id=schema_pins["migrator_image_digest"],
                                                   baseline_sha256=schema_pins["baseline_history_sha256"])),
            "verify_backup": ports.make_verify_backup(effects.read_evidence, effects.verifier, effects.clock,
                                                      {k: g6[v] for k, v in G6_MAP.items()}, effects.archive_epoch),
            "quiesce_writes": ports.make_quiesce_writes(
                effects.runner, {"pg_container": host["pg_container"], "api_container_id": host["api_container_id"],
                                 "write_roles": tuple(host["write_roles"])}),
            "schema": ports.make_schema(adapter, params, effects.runner),
            "revalidate": ports.make_revalidate(effects.runner, effects.clock, effects.abort_requested, spec,
                                                expected_104),
            "apply_api_web": ports.make_apply_api_web(effects.flight, host["target_web_sha"]),
            "uat": ports.make_uat(effects.uat_checks),
        }
    except Stop:
        raise
    except Exception:  # noqa: BLE001  # includes ports/adapter/plan Stops: never echo their text
        raise Stop("SCHEMA_HOST_BUILD_FAILED") from None
    return steps


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_HOST_LIBRARY_ONLY_NO_GO")
