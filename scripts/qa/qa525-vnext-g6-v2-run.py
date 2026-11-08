#!/usr/bin/env python3
"""QA525 G6 v2 ROOT ENTRYPOINT (INACTIVE in dev: CI never runs it, never a GO).

Wires the three G6 v2 libraries -- producer (INIT/BACKUP/RESTORE/REVIEW), physical captor and
isolated restore verifier -- into ONE operator command so the Sponsor can run a backup/restore
cycle from a phone terminal, one fixed subcommand at a time:

    preflight | init | backup | restore | review

Rules enforced here (the libraries enforce the rest):
* Subcommands and option names are a closed set; every value is matched against a strict
  pattern. No abbreviations, no positional arguments, no environment or file overrides.
* Identity pins (restic sha256, repo id, key epoch, image digest, archive hashes) are CLI
  values the Sponsor copies from his own record; nothing is derived from this script.
* The Restic password is read from the terminal WITHOUT echo and only by ``init``; it is never
  accepted as an argument, printed or logged.
* This program NEVER stops, starts, restarts or removes a service container. The Silo copy only
  proceeds while the MinIO container is positively NOT running; the Sponsor stops/starts MinIO
  himself with separate commands inside the authorized window.
* Output is ``KEY=VALUE`` lines of public identifiers/hashes. Failures print one fixed code
  ``G6RUN_STOP=<CODE>``; never a traceback, path or command output.

NOT proven here: the authenticity of this file and its siblings (the Sponsor must hash them
against the published list before running), the docker daemon, the host, or that the data are
production's. Needs root (docker socket, root-only state directory).
"""
from __future__ import annotations

import datetime as dt
import getpass
import hashlib
import hmac
import importlib.util
import json
import os
from pathlib import Path
import re
import contextlib
import shutil
import signal
import socket
import stat
import sys
import time

HERE = Path(__file__).resolve().parent
STATE_ROOT = "/var/lib/pmgm-qa525-g6-v2"
RESTIC = "/usr/bin/restic"
SILO_SOURCE = ("/var/lib/docker/volumes/centenario-qa-lab2_pmgm_srv01_minio_data/_data")
MINIO_DATA_DESTINATION = "/data"
EXPECTED_HOST = "glmdch"      # defence in depth only: never proof of provenance or authorization
SYSTEMCTL = "/usr/bin/systemctl"
WATCHDOG_TIMER = "pmgm-g6-minio-restart.timer"
WATCHDOG_MIN_SECONDS = 600    # must not fire before the largest allowed window
WATCHDOG_MAX_SECONDS = 900    # nor be so far away that a hung MinIO stays down
WATCHDOG_MARGIN_SECONDS = 30  # the copy is cut this long BEFORE the independent restart
STOP_WAIT_SECONDS = 180      # operator time to stop MinIO once asked
WINDOW_MAX_SECONDS = 600     # hard cap, stop observed -> Silo copy finished
POLL_SECONDS = 2
FORBIDDEN_PREFIXES = ["/var/lib/docker", "/opt/centenario", "/srv"]
SIBLINGS = {"producer": "qa525-vnext-g6-v2-producer.py",
            "captor": "qa525-vnext-g6-v2-captor.py",
            "verifier": "qa525-vnext-g6-v2-verifier.py"}
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
EPOCH = re.compile(r"[a-f0-9]{32}\Z")
SNAPSHOT = re.compile(r"[a-f0-9]{8,64}\Z")
ARCHIVE = re.compile(r"centenario-qa-g6-v2-CIFRADO-(\d{8}T\d{6}Z)\.tar\.gz\Z")
DRIVE_ID = re.compile(r"[A-Za-z0-9_-]{15,128}\Z")
IMAGE = re.compile(r"[a-z0-9][a-z0-9._/-]{0,127}@sha256:[0-9a-f]{64}\Z")
CODE = re.compile(r"G6[A-Z]{1,6}_[A-Z0-9_]{3,60}\Z")
OPTION_PATTERNS = {
    "restic-sha256": HEX64, "repo-id": HEX64, "key-epoch": EPOCH, "image": IMAGE,
    "archive-name": ARCHIVE, "archive-sha256": HEX64, "snapshot-id": SNAPSHOT,
    "manifest-sha256": HEX64, "drive-file-id": DRIVE_ID,
}
COMMANDS = {
    "preflight": ((), ("restic-sha256", "image")),
    "init": (("restic-sha256",), ()),
    "backup": (("restic-sha256", "repo-id", "key-epoch"), ()),
    "restore": (("restic-sha256", "repo-id", "key-epoch", "image", "archive-name",
                 "archive-sha256", "snapshot-id", "manifest-sha256"), ()),
    "review": (("archive-name", "drive-file-id", "repo-id", "key-epoch"), ()),
}
MIN_KEY_CHARS = 32
KEY_CHARS = re.compile(r"[\x21-\x7e]+\Z")


class Stop(Exception):
    """Fixed public codes only; never echo output, paths or errors."""


def parse(argv):
    """Closed grammar: ``<command> [--name value]...``; returns (command, options)."""
    if type(argv) is not list or not argv or any(type(a) is not str for a in argv):
        raise Stop("G6RUN_USAGE")
    command, rest = argv[0], argv[1:]
    if command not in COMMANDS:
        raise Stop("G6RUN_USAGE")
    required, optional = COMMANDS[command]
    if len(rest) % 2:
        raise Stop("G6RUN_USAGE")
    options = {}
    for flag, value in zip(rest[0::2], rest[1::2]):
        if not flag.startswith("--"):
            raise Stop("G6RUN_USAGE")
        name = flag[2:]
        if name not in required + optional or name in options:
            raise Stop("G6RUN_USAGE")
        if OPTION_PATTERNS[name].fullmatch(value) is None:
            raise Stop("G6RUN_VALUE_INVALID")
        options[name] = value
    if any(name not in options for name in required):
        raise Stop("G6RUN_USAGE")
    return command, options


def _trusted_sibling(path, uid):
    try:
        info = path.lstat()
    except OSError:
        raise Stop("G6RUN_MODULE_UNAVAILABLE") from None
    if (not stat.S_ISREG(info.st_mode) or info.st_uid not in (0, uid)
            or info.st_mode & 0o022):
        raise Stop("G6RUN_MODULE_UNTRUSTED")


def _sha256_file(path):
    """SHA-256 of a regular file opened without following links; None if unavailable."""
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    except OSError:
        return None
    try:
        if not stat.S_ISREG(os.fstat(fd).st_mode):
            return None
        digest = hashlib.sha256()
        while True:
            block = os.read(fd, 1 << 20)
            if not block:
                return digest.hexdigest()
            digest.update(block)
    except OSError:
        return None
    finally:
        os.close(fd)


def _load_module(alias, filename, uid):
    path = HERE / filename
    _trusted_sibling(path, uid)
    spec = importlib.util.spec_from_file_location(alias, path)
    if spec is None or spec.loader is None:
        raise Stop("G6RUN_MODULE_UNAVAILABLE")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class Env:
    """Everything with side effects, in one place so tests can substitute it."""

    def __init__(self, *, producer, captor, verifier, uid, now, docker_runner, make_restic,
                 read_key, emit, state_root=STATE_ROOT, silo_source=SILO_SOURCE,
                 restic=RESTIC, forbidden=None, module_hashes=None,
                 sleep=time.sleep, monotonic=time.monotonic,
                 systemctl_runner=None, systemctl=SYSTEMCTL, deadline_guard=None):
        self.producer, self.captor, self.verifier = producer, captor, verifier
        self.uid, self.now, self.docker_runner = uid, now, docker_runner
        self.make_restic, self.read_key, self.emit = make_restic, read_key, emit
        self.state_root, self.silo_source, self.restic = state_root, silo_source, restic
        self.forbidden = list(FORBIDDEN_PREFIXES if forbidden is None else forbidden)
        self.module_hashes = dict(module_hashes or {})
        self.sleep, self.monotonic = sleep, monotonic
        self.systemctl_runner, self.systemctl = systemctl_runner, systemctl
        self.deadline_guard = deadline_guard or real_deadline
        self.docker = captor.DOCKER
        self.project = captor.PROJECT
        self.minio = captor.PROJECT + "-minio-1"


@contextlib.contextmanager
def real_deadline(seconds):
    """Interrupt the enclosed work after ``seconds`` (SIGALRM): a real cut, not an after-check."""
    def fire(signum, frame):
        raise Stop("G6RUN_WINDOW_EXCEEDED")
    previous = signal.signal(signal.SIGALRM, fire)
    signal.setitimer(signal.ITIMER_REAL, seconds)
    try:
        yield
    finally:
        signal.setitimer(signal.ITIMER_REAL, 0)
        signal.signal(signal.SIGALRM, previous)


def minio_quiescent(env):
    """True only if MinIO of THIS compose project is positively NOT running."""
    code, out = env.docker_runner(env.captor.argv_inspect(env.docker, env.minio),
                                  capture_limit=4096)
    return code == 0 and out.decode("ascii", "replace").strip() == \
        "|".join((env.project, "minio", "false"))


def silo_bound(env):
    """True only if MinIO's /data mount IS the volume directory this program will copy."""
    code, out = env.docker_runner(
        [env.docker, "--host", env.captor.SOCKET, "inspect", "--format", "{{json .Mounts}}",
         env.minio], capture_limit=65536)
    if code != 0:
        return False
    try:
        mounts = json.loads(out.decode("utf-8"))
    except (ValueError, UnicodeDecodeError):
        return False
    if type(mounts) is not list:
        return False
    data = [m for m in mounts if type(m) is dict
            and m.get("Destination") == MINIO_DATA_DESTINATION]
    if len(data) != 1:
        return False
    source = data[0].get("Source")
    return (data[0].get("Type") == "volume" and type(source) is str
            and source == env.silo_source and os.path.realpath(source) == source
            and not os.path.islink(source))


def _props(raw):
    out = {}
    for line in raw.decode("utf-8", "replace").splitlines():
        key, sep, value = line.partition("=")
        if sep:
            out[key] = value
    return out


_SPAN = re.compile(r"(\d+(?:\.\d+)?)(us|ms|min|month|s|h|d|w|y)")
_SPAN_UNITS = {"us": 1e-6, "ms": 1e-3, "s": 1.0, "min": 60.0, "h": 3600.0, "d": 86400.0,
               "w": 604800.0, "month": 2629800.0, "y": 31557600.0}


def usec_property_seconds(text):
    """Seconds from a systemd USec property: raw microseconds OR its timespan rendering.

    Seen in glmdch (systemd, 08-11-2026): ``NextElapseUSecMonotonic=3w 5d 1h 20min 7.412742s``.
    """
    text = text.strip()
    if text.isdigit():
        return int(text) / 1e6
    if not text or _SPAN.sub("", text).strip(" ") != "":
        raise ValueError("unparsable")
    return sum(float(n) * _SPAN_UNITS[u] for n, u in _SPAN.findall(text))


def watchdog_deadline(env):
    """Monotonic deadline of an INDEPENDENT, armed restart of MinIO, or Stop.

    The operator arms ``systemd-run --on-active=11min --unit=pmgm-g6-minio-restart
    /usr/bin/docker start <minio>`` BEFORE the window. This reads systemd (not this process),
    so it survives the terminal dying or SIGKILL of this program.
    """
    if env.systemctl_runner is None:
        raise Stop("G6RUN_WATCHDOG_MISSING")
    shown = [env.systemctl, "show", "--no-pager", WATCHDOG_TIMER,
             "--property=ActiveState,SubState,Unit,NextElapseUSecMonotonic"]
    code, raw = env.systemctl_runner(shown, capture_limit=4096)
    props = _props(raw) if code == 0 else {}
    if props.get("ActiveState") != "active" or props.get("SubState") != "waiting":
        raise Stop("G6RUN_WATCHDOG_MISSING")
    service = props.get("Unit", "")
    if not re.fullmatch(r"[a-zA-Z0-9_.-]{1,80}\.service", service):
        raise Stop("G6RUN_WATCHDOG_WRONG")
    code, raw = env.systemctl_runner(
        [env.systemctl, "show", "--no-pager", service, "--property=ExecStart"],
        capture_limit=8192)
    found = re.search(r"argv\[\]=(.*?) ;", raw.decode("utf-8", "replace")) if code == 0 else None
    if (found is None or found.group(1).split(" ") != [env.docker, "start", env.minio]):
        raise Stop("G6RUN_WATCHDOG_WRONG")
    try:
        remaining = usec_property_seconds(props["NextElapseUSecMonotonic"]) - env.monotonic()
    except (KeyError, ValueError):
        raise Stop("G6RUN_WATCHDOG_TIMING") from None
    if not WATCHDOG_MIN_SECONDS <= remaining <= WATCHDOG_MAX_SECONDS:
        raise Stop("G6RUN_WATCHDOG_TIMING")
    return env.monotonic() + remaining


class SiloGate:
    """The ``silo_quiescent`` port: binds the source, measures and caps the stop window.

    The program never stops or starts MinIO. It asks the operator (one fixed line) and
    watches. MinIO must be seen RUNNING first, so the window start is observed, not assumed.
    """

    def __init__(self, env):
        self.env, self.phase, self.opened_at, self.closed = env, 0, None, False
        self.deadline = None

    def __call__(self):
        env = self.env
        if self.phase == 0:
            self.phase = 1
            if not silo_bound(env):
                raise Stop("G6RUN_SILO_UNBOUND")
            if minio_quiescent(env):
                raise Stop("G6RUN_MINIO_STOPPED_EARLY")
            self.deadline = watchdog_deadline(env)
            _emit(env, g6run_action="STOP_MINIO_NOW", window_max_seconds=WINDOW_MAX_SECONDS,
                  wait_seconds=STOP_WAIT_SECONDS)
            waited = 0
            while not minio_quiescent(env):
                if waited >= STOP_WAIT_SECONDS:
                    return False
                env.sleep(POLL_SECONDS)
                waited += POLL_SECONDS
            self.opened_at = env.monotonic()
            return True
        self.phase = 2
        result = minio_quiescent(env)
        self.closed = True
        elapsed = int(env.monotonic() - self.opened_at)
        _emit(env, g6run_action="START_MINIO_NOW", minio_window_seconds=elapsed)
        if (elapsed > WINDOW_MAX_SECONDS
                or env.monotonic() >= self.deadline - WATCHDOG_MARGIN_SECONDS):
            raise Stop("G6RUN_WINDOW_EXCEEDED")
        return result

    def copy_budget(self):
        """Seconds the copy may run: never past the window cap NOR the independent restart."""
        limit = min(self.opened_at + WINDOW_MAX_SECONDS, self.deadline - WATCHDOG_MARGIN_SECONDS)
        return limit - self.env.monotonic()

    def abort_notice(self):
        if self.opened_at is not None and not self.closed:
            self.closed = True
            _emit(self.env, g6run_action="START_MINIO_NOW", reason="ABORTED")


def _emit(env, **pairs):
    for key, value in pairs.items():
        text = str(value)
        if re.fullmatch(r"[A-Za-z0-9._:/@+=,|-]{0,200}", text) is None:
            raise Stop("G6RUN_OUTPUT_REFUSED")
        env.emit(key.upper() + "=" + text)


def _tree_stats(root, max_files, max_bytes):
    """Bounded, link-free census of regular files; (files, bytes, safe_and_within_limits)."""
    files = size = 0
    try:
        if not stat.S_ISDIR(os.lstat(root).st_mode):
            return 0, 0, False
        for base, dirs, leaves in os.walk(root, followlinks=False):
            for name in dirs + leaves:
                info = os.lstat(os.path.join(base, name))
                if stat.S_ISDIR(info.st_mode):
                    continue
                if not stat.S_ISREG(info.st_mode):
                    return files, size, False
                files += 1
                size += info.st_size
                if files > max_files or size > max_bytes:
                    return files, size, False
    except OSError:
        return files, size, False
    return files, size, True


def _yesno(flag):
    return "yes" if flag else "no"


def image_matches_pin(raw, image):
    """Exact: ``<Id> <json RepoDigests>`` where one entry equals the pinned reference."""
    try:
        ident, digests = raw.decode("ascii").strip().split(" ", 1)
        listed = json.loads(digests)
    except (ValueError, UnicodeDecodeError):
        return False
    return (re.fullmatch(r"sha256:[0-9a-f]{64}", ident) is not None and type(listed) is list
            and any(type(entry) is str and entry == image for entry in listed))


def cmd_preflight(env, opts):
    """Read-only checks; writes nothing and changes nothing."""
    cap = env.captor
    fcntl_ok = False
    try:
        import fcntl
        fcntl_ok = all(hasattr(fcntl, n) for n in (
            "F_ADD_SEALS", "F_GET_SEALS", "F_SEAL_WRITE", "F_SEAL_SEAL",
            "F_SEAL_SHRINK", "F_SEAL_GROW"))
    except ImportError:
        pass
    _emit(env, euid=os.geteuid(), python=".".join(map(str, sys.version_info[:3])),
          python_fcntl_seals=_yesno(fcntl_ok))
    for label, digest in sorted(env.module_hashes.items()):
        _emit(env, **{"sha256_" + label: digest})
    docker_hash = _sha256_file(env.docker)
    _emit(env, docker_sha256=docker_hash or "unavailable")
    restic_hash = _sha256_file(env.restic)
    _emit(env, restic_sha256=restic_hash or "unavailable")
    if "restic-sha256" in opts:
        _emit(env, restic_matches_pin=_yesno(
            restic_hash is not None
            and hmac.compare_digest(restic_hash, opts["restic-sha256"])))
    for service, container in (("postgres", cap.POSTGRES_CONTAINER), ("minio", env.minio)):
        code, out = env.docker_runner(cap.argv_inspect(env.docker, container), capture_limit=4096)
        text = out.decode("ascii", "replace").strip() if code == 0 else "unavailable"
        _emit(env, **{service + "_identity": text})
    _emit(env, minio_quiescent=_yesno(minio_quiescent(env)))
    bound = silo_bound(env)
    _emit(env, silo_bound_to_mount=_yesno(bound))
    files, size, within = _tree_stats(env.silo_source, cap.SILO_MAX_FILES, cap.SILO_MAX_BYTES)
    _emit(env, silo_source_files=files, silo_source_bytes=size,
          silo_source_ok=_yesno(within and files > 0))
    ready = [fcntl_ok, bound, within and files > 0]
    if "restic-sha256" in opts:
        ready.append(restic_hash is not None
                     and hmac.compare_digest(restic_hash, opts["restic-sha256"]))
    probe = env.state_root if os.path.isdir(env.state_root) else os.path.dirname(env.state_root)
    try:
        free = shutil.disk_usage(probe).free
    except OSError:
        free = 0
    free_ok = free >= 3 * size + 2 * 1024 ** 3 and within
    ready.append(free_ok)
    _emit(env, state_root=("present" if os.path.isdir(env.state_root) else "missing"),
          free_bytes=free, free_ok=_yesno(free_ok))
    if "image" in opts:
        code, out = env.docker_runner(
            [env.docker, "--host", cap.SOCKET, "image", "inspect", "--format",
             "{{.Id}} {{json .RepoDigests}}", opts["image"]], capture_limit=8192)
        matches = code == 0 and image_matches_pin(out, opts["image"])
        ready.append(matches)
        _emit(env, image_present=_yesno(code == 0), image_matches_pin=_yesno(matches))
    ok = all(ready)
    _emit(env, preflight_ready=_yesno(ok))
    return 0 if ok else 1


def cmd_init(env, opts):
    key = env.read_key()
    if (type(key) is not bytes or len(key) < MIN_KEY_CHARS
            or KEY_CHARS.fullmatch(key.decode("ascii", "replace")) is None):
        raise Stop("G6RUN_KEY_REFUSED")
    result = env.producer.init_repo(env.state_root, runner=env.make_restic(opts["restic-sha256"]),
                                    key_bytes=key, uid=env.uid, now=env.now())
    _emit(env, restic_repo_id=result["restic_repo_id"], key_epoch=result["key_epoch"])
    return 0


def cmd_backup(env, opts):
    cap = env.captor

    gate = SiloGate(env)

    def copy_silo(dest):
        budget = gate.copy_budget()
        if budget <= 0:
            raise Stop("G6RUN_WINDOW_EXCEEDED")
        with env.deadline_guard(budget):  # cut BEFORE the watchdog could restart MinIO
            cap.copy_tree_nofollow(env.silo_source, dest)

    captor = cap.build_captor(runner=env.docker_runner, copy_silo=copy_silo,
                              silo_quiescent=gate, uid=env.uid, docker=env.docker)
    try:
        out = env.producer.backup(env.state_root,
                                  runner=env.make_restic(opts["restic-sha256"]),
                                  captor=captor, expected_repo_id=opts["repo-id"],
                                  expected_key_epoch=opts["key-epoch"], uid=env.uid,
                                  now=env.now())
    finally:
        gate.abort_notice()
    _emit(env, archive_name=out["archive_name"], archive_sha256=out["archive_sha256"],
          snapshot_id=out["snapshot_id"], manifest_sha256=out["manifest_sha256"],
          stamp=out["stamp"])
    return 0


def cmd_restore(env, opts):
    match = ARCHIVE.fullmatch(opts["archive-name"])
    verifier = env.verifier.build_verifier(
        runner=env.docker_runner, image=opts["image"], stamp=match.group(1), docker=env.docker)
    report = env.producer.restore(
        env.state_root, runner=env.make_restic(opts["restic-sha256"]), verifier=verifier,
        archive_name=opts["archive-name"], archive_sha256=opts["archive-sha256"],
        snapshot_id=opts["snapshot-id"], expected_manifest_sha256=opts["manifest-sha256"],
        expected_repo_id=opts["repo-id"], expected_key_epoch=opts["key-epoch"],
        forbidden_prefixes=env.forbidden, uid=env.uid, now=env.now())
    tables = report["postgres_tables_by_database"]
    _emit(env, result=report["result"], archive_sha256=report["archive_sha256"],
          pmgm_tables=tables["pmgm"], pmgm_keycloak_tables=tables["pmgm_keycloak"],
          silo_objects_verified=report["silo_objects_verified"],
          production_touched=str(report["production_touched"]).lower())
    return 0


def cmd_review(env, opts):
    review = env.producer.finalize_review(
        env.state_root, archive_name=opts["archive-name"], drive_file_id=opts["drive-file-id"],
        expected_repo_id=opts["repo-id"], expected_key_epoch=opts["key-epoch"], uid=env.uid)
    _emit(env, g6_archive_name=review["g6_archive_name"],
          g6_archive_sha256=review["g6_archive_sha256"],
          g6_offhost_sha256_verified=str(review["g6_offhost_sha256_verified"]).lower())
    return 0


HANDLERS = {"preflight": cmd_preflight, "init": cmd_init, "backup": cmd_backup,
            "restore": cmd_restore, "review": cmd_review}


def dispatch(command, opts, env):
    """Run one parsed command; a library Stop becomes one fixed code line and exit 1."""
    try:
        return HANDLERS[command](env, opts)
    except Exception as error:  # noqa: BLE001 - map to a fixed public code, never a traceback
        text = str(error)
        code = text if type(error).__name__ == "Stop" and CODE.fullmatch(text) else \
            "G6RUN_UNEXPECTED"
        env.emit("G6RUN_STOP=" + code)
        return 1


def _read_key_from_terminal():
    first = getpass.getpass("Clave nueva de Restic (sin eco): ")
    second = getpass.getpass("Repita la clave (sin eco): ")
    if not hmac.compare_digest(first.encode("utf-8", "replace"),
                               second.encode("utf-8", "replace")):
        raise Stop("G6RUN_KEY_MISMATCH")
    try:
        return first.rstrip("\r\n").encode("ascii")
    except UnicodeEncodeError:
        raise Stop("G6RUN_KEY_REFUSED") from None


def real_env(command):
    """Production wiring. Root only; no parameter lets a caller replace a runner."""
    if socket.gethostname() != EXPECTED_HOST:  # before any module is loaded
        raise Stop("G6RUN_HOST_REFUSED")
    if os.geteuid() != 0:
        raise Stop("G6RUN_ROOT_REQUIRED")
    uid = os.geteuid()
    modules, hashes = {}, {}
    for label, filename in SIBLINGS.items():
        modules[label] = _load_module("qa525_g6_run_" + label, filename, uid)
        hashes[label] = _sha256_file(HERE / filename) or "unavailable"
    hashes["entrypoint"] = _sha256_file(Path(__file__).resolve()) or "unavailable"
    captor = modules["captor"]
    producer = modules["producer"]
    return Env(
        producer=producer, captor=captor, verifier=modules["verifier"], uid=uid,
        now=lambda: dt.datetime.now(dt.timezone.utc),
        docker_runner=captor.FixedRunner(),
        systemctl_runner=captor.FixedRunner(SYSTEMCTL, 15),
        make_restic=lambda sha: producer.ResticCli(RESTIC, binary_sha256=sha),
        read_key=_read_key_from_terminal, emit=print, module_hashes=hashes)


def main(argv):
    try:
        command, opts = parse(list(argv))
        env = real_env(command)
    except Stop as stop:
        print("G6RUN_STOP=" + str(stop))
        return 1
    except Exception:  # noqa: BLE001
        print("G6RUN_STOP=G6RUN_UNEXPECTED")
        return 1
    try:
        return dispatch(command, opts, env)
    except KeyboardInterrupt:
        print("G6RUN_STOP=G6RUN_INTERRUPTED")
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
