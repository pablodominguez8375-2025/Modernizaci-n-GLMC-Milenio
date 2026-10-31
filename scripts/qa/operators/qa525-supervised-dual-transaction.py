#!/usr/bin/env python3
"""QA525 supervised API+WEB transaction core.

No CLI and no automatic host approval. A separately reviewed ROOT-owned
entrypoint must supply live Sigstore, G6, EF/identity, supervisor and runtime
verifiers under the same lock. Tests run with a fake Docker transport.
"""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import stat

PROJECT = "centenario-qa-lab2"
# Exact physical environment context required by dev's QA525 host preflight.
ENV_FILE = "/opt/centenario/qa-auto-lite/empty.env"
TARGETS = ("api", "web")
DEPENDENCIES = ("keycloak", "postgres", "minio", "gateway", "clamav")
ONESHOTS = ("db-grants", "migrator", "minio-init")
SHA = re.compile(r"[0-9a-f]{40}\Z")
IMAGE = re.compile(r"sha256:[0-9a-f]{64}\Z")
HASH = re.compile(r"[0-9a-f]{64}\Z")


class Blocked(Exception):
    pass


def require(value, code):
    if not value:
        raise Blocked(code)


def root_path(path, directory=False):
    """Reject untrusted ancestors, writable paths and symlinks."""
    path = Path(path)
    require(path.is_absolute() and ".." not in path.parts, "PATH_NOT_ABSOLUTE")
    for item in (path, *path.parents):
        try:
            mode = item.lstat()
        except OSError:
            raise Blocked("ROOT_PATH_MISSING") from None
        require(mode.st_uid == 0 and not stat.S_ISLNK(mode.st_mode)
                and not mode.st_mode & 0o022, "ROOT_PATH_UNTRUSTED")
    require(path.is_dir() if directory else path.is_file(), "ROOT_PATH_TYPE")
    return path


def sha256(path):
    return hashlib.sha256(root_path(path).read_bytes()).hexdigest()


def normalized_layers(record, root):
    """API: base+API+identity. WEB: base+WEB. Preserve order exactly."""
    require(type(record) is dict, "LAYER_RECORD_INVALID")
    api = record.get("api")
    web = record.get("web")
    require(type(api) is list and type(web) is list
            and len(api) == 3 and len(web) == 2
            and api[0] == web[0] and len(set(api + web)) == 4,
            "COMPOSE_LAYERS_UNRECOGNIZED")
    base = str(root / "compose.recovery.json")
    require(api[0] == base and Path(api[1]).name.startswith("api-")
            and re.fullmatch(r"api-[0-9a-f]{40}\.json", Path(api[1]).name)
            and api[2] == "/etc/pmgm/qa754-identity-overlay.stage.json"
            and re.fullmatch(r"web-[0-9a-f]{64}\.json", Path(web[1]).name),
            "COMPOSE_LAYER_BINDING_INVALID")
    require(Path(api[1]).parent == root.parent.parent / "qa-api-refresh"
            and Path(web[1]).parent in (root, Path("/opt/centenario/qa-auto-lite")),
            "COMPOSE_LAYER_LOCATION_INVALID")
    return {k: tuple(v) for k, v in (("api", api), ("web", web))}


def baseline_contract(before, new, source_sha, layers, workdir):
    require(SHA.fullmatch(source_sha or "") is not None, "SOURCE_SHA_INVALID")
    require(type(before) is dict and set(before) == set(TARGETS + DEPENDENCIES + ONESHOTS),
            "BASELINE_SERVICE_SET_INVALID")
    require(type(new) is dict and set(new) == set(TARGETS)
            and all(type(new[n]) is str and IMAGE.fullmatch(new[n]) for n in TARGETS)
            and new["api"] != new["web"], "CANDIDATE_IDS_INVALID")
    for name in TARGETS + DEPENDENCIES:
        value = before[name]
        require(type(value) is dict and set(value) >= {"image", "id", "status"}
                and IMAGE.fullmatch(value["image"]) and HASH.fullmatch(value["id"])
                and value["status"] == "running", "BASELINE_RUNNING_SERVICE_INVALID")
    for name in ONESHOTS:
        value = before[name]
        require(type(value) is dict and value.get("status") == "exited"
                and value.get("exit_code") == 0 and HASH.fullmatch(value.get("id", "")),
                "BASELINE_JOB_INVALID")
    for name in TARGETS:
        require(new[name] != before[name]["image"], "NO_IMAGE_CHANGE")
    require(type(layers) is dict and set(layers) == set(TARGETS)
            and all(type(x) is tuple for x in layers.values()),
            "COMPOSE_LAYERS_INVALID")
    require(type(workdir) is str and Path(workdir).is_absolute(), "WORKDIR_INVALID")


def make_override(state, source, name, image):
    """Immutable root-owned layer, preserving prior base, API and identity."""
    require(name in TARGETS and SHA.fullmatch(source or "") and IMAGE.fullmatch(image or ""),
            "OVERRIDE_SCOPE_INVALID")
    root_path(state, directory=True)
    require(stat.S_IMODE(Path(state).stat().st_mode) == 0o700, "STATE_DIR_NOT_PRIVATE")
    path = Path(state) / ("qa525-" + name + "-" + source + "-" + image[7:] + ".json")
    payload = json.dumps({"services": {name: {"image": image}}}, sort_keys=True) + "\n"
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW
    try:
        fd = os.open(path, flags, 0o600)
    except FileExistsError:
        require(root_path(path).read_text() == payload, "OVERRIDE_COLLISION")
    else:
        with os.fdopen(fd, "w", encoding="utf-8") as output:
            output.write(payload)
            output.flush()
            os.fsync(output.fileno())
    return str(path)


def compose_args(workdir, base_layers, extra, service):
    require(service in TARGETS and type(base_layers) is tuple
            and 2 <= len(base_layers) <= 128
            and len(base_layers) == len(set(base_layers)), "COMPOSE_SCOPE_INVALID")
    args = ["compose", "--project-name", PROJECT, "--project-directory", workdir,
            "--env-file", ENV_FILE]
    for path in (*base_layers, extra):
        args += ["--file", path]
    args += ["up", "--detach", "--no-deps", "--no-build", "--pull", "never",
             "--force-recreate", "--wait", "--wait-timeout", "90", service]
    return args


def transact(*, before, new, source_sha, layers, workdir, state_dir, run,
             live_snapshot, evidence, health, audit):
    """One locked session. Callbacks MUST verify physical host, not receipts.

    Caller holds exclusive QA update locks for the entire call. Once mutation
    begins, any error triggers reversal of BOTH services (including partial
    success). No down, rm, volume delete, migration or other service updates.
    """
    baseline_contract(before, new, source_sha, layers, workdir)
    require(callable(run) and callable(live_snapshot) and callable(evidence)
            and callable(health) and callable(audit), "TRUSTED_CALLBACK_MISSING")
    # Reject prior successful, failed or interrupted attempts BEFORE touching
    # host evidence: a stale deployment is never implicitly retried.
    root_path(state_dir, directory=True)
    record = Path(state_dir) / ("dual-" + source_sha + ".json")
    require(not record.exists() and not record.is_symlink(),
            "TRANSACTION_ALREADY_EXISTS_OR_INCOMPLETE")
    require(evidence("pre", source_sha, before, new) is True,
            "SIGNED_G6_SENSITIVE_OR_SUPERVISION_EVIDENCE_MISSING")
    require(live_snapshot() == before, "BASELINE_CHANGED_BEFORE_APPLY")
    api_new = make_override(state_dir, source_sha, "api", new["api"])
    web_new = make_override(state_dir, source_sha, "web", new["web"])
    api_old = make_override(state_dir, source_sha, "api", before["api"]["image"])
    web_old = make_override(state_dir, source_sha, "web", before["web"]["image"])
    journal = {"source_sha": source_sha, "status": "PENDING",
               "old_images": {k: before[k]["image"] for k in TARGETS},
               "new_images": new, "original_layers": {k: list(v) for k, v in layers.items()}}
    fd = os.open(record, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "w") as output:
        json.dump(journal, output, sort_keys=True)
        output.flush()
        os.fsync(output.fileno())
    # Ensure initial crash-recovery receipt survives power loss before any up.
    dirfd = os.open(record.parent, os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(dirfd)
    finally:
        os.close(dirfd)
    # Do not print Docker output or identity environment.
    started = False
    try:
        require(evidence("just-before-apply", source_sha, before, new) is True,
                "SIGNED_G6_OR_APPROVAL_CHANGED")
        require(live_snapshot() == before, "BASELINE_CHANGED_AT_ACTIVATION")
        started = True  # record may become partly mutated by the first Docker call
        run(compose_args(workdir, layers["api"], api_new, "api"))
        run(compose_args(workdir, layers["web"], web_new, "web"))
        current = live_snapshot()
        require(set(current) == set(before)
                and all(current[x] == before[x] for x in DEPENDENCIES + ONESHOTS)
                and all(current[x]["image"] == new[x]
                        and current[x]["status"] == "running" for x in TARGETS),
                "POST_APPLY_INVARIANT_FAILED")
        require(health() is True, "POST_APPLY_HEALTH_FAILED")
        require(evidence("post", source_sha, before, new) is True,
                "POST_APPLY_EVIDENCE_INVALID")
        audit("DUAL_API_WEB_APPLIED", source_sha)
        journal["status"] = "APPLIED"
        # Receipt updated only after all postconditions. If persistence fails,
        # rollback is still attempted using the initial durable PENDING file.
        _replace_journal(record, journal)
        return {"status": "DUAL_API_WEB_APPLIED", "source_sha": source_sha}
    except BaseException:
        if started:
            try:
                # Always roll both targets back, even if one apply failed halfway.
                run(compose_args(workdir, layers["web"], web_old, "web"))
                run(compose_args(workdir, layers["api"], api_old, "api"))
                current = live_snapshot()
                require(set(current) == set(before)
                        and all(current[x] == before[x] for x in DEPENDENCIES + ONESHOTS)
                        and all(current[x]["image"] == before[x]["image"]
                                and current[x]["status"] == "running" for x in TARGETS)
                        and health() is True, "ROLLBACK_RUNTIME_NOT_RESTORED")
                journal["status"] = "ROLLED_BACK"
                _replace_journal(record, journal)
                audit("DUAL_API_WEB_ROLLED_BACK", source_sha)
            except BaseException:
                journal["status"] = "RECOVERY_MANUALLY_REQUIRED"
                _replace_journal(record, journal)
                audit("DUAL_API_WEB_RECOVERY_REQUIRED", source_sha)
                raise Blocked("DUAL_ROLLBACK_FAILED_MANUAL_RECOVERY_REQUIRED") from None
        raise


def _replace_journal(path, value):
    temp = path.with_name(path.name + ".next")
    require(not temp.exists() and not temp.is_symlink(), "JOURNAL_TEMP_COLLISION")
    fd = os.open(temp, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "w") as output:
        json.dump(value, output, sort_keys=True)
        output.flush()
        os.fsync(output.fileno())
    os.replace(temp, path)
    folder = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(folder)
    finally:
        os.close(folder)


if __name__ == "__main__":
    raise SystemExit("NO_CLI_TRUSTED_HOST_WRAPPER_REQUIRED")
