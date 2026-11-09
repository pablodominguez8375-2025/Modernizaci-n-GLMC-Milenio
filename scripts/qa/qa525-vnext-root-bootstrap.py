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
NAME_H = re.compile(r"[a-z0-9][a-z0-9_-]{0,31}\Z")
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
    def pairs(items):
        keys = [k for k, _ in items]
        if len(keys) != len(set(keys)):
            raise ValueError("duplicate key")
        return dict(items)
    try:
        manifest = json.loads(manifest_bytes, object_pairs_hook=pairs)
    except (ValueError, TypeError):
        raise Stop("BOOT_MANIFEST_INVALID") from None
    if (not isinstance(manifest, dict)
            or set(manifest) not in ({"files", "interpreter"}, {"files", "helpers", "interpreter"})
            or json.dumps(manifest, sort_keys=True, separators=(",", ":")).encode() != manifest_bytes):
        raise Stop("BOOT_MANIFEST_INVALID")  # exact keys, canonical form only
    helpers = manifest.get("helpers", {})
    if (not isinstance(helpers, dict) or len(helpers) > 8
            or any(NAME_H.fullmatch(k) is None or type(v) is not dict or set(v) != {"path", "sha256"}
                   or type(v["path"]) is not str or not os.path.isabs(v["path"])
                   or os.path.normpath(v["path"]) != v["path"]
                   or type(v["sha256"]) is not str or SHA.fullmatch(v["sha256"]) is None
                   for k, v in helpers.items())):
        raise Stop("BOOT_MANIFEST_INVALID")
    files = manifest.get("files") if isinstance(manifest, dict) else None
    interp = manifest.get("interpreter") if isinstance(manifest, dict) else None
    if (not isinstance(files, dict) or not files or not isinstance(interp, dict)
            or set(interp) != {"path", "sha256"} or SHA.fullmatch(str(interp["sha256"])) is None
            or not os.path.isabs(str(interp["path"]))
            or any(NAME.fullmatch(k) is None or type(v) is not str or SHA.fullmatch(v) is None
                   for k, v in files.items())):
        raise Stop("BOOT_MANIFEST_INVALID")
    return files, interp, helpers


_TOKEN = object()


class Verified:
    """Opaque product of verify_install(): the ONLY thing load_module() accepts."""
    __slots__ = ("_code", "record")

    def __init__(self, token, code, record):
        if token is not _TOKEN:
            raise Stop("BOOT_VERIFIED_FORGED")
        object.__setattr__(self, "_code", dict(code))
        object.__setattr__(self, "record", dict(record))

    def __setattr__(self, name, value):
        raise Stop("BOOT_VERIFIED_FORGED")


def _interpreter_real():
    return os.path.realpath(sys.executable)


def verify_install(install_dir, manifest_bytes, external_pin, *, uid=0, anchor="/"):
    """Return {name: verified bytes} and the trust record. Raises Stop before anything is loaded."""
    files, interp, helpers = check_manifest_pin(manifest_bytes, external_pin)
    if sys.flags.isolated != 1 or sys.flags.no_site != 1 or sys.flags.ignore_environment != 1:
        raise Stop("BOOT_NOT_ISOLATED")  # requires python -I -S
    _safe_dir(install_dir, uid, anchor)
    real = _interpreter_real()
    if real != os.path.realpath(interp["path"]):
        raise Stop("BOOT_INTERPRETER_MISMATCH")
    _safe_dir(os.path.dirname(real), 0)  # the OS owns the interpreter: root-owned, from /
    _read_verified(real, 0, interp["sha256"], MAX_INTERPRETER)
    for hname, h in sorted(helpers.items()):  # OS-owned helper binaries (e.g. ssh-keygen), hashed too
        hreal = os.path.realpath(h["path"])
        if hreal != h["path"]:
            raise Stop("BOOT_HELPER_PATH_UNSAFE")  # no symlinks in the pinned helper path
        _safe_dir(os.path.dirname(hreal), 0)
        _read_verified(hreal, 0, h["sha256"], MAX_INTERPRETER)
    code = {name: _read_verified(os.path.join(install_dir, name), uid, want)
            for name, want in sorted(files.items())}
    record = {"schema": "qa525-pre-root-trust-v1", "manifest_pin": external_pin,
              "interpreter": {"real": real, "sha256": interp["sha256"]},
              "files": dict(sorted(files.items())),
              "helpers": {k: dict(v) for k, v in sorted(helpers.items())}}
    return Verified(_TOKEN, code, record)


def load_module(verified, name):
    """Compile ONLY from the bytes held by a Verified object; never imports from disk."""
    if type(verified) is not Verified:
        raise Stop("BOOT_VERIFIED_FORGED")
    code = object.__getattribute__(verified, "_code")
    if name not in code:
        raise Stop("BOOT_MODULE_UNKNOWN")
    import types
    mod = types.ModuleType("qa525_boot_" + name[:-3].replace("-", "_"))
    exec(compile(code[name], "<verified:%s>" % name, "exec"), mod.__dict__)
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
    dfd = os.open(evidence_dir, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        os.fsync(dfd)  # the directory entry itself must survive a power cut
    finally:
        os.close(dfd)
    return name


LAUNCHER = "qa525-vnext-root-launcher.py"
EVIDENCE_DIR = "/var/lib/pmgm-qa525/pre-root-trust"


def launch(argv, stdin, *, euid, uid=0, anchor="/", evidence_dir=EVIDENCE_DIR):
    """`python3 -I -S bootstrap.py run INSTALL_DIR MANIFEST` (root). The pin is read from stdin ONLY
    (typed by the Sponsor from Bitwarden), never argv/env/file. Order: root -> pin -> verify every byte
    -> durable pre-root-trust record -> ONLY THEN compile the launcher from the verified bytes and hand
    it a loader that cannot reach disk. Any deviation is a fixed STOP before a single module exists."""
    if len(argv) != 4 or argv[1] != "run":
        raise Stop("BOOT_USAGE")
    if euid != 0:
        raise Stop("BOOT_NOT_ROOT")
    line = stdin.readline(100)
    pin = line[:-1] if line.endswith("\n") else line
    if stdin.read(1) != "":
        raise Stop("BOOT_PIN_INVALID")  # exactly one short line, nothing after it
    with open(argv[3], "rb") as f:
        manifest = f.read(MAX_FILE)
    verified = verify_install(argv[2], manifest, pin, uid=uid, anchor=anchor)
    write_record(evidence_dir, verified.record, uid=uid, anchor=anchor)
    launcher = load_module(verified, LAUNCHER)
    return launcher.launch(lambda name: load_module(verified, name), dict(verified.record))


def main(argv):
    """`python3 -I -S bootstrap.py check INSTALL_DIR MANIFEST PIN`: read-only check run by `soporte`
    WITHOUT sudo, before any sudo. Prints a fixed code only; never executes the verified modules."""
    if len(argv) == 4 and argv[1] == "run":
        try:
            launch(argv, sys.stdin, euid=os.geteuid())
            print("BOOT_RUN_DONE")
            return 0
        except Stop as exc:
            print(str(exc))
        except OSError:
            print("BOOT_IO_ERROR")
        return 1
    if len(argv) != 5 or argv[1] != "check":
        print("BOOT_USAGE")
        return 2
    try:
        with open(argv[3], "rb") as f:
            manifest = f.read(MAX_FILE)
        verified = verify_install(argv[2], manifest, argv[4], uid=0)
        print("BOOT_CHECK_OK manifest_pin=%s files=%d" % (verified.record["manifest_pin"],
                                                          len(verified.record["files"])))
        return 0
    except Stop as exc:
        print(str(exc))
    except OSError:
        print("BOOT_IO_ERROR")
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
