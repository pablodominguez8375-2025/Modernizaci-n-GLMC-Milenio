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
import socket
import stat
import subprocess
import sys

QA = Path(__file__).resolve().parents[1]
DOCKER = "/usr/bin/docker"
HOST = "glmdch"
# Everything this helper executes AS ROOT: itself plus the exact sibling modules it loads (and the one host-adapters loads).
CHAIN = (Path(__file__).resolve(), QA / "qa525-vnext-host-adapters.py", QA / "qa525-vnext-containment.py",
         QA / "qa525-vnext-state-reader.py", QA / "qa525-vnext-baseline-plan.py")
STATUS = "BASELINE_CAPTURED_READ_ONLY_NO_APPLY"
CLEAN_ENV = {"PATH": "/usr/bin:/bin", "LC_ALL": "C"}  # no DOCKER_HOST/DOCKER_CONFIG/DOCKER_CONTEXT from the caller
_VERIFIED = {}  # file name -> the EXACT bytes that were hashed (what gets executed, no re-read: closes hash/import TOCTOU)


class Blocked(Exception):
    pass


def trusted(path):
    """Every path component must be root-owned, not a symlink and not group/world-writable; the file a regular file.
    Run BEFORE any sibling module is imported/executed as root (same rule as qa525-current-image-preparation)."""
    path = Path(path)
    if not path.is_absolute():
        raise Blocked("TRUST_PATH_NOT_ABSOLUTE")
    for segment in (path, *path.parents):
        try:
            st = segment.lstat()
        except OSError:
            raise Blocked("TRUST_PATH_MISSING") from None
        if stat.S_ISLNK(st.st_mode) or st.st_uid != 0 or st.st_mode & 0o022:
            raise Blocked("TRUST_PATH_UNSAFE")
    if not path.is_file():
        raise Blocked("TRUST_PATH_TYPE")
    return path


def verify_chain(chain=CHAIN, host_ok=None, euid=None):
    """Fail closed BEFORE importing anything: right host, root, and a trusted source chain. Returns the sha256 of every
    file so the operator can compare them with the signed release manifest (evidence, not a new trust root)."""
    if (socket.gethostname() == HOST) is not (True if host_ok is None else host_ok):
        raise Blocked("HOST_NOT_AUTHORIZED")
    if (os.geteuid() if euid is None else euid) != 0:
        raise Blocked("ROOT_REQUIRED")
    digests = {}
    for path in chain:
        trusted(path)
        data = path.read_bytes()
        _VERIFIED[path.name] = data
        digests[path.name] = hashlib.sha256(data).hexdigest()
    return digests


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, QA / filename)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    data = _VERIFIED.get(filename)
    if data is None:
        raise Blocked("MODULE_NOT_VERIFIED")  # never execute bytes that verify_chain did not hash
    exec(compile(data, str(QA / filename), "exec"), module.__dict__)  # noqa: S102 - the verified bytes
    return module


def _docker(args):
    trusted(DOCKER)  # the binary itself: root-owned, not writable, not a symlink
    return subprocess.run([DOCKER, *args], check=True, capture_output=True, text=True, timeout=30,
                          env=dict(CLEAN_ENV)).stdout


def capture(run=_docker, modules=None, module_sha256=None):
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
    return {"status": STATUS, "module_sha256": module_sha256 or {}, "baseline_sha256": plan.digest(first), "baseline": first,
            "canonical_sha256_of_bytes": hashlib.sha256(plan.canonical(first)).hexdigest(),
            "reads": 2, "wrote_files": False, "may_apply": False, "go_spent": False}


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    try:
        if args != ["--capture"]:
            raise Blocked("ONLY_EXPLICIT_CAPTURE_SUPPORTED")
        digests = verify_chain()  # host + root + trusted chain BEFORE any sibling import
        print(json.dumps(capture(module_sha256=digests), sort_keys=True))
        return 0
    except Exception as exc:  # noqa: BLE001
        code = str(exc) if isinstance(exc, Blocked) else "BASELINE_NOT_CAPTURED"
        print(json.dumps({"status": "BLOCKED_QA525_BASELINE_CAPTURE", "code": code, "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
