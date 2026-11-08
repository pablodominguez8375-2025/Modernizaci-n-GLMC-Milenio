#!/usr/bin/env python3
"""QA525 root bootstrap TRUST CHECK (PRE-4). Stdlib only. INACTIVE: installs/executes nothing.

Authenticates, BEFORE any privileged module is loaded, the install directory, every file named by an
installer manifest and the interpreter, against a manifest pin supplied OUT OF BAND (never read from
the package, never a self-hash). Code is compiled from the very bytes that were hashed (no re-read of
the path => no TOCTOU), so a sibling swapped after verification is never executed.
It grants NO authority: no GO, no nonce consumption, no sudo, no network, no commands.
"""
import hashlib
import hmac
import json
import os
import re
import stat
import sys

SHA = re.compile(r"[0-9a-f]{64}\Z")
NAME = re.compile(r"[A-Za-z0-9_-]{1,64}\.py\Z")
MAX_FILE = 4 * 1024 * 1024
MAX_INTERPRETER = 128 * 1024 * 1024


class Stop(Exception):
    """Opaque STOP code."""


def _safe_dir(path, uid, anchor="/"):
    if not isinstance(path, str) or not os.path.isabs(path) or os.path.normpath(path) != path:
        raise Stop("BOOT_PATH_INVALID")
    if path != anchor and not path.startswith(anchor.rstrip("/") + "/"):
        raise Stop("BOOT_PATH_INVALID")
    cur = anchor
    for part in [p for p in path[len(anchor):].split("/") if p]:
        cur = os.path.join(cur, part)
        info = os.lstat(cur)
        if not stat.S_ISDIR(info.st_mode):  # symlinks/files in the path are refused
            raise Stop("BOOT_DIR_UNSAFE")
        if (info.st_uid not in (uid, 0) if cur != path else info.st_uid != uid) or info.st_mode & 0o022:
            raise Stop("BOOT_DIR_UNSAFE")


def _read_verified(path, uid, want, limit=MAX_FILE):
    flags = os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC
    fd = os.open(path, flags)
    try:
        info = os.fstat(fd)
        if (not stat.S_ISREG(info.st_mode) or info.st_uid != uid or info.st_mode & 0o022
                or info.st_nlink != 1 or not 0 < info.st_size <= limit):
            raise Stop("BOOT_FILE_UNSAFE")
        data = b""
        while len(data) < info.st_size:
            block = os.read(fd, info.st_size - len(data))
            if not block:
                raise Stop("BOOT_FILE_UNSAFE")
            data += block
    finally:
        os.close(fd)
    if not hmac.compare_digest(hashlib.sha256(data).hexdigest(), want):
        raise Stop("BOOT_HASH_MISMATCH")
    return data


def check_manifest_pin(manifest_bytes, external_pin):
    """The pin must come from the PO (Bitwarden / typed by hand), not from the package itself."""
    if type(external_pin) is not str or SHA.fullmatch(external_pin) is None:
        raise Stop("BOOT_PIN_INVALID")
    if not hmac.compare_digest(hashlib.sha256(manifest_bytes).hexdigest(), external_pin):
        raise Stop("BOOT_MANIFEST_PIN_MISMATCH")
    try:
        manifest = json.loads(manifest_bytes)
    except ValueError:
        raise Stop("BOOT_MANIFEST_INVALID") from None
    files = manifest.get("files") if isinstance(manifest, dict) else None
    interp = manifest.get("interpreter") if isinstance(manifest, dict) else None
    if (not isinstance(files, dict) or not files or not isinstance(interp, dict)
            or set(interp) != {"path", "sha256"} or SHA.fullmatch(str(interp["sha256"])) is None
            or not os.path.isabs(str(interp["path"]))
            or any(NAME.fullmatch(k) is None or type(v) is not str or SHA.fullmatch(v) is None
                   for k, v in files.items())):
        raise Stop("BOOT_MANIFEST_INVALID")
    return files, interp


def verify_install(install_dir, manifest_bytes, external_pin, *, uid=0, interpreter_real=None, anchor="/"):
    """Return {name: verified bytes} and the trust record. Raises Stop before anything is loaded."""
    files, interp = check_manifest_pin(manifest_bytes, external_pin)
    if sys.flags.isolated != 1 or sys.flags.no_site != 1 or sys.flags.ignore_environment != 1:
        raise Stop("BOOT_NOT_ISOLATED")  # requires python -I -S
    _safe_dir(install_dir, uid, anchor)
    real = interpreter_real if interpreter_real is not None else os.path.realpath(sys.executable)
    if real != os.path.realpath(interp["path"]):
        raise Stop("BOOT_INTERPRETER_MISMATCH")
    _safe_dir(os.path.dirname(real), 0)  # the OS owns the interpreter: root-owned, from /
    _read_verified(real, 0, interp["sha256"], MAX_INTERPRETER)
    code = {name: _read_verified(os.path.join(install_dir, name), uid, want)
            for name, want in sorted(files.items())}
    record = {"schema": "qa525-pre-root-trust-v1", "manifest_pin": external_pin,
              "interpreter": {"real": real, "sha256": interp["sha256"]},
              "files": dict(sorted(files.items()))}
    return code, record


def load_module(code, name, verified):
    """Compile ONLY from verified bytes; never imports from disk."""
    if name not in verified:
        raise Stop("BOOT_MODULE_UNKNOWN")
    import types
    mod = types.ModuleType("qa525_boot_" + name[:-3].replace("-", "_"))
    exec(compile(verified[name], "<verified:%s>" % name, "exec"), mod.__dict__)
    return mod


def write_record(evidence_dir, record, uid=0, anchor="/"):
    """Durable pre-root-trust evidence (O_EXCL). Required BEFORE any nonce is consumed."""
    _safe_dir(evidence_dir, uid, anchor)
    body = json.dumps(record, sort_keys=True, separators=(",", ":")).encode()
    name = "pre-root-trust-%s.json" % hashlib.sha256(body).hexdigest()
    fd = os.open(os.path.join(evidence_dir, name),
                 os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC, 0o600)
    try:
        off = 0
        while off < len(body):
            off += os.write(fd, body[off:])
        os.fsync(fd)
    finally:
        os.close(fd)
    return name
