#!/usr/bin/env python3
"""QA525 root child for the signed API_WEB_QA_ONLY GO. INACTIVE library; verified PRE-4 loading only, never CLI.

It adds NO effect engine: it only (1) checks the AUTHENTICATED GO claims and the sealed archive FD, (2) asks a
pinned host module for the run inputs, and (3) composes the already reviewed `held-flight-bridge.run_held` under
the plane the launcher ALREADY holds (re-entrant adapter: the five locks are never re-acquired; only the
journal lease of the flight is taken). No migrator, database, identity or volume write exists in this file; the
bridge's 4-command allowlist and rollback by baseline digest are the only effect path.
"""
import contextlib
import hashlib
import os
import re
import stat

PURPOSE = "API_WEB_QA_ONLY"
CLAIM_KEYS = frozenset({"purpose", "nonce", "app_source_sha", "app_archive_sha256"})
HEX = re.compile(r"[0-9a-f]{64}\Z")
SOURCE = re.compile(r"[0-9a-f]{40}\Z")
SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")
MODULES = ("root_locks", "bridge", "boundary", "b7", "observer", "scopes", "switch", "flight", "g6_anchor", "run_held")
MAX_ARCHIVE = 512 * 1024 * 1024
HOST_KEYS = frozenset({"g6_evidence", "inputs"})
FORBIDDEN = ("migrat", "alembic", "prisma", "psql", "pg_dump", "volume", "identity")


class Stop(Exception):
    """Fixed code only."""


class Reentrant:
    """Quacks as `root_locks` for run_held/Boundary but reuses the plane the launcher already holds."""

    def __init__(self, rl, outer):
        self._rl, self._outer = rl, outer
        for name in ("Stop", "RootPlane", "STATUS", "LEGACY_LOCKS"):
            if hasattr(rl, name):
                setattr(self, name, getattr(rl, name))

    @contextlib.contextmanager
    def root_flight(self, port, journal, root, admission, admission_sha256, now):
        outer = self._outer()
        if type(outer) is not self._rl.RootPlane:
            raise Stop("APIWEB_OUTER_PLANE_INVALID")
        outer.assert_held()  # raises if any lock/lease is lost
        lease = None
        try:
            lease = journal.begin(root, admission, admission_sha256, now)  # ONLY the flight lease is new
            plane = self._rl.RootPlane(port, journal, lease, tuple(outer._entries))
            plane.assert_held()
            yield plane
        finally:
            if lease is not None:
                try:
                    lease.close()
                finally:
                    pass  # outer lock fds are NEVER closed here


def _claims(claims):
    if (type(claims) is not dict or set(claims) != CLAIM_KEYS or claims["purpose"] != PURPOSE
            or type(claims["nonce"]) is not str or HEX.fullmatch(claims["nonce"]) is None
            or type(claims["app_source_sha"]) is not str or SOURCE.fullmatch(claims["app_source_sha"]) is None
            or type(claims["app_archive_sha256"]) is not str or SHA.fullmatch(claims["app_archive_sha256"]) is None):
        raise Stop("APIWEB_GO_CLAIMS_INVALID")
    return dict(claims)


def _read_fd(fd):
    try:
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode) or st.st_size > MAX_ARCHIVE:
            raise Stop("APIWEB_FD_INVALID")
        os.lseek(fd, 0, os.SEEK_SET)
        out, left = [], st.st_size
        while left > 0:
            chunk = os.read(fd, min(left, 1 << 20))
            if not chunk:
                raise Stop("APIWEB_FD_INVALID")
            out.append(chunk)
            left -= len(chunk)
        if os.read(fd, 1):
            raise Stop("APIWEB_FD_INVALID")
        return b"".join(out)
    except OSError:
        raise Stop("APIWEB_FD_INVALID") from None


def act_for(*, claims, modules, host, outer_plane, port):
    """Build the child from AUTHENTICATED claims. `modules`: pinned-loader modules; `host`: pinned host module
    exposing build(archive_bytes, claims) -> {"g6_evidence": dict, "inputs": dict}; `outer_plane`: () -> the
    RootPlane held by the launcher."""
    claims = _claims(claims)
    if (type(modules) is not dict or set(modules) != set(MODULES)
            or not callable(getattr(host, "build", None)) or not callable(outer_plane)):
        raise Stop("APIWEB_BINDING_INVALID")
    run_held = getattr(modules["run_held"], "run_held", None)
    if not callable(run_held):
        raise Stop("APIWEB_BINDING_INVALID")

    def act(fd):
        data = _read_fd(fd)
        if "sha256:" + hashlib.sha256(data).hexdigest() != claims["app_archive_sha256"]:
            raise Stop("APIWEB_ARCHIVE_MISMATCH")
        try:
            built = host.build(data, dict(claims))
        except Exception:
            raise Stop("APIWEB_HOST_DENIED") from None
        if type(built) is not dict or set(built) != HOST_KEYS or type(built["inputs"]) is not dict:
            raise Stop("APIWEB_HOST_INVALID")
        inputs = dict(built["inputs"])
        if any(any(w in str(k).lower() for w in FORBIDDEN) for k in inputs):
            raise Stop("APIWEB_HOST_INVALID")
        inputs["port"] = port
        try:
            result = run_held(
                root_locks=Reentrant(modules["root_locks"], outer_plane), bridge=modules["bridge"],
                boundary=modules["boundary"], b7=modules["b7"], observer=modules["observer"],
                scopes=modules["scopes"], switch=modules["switch"], flight=modules["flight"],
                g6_anchor=modules["g6_anchor"], g6_evidence=dict(built["g6_evidence"]), **inputs)
        except BaseException as exc:  # noqa: BLE001 - one fixed code, no host detail
            code = str(exc)
            raise Stop(code if re.fullmatch(r"[A-Z0-9_]{3,64}", code) else "APIWEB_RUN_FAILED") from None
        outcome = result.get("terminal_outcome") if type(result) is dict else None
        if outcome == "APPLIED":
            return "APIWEB_APPLIED"
        if outcome == "ROLLED_BACK":
            raise Stop("APIWEB_ROLLED_BACK")  # a rolled back flight is NEVER reported as success
        raise Stop("APIWEB_TERMINAL_UNKNOWN")
    return act


if __name__ == "__main__":
    raise SystemExit("QA525_APIWEB_CHILD_LIBRARY_ONLY_NO_GO")
