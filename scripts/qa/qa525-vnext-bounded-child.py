#!/usr/bin/env python3
"""QA525 vNext: run ONE fixed child with a hard external deadline. INACTIVE library.

``qa525-vnext-https-fetch.py`` bounds every socket operation but cannot bound DNS
(``getaddrinfo``).  The caller of a physical fetch therefore needs the deadline
OUTSIDE the process.  ``run_bounded`` provides it:

* argv is validated (absolute executable, bounded plain strings), ``shell=False``,
  stdin closed, working directory ``/``, minimal fixed environment (no inherited
  variables, so no proxies, tokens or credentials reach the child);
* the child runs in its own session/process group; on timeout, output overflow or
  any error the WHOLE group is SIGKILLed and the leader reaped; after a normal
  exit the group is also killed before reaping so no straggler survives;
* stdout is size-bounded; stderr is drained, counted and DISCARDED (never echoed);
* every failure is a fixed code; nothing from the child is ever put in an error.

``fetch_via_child`` adapts that to the ``fetch(url) -> (status, final_url, body)``
contract of the health provider, using a child entry point such as
``qa525-vnext-https-fetch-child.py``.  There is NO default executable or child path:
a future, separately reviewed root entrypoint supplies them.  Process-group kill is
not a cgroup: a child that calls ``setsid`` can escape it, so the real entrypoint
should still run this under the transient systemd scope of ``qa525-vnext-containment``.
This module never runs Docker and takes no locks; it authorizes nothing.
"""
from __future__ import annotations

import base64
import importlib.util
import json
import os
from pathlib import Path
import re
import selectors
import signal
import subprocess
import time

HERE = Path(__file__).resolve().parent
MAX_OUTPUT = 400000
MAX_TIMEOUT = 120.0
ENV = {"PATH": "/usr/bin:/bin", "LANG": "C.UTF-8", "LC_ALL": "C.UTF-8"}
ABS_PATH = re.compile(r"/[A-Za-z0-9._/-]{1,200}\Z")
ERROR_CODE = re.compile(r"HTTPSFETCH_[A-Z_]{3,40}\Z")
CHUNK = 65536


class Stop(Exception):
    """Fixed codes only; never carries child output, argv or paths."""


def _spec(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


fetch_lib = _spec("qa525_vnext_https_fetch_for_bounded", "qa525-vnext-https-fetch.py")


def _argv(argv):
    if (type(argv) is not list or not 1 <= len(argv) <= 16
            or not all(type(a) is str and 0 < len(a) <= 2048 and "\x00" not in a for a in argv)
            or ABS_PATH.fullmatch(argv[0]) is None or ".." in argv[0].split("/")):
        raise Stop("BOUNDED_ARGV_INVALID")
    return argv


def _kill_group(proc):
    try:
        os.killpg(proc.pid, signal.SIGKILL)  # leader not yet reaped: pgid is still ours
    except (ProcessLookupError, PermissionError):
        pass


def run_bounded(argv, *, timeout, max_output=MAX_OUTPUT, clock=time.monotonic):
    """Return ``(returncode, stdout_bytes)`` or raise a fixed ``Stop``."""
    _argv(argv)
    if (type(timeout) not in (int, float) or isinstance(timeout, bool)
            or not 0 < timeout <= MAX_TIMEOUT
            or type(max_output) is not int or isinstance(max_output, bool)
            or not 0 < max_output <= 4 * MAX_OUTPUT):
        raise Stop("BOUNDED_LIMITS_INVALID")
    deadline = clock() + timeout
    try:
        proc = subprocess.Popen(
            argv, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            close_fds=True, shell=False, cwd="/", env=dict(ENV), start_new_session=True)
    except (OSError, ValueError):
        raise Stop("BOUNDED_SPAWN_FAILED") from None
    out, total, code = bytearray(), 0, None
    selector = selectors.DefaultSelector()
    try:
        selector.register(proc.stdout, selectors.EVENT_READ, "out")
        selector.register(proc.stderr, selectors.EVENT_READ, "err")
        open_streams = 2
        while open_streams:
            left = deadline - clock()
            if left <= 0:
                raise Stop("BOUNDED_TIMEOUT")
            for key, _ in selector.select(min(left, 0.2)):
                chunk = os.read(key.fileobj.fileno(), CHUNK)
                if not chunk:
                    selector.unregister(key.fileobj)
                    open_streams -= 1
                    continue
                total += len(chunk)
                if total > max_output:
                    raise Stop("BOUNDED_OUTPUT_TOO_LARGE")
                if key.data == "out":
                    out += chunk
        _kill_group(proc)  # a normal exit must not leave stragglers behind
        left = deadline - clock()
        if left <= 0:
            raise Stop("BOUNDED_TIMEOUT")
        try:
            code = proc.wait(timeout=left)
        except subprocess.TimeoutExpired:
            raise Stop("BOUNDED_TIMEOUT") from None
        return code, bytes(out)
    except Stop:
        raise
    except Exception:
        raise Stop("BOUNDED_RUN_FAILED") from None
    finally:
        _kill_group(proc)
        try:
            proc.wait(timeout=5)
        except Exception:
            pass
        selector.close()
        for stream in (proc.stdout, proc.stderr):
            try:
                stream.close()
            except Exception:
                pass


def fetch_via_child(url, *, python, child, timeout, run=run_bounded):
    """``fetch(url)`` contract through a bounded child; no default executables."""
    if url not in fetch_lib.ALLOWED_URLS:
        raise Stop("BOUNDED_URL_NOT_ALLOWED")
    for path in (python, child):
        if type(path) is not str or ABS_PATH.fullmatch(path) is None or ".." in path.split("/"):
            raise Stop("BOUNDED_ARGV_INVALID")
    code, out = run([python, "-I", child, url], timeout=timeout)
    try:
        doc = json.loads(out.decode("ascii"))
    except (UnicodeDecodeError, ValueError, RecursionError):
        raise Stop("BOUNDED_CHILD_FAILED") from None
    if type(doc) is not dict:
        raise Stop("BOUNDED_CHILD_FAILED")
    if code != 0:
        error = doc.get("error")
        if (code == 2 and set(doc) == {"error"} and type(error) is str
                and ERROR_CODE.fullmatch(error)):
            raise Stop(error)
        raise Stop("BOUNDED_CHILD_FAILED")
    if set(doc) != {"status", "final_url", "body"}:
        raise Stop("BOUNDED_CHILD_FAILED")
    status, final, body = doc["status"], doc["final_url"], doc["body"]
    if (type(status) is not int or isinstance(status, bool) or not 100 <= status <= 599
            or type(final) is not str or type(body) is not str):
        raise Stop("BOUNDED_CHILD_FAILED")
    try:
        raw = base64.b64decode(body.encode("ascii"), validate=True)
    except (ValueError, UnicodeEncodeError):
        raise Stop("BOUNDED_CHILD_FAILED") from None
    if len(raw) > fetch_lib.MAX_BODY:
        raise Stop("BOUNDED_CHILD_FAILED")
    return status, final, raw
