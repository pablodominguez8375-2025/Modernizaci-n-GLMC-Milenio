#!/usr/bin/env python3
"""QA525 schema-cut PORT builders. Pure library; NO signature, root, lock, nonce or host authority.

Builds the zero-argument callables that `qa525-vnext-schema-cut-executor.py` sequences. Every host effect is an
injected `runner(argv, timeout, stdin) -> (rc, stdout_bytes)` over CLOSED argv lists (no shell, no secret in argv),
an injected `clock`, `abort_requested`, `read_evidence` and, for the API/WEB flight and UAT, injected callables. A
port returns only what the sequencer accepts (`None`, `True`, `{"terminal": "APPLIED"}`) and raises on any doubt;
it never writes the cut journal, never retries and never reaches the host by itself.

Ports (all read-only except `quiesce_writes`, which only stops the API container by its exact baseline id):
  preflight      GO/plan not expired, abort absent, staged images + migrator image by exact id/label, DB EF history
                 == the plan's baseline hash
  verify_backup  five G6 v2 evidence files == pins, age <= 48 h, injected verifier accepts the set
  quiesce_writes `docker stop` of the exact API container id, API not running, zero write sessions on `pmgm`
  schema         phases 4-6 through the adapter, receiving the sequencer's journal
  revalidate     DB history == 104, staged images still present, GO not expired, abort absent
  apply_api_web  wraps the reviewed flight; APPLIED only with journal APPLIED + health + served sha == target
  uat            all injected checks must return True
"""
import hashlib
import re

DOCKER = "/usr/bin/docker"
SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")
HEX40 = re.compile(r"[0-9a-f]{40}\Z")
CID = re.compile(r"[0-9a-f]{12,64}\Z")
NAME = re.compile(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,62}\Z")
ROLE = re.compile(r"pmgm_[a-z_]{1,24}\Z")
REVISION_FORMAT = '{{index .Config.Labels "org.opencontainers.image.revision"}}'
EVIDENCE = ("admission", "approval", "review", "receipt", "restore")
MAX_BACKUP_AGE = 48 * 3600
TIMEOUT = 120
MAX_OUT = 1 << 20
HISTORY_SQL = 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId" COLLATE "C"'


class Stop(Exception):
    """Fixed code only; never output, argv, paths or secrets."""


def _digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


def _need(ok):
    if not ok:
        raise Stop("SCHEMA_PORT_INPUT_INVALID")


def _run(runner, argv, stdin=None):
    try:
        rc, out = runner(tuple(argv), TIMEOUT, stdin)
    except Exception:  # noqa: BLE001
        raise Stop("SCHEMA_PORT_EFFECT_FAILED") from None
    if type(rc) is not int or isinstance(rc, bool) or type(out) is not bytes or len(out) > MAX_OUT:
        raise Stop("SCHEMA_PORT_EFFECT_FAILED")
    return rc, out


def _psql(pg, sql):
    return (DOCKER, "exec", "-i", pg, "psql", "-U", "pmgm_migrator", "-d", "pmgm", "-X", "-A", "-t",
            "-v", "ON_ERROR_STOP=1", "-c", sql)


def _history_sha(runner, pg):
    rc, out = _run(runner, _psql(pg, HISTORY_SQL))
    if rc != 0 or not out.endswith(b"\n"):
        raise Stop("SCHEMA_PORT_HISTORY_UNREADABLE")
    try:
        lines = out.decode("ascii").split("\n")[:-1]
    except UnicodeError:
        raise Stop("SCHEMA_PORT_HISTORY_UNREADABLE") from None
    return len(lines), _digest("\n".join(lines).encode("ascii"))


def _images_present(runner, images, revisions):
    """Each staged image must inspect by its exact id and carry the expected revision label."""
    for service, image in images.items():
        rc, out = _run(runner, (DOCKER, "image", "inspect", "--format", REVISION_FORMAT, image))
        if rc != 0 or out != (revisions[service] + "\n").encode("ascii"):
            raise Stop("SCHEMA_PORT_IMAGE_MISSING")


def _gate(clock, abort, expires_at):
    try:
        if abort() is not False or not clock().timestamp() < expires_at:
            raise Stop("SCHEMA_PORT_GATE_CLOSED")
    except Stop:
        raise
    except Exception:  # noqa: BLE001
        raise Stop("SCHEMA_PORT_GATE_CLOSED") from None


def _common(spec):
    _need(type(spec) is dict and NAME.fullmatch(spec.get("pg_container", "")) is not None
          and type(spec.get("expires_at")) is int and not isinstance(spec["expires_at"], bool)
          and spec["expires_at"] > 0
          and type(spec.get("images")) is dict and set(spec["images"]) == {"api", "web"}
          and all(type(v) is str and SHA.fullmatch(v) for v in spec["images"].values())
          and type(spec.get("revisions")) is dict and set(spec["revisions"]) == {"api", "web"}
          and all(type(v) is str and HEX40.fullmatch(v) for v in spec["revisions"].values()))


def make_preflight(runner, clock, abort, spec):
    """spec: pg_container, expires_at, images{api,web}, revisions{api,web}, migrator_image_id, baseline_sha256."""
    _common(spec)
    _need(SHA.fullmatch(spec.get("migrator_image_id", "")) and SHA.fullmatch(spec.get("baseline_sha256", "")))

    def preflight():
        _gate(clock, abort, spec["expires_at"])
        rc, out = _run(runner, (DOCKER, "image", "inspect", "--format", "{{.Id}}", spec["migrator_image_id"]))
        if rc != 0 or out != (spec["migrator_image_id"] + "\n").encode("ascii"):
            raise Stop("SCHEMA_PORT_MIGRATOR_IMAGE_MISSING")
        _images_present(runner, spec["images"], spec["revisions"])
        count, sha = _history_sha(runner, spec["pg_container"])
        if count != 100 or sha != spec["baseline_sha256"]:
            raise Stop("SCHEMA_PORT_BASELINE_MISMATCH")
    return preflight


def make_verify_backup(read_evidence, verifier, clock, pins, archive_epoch):
    """pins: {admission,approval,review,receipt,restore} -> sha256; archive_epoch: int (from the signed receipt)."""
    _need(type(pins) is dict and set(pins) == set(EVIDENCE) and all(SHA.fullmatch(v) for v in pins.values())
          and type(archive_epoch) is int and not isinstance(archive_epoch, bool) and archive_epoch > 0
          and callable(read_evidence) and callable(verifier))

    def verify_backup():
        data = {}
        for name in EVIDENCE:
            raw = read_evidence(name)
            if type(raw) is not bytes or _digest(raw) != pins[name]:
                raise Stop("SCHEMA_PORT_EVIDENCE_PIN_MISMATCH")
            data[name] = raw
        age = clock().timestamp() - archive_epoch
        if not 0 <= age <= MAX_BACKUP_AGE:
            raise Stop("SCHEMA_PORT_BACKUP_STALE")
        if verifier(data) is not True:
            raise Stop("SCHEMA_PORT_BACKUP_REJECTED")
        return True
    return verify_backup


def make_quiesce_writes(runner, spec):
    """spec: pg_container, api_container_id (exact baseline id), write_roles (tuple of pmgm_* roles)."""
    _need(type(spec) is dict and NAME.fullmatch(spec.get("pg_container", "")) is not None
          and CID.fullmatch(spec.get("api_container_id", "")) is not None
          and type(spec.get("write_roles")) is tuple and 1 <= len(spec["write_roles"]) <= 6
          and all(type(r) is str and ROLE.fullmatch(r) for r in spec["write_roles"])
          and len(set(spec["write_roles"])) == len(spec["write_roles"]))
    roles = ",".join("'" + r + "'" for r in spec["write_roles"])
    sessions = ("SELECT count(*) FROM pg_stat_activity WHERE datname = 'pmgm' AND usename IN (" + roles
                + ") AND pid <> pg_backend_pid()")

    def quiesce_writes():
        rc, _ = _run(runner, (DOCKER, "stop", "--time", "30", spec["api_container_id"]))
        if rc != 0:
            raise Stop("SCHEMA_PORT_API_NOT_STOPPED")
        rc, out = _run(runner, (DOCKER, "inspect", "--format", "{{.State.Running}}", spec["api_container_id"]))
        if rc != 0 or out != b"false\n":
            raise Stop("SCHEMA_PORT_API_STILL_RUNNING")
        rc, out = _run(runner, _psql(spec["pg_container"], sessions))
        if rc != 0 or out != b"0\n":
            raise Stop("SCHEMA_PORT_WRITE_SESSIONS_OPEN")
        return True
    return quiesce_writes


def make_schema(adapter, params, runner):
    """Binds adapter #1074 `run_phases`; the sequencer passes its own journal wrapper."""
    _need(callable(getattr(adapter, "run_phases", None)) and callable(runner) and type(params) is dict)
    adapter.plan_commands(params)  # fail closed on a malformed plan before the sequencer starts

    def schema(journal):
        return adapter.run_phases(journal, runner, params)
    return schema


def make_revalidate(runner, clock, abort, spec, expected_104_sha256):
    _common(spec)
    _need(SHA.fullmatch(expected_104_sha256 or ""))

    def revalidate():
        _gate(clock, abort, spec["expires_at"])
        count, sha = _history_sha(runner, spec["pg_container"])
        if count != 104 or sha != expected_104_sha256:
            raise Stop("SCHEMA_PORT_HISTORY_NOT_104")
        _images_present(runner, spec["images"], spec["revisions"])
        return True
    return revalidate


def make_apply_api_web(flight, target_web_sha):
    """`flight()` runs the reviewed held flight and returns {"journal": str, "health": bool, "served_sha": str}."""
    _need(callable(flight) and HEX40.fullmatch(target_web_sha or "") is not None)

    def apply_api_web():
        result = flight()
        if (type(result) is dict and set(result) == {"journal", "health", "served_sha"}
                and result["journal"] == "APPLIED" and result["health"] is True
                and result["served_sha"] == target_web_sha):
            return {"terminal": "APPLIED"}
        return {"terminal": "FAILED"}
    return apply_api_web


def make_uat(checks):
    """checks: ordered tuple of callables returning True; stops at the first non-True (no retries)."""
    _need(type(checks) is tuple and 1 <= len(checks) <= 32 and all(callable(c) for c in checks))

    def uat():
        for check in checks:
            if check() is not True:
                return False
        return True
    return uat


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_PORTS_LIBRARY_ONLY_NO_GO")
