#!/usr/bin/env python3
"""QA525 B7: byte-pin audit for the full Compose stack. READ-ONLY / INACTIVE.

Nothing runs on import. The caller injects a trusted read-only descriptor port;
there is no default POSIX adapter, root escalation, network, Docker or APPLY.
Hash equality only proves consistency with caller-provided pins, NOT that those
pins came from a signed OPS/APP authorization. Root ownership does not sign code.
"""
import hashlib
import hmac
import json
import re
import stat

TARGETS = ("api", "web")
MINIMUM = {"api": 6, "web": 5}
MAX_LAYERS = 64
MAX_PATH = 512
MAX_FILE_BYTES = 4 * 1024 * 1024
BLOCK = 65536
SHA = re.compile(r"sha256:[a-f0-9]{64}\Z")
STATUS = "QA525_COMPOSE_BYTES_MATCH_UNAUTHENTICATED_NO_APPLY"
REQUIRED_PORT = ("euid", "lstat", "open_ro", "fstat", "read", "close")


class Stop(Exception):
    """Fixed codes only. Never include a path, digest or external bytes."""


def _safe_path(value):
    if (type(value) is not str or not 1 < len(value) <= MAX_PATH
            or not value.startswith("/") or "\x00" in value or value.endswith("/")):
        raise Stop("COMPOSE_PATH_INVALID")
    parts = value.split("/")[1:]
    if any(part in ("", ".", "..") for part in parts):
        raise Stop("COMPOSE_PATH_INVALID")
    return value


def _parents(path):
    parts = path.split("/")[1:-1]
    result, prefix = ["/"], ""
    for part in parts:
        prefix += "/" + part
        result.append(prefix)
    return result


def _ident(value):
    try:
        return tuple(getattr(value, "st_" + key) for key in (
            "dev", "ino", "uid", "gid", "mode", "nlink", "size", "mtime_ns", "ctime_ns"))
    except (AttributeError, TypeError):
        raise Stop("COMPOSE_STAT_INVALID") from None


def _trusted(stat_value, *, directory):
    ident = _ident(stat_value)
    _, _, uid, gid, mode, links, size, _, _ = ident
    if (type(uid) is not int or type(gid) is not int or uid != 0 or gid != 0
            or type(mode) is not int or mode & 0o022
            or type(links) is not int or links <= 0
            or (stat.S_ISDIR(mode) if directory else stat.S_ISREG(mode)) is not True):
        raise Stop("COMPOSE_ROOT_PATH_UNTRUSTED")
    if not directory and (links != 1 or type(size) is not int
                          or not 0 < size <= MAX_FILE_BYTES):
        raise Stop("COMPOSE_FILE_UNTRUSTED")
    return ident


def _validate(layers, pins):
    if type(layers) is not dict or set(layers) != set(TARGETS):
        raise Stop("COMPOSE_LAYERS_INVALID")
    union = set()
    base = None
    for name in TARGETS:
        paths = layers[name]
        if (type(paths) is not list
                or not MINIMUM[name] <= len(paths) <= MAX_LAYERS):
            raise Stop("COMPOSE_LAYERS_INVALID")
        if len(set(map(str, paths))) != len(paths):
            raise Stop("COMPOSE_LAYERS_INVALID")
        for path in paths:
            _safe_path(path)
            union.add(path)
        if base is None:
            base = paths[0]
        elif paths[0] != base:
            raise Stop("COMPOSE_BASE_MISMATCH")
    if type(pins) is not dict or set(pins) != union:
        raise Stop("COMPOSE_PINS_NOT_EXACT")
    if any(type(value) is not str or SHA.fullmatch(value) is None
           for value in pins.values()):
        raise Stop("COMPOSE_DIGEST_INVALID")
    return union


def _port(port):
    if any(not callable(getattr(port, name, None)) for name in REQUIRED_PORT):
        raise Stop("COMPOSE_PORT_INVALID")
    try:
        root = port.euid()
    except Exception:
        raise Stop("COMPOSE_PORT_UNTRUSTED") from None
    if type(root) is not int or root != 0:
        raise Stop("COMPOSE_PORT_UNTRUSTED")


def _read_checked(port, path, expected):
    try:
        for parent in _parents(path):
            _trusted(port.lstat(parent), directory=True)
        initial = _trusted(port.lstat(path), directory=False)
    except Stop:
        raise
    except Exception:
        raise Stop("COMPOSE_PATH_CHECK_FAILED") from None

    fd = None
    try:
        # Physical port MUST use O_RDONLY|O_NOFOLLOW|O_CLOEXEC|O_NONBLOCK.
        # The open descriptor, not the pathname, is the only read source.
        fd = port.open_ro(path)
        if type(fd) is not int or fd < 0:
            raise Stop("COMPOSE_DESCRIPTOR_INVALID")
        if _trusted(port.fstat(fd), directory=False) != initial:
            raise Stop("COMPOSE_DESCRIPTOR_CHANGED")
        size = 0
        digest = hashlib.sha256()
        while True:
            part = port.read(fd, BLOCK)
            if type(part) is not bytes or len(part) > BLOCK:
                raise Stop("COMPOSE_READ_INVALID")
            if not part:
                break
            size += len(part)
            if size > MAX_FILE_BYTES or size > initial[6]:
                raise Stop("COMPOSE_READ_TOO_LARGE")
            digest.update(part)
        if size != initial[6]:
            raise Stop("COMPOSE_SIZE_CHANGED")
        final = _trusted(port.fstat(fd), directory=False)
        if final != initial or _trusted(port.lstat(path), directory=False) != initial:
            raise Stop("COMPOSE_DESCRIPTOR_CHANGED")
        actual = "sha256:" + digest.hexdigest()
        if not hmac.compare_digest(actual, expected):
            raise Stop("COMPOSE_DIGEST_MISMATCH")
    except Stop:
        raise
    except Exception:
        raise Stop("COMPOSE_READ_FAILED") from None
    finally:
        if fd is not None and type(fd) is int and fd >= 0:
            try:
                port.close(fd)
            except Exception:
                raise Stop("COMPOSE_CLOSE_FAILED") from None


def inspect(*, layers, pinned_sha256, port):
    """Read every distinct pinned file exactly once, including all old overlays.

    A future signed B7 caller must authenticate pinned_sha256, the code
    executing this library and the port itself before using this information.
    """
    union = _validate(layers, pinned_sha256)
    _port(port)
    # Deterministic ordering independent of data dict insertion.
    for path in sorted(union):
        _read_checked(port, path, pinned_sha256[path])
    description = {
        "api": list(layers["api"]), "web": list(layers["web"]),
        "sha256": {path: pinned_sha256[path] for path in sorted(union)},
    }
    manifest_hash = "sha256:" + hashlib.sha256(
        (json.dumps(description, sort_keys=True, separators=(",", ":")) + "\n").encode()
    ).hexdigest()
    return {
        "status": STATUS,
        "manifest_sha256": manifest_hash,
        "files_verified": len(union),
        "api_layers": len(layers["api"]),
        "web_layers": len(layers["web"]),
        "all_files_match_caller_pins": True,
        "pins_authenticated_here": False,
        "port_authenticated_here": False,
        "sponsor_approved_here": False,
        "may_apply": False,
        "may_install": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_COMPOSE_BYTES_LIBRARY_ONLY_NO_HOST_OR_APPLY")
