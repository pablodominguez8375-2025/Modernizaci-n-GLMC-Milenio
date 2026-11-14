#!/usr/bin/env python3
"""QA525 PRE-4 installer-manifest BUILDER (offline, no root, no network, INACTIVE).

Reads ONE exact signed release asset (the outer ZIP whose sha256 the reviewer already knows) entirely in
memory, never extracts it and never executes any member. It derives the canonical PRE-4 installer manifest
(v3: exact keys config/files/helpers/interpreter) that `qa525-vnext-root-bootstrap.py` verifies.

It grants NO authority. Sigstore/attestation is NOT verified here (SIGSTORE_VERIFIED_BY_BUILDER=false): the
asset must already have been verified independently. The printed manifest sha256 is only a CANDIDATE: the
Sponsor fixes the real pin out of band (Bitwarden) after recomputing it on an independent machine.
Installing INSTALL_DIR as root is a DIFFERENT phase and is never done here. Config content, signers and
fingerprints are never printed: only file names and sha256.
"""
import argparse
import ast
import hashlib
import io
import json
import math
import os
import re
import stat
import sys
import zipfile
from pathlib import PurePosixPath

SHA = re.compile(r"[0-9a-f]{64}\Z")
SHA_P = re.compile(r"sha256:[0-9a-f]{64}\Z")
GITSHA = re.compile(r"[0-9a-f]{40}\Z")
FP = re.compile(r"SHA256:[A-Za-z0-9+/]{43}\Z")
REPO_ID = re.compile(r"[1-9][0-9]{0,19}\Z")
NAME = re.compile(r"[A-Za-z0-9_-]{1,64}\.py\Z")
MODULE = re.compile(r"qa525-vnext-[a-z0-9-]{1,60}\.py\Z")
CONFIG_FILE = re.compile(r"[A-Za-z0-9_-]{1,64}\.json\Z")
MANIFEST_LINE = re.compile(r"([0-9a-f]{64})  (\./[^\n]+)\Z")
SCRIPTS = "scripts/qa/"
LAUNCHER = "qa525-vnext-root-launcher.py"
CONFIG_MODULE = "qa525-vnext-root-launch-config.py"
BOOTSTRAP = "qa525-vnext-root-bootstrap.py"
HELPERS = ("gh", "ssh-keygen")  # both are pinned by the launcher (_helper_pin)
EXPECTED_CLOSURE = frozenset({
    "qa525-vnext-admission-bridge.py",
    "qa525-vnext-b7-held-readonly.py",
    "qa525-vnext-b7-root-boundary.py",
    "qa525-vnext-baseline-plan.py",
    "qa525-vnext-containment.py",
    "qa525-vnext-evidence.py",
    "qa525-vnext-flight.py",
    "qa525-vnext-g6-anchor.py",
    "qa525-vnext-g6-v2-captor.py",
    "qa525-vnext-g6-v2-evidence.py",
    "qa525-vnext-g6-v2-producer.py",
    "qa525-vnext-g6-v2-run.py",
    "qa525-vnext-g6-v2-verifier.py",
    "qa525-vnext-go-cut-contract.py",
    "qa525-vnext-go-nonce-journal.py",
    "qa525-vnext-health.py",
    "qa525-vnext-held-flight-bridge.py",
    "qa525-vnext-held-physical-runner.py",
    "qa525-vnext-host-adapters.py",
    "qa525-vnext-journal.py",
    "qa525-vnext-observer.py",
    "qa525-vnext-ops-signature.py",
    "qa525-vnext-posix-port.py",
    "qa525-vnext-root-apiweb-child.py",
    "qa525-vnext-root-apiweb-effects.py",
    "qa525-vnext-root-apiweb-host.py",
    "qa525-vnext-root-authenticity.py",
    "qa525-vnext-root-boundary-recv.py",
    "qa525-vnext-root-entry.py",
    "qa525-vnext-root-g6-backup.py",
    "qa525-vnext-root-g6-child-core.py",
    "qa525-vnext-root-g6-init.py",
    "qa525-vnext-root-g6-preflight.py",
    "qa525-vnext-root-g6-restore.py",
    "qa525-vnext-root-g6-review.py",
    "qa525-vnext-root-go-verify.py",
    "qa525-vnext-root-launch-config.py",
    "qa525-vnext-root-launcher-child.py",
    "qa525-vnext-root-launcher.py",
    "qa525-vnext-root-locks.py",
    "qa525-vnext-root-plane.py",
    "qa525-vnext-scope-store.py",
    "qa525-vnext-stage-store.py",
    "qa525-vnext-state-reader.py",
    "qa525-vnext-switch.py",
    "qa525-vnext-terminal.py",
    "qa525-vnext-trust-binding.py",
    "qa525-vnext-verify.py",
})
ALLOWED_FRAGMENTS = frozenset({  # the only non-literal module-name shapes tolerated inside the closure
    "qa525-vnext-root-g6-%s.py",  # CHILD_MODULES template; every expansion is enumerated by CHILDREN
    "qa525-vnext-[a-z0-9-]{1,60}\\.py\\Z",  # the launcher's own MODULE pattern
})
MAX_CONFIG = 64 * 1024
MAX_MEMBER = 1024 ** 3
MAX_TOTAL = 2 * 1024 ** 3
MAX_MANIFEST = 5_000_000
STORAGE_MARK = "STORAGE=SIGNED_REBUILT_SILO_ARCHIVE_SYNTHETIC_QA"
CHILDREN = {"LAUNCHER_ONLY": "qa525-vnext-root-launcher-child.py",
            "API_WEB_QA_ONLY": "qa525-vnext-root-apiweb-child.py",
            **{"G6:" + c: "qa525-vnext-root-g6-%s.py" % c
               for c in ("preflight", "init", "backup", "restore", "review")}}
CONFIG_KEYS = {"pins", "launcher_pins", "allowed_signers", "go_fingerprint", "ops_v3_sha256",
               "app_subject_sha256", "inbox", "temp_root", "socket_path", "journal_root", "lease_dir",
               "peer_uid", "timeout", "children", "g6_pins", "authenticity", "apiweb"}
PINS_KEYS = {"ops_source_sha", "app_source_sha", "app_archive_sha256", "plan_sha256", "admission_sha256",
             "policy_hash"}
LAUNCHER_PIN_KEYS = {"release_asset_sha256", "old_launcher_sha256", "candidate_operator_sha256"}
AUTH_KEYS = {"ops_allowed_signers", "ops_signature_sha256", "app_bundle_sha256", "trusted_root_sha256",
             "app_archive_path", "drop_uid", "drop_gid", "source_repository_id"}


class Stop(Exception):
    """Fixed code only; nothing from inputs is echoed."""


def _pairs(items):
    keys = [k for k, _ in items]
    if len(keys) != len(set(keys)):
        raise ValueError("duplicate key")
    return dict(items)


def _refuse(_):
    raise ValueError("non-finite number")


# ---------------------------------------------------------------- the signed release (in memory)
def _check_member(info):
    raw = info.filename
    path = PurePosixPath(raw)
    if (not raw or raw.startswith("/") or "\\" in raw or "\x00" in raw or len(raw) > 512
            or any(p in ("", ".", "..") for p in raw.rstrip("/").split("/"))
            or str(path) != raw.rstrip("/")):
        raise Stop("BUILD_UNSAFE_ARCHIVE_MEMBER")
    mode = info.external_attr >> 16
    if stat.S_ISLNK(mode):
        raise Stop("BUILD_ARCHIVE_SYMLINK")
    if not (info.is_dir() or not stat.S_IFMT(mode) or stat.S_ISREG(mode)):
        raise Stop("BUILD_ARCHIVE_FILE_TYPE")
    if info.file_size > MAX_MEMBER:
        raise Stop("BUILD_ARCHIVE_SIZE")


def _members(archive):
    seen, total, out = set(), 0, []
    for info in archive.infolist():
        _check_member(info)
        key = info.filename.rstrip("/")
        if key in seen:
            raise Stop("BUILD_DUPLICATE_MEMBER")
        seen.add(key)
        total += info.file_size
        if total > MAX_TOTAL:
            raise Stop("BUILD_ARCHIVE_SIZE")
        if not info.is_dir():
            out.append(info)
    return out


def _digest(archive, info):
    h, size = hashlib.sha256(), 0
    with archive.open(info) as stream:
        while True:
            chunk = stream.read(1 << 20)
            if not chunk:
                break
            size += len(chunk)
            if size > info.file_size or size > MAX_MEMBER:
                raise Stop("BUILD_ARCHIVE_SIZE")
            h.update(chunk)
    if size != info.file_size:
        raise Stop("BUILD_ARCHIVE_SIZE")
    return h.hexdigest()


def _read(archive, info, limit):
    with archive.open(info) as stream:
        data = stream.read(limit + 1)
    if len(data) > limit:
        raise Stop("BUILD_ARCHIVE_SIZE")
    return data


def load_release(asset_bytes, expected_asset_sha256, source_sha):
    """Return ({package_relative_path: sha256}, {name: bytes of scripts/qa/qa525-vnext-*.py}).
    Integrity only: the asset digest is NOT provenance."""
    if SHA.fullmatch(expected_asset_sha256 or "") is None or GITSHA.fullmatch(source_sha or "") is None:
        raise Stop("BUILD_INPUT_INVALID")
    if hashlib.sha256(asset_bytes).hexdigest() != expected_asset_sha256:
        raise Stop("BUILD_ASSET_DIGEST_MISMATCH")
    root = "Proyecto-Centenario-QA-srv01-" + source_sha[:12]
    try:
        outer = zipfile.ZipFile(io.BytesIO(asset_bytes))
        infos = _members(outer)
        names = {i.filename for i in infos}
        if names != {root + ".zip", root + ".zip.verification.json", "installable-provenance.sigstore.json"}:
            raise Stop("BUILD_OUTER_CONTENT_UNEXPECTED")
        inner_info = next(i for i in infos if i.filename == root + ".zip")
        inner = zipfile.ZipFile(io.BytesIO(_read(outer, inner_info, MAX_MEMBER)))
        files = {}
        for info in _members(inner):
            parts = info.filename.split("/")
            if parts[0] != root or len(parts) < 2:
                raise Stop("BUILD_PACKAGE_ROOT_UNEXPECTED")
            files["/".join(parts[1:])] = info
        build = _read(inner, files["BUILD-INFO.txt"], 65536).decode("utf-8").splitlines() \
            if "BUILD-INFO.txt" in files else None
        if build is None or "MANIFEST.sha256" not in files:
            raise Stop("BUILD_INFO_OR_MANIFEST_MISSING")
        if build.count("SOURCE_SHA=" + source_sha) != 1 or build.count(STORAGE_MARK) != 1:
            raise Stop("BUILD_SOURCE_BINDING_MISMATCH")
        expected = {}
        for line in _read(inner, files["MANIFEST.sha256"], MAX_MANIFEST).decode("utf-8").splitlines():
            m = MANIFEST_LINE.fullmatch(line)
            if m is None:
                raise Stop("BUILD_MANIFEST_NONCANONICAL")
            path = m.group(2)[2:]
            if (path == "MANIFEST.sha256" or path in expected
                    or any(s in ("", ".", "..") for s in path.split("/"))):
                raise Stop("BUILD_MANIFEST_PATH_INVALID")
            expected[path] = m.group(1)
        if not expected or set(expected) != set(files) - {"MANIFEST.sha256"}:
            raise Stop("BUILD_MANIFEST_FILE_SET_MISMATCH")
        for path, want in expected.items():
            if _digest(inner, files[path]) != want:
                raise Stop("BUILD_MANIFEST_CHECKSUM_MISMATCH")
        code = {}
        for path in expected:
            if path.startswith(SCRIPTS) and "/" not in path[len(SCRIPTS):] and MODULE.fullmatch(path[len(SCRIPTS):]) \
                    or path == SCRIPTS + BOOTSTRAP:
                code[path[len(SCRIPTS):]] = _read(inner, files[path], 4 * 1024 * 1024)
        return expected, code
    except (zipfile.BadZipFile, OSError, UnicodeDecodeError, KeyError, StopIteration, RuntimeError):
        raise Stop("BUILD_ARCHIVE_INVALID") from None


# ---------------------------------------------------------------- launcher closure (static, never executed)
def _names(source):
    """(module names mentioned as string constants, other strings containing the module prefix).
    Docstrings are skipped. A prefix fragment means a name may be built dynamically."""
    tree = ast.parse(source)
    skip = set()
    for node in ast.walk(tree):
        if isinstance(node, (ast.Module, ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
            body = node.body
            if body and isinstance(body[0], ast.Expr) and isinstance(getattr(body[0], "value", None), ast.Constant):
                skip.add(id(body[0].value))
    refs, frags = set(), set()
    for n in ast.walk(tree):
        if isinstance(n, ast.Constant) and isinstance(n.value, str) and id(n) not in skip:
            if MODULE.fullmatch(n.value):
                refs.add(n.value)
            elif "qa525-vnext" in n.value:
                frags.add(n.value)
    return refs, frags


def closure(code):
    """Transitive static closure from the launcher, config module and every scope child, then an exact-set
    comparison against the versioned EXPECTED_CLOSURE. Any dynamically built name (prefix fragment outside
    ALLOWED_FRAGMENTS) or any difference in either direction is a STOP, never a silently incomplete manifest."""
    roots = {LAUNCHER, CONFIG_MODULE, *CHILDREN.values()}
    todo, seen = sorted(roots), set()
    while todo:
        name = todo.pop()
        if name in seen:
            continue
        if name not in code:
            raise Stop("BUILD_CLOSURE_MEMBER_MISSING")
        seen.add(name)
        try:
            refs, frags = _names(code[name])
        except (SyntaxError, ValueError, RecursionError):
            raise Stop("BUILD_CLOSURE_UNPARSEABLE") from None
        if frags - ALLOWED_FRAGMENTS:
            raise Stop("BUILD_CLOSURE_DYNAMIC_NAME")
        todo.extend(sorted(r for r in refs if r not in seen and r != BOOTSTRAP))
    if any(NAME.fullmatch(n) is None for n in seen):
        raise Stop("BUILD_CLOSURE_NAME_INVALID")
    if seen != EXPECTED_CLOSURE:
        raise Stop("BUILD_CLOSURE_ALLOWLIST_MISMATCH")
    return seen


# ---------------------------------------------------------------- Sponsor config (shape only, never echoed)
def _m(value, pattern):
    return type(value) is str and pattern.fullmatch(value) is not None


def _path(value):
    return type(value) is str and value.startswith("/") and "\0" not in value and os.path.normpath(value) == value


def _signers(value):
    return type(value) is str and 0 < len(value) <= 4096 and value.isascii()


def _shape(value, keys):
    return type(value) is dict and set(value) == keys


def validate_config(raw):
    """Independent mirror of the audited launch-config contract (parity is asserted by the tests)."""
    if type(raw) is not bytes or not 0 < len(raw) <= MAX_CONFIG:
        raise Stop("BUILD_CONFIG_INVALID")
    try:
        data = json.loads(raw.decode("utf-8"), object_pairs_hook=_pairs, parse_constant=_refuse)
    except (ValueError, TypeError, RecursionError):
        raise Stop("BUILD_CONFIG_INVALID") from None
    if not _shape(data, CONFIG_KEYS):
        raise Stop("BUILD_CONFIG_INVALID")
    pins, lp, auth, g6 = data["pins"], data["launcher_pins"], data["authenticity"], data["g6_pins"]
    ok = (_shape(pins, PINS_KEYS) and _shape(lp, LAUNCHER_PIN_KEYS) and _shape(auth, AUTH_KEYS)
          and data["children"] == CHILDREN)
    ok = ok and (g6 is None or (_shape(g6, {"g6_command", "g6_operation_sha256"})
                                and g6["g6_command"] in ("preflight", "init", "backup", "restore", "review")
                                and _m(g6["g6_operation_sha256"], SHA_P)))
    ok = ok and (data["apiweb"] is None or type(data["apiweb"]) is dict)
    if not ok:
        raise Stop("BUILD_CONFIG_INVALID")
    ok = (all(_m(pins[k], GITSHA) for k in ("ops_source_sha", "app_source_sha"))
          and all(_m(pins[k], SHA_P) for k in ("app_archive_sha256", "plan_sha256", "admission_sha256", "policy_hash"))
          and _m(lp["release_asset_sha256"], SHA_P)
          and all(_m(lp[k], SHA) for k in ("old_launcher_sha256", "candidate_operator_sha256"))
          and _signers(data["allowed_signers"]) and _m(data["go_fingerprint"], FP)
          and all(_m(data[k], SHA_P) for k in ("ops_v3_sha256", "app_subject_sha256"))
          and all(_path(data[k]) for k in ("inbox", "temp_root", "socket_path", "journal_root", "lease_dir"))
          and _signers(auth["ops_allowed_signers"])
          and all(_m(auth[k], SHA_P) for k in ("ops_signature_sha256", "app_bundle_sha256", "trusted_root_sha256"))
          and _path(auth["app_archive_path"]) and _m(auth["source_repository_id"], REPO_ID))
    if not ok:
        raise Stop("BUILD_CONFIG_INVALID")
    for v in (data["peer_uid"], auth["drop_uid"], auth["drop_gid"]):
        if type(v) is not int or v <= 0:
            raise Stop("BUILD_CONFIG_INVALID")
    t = data["timeout"]
    if (auth["drop_uid"] == data["peer_uid"] or type(t) not in (int, float) or not math.isfinite(t)
            or not 0 < t <= 60):
        raise Stop("BUILD_CONFIG_INVALID")
    return data


# ---------------------------------------------------------------- manifest
def _hostpin(spec, with_name):
    """`[NAME=]ABSPATH=SHA256` read-only host inventory values (no file is opened here)."""
    parts = spec.split("=")
    if len(parts) != (3 if with_name else 2):
        raise Stop("BUILD_HOST_PIN_INVALID")
    path, digest = parts[-2], parts[-1]
    if not _path(path) or SHA.fullmatch(digest) is None:
        raise Stop("BUILD_HOST_PIN_INVALID")
    return (parts[0] if with_name else None), {"path": path, "sha256": digest}


def build_manifest(asset_bytes, *, asset_sha256, source_sha, config_name, config_bytes, interpreter, helpers):
    if CONFIG_FILE.fullmatch(config_name or "") is None:
        raise Stop("BUILD_CONFIG_NAME_INVALID")
    cfg = validate_config(config_bytes)
    if cfg["launcher_pins"]["release_asset_sha256"] != "sha256:" + asset_sha256:
        raise Stop("BUILD_CONFIG_ASSET_PIN_MISMATCH")
    _, code = load_release(asset_bytes, asset_sha256, source_sha)
    if BOOTSTRAP not in code or LAUNCHER not in code:
        raise Stop("BUILD_CLOSURE_MEMBER_MISSING")
    names = closure(code)
    if sorted(helpers) != sorted(HELPERS):
        raise Stop("BUILD_HELPERS_INCOMPLETE")
    manifest = {"config": {"name": config_name, "sha256": hashlib.sha256(config_bytes).hexdigest()},
                "files": {n: hashlib.sha256(code[n]).hexdigest() for n in sorted(names)},
                "helpers": {k: dict(helpers[k]) for k in sorted(helpers)},
                "interpreter": dict(interpreter)}
    blob = json.dumps(manifest, sort_keys=True, separators=(",", ":")).encode()
    return blob, {"bootstrap_sha256": hashlib.sha256(code[BOOTSTRAP]).hexdigest(),
                  "manifest_sha256": hashlib.sha256(blob).hexdigest(), "files": len(names)}


def _write_new(path, data):
    if not _path(path) or os.path.islink(os.path.dirname(path)):
        raise Stop("BUILD_OUTPUT_INVALID")
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    except OSError:
        raise Stop("BUILD_OUTPUT_INVALID") from None
    with os.fdopen(fd, "wb") as out:
        out.write(data)
        out.flush()
        os.fsync(out.fileno())


def _regular(path, limit):
    """Bounded read of a regular file: no symlink, same inode before/after open, size and mtime unchanged after
    the read (a candidate manifest must never take bytes from a file that changed under us)."""
    try:
        before = os.lstat(path)
        if not stat.S_ISREG(before.st_mode):
            raise Stop("BUILD_INPUT_NOT_REGULAR")
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    except OSError:
        raise Stop("BUILD_INPUT_NOT_REGULAR") from None
    with os.fdopen(fd, "rb") as f:
        try:
            opened = os.fstat(f.fileno())
            if (not stat.S_ISREG(opened.st_mode) or (opened.st_dev, opened.st_ino) != (before.st_dev, before.st_ino)
                    or opened.st_size > limit or opened.st_size != before.st_size):
                raise Stop("BUILD_INPUT_CHANGED")
            data = f.read(opened.st_size + 1)
            after = os.fstat(f.fileno())
        except OSError:
            raise Stop("BUILD_INPUT_NOT_REGULAR") from None
    if (len(data) != opened.st_size or (after.st_size, after.st_mtime_ns, after.st_ino) !=
            (opened.st_size, opened.st_mtime_ns, opened.st_ino)):
        raise Stop("BUILD_INPUT_CHANGED")
    return data


def main(argv):
    ap = argparse.ArgumentParser(prog="qa525-vnext-installer-manifest-build")
    ap.add_argument("--asset", help="outer signed release ZIP, already verified independently")
    ap.add_argument("--asset-sha256")
    ap.add_argument("--source-sha")
    ap.add_argument("--config", help="Sponsor config JSON (never printed)")
    ap.add_argument("--interpreter", help="ABSREALPATH=SHA256 from read-only host inventory")
    ap.add_argument("--helper", action="append", default=[], help="NAME=ABSREALPATH=SHA256 (gh, ssh-keygen)")
    ap.add_argument("--output", help="new file, created exclusively 0600")
    try:
        a = ap.parse_args(argv[1:])
    except SystemExit:
        print("BUILD_USAGE")
        return 2
    if os.geteuid() == 0:
        print("BUILD_MUST_NOT_RUN_AS_ROOT")
        return 1
    missing = [n for n, v in (("asset", a.asset), ("asset-sha256", a.asset_sha256), ("source-sha", a.source_sha),
                              ("config", a.config), ("interpreter", a.interpreter), ("output", a.output)) if not v]
    have = {h.split("=", 1)[0] for h in a.helper}
    missing += ["helper:" + h for h in HELPERS if h not in have]
    if missing:
        print("BUILD_MISSING_PHYSICAL_INPUTS " + ",".join(missing))
        return 3
    try:
        _, interp = _hostpin(a.interpreter, False)
        hp = {}
        for spec in a.helper:
            name, pin = _hostpin(spec, True)
            if name not in HELPERS or name in hp:
                raise Stop("BUILD_HOST_PIN_INVALID")
            hp[name] = pin
        blob, info = build_manifest(
            _regular(a.asset, 2 * 1024 ** 3), asset_sha256=a.asset_sha256, source_sha=a.source_sha,
            config_name=os.path.basename(a.config), config_bytes=_regular(a.config, MAX_CONFIG),
            interpreter=interp, helpers=hp)
        _write_new(a.output, blob)
    except Stop as exc:
        print(str(exc))
        return 1
    print("BUILD_OK SIGSTORE_VERIFIED_BY_BUILDER=false AUTHORITY=NONE files=%d bootstrap_sha256=%s "
          "candidate_manifest_sha256=%s" % (info["files"], info["bootstrap_sha256"], info["manifest_sha256"]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
