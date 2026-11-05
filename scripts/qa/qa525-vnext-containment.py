#!/usr/bin/env python3
"""QA525 vNext child-containment gate -- READ-ONLY, INACTIVE, NOT an executor.

The journal's flock dies with the orchestrator, but a compose
child it started may keep mutating the runtime. Every future mutating child
must therefore run inside a dedicated systemd scope (a cgroup v2 subtree).
This module only (a) builds the argv that would start a child in such a
scope and (b) reads the cgroup to decide whether anything is still alive.
It never starts, signals or kills a process; no subprocess/socket/Docker.

Recovery rule: ``require_quiescent`` must pass before ``reconcile_after_crash``
classifies a crashed flight. Unreadable or ambiguous state is Indeterminate
(a Stop), never "empty".
"""
import hashlib
import re
import stat
from pathlib import Path

CGROUP_ROOT = Path("/sys/fs/cgroup")
SLICE = "system.slice"
UNIT = re.compile(r"^qa525-[a-f0-9]{16}-(?:api|web)\.scope$")
SAFE_ARG = re.compile(r"^[A-Za-z0-9_@%+=:,./-]{1,512}$")
MAX_ARGV = 64
EXECUTABLES = frozenset({"/usr/bin/docker"})


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


def systemd_run_argv(unit: str, command: list) -> list:
    """Argv (never a shell string) running ``command`` inside a transient scope.

    --collect makes systemd remove the scope when it empties, so an ABSENT
    scope later means "ended" only together with a readable parent cgroup.
    """
    if type(unit) is not str or UNIT.fullmatch(unit) is None:
        raise Stop("UNIT_INVALID")
    if type(command) is not list or not 1 <= len(command) <= MAX_ARGV:
        raise Stop("COMMAND_INVALID")
    if command[0] not in EXECUTABLES:
        raise Stop("EXECUTABLE_NOT_ALLOWED")
    for arg in command:
        if type(arg) is not str or SAFE_ARG.fullmatch(arg) is None:
            raise Stop("COMMAND_ARG_INVALID")
    return ["/usr/bin/systemd-run", "--scope", "--collect", "--quiet",
            "--slice=" + SLICE, "--unit=" + unit, "--"] + list(command)


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
            raw = events.read_bytes()[:4096].decode("ascii")
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
            if procs.read_bytes()[:64].strip():
                raise Indeterminate("CGROUP_PROCS_DISAGREE")
        except Indeterminate:
            raise
        except OSError:
            raise Indeterminate("CGROUP_PROCS_UNREADABLE") from None
        return "EMPTY"


def require_quiescent(scopes) -> None:
    """Raise unless EVERY scope is provably empty/gone. Gate before recovery."""
    scopes = list(scopes)
    if not 1 <= len(scopes) <= 8 or any(not hasattr(s, "state") for s in scopes):
        raise Stop("SCOPES_INVALID")
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
