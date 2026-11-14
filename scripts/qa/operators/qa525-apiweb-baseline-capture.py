#!/usr/bin/env python3
"""QA525 API+WEB canonical baseline capture: STRICTLY READ-ONLY, offline of any GO, no apply.

Reads the ten containers of the Compose project ``centenario-qa-lab2`` through the SAME guarded docker reader
(``ps -a`` / ``inspect --format`` / ``image inspect --format`` with the exact allowlisted argv) and the SAME
``StateReader.capture_baseline`` that the flight uses, twice, and prints the canonical baseline plus its digest
(``apiweb.baseline_sha256``). It never writes a file, never starts/stops/pulls/builds anything and spends no GO.
Usage (root on glmdch): ``qa525-apiweb-baseline-capture.py --capture``."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys

QA = Path(__file__).resolve().parents[1]
DOCKER = "/usr/bin/docker"
STATUS = "BASELINE_CAPTURED_READ_ONLY_NO_APPLY"


class Blocked(Exception):
    pass


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, QA / filename)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def _docker(args):
    return subprocess.run([DOCKER, *args], check=True, capture_output=True, text=True, timeout=30).stdout


def capture(run=_docker, modules=None):
    adapters, sr, plan = modules or (_load("qa525_cap_adapters", "qa525-vnext-host-adapters.py"),
                                     _load("qa525_cap_reader", "qa525-vnext-state-reader.py"),
                                     _load("qa525_cap_plan", "qa525-vnext-baseline-plan.py"))
    read = adapters.read_only_docker(run)
    last = {"web_image": None}

    def metadata():
        rows = {}
        for cid in [i for i in read(adapters.LIST_ARGV).split() if i]:
            value = json.loads(read(["inspect", "--format", adapters.INSPECT_TEMPLATE, cid]))
            if type(value) is not list or len(value) != 12 or type(value[5]) is not str or value[5] in rows:
                raise Blocked("BASELINE_METADATA_INVALID")
            rows[value[5]] = value
        last["web_image"] = rows.get("web", [None, None])[1]
        return rows

    def web_revision():
        image, last["web_image"] = last["web_image"], None
        if type(image) is not str:
            raise Blocked("BASELINE_WEB_IMAGE_UNKNOWN")
        return sr.web_revision_label(read(["image", "inspect", "--format", adapters.REVISION_FORMAT, image]))

    reader = sr.StateReader(metadata, web_revision, lambda: {"ok": True, "web_sha": ""})
    first, second = reader.capture_baseline(), reader.capture_baseline()
    if plan.canonical(first) != plan.canonical(second):
        raise Blocked("BASELINE_UNSTABLE_BETWEEN_READS")
    plan.validate_baseline(first)
    return {"status": STATUS, "baseline_sha256": plan.digest(first), "baseline": first,
            "canonical_sha256_of_bytes": hashlib.sha256(plan.canonical(first)).hexdigest(),
            "reads": 2, "wrote_files": False, "may_apply": False, "go_spent": False}


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    try:
        if args != ["--capture"]:
            raise Blocked("ONLY_EXPLICIT_CAPTURE_SUPPORTED")
        if os.geteuid() != 0:
            raise Blocked("ROOT_REQUIRED")
        print(json.dumps(capture(), sort_keys=True))
        return 0
    except Exception as exc:  # noqa: BLE001
        code = str(exc) if isinstance(exc, Blocked) else "BASELINE_NOT_CAPTURED"
        print(json.dumps({"status": "BLOCKED_QA525_BASELINE_CAPTURE", "code": code, "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
