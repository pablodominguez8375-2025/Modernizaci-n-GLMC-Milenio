#!/usr/bin/env python3
"""QA525 vNext durable scope manifest -- INACTIVE, no process execution.

The containment contract requires the exact transient systemd unit names to
be durable before any child can start.  This module writes that two-unit
manifest under the same journal lease and later lets the switcher prove that
each requested unit belongs to it.  It never starts a process, reads cgroups,
or authorizes a physical APPLY.
"""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import stat

try:
    HERE = Path(__file__).resolve().parent
except NameError:  # PRE-4 Verified exec has no __file__: siblings are INJECTED by bind_dependencies, never read from disk
    HERE = None
DIGEST = re.compile(r"sha256:[a-f0-9]{64}\Z")
UNIT = re.compile(r"qa525-[a-f0-9]{16}-(?:api|web)\.scope\Z")
MAX_BYTES = 4096


class Stop(Exception):
    """Fixed fail-closed code; never contains external text."""


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


journal = _load("qa525_vnext_journal_for_scope_store", "qa525-vnext-journal.py") if HERE is not None else None

SIBLING_MODULES = {"journal": "qa525-vnext-journal.py"}
_BOUND = []


def bind_dependencies(mapping):
    """PRE-4 ONLY: receive the exact sibling modules (one verified instance per file, shared by the whole flight)."""
    if (HERE is not None or _BOUND or type(mapping) is not dict or set(mapping) != set(SIBLING_MODULES)
            or any(type(m).__name__ != "module" or type(m).__module__ != "builtins" for m in mapping.values())):
        raise Stop("DEPENDENCY_BINDING_INVALID")
    globals().update(mapping)
    _BOUND.append(True)


def _path(lease):
    return Path(lease.root) / ("scopes-" + lease.nonce + ".json")


def _canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n").encode()


def _unique_json_keys(pairs):
    """Reject ambiguity even when duplicate keys resolve to the same value."""
    result = {}
    for key, value in pairs:
        if key in result:
            raise Stop("SCOPE_MANIFEST_INVALID")
        result[key] = value
    return result


def _lease(lease):
    try:
        if journal.lease_is_held(lease) is not True:
            raise Stop("SCOPE_LEASE_NOT_HELD")
    except Stop:
        raise
    except Exception:
        raise Stop("SCOPE_LEASE_NOT_HELD") from None


def _expected(lease, admission_sha256, units):
    if type(admission_sha256) is not str or DIGEST.fullmatch(admission_sha256) is None:
        raise Stop("SCOPE_ADMISSION_INVALID")
    if (type(units) not in (list, tuple) or len(units) != 2
            or len(set(units)) != 2
            or any(type(unit) is not str or UNIT.fullmatch(unit) is None for unit in units)
            or not any(unit.endswith("-api.scope") for unit in units)
            or not any(unit.endswith("-web.scope") for unit in units)):
        raise Stop("SCOPE_UNITS_INVALID")
    ordered = sorted(units, key=lambda item: (not item.endswith("-api.scope"), item))
    body = {"schema": "qa525-scope-manifest-v1", "nonce": lease.nonce,
            "admission_sha256": admission_sha256, "units": ordered,
            "may_apply": False, "may_install": False}
    return {**body, "manifest_sha256": "sha256:" + hashlib.sha256(_canonical(body)).hexdigest()}


def _fsync_dir(path):
    try:
        fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_CLOEXEC)
        try:
            os.fsync(fd)
        finally:
            os.close(fd)
    except OSError:
        raise Stop("SCOPE_DIRECTORY_FSYNC_FAILED") from None


def _read(path):
    try:
        info = path.lstat()
        if (not stat.S_ISREG(info.st_mode) or stat.S_ISLNK(info.st_mode)
                or info.st_uid != os.geteuid() or info.st_gid != os.getegid()
                or stat.S_IMODE(info.st_mode) != 0o600 or info.st_nlink != 1
                or info.st_size > MAX_BYTES):
            raise Stop("SCOPE_MANIFEST_UNTRUSTED")
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        try:
            opened = os.fstat(fd)
            if (opened.st_dev, opened.st_ino) != (info.st_dev, info.st_ino):
                raise Stop("SCOPE_MANIFEST_CHANGED")
            raw = os.read(fd, MAX_BYTES + 1)
            if len(raw) > MAX_BYTES:
                raise Stop("SCOPE_MANIFEST_UNTRUSTED")
        finally:
            os.close(fd)
        if (path.lstat().st_dev, path.lstat().st_ino) != (info.st_dev, info.st_ino):
            raise Stop("SCOPE_MANIFEST_CHANGED")
        parsed = json.loads(
            raw, object_pairs_hook=_unique_json_keys,
            parse_constant=lambda _: (_ for _ in ()).throw(
                Stop("SCOPE_MANIFEST_INVALID")))
        # The exact bytes written by initialize() must be the bytes consumed.
        # A round trip rejects extra whitespace, reordered keys, duplicate
        # keys, non-finite numbers and an otherwise equivalent altered file.
        if type(parsed) is not dict or _canonical(parsed) != raw:
            raise Stop("SCOPE_MANIFEST_INVALID")
        return parsed
    except Stop:
        raise
    except (OSError, ValueError, UnicodeDecodeError, TypeError, RecursionError,
            OverflowError):
        raise Stop("SCOPE_MANIFEST_INVALID") from None


def initialize(lease, admission_sha256, units, checkpoint=None):
    """Write the complete manifest once, fsyncing file and parent directory."""
    _lease(lease)
    record = _expected(lease, admission_sha256, units)
    path = _path(lease)
    fd = None
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW
                     | os.O_CLOEXEC, 0o600)
        if checkpoint:
            checkpoint("scope_opened")
        raw = _canonical(record)
        written = 0
        while written < len(raw):
            count = os.write(fd, raw[written:])
            if count <= 0:
                raise Stop("SCOPE_MANIFEST_WRITE_FAILED")
            written += count
        os.fsync(fd)
        if checkpoint:
            checkpoint("scope_fsynced")
    except FileExistsError:
        raise Stop("SCOPE_MANIFEST_EXISTS") from None
    except Stop:
        raise
    except OSError:
        raise Stop("SCOPE_MANIFEST_WRITE_FAILED") from None
    finally:
        if fd is not None:
            os.close(fd)
    _fsync_dir(Path(lease.root))
    if _read(path) != record:
        raise Stop("SCOPE_MANIFEST_VERIFY_FAILED")
    return record


def require_recorded(lease, admission_sha256, unit):
    """Fail unless ``unit`` is in the exact manifest for this held flight."""
    _lease(lease)
    expected = _expected(lease, admission_sha256,
                         [unit[:-9] + "api.scope", unit[:-9] + "web.scope"])
    actual = _read(_path(lease))
    if actual != expected or unit not in actual["units"]:
        raise Stop("SCOPE_UNIT_NOT_RECORDED")
    return actual["manifest_sha256"]


if __name__ == "__main__":
    raise SystemExit("QA525_SCOPE_STORE_LIBRARY_ONLY_NO_EXECUTION")
