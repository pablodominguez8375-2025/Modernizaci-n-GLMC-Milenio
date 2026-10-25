#!/usr/bin/env python3
"""One supervised installation on glmdch. No passwords, tokens or PC needed.

Obtain this script from its reviewed immutable commit and check its SHA256
before running as root. Every downloaded code file is separately hash-pinned.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import stat
import subprocess
import sys
from urllib.request import Request, urlopen

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
STATE = Path("/opt/centenario/qa-auto-lite")
PINNED_CODE = {
    "check-public-readiness.py": "490a3b03004ce8ef54888046de3d255c93aee9c3b6287edf22753cb6893a1857",
    "qa-agent-readiness.py": "afc1d54a90696a4a33bdfe689c47406b5dc0aaf8838426f907761adab20ddd55",
    "qa-lite-compose-adapter.py": "5fa03db393159a49c3606276635248467f6cada8ffdbd5ccaded5afb8b68ed7d",
    "qa-lite-install.py": "0162384605473b15ded3602207be5a978d9ddbcb0bfd59aa5de7bfd8f6551388",
    "qa-lite-web-build.py": "299aa40b12df1ae615a8aaa1cbc6437effc2fd0bf1d83211b5a97550084c1aef",
    "qa-lite-web-transaction.py": "a06fd6faec00e3a58e5ecd9f3b4fa3f26e7ab869b7afe8b1402e73cd974ddae5",
    "qa-public-release-download.py": "3fbce9e62961dd23619f320fd5b592b48ea2f04f6aeb35fc48cf4c3e5f5574b5",
    "qa-signed-staging.py": "ebc9acd2845b4185f8362a81d3eb1657e9a83b1c2cd3d53e4d3dc739fd911e88"
}


def root_directory(path):
    path = Path(path)
    path.mkdir(mode=0o700, parents=False, exist_ok=True)
    for component in (path, *path.parents):
        info = component.lstat()
        if (not stat.S_ISDIR(info.st_mode) or info.st_uid != 0
                or info.st_mode & 0o022):
            raise RuntimeError("BOOTSTRAP_PATH_NOT_ROOT_CONTROLLED")
    return path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True)
    args = parser.parse_args()
    if os.geteuid() != 0 or socket.gethostname() != "glmdch":
        raise RuntimeError("BOOTSTRAP_HOST_NOT_AUTHORIZED")
    if not re.fullmatch(r"[a-f0-9]{40}", args.source) or not PINNED_CODE:
        raise RuntimeError("BOOTSTRAP_SOURCE_INVALID")
    os.umask(0o077)
    root_directory(STATE)
    if (STATE / "installed.json").exists():
        raise RuntimeError("BOOTSTRAP_ALREADY_COMPLETED_USE_INSTALLED_AGENT")
    code = root_directory(STATE / ("code-" + args.source))
    print("QA_BOOTSTRAP_FETCHING_REVIEWED_CODE", flush=True)
    for filename, expected in PINNED_CODE.items():
        if not re.fullmatch(r"[a-z0-9-]+\.py", filename):
            raise RuntimeError("BOOTSTRAP_FILE_NOT_ALLOWED")
        url = "https://raw.githubusercontent.com/" + REPO + "/" + args.source + "/scripts/qa/" + filename
        with urlopen(Request(url, headers={"User-Agent": "pmgm-qa-bootstrap-v1"}), timeout=30) as response:
            if response.geturl() != url:
                raise RuntimeError("BOOTSTRAP_REDIRECT_FORBIDDEN")
            content = response.read(262145)
        if len(content) > 262144 or hashlib.sha256(content).hexdigest() != expected:
            raise RuntimeError("BOOTSTRAP_CODE_DIGEST_MISMATCH")
        out = code / filename
        if out.exists() or out.is_symlink():
            info = out.lstat()
            if (not stat.S_ISREG(info.st_mode) or info.st_uid != 0
                    or info.st_mode & 0o022 or out.read_bytes() != content):
                raise RuntimeError("BOOTSTRAP_EXISTING_CODE_DIFFERS")
        else:
            fd = os.open(out, os.O_CREAT | os.O_EXCL | os.O_WRONLY | os.O_NOFOLLOW, 0o500)
            with os.fdopen(fd, "wb") as stream:
                stream.write(content)
    print("QA_BOOTSTRAP_ALL_CODE_HASHES_VERIFIED", flush=True)
    gh = Path("/usr/bin/gh")
    if not gh.is_file():
        print("QA_BOOTSTRAP_INSTALLING_PUBLIC_SIGNATURE_VERIFIER", flush=True)
        subprocess.run(["/usr/bin/apt-get", "update", "-qq"], check=True,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=300)
        subprocess.run(["/usr/bin/apt-get", "install", "-y", "-qq", "gh"], check=True,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=300)
    subprocess.run([str(gh), "attestation", "verify", "--help"], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=30)
    subprocess.run(["/usr/bin/systemd-run", "--unit=pmgm-qa-lite-firstboot", "--collect",
                    "--property=Type=oneshot", "--property=TimeoutStartSec=1800",
                    "/usr/bin/python3", str(code / "qa-lite-install.py"),
                    "--bootstrap", "--enable-timer"], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    print(json.dumps({"status": "QA_FIRSTBOOT_STARTED_ON_SERVER_NOT_YET_VERIFIED",
                      "unit": "pmgm-qa-lite-firstboot", "code_source_sha": args.source,
                      "log_command": "sudo journalctl -u pmgm-qa-lite-firstboot -n 80 --no-pager"}))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception:
        print("BLOCKED:QA_BOOTSTRAP_FAILED_NO_DEPLOYMENT_CLAIM", file=sys.stderr)
        sys.exit(1)
