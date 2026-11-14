#!/usr/bin/env python3
"""QA525 vNext host adapters -- INACTIVE: no process, no default executor.

This module imports nothing that can spawn a process and has NO default way to run
anything. It offers validators/wrappers that only become useful when a future,
separately reviewed, signed root entrypoint injects the effect:

* ``held_runner(plane, run)``: returns ``runner(argv) -> int``. Each call first
  proves, through ``plane.assert_held()``, that the five-lock plane and the
  journal lease are held right now, then accepts ONLY the argv that
  ``qa525-vnext-containment.systemd_run_argv`` builds (a forced, no-deps,
  no-build, never-pull recreate of ``api`` or ``web`` in a transient scope).
  ``run`` has no default: it is the caller's process effect. Calling this with a
  canonical argv but without a held plane never reaches ``run``.
* ``read_only_docker(run)``: returns ``read(args) -> str`` accepting ONLY the
  exact argv forms the reader needs (project listing, the fixed 12-field
  ``inspect`` template on a 64-hex container id, and the web image revision
  label on an image id). Any other flag, format string or id (for example a
  ``--format`` that would print ``Config.Env``) is refused BEFORE ``run``, and
  the answer is size-bounded.
* ``settle_pause`` (bounded sleep) and ``cgroup_scope`` (read-only view).

The engine only accepts a synthetic lock plane that rejects real host paths,
so none of this can drive a physical APPLY. Errors are fixed codes; process
output is never echoed.
"""
import importlib.util
from pathlib import Path
import re
import time

try:
    HERE = Path(__file__).resolve().parent
except NameError:  # PRE-4 Verified exec has no __file__: siblings are INJECTED by bind_dependencies, never read from disk
    HERE = None
STATUS = "HOST_ADAPTERS_NOT_AUTHENTICATED_NOT_AUTHORIZED_FOR_PHYSICAL_APPLY"
SYSTEMD_RUN = "/usr/bin/systemd-run"
DOCKER = "/usr/bin/docker"
MAX_SETTLE = 30.0
MAX_OUTPUT = 262144
PROJECT = "centenario-qa-lab2"
CONTAINER_ID = re.compile(r"[a-f0-9]{64}\Z")
IMAGE_ID = re.compile(r"(?:sha256:)?[a-f0-9]{64}\Z")
LIST_ARGV = ["ps", "-a", "--filter", "label=com.docker.compose.project=" + PROJECT,
             "--format", "{{.ID}}", "--no-trunc"]
INSPECT_TEMPLATE = (
    '[{{json .Id}},{{json .Image}},{{json .State.Status}},{{json .State.StartedAt}},'
    '{{json .RestartCount}},{{json (index .Config.Labels "com.docker.compose.service")}},'
    '{{json (index .Config.Labels "com.docker.compose.project.working_dir")}},'
    '{{json (index .Config.Labels "com.docker.compose.project.config_files")}},'
    '{{json .Mounts}},{{json .NetworkSettings.Networks}},{{json .NetworkSettings.Ports}},'
    '{{json .State.ExitCode}}]')
REVISION_FORMAT = '{{index .Config.Labels "org.opencontainers.image.revision"}}'


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


containment = _load("qa525_vnext_containment_for_adapters", "qa525-vnext-containment.py") if HERE is not None else None

SIBLING_MODULES = {"containment": "qa525-vnext-containment.py"}
_BOUND = []


def bind_dependencies(mapping):
    """PRE-4 ONLY: receive the exact sibling modules (one verified instance per file, shared by the whole flight)."""
    if (HERE is not None or _BOUND or type(mapping) is not dict or set(mapping) != set(SIBLING_MODULES)
            or any(type(m).__name__ != "module" or type(m).__module__ != "builtins" for m in mapping.values())):
        raise Stop("DEPENDENCY_BINDING_INVALID")
    globals().update(mapping)
    _BOUND.append(True)


def _parse(argv):
    """Rebuild the canonical argv from its own fields; any deviation is a Stop."""
    if type(argv) is not list or not all(type(item) is str for item in argv):
        raise Stop("ADAPTER_ARGV_INVALID")
    try:
        if len(argv) < 30 or argv[0] != SYSTEMD_RUN or argv[7] != DOCKER:
            raise Stop("ADAPTER_ARGV_REJECTED")
        if not argv[5].startswith("--unit="):
            raise Stop("ADAPTER_ARGV_REJECTED")
        unit, project, workdir, env_file = argv[5][len("--unit="):], argv[10], argv[12], argv[14]
        index, files = 15, []
        while argv[index] == "--file":
            files.append(argv[index + 1])
            index += 2
        expected = containment.systemd_run_argv(unit, project, workdir, env_file,
                                                files, argv[-1])
    except Stop:
        raise
    except Exception:
        raise Stop("ADAPTER_ARGV_REJECTED") from None
    if argv != expected:
        raise Stop("ADAPTER_ARGV_REJECTED")
    return expected


def held_runner(plane, run):
    """Bind a process effect to a HELD synthetic plane; return ``runner(argv)``."""
    if not (callable(run) and callable(getattr(plane, "assert_held", None))):
        raise Stop("ADAPTER_AUTHORITY_REQUIRED")

    def runner(argv):
        command = _parse(argv)
        try:
            status = plane.assert_held()
            if (type(status) is not dict or status.get("legacy_locks_held") != 5
                    or status.get("root_lease_held_synthetically") is not True):
                raise Stop("ADAPTER_PLANE_NOT_HELD")
        except Stop:
            raise
        except Exception:
            raise Stop("ADAPTER_PLANE_NOT_HELD") from None
        try:
            code = run(list(command))
        except Exception:
            raise Stop("ADAPTER_RUN_FAILED") from None
        if type(code) is not int:
            raise Stop("ADAPTER_RUN_FAILED")
        return code

    return runner


def read_only_docker(run):
    """Return ``read(args) -> str`` for the exact reader argv forms only."""
    if not callable(run):
        raise Stop("ADAPTER_RUN_INVALID")

    def read(args):
        if type(args) is not list or not all(type(item) is str for item in args):
            raise Stop("ADAPTER_VERB_REFUSED")
        if not (args == LIST_ARGV
                or (len(args) == 4 and args[:3] == ["inspect", "--format", INSPECT_TEMPLATE]
                    and CONTAINER_ID.fullmatch(args[3]))
                or (len(args) == 5 and args[:4] == ["image", "inspect", "--format", REVISION_FORMAT]
                    and IMAGE_ID.fullmatch(args[4]))):
            raise Stop("ADAPTER_VERB_REFUSED")
        try:
            out = run(list(args))
        except Exception:
            raise Stop("ADAPTER_READ_FAILED") from None
        if type(out) is not str or len(out) > MAX_OUTPUT:
            raise Stop("ADAPTER_READ_FAILED")
        return out.strip()

    return read


def settle_pause(seconds=2.0, *, sleep=time.sleep):
    if type(seconds) not in (int, float) or not 0 <= seconds <= MAX_SETTLE:
        raise Stop("ADAPTER_SETTLE_INVALID")
    sleep(seconds)


def cgroup_scope(unit, root=None):
    try:
        if root is None:
            return containment.CgroupScope(unit)
        return containment.CgroupScope(unit, root)
    except Exception:
        raise Stop("ADAPTER_SCOPE_INVALID") from None


if __name__ == "__main__":
    raise SystemExit("QA525_HOST_ADAPTERS_LIBRARY_ONLY_NO_EXECUTION")
