#!/usr/bin/env python3
"""Fail-closed, read-only contract for an OFFHOST disposable PostgreSQL lab.

Checks an inert Compose template or operator-provided rendered JSON.
No Docker/Compose execution, no image pulls, no files written, no deploy.
"""
import json
from pathlib import Path
import re
import sys

SOURCE = Path(__file__).resolve().parents[3] / "infrastructure/qa525/restore-lab.compose.json"
SCOPE = "OFFHOST_QA525_RESTORE_LAB_STATIC_CONTRACT_NO_APPLY"
ENV_IMAGE = "${QA525_POSTGRES_IMAGE:?Set an independently verified postgres:17@sha256 digest}"
DIGEST_IMAGE = re.compile(r"(?:docker\.io/library/)?postgres:17@sha256:[0-9a-f]{64}\Z")
PROJECT = "qa525-restore-lab"
CONTAINER = "qa525-restore-lab-postgres-1"
VOLUME = "qa525-restore-lab_pgdata"
PGDATA = "/var/lib/postgresql/data"
CAPS = frozenset(("NET_ADMIN", "SYS_ADMIN", "SYS_PTRACE"))
SECURITY = ["no-new-privileges:true"]
HEALTH = ["CMD-SHELL", "pg_isready -U postgres -d postgres"]
TOP = frozenset(("name", "services", "volumes", "networks"))
SVC = frozenset(("image", "container_name", "network_mode", "pid", "ipc",
                 "privileged", "restart", "cap_drop", "security_opt", "ports",
                 "environment", "volumes", "healthcheck", "networks"))


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def validate(doc, *, rendered=False):
    """Structural template validation does NOT establish image provenance."""
    require(type(doc) is dict and set(doc).issubset(TOP)
            and doc.get("name") == PROJECT
            and not doc.get("networks")
            and type(doc.get("services")) is dict
            and set(doc["services"]) == {"postgres"},
            "LAB_PROJECT_OR_TOPOLOGY_INVALID")
    svc = doc["services"]["postgres"]
    require(type(svc) is dict and set(svc).issubset(SVC),
            "LAB_SERVICE_HAS_UNREVIEWED_FIELDS")
    img = svc.get("image")
    require(type(img) is str and
            (DIGEST_IMAGE.fullmatch(img) if rendered else img == ENV_IMAGE),
            "POSTGRES_IMAGE_NOT_EXACT_DIGEST")
    require(svc.get("container_name") == CONTAINER
            and svc.get("network_mode") == "none"
            and svc.get("pid") == "private"
            and svc.get("ipc") == "private"
            and svc.get("privileged") is False
            and svc.get("restart") == "no"
            and not svc.get("ports")
            and not svc.get("networks")
            and type(svc.get("cap_drop")) is list
            and set(svc["cap_drop"]) == CAPS
            and len(svc["cap_drop"]) == len(CAPS)
            and svc.get("security_opt") == SECURITY,
            "LAB_ISOLATION_CHANGED")
    require(svc.get("environment") == {
                "POSTGRES_HOST_AUTH_METHOD": "trust",
                "POSTGRES_DB": "postgres",
            },
            "LAB_ENVIRONMENT_UNEXPECTED")
    mounts = svc.get("volumes")
    require(type(mounts) is list and len(mounts) == 1,
            "LAB_VOLUME_INVALID")
    if rendered:
        require(type(mounts[0]) is dict and
                mounts[0].get("type") == "volume"
                and mounts[0].get("source") == "pgdata"
                and mounts[0].get("target") == PGDATA
                and not mounts[0].get("read_only")
                and set(mounts[0]).issubset({
                    "type", "source", "target", "read_only", "volume",
                }),
                "LAB_VOLUME_INVALID")
    else:
        require(mounts == ["pgdata:" + PGDATA], "LAB_VOLUME_INVALID")
    health = svc.get("healthcheck")
    require(type(health) is dict and health.get("test") == HEALTH
            and health.get("disable") is not True
            and set(health).issubset({
                "test", "interval", "timeout", "retries", "start_period",
            }),
            "LAB_HEALTHCHECK_INVALID")
    volumes = doc.get("volumes")
    require(type(volumes) is dict and set(volumes) == {"pgdata"},
            "LAB_VOLUME_INVALID")
    definition = volumes["pgdata"]
    require(type(definition) is dict
            and definition.get("name") == VOLUME
            and definition.get("external", False) is False
            and not definition.get("labels")
            and not definition.get("driver_opts")
            and definition.get("driver", "local") == "local"
            and set(definition).issubset({
                "name", "external", "labels", "driver", "driver_opts",
            }),
            "LAB_VOLUME_NOT_EXCLUSIVE")
    return {
        "status": "OFFHOST_LAB_STRUCTURE_MATCHES_NO_ATTESTATION",
        "scope": SCOPE,
        "configuration_kind": "rendered" if rendered else "template",
        "image_digest_syntax_checked": bool(rendered),
        "image_signature_verified": False,
        "lab_provisioned": False,
        "host_isolation_verified": False,
        "offhost_custody_verified": False,
        "restoration_drill_verified": False,
        "may_apply": False,
    }


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    try:
        require(args in (["--check-template"], ["--check-rendered"]),
                "ONLY_STATIC_CHECKS_SUPPORTED")
        if args == ["--check-template"]:
            payload = SOURCE.read_bytes()
        else:
            payload = sys.stdin.buffer.read(32769)
        require(0 < len(payload) <= 32768, "COMPOSE_INPUT_INVALID")
        result = validate(json.loads(payload),
                          rendered=args == ["--check-rendered"])
        print(json.dumps(result, sort_keys=True))
        return 0
    except Exception:
        print(json.dumps({"status": "BLOCKED_QA525_OFFHOST_LAB",
                          "scope": SCOPE, "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
