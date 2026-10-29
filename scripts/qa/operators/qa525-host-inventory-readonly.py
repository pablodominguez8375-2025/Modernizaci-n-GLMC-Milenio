#!/usr/bin/env python3
"""glmdch QA525: inventory exact physical runtime, NO version assumptions.

Read-only Docker inspect and SHA256 of already-installed Compose files. This
tool cannot prepare, back up, start, stop, migrate, install or apply anything.
No Config.Env, network addresses, mounts, secrets or Docker logs are emitted.
"""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import socket
import stat
import sys

HERE = Path(__file__).resolve().parent
ADAPTER = HERE.parent / "qa-lite-compose-adapter.py"
SCOPE = "HOST_INVENTORY_READONLY_NO_SECRETS_NO_INSTALL_NO_APPLY"
PROJECT = "centenario-qa-lab2"
SERVICES = ("api", "web", "keycloak", "postgres", "minio", "gateway", "clamav")
IMAGE = re.compile(r"^sha256:[0-9a-f]{64}$")
CID = re.compile(r"^[0-9a-f]{64}$")
SHA = re.compile(r"^[0-9a-f]{64}$")


class Blocked(Exception):
    pass


def require(condition, code):
    if not condition:
        raise Blocked(code)


def root_trusted(path, directory=False):
    path = Path(path)
    require(path.is_absolute(), "SOURCE_PATH_NOT_ABSOLUTE")
    for entry in (path, *path.parents):
        try:
            st = entry.lstat()
        except OSError:
            raise Blocked("ROOT_PATH_MISSING") from None
        require(not stat.S_ISLNK(st.st_mode) and st.st_uid == 0
                and not stat.S_IMODE(st.st_mode) & 0o022,
                "ROOT_PATH_UNTRUSTED")
    require(path.is_dir() if directory else path.is_file(), "ROOT_PATH_TYPE")


def load_adapter():
    # Do not import source Python from a group/other-writable directory.
    for path in (Path(__file__), ADAPTER):
        root_trusted(path)
    spec = importlib.util.spec_from_file_location("qa525_inventory_adapter", ADAPTER)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def compose_digest(path):
    path = Path(path)
    root_trusted(path)
    st = path.stat()
    require(0 < st.st_size <= 1048576, "COMPOSE_SIZE_INVALID")
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for data in iter(lambda: stream.read(65536), b""):
            h.update(data)
    require(path.stat().st_size == st.st_size, "COMPOSE_CHANGED_DURING_READ")
    return h.hexdigest()


def safe_compose_paths(label):
    require(type(label) is str and 0 < len(label) <= 4096,
            "COMPOSE_LABEL_INVALID")
    result = label.split(",")
    require(0 < len(result) <= 12 and len(set(result)) == len(result),
            "COMPOSE_PATHS_INVALID")
    for value in result:
        p = Path(value)
        require(p.is_absolute() and not any(c in value for c in ("\n", "\r", "\x00"))
                and ".." not in p.parts, "COMPOSE_PATHS_INVALID")
    return result


def summarize(metadata, hash_file):
    """Pure, bounded and no-secrets projection of Docker inspect metadata."""
    require(type(metadata) is dict and set(SERVICES).issubset(metadata),
            "SEVEN_SERVICE_INVENTORY_INCOMPLETE")
    report = {}
    configs = set()
    working_dir = None
    for service in SERVICES:
        record = metadata[service]
        require(type(record) is list and len(record) == 12,
                "DOCKER_INSPECT_SHAPE_INVALID")
        cid, image_id, state = record[:3]
        require(type(cid) is str and CID.fullmatch(cid)
                and type(image_id) is str and IMAGE.fullmatch(image_id)
                and state == "running" and record[5] == service,
                "CONTAINER_RUNTIME_INVALID")
        work = record[6]
        require(type(work) is str and Path(work).is_absolute()
                and ".." not in Path(work).parts,
                "COMPOSE_WORKDIR_INVALID")
        if working_dir is None:
            working_dir = work
        require(work == working_dir, "COMPOSE_PROJECT_WORKDIR_DIVERGED")
        files = safe_compose_paths(record[7])
        if service in ("api", "web"):
            configs.update(files)
        report[service] = {"container_id": cid, "image_id": image_id,
                           "status": "running",
                           "compose_files_count": len(files)}
    require(configs, "API_WEB_COMPOSE_FILES_MISSING")
    # Paths are already-installed local files, never contents or credentials.
    digests = []
    for filename in sorted(configs):
        sha = hash_file(filename)
        require(type(sha) is str and SHA.fullmatch(sha),
                "COMPOSE_DIGEST_NOT_VERIFIED")
        digests.append({"path": filename, "sha256": sha})
    return {
        "status": "PHYSICAL_METADATA_CAPTURED_NO_CHANGE_AUTHORITY",
        "scope": SCOPE,
        "project": PROJECT,
        "working_dir": working_dir,
        "services": report,
        "compose_file_digests": digests,
        "current_git_release_identified": False,
        "identity_verified": False,
        "backup_offhost_verified": False,
        "rollback_verified": False,
        "may_prepare": False,
        "may_apply": False,
    }


def inventory():
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "HOST_NOT_AUTHORIZED")
    adapter = load_adapter()
    require(adapter.PROJECT == PROJECT, "DOCKER_PROJECT_MISMATCH")
    return summarize(adapter.metadata(), compose_digest)


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    try:
        require(args == ["--inventory"], "ONLY_READONLY_INVENTORY_SUPPORTED")
        print(json.dumps(inventory(), sort_keys=True))
        return 0
    except Blocked as ex:
        print(json.dumps({"status": "BLOCKED_QA525_HOST_INVENTORY",
                          "scope": SCOPE, "code": str(ex),
                          "may_apply": False}, sort_keys=True))
        return 2
    except Exception:
        print(json.dumps({"status": "BLOCKED_QA525_HOST_INVENTORY",
                          "scope": SCOPE, "code": "READONLY_INVENTORY_UNAVAILABLE",
                          "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
