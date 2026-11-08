#!/usr/bin/env python3
"""QA525 G6 v2 producer library. INACTIVE, NEVER physical GO, NEVER executed here.

Clean-room implementation from the contract in docs/qa/QA525-G6-RESTIC-ROTATION.md.
It is NOT derived from the host v1 scripts. It has three separate modes:

  INIT     once; every target must be absent; creates key and repo with O_EXCL.
  BACKUP   later; repo and key must already exist and match independent pins;
           never initializes, truncates or overwrites; exports ciphertext only.
  RESTORE  into a brand-new isolated directory; verifier injected by the caller.

All evidence is canonical JSON written with O_EXCL under versioned v2 paths.
Nothing here reads or writes any v1 path or receipt. The Restic password file is
only ever passed by path; secret bytes are never placed in argv, logs or
exceptions. Fixed error codes only.
"""
from __future__ import annotations

import datetime as dt
import gzip
import hashlib
import hmac
import io
import json
import os
import re
import stat
import subprocess
import tarfile

SCHEMA_REVIEW = "qa525-g6-v2-review"
SCHEMA_RECEIPT = "qa525-g6-v2-receipt"
SCHEMA_RESTORE = "qa525-g6-v2-restore"
SCHEMA_IDENTITY = "qa525-g6-v2-identity"
STATUS_RECEIPT = "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING"
RESULT_RESTORE = "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY"
ARCHIVE_FMT = "centenario-qa-g6-v2-CIFRADO-{stamp}.tar.gz"
RESTORE_FMT = "isolated-restore-v2-{stamp}.report.json"
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
EPOCH = re.compile(r"[a-f0-9]{32}\Z")
DRIVE_ID = re.compile(r"[A-Za-z0-9_-]{15,128}\Z")
MIN_KEY_BYTES = 32
MAX_ARCHIVE_BYTES = 8 * 1024 ** 3
DATABASES = frozenset({"pmgm", "pmgm_keycloak"})
# Only these names exist under the v2 root; no v1 name is ever touched.
REPO, KEY, STATE, EXPORTS, RESTORES = ("repo-v2", "key-v2", "state-v2",
                                       "exports-v2", "restores-v2")


class Stop(Exception):
    """Fixed codes only; never echo paths, bytes or tool output."""


def canonical(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":"),
                       ensure_ascii=True, allow_nan=False) + "\n").encode("ascii")


def _stamp(now):
    if type(now) is not dt.datetime or now.tzinfo is None or now.utcoffset() is None:
        raise Stop("G6P_CLOCK_INVALID")
    return now.astimezone(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def key_epoch(key_bytes):
    """Public epoch label: a one-way hash of a high-entropy key, never the key."""
    return hashlib.sha256(b"qa525-g6-v2-epoch\0" + key_bytes).hexdigest()[:32]


# ---------------------------------------------------------------- filesystem
def _safe_dir(path, uid):
    try:
        info = os.lstat(path)
    except OSError:
        raise Stop("G6P_DIR_UNSAFE") from None
    if (not stat.S_ISDIR(info.st_mode) or info.st_uid != uid
            or info.st_mode & 0o077):
        raise Stop("G6P_DIR_UNSAFE")


def _safe_file(path, uid):
    try:
        info = os.lstat(path)
    except OSError:
        raise Stop("G6P_FILE_UNSAFE") from None
    if (not stat.S_ISREG(info.st_mode) or info.st_uid != uid
            or stat.S_IMODE(info.st_mode) != 0o600 or info.st_nlink != 1):
        raise Stop("G6P_FILE_UNSAFE")


def _read_file(path, uid, limit=1 << 20):
    _safe_file(path, uid)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        data = os.read(fd, limit + 1)
    finally:
        os.close(fd)
    if not 0 < len(data) <= limit:
        raise Stop("G6P_FILE_UNSAFE")
    return data


def _create_excl(path, data):
    """Create a new 0600 file; never truncate, follow symlinks or replace."""
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC
    try:
        fd = os.open(path, flags, 0o600)
    except FileExistsError:
        raise Stop("G6P_TARGET_EXISTS") from None
    except OSError:
        raise Stop("G6P_WRITE_FAILED") from None
    try:
        view = memoryview(data)
        while view:
            view = view[os.write(fd, view):]
        os.fsync(fd)
    finally:
        os.close(fd)


def _layout(root, uid):
    if type(root) is not str or not os.path.isabs(root) or type(uid) is not int:
        raise Stop("G6P_ROOT_INVALID")
    _safe_dir(root, uid)
    return {name: os.path.join(root, name)
            for name in (REPO, KEY, STATE, EXPORTS, RESTORES)}


def _ensure_dir(path, uid):
    if not os.path.lexists(path):
        try:
            os.mkdir(path, 0o700)
        except OSError:
            raise Stop("G6P_DIR_UNSAFE") from None
    _safe_dir(path, uid)


# ---------------------------------------------------------------- Restic port
class ResticCli:
    """Thin argv-only adapter. Secrets travel by file path, never by value."""

    def __init__(self, binary, *, timeout=3600):
        if type(binary) is not str or not os.path.isabs(binary) or "\0" in binary:
            raise Stop("G6P_RESTIC_BINARY_INVALID")
        self._binary, self._timeout = binary, timeout

    def _run(self, args, repo, keyfile, *, want_output=False):
        env = {"RESTIC_REPOSITORY": repo, "RESTIC_PASSWORD_FILE": keyfile,
               "PATH": "/usr/sbin:/usr/bin:/sbin:/bin", "HOME": "/nonexistent",
               "RESTIC_CACHE_DIR": os.path.join(os.path.dirname(repo), STATE, "cache")}
        try:
            done = subprocess.run([self._binary, *args], env=env, stdin=subprocess.DEVNULL,
                                  stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                  timeout=self._timeout, check=False)
        except (OSError, subprocess.SubprocessError):
            raise Stop("G6P_RESTIC_FAILED") from None
        if done.returncode != 0:
            raise Stop("G6P_RESTIC_FAILED")
        return done.stdout if want_output else None

    def init(self, repo, keyfile):
        self._run(["init", "--quiet"], repo, keyfile)

    def repo_id(self, repo, keyfile):
        out = self._run(["cat", "config", "--json"], repo, keyfile, want_output=True)
        try:
            value = json.loads(out.decode("utf-8"))["id"]
        except (ValueError, KeyError, UnicodeError, TypeError, AttributeError):
            raise Stop("G6P_REPO_ID_INVALID") from None
        if type(value) is not str or HEX64.fullmatch(value) is None:
            raise Stop("G6P_REPO_ID_INVALID")
        return value

    def backup(self, repo, keyfile, sources):
        self._run(["backup", "--quiet", "--one-file-system", "--", *sources],
                  repo, keyfile)

    def check(self, repo, keyfile):
        self._run(["check", "--quiet"], repo, keyfile)

    def restore(self, repo, keyfile, target):
        self._run(["restore", "latest", "--target", target], repo, keyfile)


# ---------------------------------------------------------------- identity
def _identity_doc(repo_id, epoch):
    return {"schema": SCHEMA_IDENTITY, "restic_repo_id": repo_id, "key_epoch": epoch}


def init_repo(root, *, runner, key_bytes, uid, now):
    """INIT: one time only, every target must not exist."""
    paths = _layout(root, uid)
    if (type(key_bytes) is not bytes or len(key_bytes) < MIN_KEY_BYTES
            or b"\0" in key_bytes):
        raise Stop("G6P_KEY_INVALID")
    _stamp(now)
    for name in (REPO, KEY, STATE):
        if os.path.lexists(paths[name]):
            raise Stop("G6P_INIT_TARGET_EXISTS")
    _create_excl(paths[KEY], key_bytes)
    _ensure_dir(paths[STATE], uid)
    try:
        os.mkdir(paths[REPO], 0o700)
    except OSError:
        raise Stop("G6P_INIT_TARGET_EXISTS") from None
    runner.init(paths[REPO], paths[KEY])
    repo_id = runner.repo_id(paths[REPO], paths[KEY])
    if type(repo_id) is not str or HEX64.fullmatch(repo_id) is None:
        raise Stop("G6P_REPO_ID_INVALID")
    epoch = key_epoch(key_bytes)
    _create_excl(os.path.join(paths[STATE], "identity.json"),
                 canonical(_identity_doc(repo_id, epoch)))
    return {"restic_repo_id": repo_id, "key_epoch": epoch}


def _load_identity(paths, *, runner, uid, expected_repo_id, expected_key_epoch):
    """BACKUP/RESTORE precondition: everything exists and equals independent pins."""
    if (type(expected_repo_id) is not str or HEX64.fullmatch(expected_repo_id) is None
            or type(expected_key_epoch) is not str
            or EPOCH.fullmatch(expected_key_epoch) is None):
        raise Stop("G6P_PIN_INVALID")
    _safe_dir(paths[REPO], uid)
    _safe_dir(paths[STATE], uid)
    key = _read_file(paths[KEY], uid)
    raw = _read_file(os.path.join(paths[STATE], "identity.json"), uid)
    try:
        doc = json.loads(raw.decode("utf-8"))
    except (ValueError, UnicodeError):
        raise Stop("G6P_IDENTITY_INVALID") from None
    if (type(doc) is not dict or set(doc) != {"schema", "restic_repo_id", "key_epoch"}
            or doc["schema"] != SCHEMA_IDENTITY
            or not hmac.compare_digest(raw, canonical(doc))):
        raise Stop("G6P_IDENTITY_INVALID")
    epoch = key_epoch(key)
    live_id = runner.repo_id(paths[REPO], paths[KEY])
    for have in ((doc["restic_repo_id"], expected_repo_id),
                 (live_id, expected_repo_id), (doc["key_epoch"], expected_key_epoch),
                 (epoch, expected_key_epoch)):
        if (type(have[0]) is not str
                or not hmac.compare_digest(have[0], have[1])):
            raise Stop("G6P_GENERATION_MISMATCH")


# ---------------------------------------------------------------- archive
def _write_archive(repo_dir, key_path, out_path):
    """Deterministic tar.gz of the CIPHERTEXT repository only; refuses links."""
    real_repo = os.path.realpath(repo_dir)
    if os.path.commonpath([real_repo, os.path.realpath(key_path)]) == real_repo:
        raise Stop("G6P_KEY_INSIDE_REPO")
    buf = io.BytesIO()
    with gzip.GzipFile(fileobj=buf, mode="wb", mtime=0) as gz:
        with tarfile.open(fileobj=gz, mode="w", format=tarfile.PAX_FORMAT) as tar:
            for base, dirs, files in os.walk(repo_dir, followlinks=False):
                dirs.sort()
                for name in sorted(dirs) + sorted(files):
                    full = os.path.join(base, name)
                    info = os.lstat(full)
                    if not (stat.S_ISREG(info.st_mode) or stat.S_ISDIR(info.st_mode)):
                        raise Stop("G6P_ARCHIVE_SPECIAL_FILE")
                    entry = tar.gettarinfo(full, os.path.relpath(full, repo_dir))
                    entry.uid = entry.gid = 0
                    entry.uname = entry.gname = ""
                    entry.mtime = 0
                    entry.mode = 0o700 if stat.S_ISDIR(info.st_mode) else 0o600
                    if entry.isreg():
                        with open(full, "rb") as handle:
                            tar.addfile(entry, handle)
                    else:
                        tar.addfile(entry)
    data = buf.getvalue()
    if not 0 < len(data) <= MAX_ARCHIVE_BYTES:
        raise Stop("G6P_ARCHIVE_SIZE")
    _create_excl(out_path, data)
    return hashlib.sha256(data).hexdigest()


def backup(root, *, runner, sources, expected_repo_id, expected_key_epoch, uid, now):
    """BACKUP: never initializes. Produces ciphertext archive + versioned receipt."""
    paths = _layout(root, uid)
    _load_identity(paths, runner=runner, uid=uid, expected_repo_id=expected_repo_id,
                   expected_key_epoch=expected_key_epoch)
    if (type(sources) is not list or not sources
            or any(type(s) is not str or not os.path.isabs(s) or "\0" in s
                   for s in sources)):
        raise Stop("G6P_SOURCES_INVALID")
    stamp = _stamp(now)
    _ensure_dir(paths[EXPORTS], uid)
    runner.backup(paths[REPO], paths[KEY], sources)
    runner.check(paths[REPO], paths[KEY])
    name = ARCHIVE_FMT.format(stamp=stamp)
    sha = _write_archive(paths[REPO], paths[KEY], os.path.join(paths[EXPORTS], name))
    receipt = {"schema": SCHEMA_RECEIPT, "archive_name": name, "sha256": sha,
               "status": STATUS_RECEIPT, "restic_repo_id": expected_repo_id,
               "key_epoch": expected_key_epoch}
    _create_excl(os.path.join(paths[STATE], "receipt-" + stamp + ".json"),
                 canonical(receipt))
    return {"archive_name": name, "archive_sha256": sha, "stamp": stamp}


def restore(root, *, runner, verifier, archive_name, archive_sha256, expected_repo_id,
            expected_key_epoch, forbidden_prefixes, uid, now):
    """RESTORE into a brand-new isolated directory; verifier is caller-injected."""
    paths = _layout(root, uid)
    _load_identity(paths, runner=runner, uid=uid, expected_repo_id=expected_repo_id,
                   expected_key_epoch=expected_key_epoch)
    match = re.fullmatch(r"centenario-qa-g6-v2-CIFRADO-(\d{8}T\d{6}Z)\.tar\.gz", archive_name
                         if type(archive_name) is str else "")
    if match is None or type(archive_sha256) is not str \
            or HEX64.fullmatch(archive_sha256) is None:
        raise Stop("G6P_ARCHIVE_REF_INVALID")
    actual = _hash_big(os.path.join(paths[EXPORTS], archive_name), uid)
    if not hmac.compare_digest(actual, archive_sha256):
        raise Stop("G6P_ARCHIVE_HASH_MISMATCH")
    _ensure_dir(paths[RESTORES], uid)
    target = os.path.join(paths[RESTORES], match.group(1))
    real_target = os.path.realpath(target)
    if (type(forbidden_prefixes) is not list or not forbidden_prefixes
            or any(os.path.commonpath([real_target, os.path.realpath(p)])
                   == os.path.realpath(p) for p in forbidden_prefixes)):
        raise Stop("G6P_RESTORE_TARGET_FORBIDDEN")
    try:
        os.mkdir(target, 0o700)
    except OSError:
        raise Stop("G6P_TARGET_EXISTS") from None
    runner.restore(paths[REPO], paths[KEY], target)
    tables, silo = verifier(target)
    if (type(tables) is not dict or set(tables) != DATABASES
            or any(type(v) is not int or isinstance(v, bool) or v <= 0
                   for v in tables.values())
            or type(silo) is not int or isinstance(silo, bool) or silo < 0):
        raise Stop("G6P_RESTORE_VERIFY_FAILED")
    report = {"schema": SCHEMA_RESTORE, "result": RESULT_RESTORE,
              "archive_filename": archive_name, "archive_sha256": archive_sha256,
              "restic_repo_id": expected_repo_id, "key_epoch": expected_key_epoch,
              "postgres_tables_by_database": dict(tables),
              "silo_objects_verified": silo, "production_touched": False}
    _create_excl(os.path.join(paths[STATE], RESTORE_FMT.format(stamp=match.group(1))),
                 canonical(report))
    return report


def _hash_big(path, uid):
    """Stream-hash a regular, owned, non-link file with a hard size cap."""
    _safe_file(path, uid)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    digest, total = hashlib.sha256(), 0
    try:
        while True:
            chunk = os.read(fd, 1 << 20)
            if not chunk:
                break
            total += len(chunk)
            if total > MAX_ARCHIVE_BYTES:
                raise Stop("G6P_ARCHIVE_SIZE")
            digest.update(chunk)
    finally:
        os.close(fd)
    if total == 0:
        raise Stop("G6P_ARCHIVE_SIZE")
    return digest.hexdigest()


def finalize_review(root, *, archive_name, archive_sha256, downloaded_sha256,
                    drive_file_id, expected_repo_id, expected_key_epoch, uid):
    """Write the review only when the DOWNLOADED copy hash equals the archive hash."""
    paths = _layout(root, uid)
    match = re.fullmatch(r"centenario-qa-g6-v2-CIFRADO-(\d{8}T\d{6}Z)\.tar\.gz",
                         archive_name if type(archive_name) is str else "")
    if (match is None or type(drive_file_id) is not str
            or DRIVE_ID.fullmatch(drive_file_id) is None):
        raise Stop("G6P_REVIEW_INPUT_INVALID")
    for value in (archive_sha256, downloaded_sha256, expected_repo_id):
        if type(value) is not str or HEX64.fullmatch(value) is None:
            raise Stop("G6P_REVIEW_INPUT_INVALID")
    if type(expected_key_epoch) is not str or EPOCH.fullmatch(expected_key_epoch) is None:
        raise Stop("G6P_REVIEW_INPUT_INVALID")
    if not hmac.compare_digest(archive_sha256, downloaded_sha256):
        raise Stop("G6P_OFFHOST_HASH_MISMATCH")
    review = {"schema": SCHEMA_REVIEW, "g6_archive_name": archive_name,
              "g6_archive_sha256": archive_sha256,
              "g6_restore_report_name": RESTORE_FMT.format(stamp=match.group(1)),
              "g6_offhost_sha256_verified": True, "g6_drive_file_id": drive_file_id,
              "restic_repo_id": expected_repo_id, "key_epoch": expected_key_epoch}
    _create_excl(os.path.join(paths[STATE], "review-" + match.group(1) + ".json"),
                 canonical(review))
    return review


if __name__ == "__main__":
    raise SystemExit("QA525_G6_V2_PRODUCER_LIBRARY_ONLY_NO_EXECUTION")
