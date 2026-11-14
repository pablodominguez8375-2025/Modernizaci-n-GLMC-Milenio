#!/usr/bin/env python3
"""QA525 API_WEB host assembly. INACTIVE library; verified PRE-4 loading only, never CLI.

`build(archive_bytes, claims, authority, modules, effects)` turns Sponsor-pinned authority (the closed `apiweb`
section of the bootstrap-authenticated launch configuration) into the exact inputs of `held-flight-bridge.run_held`.
NOTHING authoritative comes from the ZIP: target images/layers/web sha, G6 pins, exec pins, paths and the baseline
pin all come from `authority`; evidence files are read from a root-owned directory and must match their pins.
The only effects are the injected `effects` object (docker reads, the 4-command executor, pin hashing, HTTPS GET).
No migrator, database, identity or volume operation is expressible here.
"""
import datetime as dt
import hashlib
import json
import os
import re
import stat
from pathlib import Path

SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")
HEX64 = re.compile(r"[0-9a-f]{64}\Z")
HEX32 = re.compile(r"[0-9a-f]{32}\Z")
SOURCE = re.compile(r"[0-9a-f]{40}\Z")
G6_PIN_KEYS = ("pinned_admission_sha256", "pinned_approval_sha256", "pinned_review_sha256",
               "pinned_receipt_sha256", "pinned_restore_sha256", "observed_archive_sha256",
               "downloaded_offhost_sha256")
EXEC_KEYS = ("systemd_run_sha256", "docker_sha256", "python_sha256", "child_sha256", "compose_stack_sha256")
EVIDENCE = {"admission": "admission.json", "approval": "approval.json", "review": "g6-review.json",
            "receipt": "g6-receipt.json", "restore": "g6-restore.json"}
AUTHORITY_KEYS = frozenset({"g6", "exec_pins", "exec_paths", "target", "baseline_sha256", "host",
                            "admission_sha256", "journal_root", "policy_hash"})
HOST_KEYS = frozenset({"project", "workdir", "env_file", "evidence_dir"})
MAX_EVIDENCE = 1 << 20
ROOT_UID = 0  # tests may rebind; production semantics are root-owned evidence only
MODULE_KEYS = frozenset({"root_locks", "boundary", "scopes", "containment", "host_adapters",
                         "state_reader", "health", "plan", "b7", "physical_runner"})
REVISION_FORMAT = '{{index .Config.Labels "org.opencontainers.image.revision"}}'
EFFECT_METHODS = ("docker_reader", "executor", "pin_verifier", "https_fetch")


class Stop(Exception):
    """Fixed code only."""


def _abs(value):
    return type(value) is str and value.startswith("/") and "\0" not in value and ".." not in value.split("/")


def _shape(authority):
    a = authority
    g6, target, host = (a.get("g6"), a.get("target"), a.get("host")) if type(a) is dict else (None,) * 3
    try:
        ok = (set(a) == AUTHORITY_KEYS and type(g6) is dict
              and set(g6) == set(G6_PIN_KEYS) | {"expected_restic_repo_id", "expected_key_epoch"}
              and all(type(g6[k]) is str and SHA.fullmatch(g6[k]) for k in G6_PIN_KEYS)
              and HEX64.fullmatch(g6["expected_restic_repo_id"]) and HEX32.fullmatch(g6["expected_key_epoch"])
              and type(a["exec_pins"]) is dict and set(a["exec_pins"]) == set(EXEC_KEYS)
              and all(type(v) is str and SHA.fullmatch(v) for v in a["exec_pins"].values())
              and type(a["exec_paths"]) is dict and set(a["exec_paths"]) == set(EXEC_KEYS)
              and all(_abs(v) for v in a["exec_paths"].values())
              and type(target) is dict and set(target) == {"images", "layers", "web_sha"}
              and type(target["images"]) is dict and set(target["images"]) == {"api", "web"}
              and all(type(v) is str and re.fullmatch(r"(?:sha256:)?[0-9a-f]{64}", v) for v in target["images"].values())
              and type(target["layers"]) is dict and set(target["layers"]) == {"api", "web"}
              and all(type(rows) is list and 1 <= len(rows) <= 64 and all(_abs(p) for p in rows)
                      for rows in target["layers"].values())
              and type(target["web_sha"]) is str and SOURCE.fullmatch(target["web_sha"])
              and type(a["baseline_sha256"]) is str and SHA.fullmatch(a["baseline_sha256"])
              and type(a["admission_sha256"]) is str and SHA.fullmatch(a["admission_sha256"])
              and type(a["policy_hash"]) is str and SHA.fullmatch(a["policy_hash"])
              and type(host) is dict and set(host) == HOST_KEYS and all(_abs(host[k]) for k in ("workdir", "env_file", "evidence_dir"))
              and type(host["project"]) is str and _abs(a["journal_root"]))
    except Exception:  # noqa: BLE001
        ok = False
    if not ok:
        raise Stop("APIWEB_HOST_AUTHORITY_INVALID")


def _read_evidence(directory, name):
    """Root-owned regular file, no symlink, single link, no group/other access, bounded."""
    dfd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        dst = os.fstat(dfd)
        if dst.st_uid != ROOT_UID or dst.st_mode & 0o022:
            raise Stop("APIWEB_EVIDENCE_UNSAFE")
        fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=dfd)
        try:
            st = os.fstat(fd)
            if (not stat.S_ISREG(st.st_mode) or st.st_uid != ROOT_UID or st.st_nlink != 1
                    or st.st_mode & 0o077 or st.st_size > MAX_EVIDENCE):
                raise Stop("APIWEB_EVIDENCE_UNSAFE")
            data = os.read(fd, MAX_EVIDENCE + 1)
            if len(data) != st.st_size:
                raise Stop("APIWEB_EVIDENCE_UNSAFE")
            return data
        finally:
            os.close(fd)
    except OSError:
        raise Stop("APIWEB_EVIDENCE_MISSING") from None
    finally:
        os.close(dfd)


def _sha(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


FORWARD_STAGES = frozenset({"API_SWITCHING", "WEB_SWITCHING"})
RECOVERY_STAGES = frozenset({"ROLLING_BACK"})


def make_gate(clock, expires_at):
    """Forward authority (new API/WEB mutation) ends when the signed GO expires or the clock fails. Recovery
    (rollback to the exact baseline) is NEVER blocked by TTL or by a clock fault: the flight/bridge still demand the
    five locks, the lease, the exact B7 record, pins, recorded scopes and the journal before each rollback command.
    Unknown stages are always refused. An expired GO is never a permission to advance."""
    def gate(stage):
        if type(stage) is not str:
            return False
        if stage in RECOVERY_STAGES:
            return True
        if stage in FORWARD_STAGES:
            try:
                return clock().timestamp() < expires_at
            except Exception:  # noqa: BLE001
                return False
        return False
    return gate


def require_staged_images(read, target, app_source_sha):
    """Fail-closed BEFORE any mutation: both target images must already exist locally with the EXACT pinned image id
    and carry the expected source revision label (API = signed app source sha, WEB = target web sha). `read` is the
    read-only docker guard (image inspect by id only); nothing is built, pulled, started or loaded here. An id that
    inspects successfully IS that image: ids are content-addressed."""
    expected = {"api": app_source_sha, "web": target["web_sha"]}
    for service in ("api", "web"):
        image = target["images"][service]
        image = image if image.startswith("sha256:") else "sha256:" + image
        try:
            label = read(["image", "inspect", "--format", REVISION_FORMAT, image])
        except Exception:  # noqa: BLE001
            raise Stop("APIWEB_STAGED_IMAGE_MISSING") from None
        if type(label) is not str or label.strip() != expected[service]:
            raise Stop("APIWEB_STAGED_IMAGE_LABEL_MISMATCH")


def build(archive_bytes, claims, authority, modules, effects, now=None):
    if (type(archive_bytes) is not bytes or type(claims) is not dict or type(modules) is not dict
            or not MODULE_KEYS <= set(modules) or any(not callable(getattr(effects, m, None)) for m in EFFECT_METHODS)):
        raise Stop("APIWEB_HOST_BINDING_INVALID")
    _shape(authority)
    expires = claims.get("expires_at")
    if type(expires) is not int or isinstance(expires, bool) or expires <= 0:
        raise Stop("APIWEB_HOST_BINDING_INVALID")
    a = authority
    host_cfg, g6 = a["host"], a["g6"]
    data = {}
    for key, name in EVIDENCE.items():
        data[key] = _read_evidence(host_cfg["evidence_dir"], name)
    pins = {"admission": g6["pinned_admission_sha256"], "approval": g6["pinned_approval_sha256"],
            "review": g6["pinned_review_sha256"], "receipt": g6["pinned_receipt_sha256"],
            "restore": g6["pinned_restore_sha256"]}
    if any(_sha(data[k]) != pins[k] for k in EVIDENCE):
        raise Stop("APIWEB_EVIDENCE_PIN_MISMATCH")
    adapters, sr, health, plan_mod = (modules[k] for k in ("host_adapters", "state_reader", "health", "plan"))
    read = adapters.read_only_docker(effects.docker_reader())

    last = {"web_image": None}

    def metadata():
        ids = [i for i in read(adapters.LIST_ARGV).split() if i]
        rows = {}
        for cid in ids:
            value = json.loads(read(["inspect", "--format", adapters.INSPECT_TEMPLATE, cid]))
            if type(value) is not list or len(value) != 12 or type(value[5]) is not str or value[5] in rows:
                raise Stop("APIWEB_METADATA_INVALID")
            rows[value[5]] = value
        last["web_image"] = rows.get("web", [None, None])[1]
        return rows
    def web_revision():
        image = last["web_image"]
        last["web_image"] = None
        if type(image) is not str:
            raise Stop("APIWEB_WEB_IMAGE_UNKNOWN")
        return sr.web_revision_label(read(["image", "inspect", "--format", adapters.REVISION_FORMAT, image]))

    check, served = health.build(effects.https_fetch())

    def health_state():
        try:
            ok = check() is True
            sha = served()
        except Exception:  # noqa: BLE001
            return {"ok": False, "web_sha": ""}
        return {"ok": ok and type(sha) is str, "web_sha": sha if type(sha) is str else ""}
    reader = sr.StateReader(metadata, web_revision, health_state)

    def inspector():
        base = reader.capture_baseline()
        obs = [reader.observe() for _ in range(2)]
        if any(plan_mod.digest(o) != plan_mod.digest(obs[0]) for o in obs):
            raise Stop("APIWEB_OBSERVATION_UNSTABLE")
        return {"status": modules["b7"].SHAPE_STATUS, "baseline_sha256": plan_mod.digest(base),
                "observation_sha256": plan_mod.digest(obs[0]), "running_services_expected": 7,
                "exited_zero_oneshots_verified": 3, "api_compose_layers": len(base["layers"]["api"]),
                "web_compose_layers": len(base["layers"]["web"]), "samples": 2,
                "sourced_from_authenticated_host": False, "sponsor_approved_here": False,
                "g6_verified_here": False, "may_apply": False, "may_install": False}

    physical = modules["physical_runner"]
    verify_pins = effects.pin_verifier(dict(a["exec_paths"]))
    execute = effects.executor()

    def runner_factory(plane, held, report, plan):
        # staged images are verified inside the held plane, after B7/G6 and before ANY mutation command is bound
        require_staged_images(read, a["target"], claims.get("app_source_sha"))
        spec = {"nonce": plane.lease.nonce, "project": host_cfg["project"], "workdir": host_cfg["workdir"],
                "env_file": host_cfg["env_file"], "baseline_layers": plan["baseline"]["layers"],
                "target_layers": plan["target"]["layers"], "admission_sha256": a["admission_sha256"]}
        return physical.bind(root_locks=modules["root_locks"], boundary_module=modules["boundary"],
                             containment=modules["containment"], scopes=modules["scopes"], plane=plane,
                             boundary=held, report=report, spec=spec, pins=dict(a["exec_pins"]),
                             verify_pins=verify_pins, execute=execute)

    clock = now or (lambda: dt.datetime.now(dt.timezone.utc))

    gate = make_gate(clock, expires)
    inputs = {
        "clock": clock, "external_gate": gate, "runner_factory": runner_factory,
        "scope_factory": adapters.cgroup_scope, "settle": lambda: adapters.settle_pause(2.0),
        "inspector": inspector, "reader": reader, "root": Path(a["journal_root"]),
        "admission": Path(host_cfg["evidence_dir"]) / EVIDENCE["admission"],
        "admission_sha256": a["admission_sha256"], "admission_bytes": data["admission"],
        "approval_bytes": data["approval"], "policy_hash": a["policy_hash"], "baseline_sha256": a["baseline_sha256"],
        "target": {"images": dict(a["target"]["images"]), "layers": {k: list(v) for k, v in a["target"]["layers"].items()},
                   "web_sha": a["target"]["web_sha"]},
        "project": host_cfg["project"], "workdir": host_cfg["workdir"], "env_file": host_cfg["env_file"],
    }
    g6_evidence = {"review_bytes": data["review"], "receipt_bytes": data["receipt"], "restore_bytes": data["restore"],
                   **{k: g6[k] for k in G6_PIN_KEYS}, "expected_restic_repo_id": g6["expected_restic_repo_id"],
                   "expected_key_epoch": g6["expected_key_epoch"]}
    return {"g6_evidence": g6_evidence, "inputs": inputs}


if __name__ == "__main__":
    raise SystemExit("QA525_APIWEB_HOST_LIBRARY_ONLY_NO_GO")
