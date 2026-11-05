#!/usr/bin/env python3
"""QA525 vNext child-containment gate -- READ-ONLY, INACTIVE, NOT an executor.

The journal's flock dies with the orchestrator, but a compose
child it started may keep mutating the runtime. Every future mutating child
must therefore run inside a dedicated systemd scope (a cgroup v2 subtree).
This module only (a) builds the argv that would start a child in such a
scope and (b) reads the cgroup to decide whether anything is still alive.
It never starts, signals or kills a process; no subprocess/socket/Docker.

Recovery rule: ``require_quiescent`` AND ``require_settled`` must pass, and
then ``verify`` must confirm the runtime, before ``reconcile_after_crash``
classifies a crashed flight. Unreadable or ambiguous state is Indeterminate
(a Stop), never "empty".

Known limits (not closed by this module):
* An empty cgroup only proves the *CLI* children are gone. The Docker daemon
  lives outside that cgroup and may still be completing a request the dead
  client made, hence the separate convergence gate ``require_settled``.
* ``ABSENT`` does not prove a scope was ever created or used. The engine must
  durably record the exact unit names BEFORE starting anything and treat a
  failed or unconfirmed launch as fatal; ``require_quiescent`` therefore needs
  that recorded set (``expected_units``) and refuses a missing or foreign unit.
* The cgroup root and its ancestors are injectable here; a root-owned
  installer must verify them on the host.
* Only the compose "up" shape built by ``systemd_run_argv`` can be launched
  through this module. Which project, directory, files and service may be
  used is decided by a signed entrypoint, not here.
"""
import hashlib
import json
import re
import stat
from pathlib import Path

CGROUP_ROOT = Path("/sys/fs/cgroup")
SLICE = "system.slice"
UNIT = re.compile(r"^qa525-[a-f0-9]{16}-(?:api|web)\.scope$")
SAFE_PATH = re.compile(r"^/[A-Za-z0-9_@%+=:,./-]{0,510}$")
PROJECT = re.compile(r"^[a-z0-9][a-z0-9_-]{0,62}$")
DOCKER = "/usr/bin/docker"
MAX_FILES = 8
MAX_EVENTS_BYTES = 4096


class Stop(Exception):
    """Fail-closed diagnostic code; never carries external text."""


class Indeterminate(Stop):
    """State could not be established; callers must treat it as NOT quiescent."""


def unit_name(nonce: str, service: str) -> str:
    """Deterministic scope name bound to the flight nonce and the service."""
    if (type(nonce) is not str or re.fullmatch(r"[A-Za-z0-9_-]{24,96}", nonce) is None
            or service not in ("api", "web")):
        raise Stop("UNIT_INPUT_INVALID")
    return "qa525-%s-%s.scope" % (hashlib.sha256(nonce.encode()).hexdigest()[:16], service)


def _safe_path(value) -> str:
    if (type(value) is not str or SAFE_PATH.fullmatch(value) is None
            or ".." in value.split("/") or value.endswith("/")):
        raise Stop("COMMAND_PATH_INVALID")
    return value


def compose_up_command(project: str, workdir: str, env_file: str, files: list,
                       service: str) -> list:
    """The ONLY command shape this module can launch: a forced, no-deps,
    no-build, never-pull recreate of one service (api or web). Arbitrary
    docker verbs (prune, rm, exec...) cannot be expressed."""
    if type(project) is not str or PROJECT.fullmatch(project) is None:
        raise Stop("COMMAND_PROJECT_INVALID")
    if service not in ("api", "web"):
        raise Stop("COMMAND_SERVICE_INVALID")
    if type(files) is not list or not 1 <= len(files) <= MAX_FILES or len(set(files)) != len(files):
        raise Stop("COMMAND_FILES_INVALID")
    command = [DOCKER, "compose", "--project-name", project,
               "--project-directory", _safe_path(workdir), "--env-file", _safe_path(env_file)]
    for path in files:
        command += ["--file", _safe_path(path)]
    return command + ["up", "--detach", "--no-deps", "--no-build", "--pull", "never",
                      "--force-recreate", "--wait", "--wait-timeout", "90", service]


def systemd_run_argv(unit: str, project: str, workdir: str, env_file: str,
                     files: list, service: str) -> list:
    """Argv (never a shell string) running the fixed compose-up inside a scope.

    The unit must belong to the same service it recreates. --collect makes
    systemd remove the scope when it empties, so an ABSENT scope later means
    "ended" only together with a readable parent cgroup AND a recorded unit.
    """
    if type(unit) is not str or UNIT.fullmatch(unit) is None:
        raise Stop("UNIT_INVALID")
    if not unit.endswith("-" + str(service) + ".scope"):
        raise Stop("UNIT_SERVICE_MISMATCH")
    command = compose_up_command(project, workdir, env_file, files, service)
    return ["/usr/bin/systemd-run", "--scope", "--collect", "--quiet",
            "--slice=" + SLICE, "--unit=" + unit, "--"] + command


class CgroupScope:
    """Read-only view of one transient scope under ``<root>/system.slice``."""

    def __init__(self, unit: str, root: Path = CGROUP_ROOT):
        if type(unit) is not str or UNIT.fullmatch(unit) is None:
            raise Stop("UNIT_INVALID")
        if not isinstance(root, Path) or not root.is_absolute() or ".." in root.parts:
            raise Stop("CGROUP_ROOT_INVALID")
        self.unit, self.root = unit, root

    def _dir(self, path: Path, missing_ok: bool):
        try:
            st = path.lstat()
        except FileNotFoundError:
            if missing_ok:
                return None
            raise Indeterminate("CGROUP_PARENT_MISSING") from None
        except OSError:
            raise Indeterminate("CGROUP_UNREADABLE") from None
        if stat.S_ISLNK(st.st_mode) or not stat.S_ISDIR(st.st_mode):
            raise Indeterminate("CGROUP_PATH_UNTRUSTED")
        return st

    def state(self) -> str:
        """ACTIVE (populated), EMPTY (exists, nothing alive) or ABSENT (gone).

        ABSENT requires a readable, non-symlink ``system.slice``; any other
        error is Indeterminate.
        """
        parent = self.root / SLICE
        self._dir(self.root, missing_ok=False)
        self._dir(parent, missing_ok=False)
        scope = parent / self.unit
        if self._dir(scope, missing_ok=True) is None:
            return "ABSENT"
        events = scope / "cgroup.events"
        try:
            st = events.lstat()
            if not stat.S_ISREG(st.st_mode):
                raise Indeterminate("CGROUP_EVENTS_UNTRUSTED")
            with events.open("rb") as handle:
                data = handle.read(MAX_EVENTS_BYTES + 1)
            if len(data) > MAX_EVENTS_BYTES:
                raise Indeterminate("CGROUP_EVENTS_TOO_LARGE")
            raw = data.decode("ascii")
        except Indeterminate:
            raise
        except (OSError, UnicodeDecodeError):
            raise Indeterminate("CGROUP_EVENTS_UNREADABLE") from None
        fields = {}
        for line in raw.splitlines():
            key, _, value = line.partition(" ")
            if key in fields:
                raise Indeterminate("CGROUP_EVENTS_MALFORMED")
            fields[key] = value
        if fields.get("populated") not in ("0", "1"):
            raise Indeterminate("CGROUP_EVENTS_MALFORMED")
        if fields["populated"] == "1":
            return "ACTIVE"
        # populated=0 must agree with an empty cgroup.procs; disagreement is odd.
        procs = scope / "cgroup.procs"
        try:
            with procs.open("rb") as handle:
                if handle.read(64).strip():
                    raise Indeterminate("CGROUP_PROCS_DISAGREE")
        except Indeterminate:
            raise
        except OSError:
            raise Indeterminate("CGROUP_PROCS_UNREADABLE") from None
        return "EMPTY"


def require_quiescent(scopes, *, expected_units) -> None:
    """Raise unless EVERY recorded scope is provably empty/gone.

    ``expected_units`` is the set of unit names the engine durably recorded
    before launching anything. The scopes passed must be exactly that set: a
    missing or foreign unit is Indeterminate, so "I looked at the wrong unit"
    can never read as quiet.
    """
    scopes = list(scopes)
    if not 1 <= len(scopes) <= 8 or any(not hasattr(s, "state") or not hasattr(s, "unit") for s in scopes):
        raise Stop("SCOPES_INVALID")
    try:
        expected = frozenset(expected_units)
    except TypeError:
        raise Stop("EXPECTED_UNITS_INVALID") from None
    if (not expected or any(type(u) is not str or UNIT.fullmatch(u) is None for u in expected)
            or len(scopes) != len({s.unit for s in scopes}) or {s.unit for s in scopes} != expected):
        raise Indeterminate("SCOPE_SET_NOT_THE_RECORDED_ONE")
    live = False
    for scope in scopes:
        try:
            state = scope.state()
        except Indeterminate:
            raise
        except Exception:
            raise Indeterminate("SCOPE_STATE_FAILED") from None
        if state == "ACTIVE":
            live = True
        elif state not in ("EMPTY", "ABSENT"):
            raise Indeterminate("SCOPE_STATE_UNKNOWN")
    if live:
        raise Stop("CHILDREN_STILL_ALIVE")


def require_settled(observe, settle, samples: int = 3) -> str:
    """Convergence gate for the Docker daemon, which no cgroup of ours covers.

    ``observe()`` returns any JSON-serializable runtime snapshot (e.g. the
    reader's observation) and ``settle()`` waits between samples; both are
    injected so this module never sleeps or touches the host. All samples
    must be identical, otherwise the runtime is still changing. Returns the
    snapshot digest for the evidence record.
    """
    if type(samples) is not int or not 2 <= samples <= 10 or not callable(observe) or not callable(settle):
        raise Stop("SETTLE_INPUT_INVALID")
    seen = None
    for index in range(samples):
        if index:
            try:
                settle()
            except Exception:
                raise Indeterminate("SETTLE_FAILED") from None
        try:
            snapshot = json.dumps(observe(), sort_keys=True, separators=(",", ":"), allow_nan=False)
        except Exception:
            raise Indeterminate("SETTLE_OBSERVE_FAILED") from None
        if seen is None:
            seen = snapshot
        elif snapshot != seen:
            raise Stop("RUNTIME_STILL_CHANGING")
    return "sha256:" + hashlib.sha256(seen.encode()).hexdigest()
