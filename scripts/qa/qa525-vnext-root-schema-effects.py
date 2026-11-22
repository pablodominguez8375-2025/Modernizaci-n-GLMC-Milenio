#!/usr/bin/env python3
"""QA525 schema-cut host EFFECTS. INACTIVE library; verified PRE-4 loading only, never CLI.

`provide(authority, claims)` is called by qa525-vnext-root-schema-host.py (precheck BEFORE the nonce, then build_steps)
and returns the effects object the reviewed ports need. It is the only schema-cut file that touches subprocess or the
filesystem. Everything it reads is root-owned, regular, single-link and 0600/0400 (evidence, UAT credential); the
credential VALUE is never logged or placed in argv; it is passed in memory only to fixed authenticated UAT GETs. Fixed diagnostic codes only.

The verified UAT sibling validates root 0600 credential bytes before the nonce and probes fixed authenticated GETs
only after the flight; G6 sibling verifies evidence through the reviewed v2 inspector. Nothing here can name a command: `runner` accepts only
argv whose first element is /usr/bin/docker and that the ports built.
"""
import datetime as dt
import os
import re
import stat
import subprocess

DOCKER = "/usr/bin/docker"
ENV = {"PATH": "/usr/sbin:/usr/bin:/sbin:/bin", "LC_ALL": "C"}
ROOT_UID = 0  # tests may rebind
ABORT_PATH = "/root/.qa525-schema-cut-abort"  # Sponsor creates it (root) to stop the cut; any entry counts
UAT_CREDENTIAL_PATH = "/root/.qa525-uat-credential"  # root 0600; value never leaves this file
COMPOSE_PROJECT = "centenario-qa-lab2"
API_SERVICE = "api"
EVIDENCE = {"admission": "admission.json", "approval": "approval.json", "review": "g6-review.json",
            "receipt": "g6-receipt.json", "restore": "g6-restore.json"}
MAX_EVIDENCE = 1 << 20
MAX_OUT = 1 << 20
CID = re.compile(r"[0-9a-f]{64}\Z")
SIBLING_MODULES = {"g6": "qa525-vnext-root-schema-g6.py", "uat": "qa525-vnext-root-schema-uat.py"}
_DEPS = {}


def bind_dependencies(mods):
    if type(mods) is not dict or set(mods) != set(SIBLING_MODULES):
        raise Stop("SCHEMA_EFFECTS_INPUT_INVALID")
    _DEPS.clear()
    _DEPS.update(mods)


class Stop(Exception):
    """Fixed code only."""


def _root_file(path, max_size, dir_ok=False):
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    except OSError:
        raise Stop("SCHEMA_EFFECTS_FILE_MISSING") from None
    try:
        st = os.fstat(fd)
        if (not stat.S_ISREG(st.st_mode) or st.st_uid != ROOT_UID or st.st_nlink != 1 or st.st_mode & 0o177
                or st.st_size > max_size):
            raise Stop("SCHEMA_EFFECTS_FILE_UNSAFE")
        return fd, st.st_size
    except Exception:
        os.close(fd)
        raise



SCHEMA_SOURCE = "/etc/pmgm/srv01.env"
SCHEMA_ENV_DIR = "/run/qa525-schema-env"


def read_schema_source():
    """Bounded root 0600 source; no secret ever leaves memory except ephemeral files."""
    fd, size = _root_file(SCHEMA_SOURCE, 65536)
    try:
        if size <= 0:
            raise Stop("SCHEMA_EFFECTS_FILE_UNSAFE")
        data = os.read(fd, 65537)
        if len(data) != size:
            raise Stop("SCHEMA_EFFECTS_FILE_UNSAFE")
        return data
    finally:
        os.close(fd)


def verify_schema_tmpfs(parent):
    """Verify /run is the exact tmpfs mount, not merely a writable directory."""
    if parent != "/run":
        return False
    try:
        with open("/proc/self/mountinfo", "r", encoding="utf-8") as stream:
            for line in stream:
                before, sep, after = line.rstrip("\\n").partition(" - ")
                fields = before.split()
                fs = after.split()
                if sep and len(fields) >= 5 and len(fs) >= 1 and fields[4] == "/run":
                    st = os.stat("/run", follow_symlinks=False)
                    return (fs[0] == "tmpfs" and stat.S_ISDIR(st.st_mode)
                            and st.st_uid == ROOT_UID and not st.st_mode & 0o022)
    except (OSError, UnicodeError, ValueError):
        return False
    return False


def read_evidence_factory(directory):
    if type(directory) is not str or not directory.startswith("/") or ".." in directory.split("/"):
        raise Stop("SCHEMA_EFFECTS_INPUT_INVALID")

    def read_evidence(name):
        if name not in EVIDENCE:
            raise Stop("SCHEMA_EFFECTS_INPUT_INVALID")
        try:
            dfd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
        except OSError:
            raise Stop("SCHEMA_EFFECTS_FILE_MISSING") from None
        try:
            dst = os.fstat(dfd)
            if dst.st_uid != ROOT_UID or dst.st_mode & 0o022:
                raise Stop("SCHEMA_EFFECTS_FILE_UNSAFE")
            try:
                fd = os.open(EVIDENCE[name], os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=dfd)
            except OSError:
                raise Stop("SCHEMA_EFFECTS_FILE_MISSING") from None
            try:
                st = os.fstat(fd)
                if (not stat.S_ISREG(st.st_mode) or st.st_uid != ROOT_UID or st.st_nlink != 1
                        or st.st_mode & 0o177 or st.st_size > MAX_EVIDENCE):
                    raise Stop("SCHEMA_EFFECTS_FILE_UNSAFE")
                data = os.read(fd, MAX_EVIDENCE + 1)
                if len(data) != st.st_size:
                    raise Stop("SCHEMA_EFFECTS_FILE_UNSAFE")
                return data
            finally:
                os.close(fd)
        finally:
            os.close(dfd)
    return read_evidence


def abort_requested():
    """True when the abort marker exists or cannot be told absent (fail closed). One lstat, nothing created."""
    try:
        os.lstat(ABORT_PATH)
    except FileNotFoundError:
        return False
    except (OSError, ValueError):
        return True
    return True


def runner(argv, timeout, stdin):
    """(rc, stdout_bytes) for a CLOSED docker argv; stderr dropped (may carry secrets); no shell."""
    if (type(argv) is not tuple or not argv or argv[0] != DOCKER or any(type(a) is not str or "\0" in a for a in argv)
            or type(timeout) is not int or not 0 < timeout <= 600 or not (stdin is None or type(stdin) is bytes)):
        raise Stop("SCHEMA_EFFECTS_ARGV_INVALID")
    try:
        done = subprocess.run(list(argv), input=stdin, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                              env=dict(ENV), timeout=timeout, check=False, shell=False)
    except Exception:  # noqa: BLE001
        raise Stop("SCHEMA_EFFECTS_EXEC_FAILED") from None
    if len(done.stdout) > MAX_OUT:
        raise Stop("SCHEMA_EFFECTS_EXEC_FAILED")
    return int(done.returncode), done.stdout


def clock():
    return dt.datetime.now(dt.timezone.utc)


def _api_container_id(run):
    rc, out = run((DOCKER, "ps", "-q", "--no-trunc", "--filter", "label=com.docker.compose.project=" + COMPOSE_PROJECT,
                   "--filter", "label=com.docker.compose.service=" + API_SERVICE), 60, None)
    lines = out.decode("ascii", "replace").split("\n")
    if rc != 0 or len(lines) != 2 or lines[1] != "" or CID.fullmatch(lines[0]) is None:
        raise Stop("SCHEMA_EFFECTS_API_CONTAINER_UNRESOLVED")
    return lines[0]


class _Effects:
    pass


def provide(authority, claims, run=None):
    """Raises Stop on any doubt. Safe to call before the nonce: it only reads (docker ps, root-owned file metadata)."""
    if type(authority) is not dict or type(claims) is not dict or type(authority.get("host")) is not dict:
        raise Stop("SCHEMA_EFFECTS_INPUT_INVALID")
    if abort_requested() is not False:
        raise Stop("SCHEMA_EFFECTS_ABORT_REQUESTED")
    if (set(_DEPS) != set(SIBLING_MODULES) or not callable(getattr(_DEPS.get("g6"), "make", None))
            or not callable(getattr(_DEPS.get("uat"), "make", None))):
        raise Stop("SCHEMA_EFFECTS_NOT_WIRED")
    def read_credential():
        fd, size = _root_file(UAT_CREDENTIAL_PATH, 4096)
        try:
            raw = os.read(fd, 4097)
            if len(raw) != size or not 0 < size <= 4096:
                raise Stop("SCHEMA_EFFECTS_FILE_UNSAFE")
            return raw
        finally:
            os.close(fd)
    read_credential()  # root ownership/mode and complete bounded read before nonce
    eff = _Effects()
    eff.runner = run or runner
    eff.read_schema_source = read_schema_source
    eff.verify_schema_tmpfs = verify_schema_tmpfs
    eff.clock = clock
    eff.abort_requested = abort_requested
    eff.read_evidence = read_evidence_factory(authority["host"].get("evidence_dir"))
    eff.api_container_id = _api_container_id(eff.runner)
    try:
        eff.verifier, eff.archive_epoch = _DEPS["g6"].make(authority, claims, eff.read_evidence, eff.clock)
    except Exception:
        raise Stop("SCHEMA_EFFECTS_G6_INVALID") from None
    if (not callable(eff.verifier) or type(eff.archive_epoch) is not int):
        raise Stop("SCHEMA_EFFECTS_G6_INVALID")
    try:
        eff.uat_checks = _DEPS["uat"].make(read_credential, eff.clock,
                                              expected_sha=claims.get("app_source_sha"))
    except Exception:
        raise Stop("SCHEMA_EFFECTS_UAT_INVALID") from None
    return eff


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_EFFECTS_LIBRARY_ONLY_NO_GO")
