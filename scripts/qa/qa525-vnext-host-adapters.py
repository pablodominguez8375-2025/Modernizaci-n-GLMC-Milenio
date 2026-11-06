#!/usr/bin/env python3
"""QA525 vNext host adapters -- INACTIVE: importable pieces, NO entrypoint.

The only code of the vNext line that can start a process. It offers exactly:

* ``scope_runner``: executes ONLY the argv that
  ``qa525-vnext-containment.systemd_run_argv`` builds (a forced, no-deps,
  no-build, never-pull recreate of ``api`` or ``web`` inside a transient scope).
  Anything else -- a different binary, flag, verb, service, unit or an extra
  token -- is refused BEFORE a process exists. No shell, no inherited
  environment, stdin/stdout/stderr discarded, bounded time.
* ``docker_read``: a read-only docker client (``ps``/``inspect``/``image
  inspect``) for the runtime reader.
* ``settle_pause``: a bounded sleep for the convergence gate.
* ``cgroup_scope``: factory for the read-only ``CgroupScope``.

Nothing here has a CLI, reads secrets, opens the network, takes locks or
decides whether an APPLY is allowed: the engine only accepts these through its
synthetic lock plane, which rejects real host paths, so this module cannot be
used for a physical APPLY until a separately reviewed, authenticated root
bootstrap exists. Errors are fixed codes; process output is never captured.
"""
import importlib.util
from pathlib import Path
import subprocess
import time

HERE = Path(__file__).resolve().parent
STATUS = "HOST_ADAPTERS_NOT_AUTHENTICATED_NOT_AUTHORIZED_FOR_PHYSICAL_APPLY"
SYSTEMD_RUN = "/usr/bin/systemd-run"
DOCKER = "/usr/bin/docker"
RUN_TIMEOUT = 150
READ_TIMEOUT = 30
MAX_SETTLE = 30.0
READ_VERBS = ("ps", "inspect")


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


containment = _load("qa525_vnext_containment_for_adapters", "qa525-vnext-containment.py")
_ENV = {"PATH": "/usr/bin:/bin", "HOME": "/root", "LC_ALL": "C"}


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


def scope_runner(argv, *, run=subprocess.run):
    """Run one canonical compose-up scope command; return its exit code."""
    command = _parse(argv)
    try:
        done = run(command, env=dict(_ENV), stdin=subprocess.DEVNULL,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                   timeout=RUN_TIMEOUT, check=False, shell=False)
    except Exception:
        raise Stop("ADAPTER_RUN_FAILED") from None
    code = getattr(done, "returncode", None)
    if type(code) is not int:
        raise Stop("ADAPTER_RUN_FAILED")
    return code


def docker_read(args, *, run=subprocess.run):
    """Read-only docker client: only ps / inspect / image inspect."""
    if (type(args) is not list or not args
            or not all(type(item) is str and item and "\0" not in item for item in args)
            or not (args[0] in READ_VERBS or args[:2] == ["image", "inspect"])):
        raise Stop("ADAPTER_VERB_REFUSED")
    try:
        done = run([DOCKER, "--host", "unix:///var/run/docker.sock", *args],
                   env=dict(_ENV), stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                   stderr=subprocess.DEVNULL, timeout=READ_TIMEOUT, check=True,
                   shell=False, text=True)
        out = done.stdout
    except Exception:
        raise Stop("ADAPTER_READ_FAILED") from None
    if type(out) is not str:
        raise Stop("ADAPTER_READ_FAILED")
    return out.strip()


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
