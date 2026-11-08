#!/usr/bin/env python3
"""QA525 G6 v2 producer library. INACTIVE, NEVER physical GO, NEVER executed here.

Clean-room implementation from the contract in docs/qa/QA525-G6-RESTIC-ROTATION.md.
It is NOT derived from the host v1 scripts. Separate modes:

  INIT     once; every target must be absent; creates key and repo with O_EXCL.
  BACKUP   later; repo and key must already exist and match independent pins;
           never initializes, truncates or overwrites. Only a controlled stage
           (2 DB dumps + globals + silo, produced by an injected captor and then
           verified) is snapshotted; the ciphertext repo is streamed to a file.
  RESTORE  ONLY from the downloaded copy of the archive (pinned SHA, held fd),
           extracted by a link-free extractor into a private repository, then
           Restic check and restore of the PINNED snapshot. The live repo is
           never used.
  REVIEW   hashes the downloaded file itself and needs an existing restore report.

The injected captor and verifier are NOT authenticated here: they stand for the
future root-verified physical adapters (pg_dump x2 + globals + silo, real
Postgres/silo checks). Evidence stays NO-GO until those exist.

All evidence is canonical JSON written with O_EXCL under versioned v2 paths.
Nothing here reads or writes any v1 path. The Restic password file is passed by
path only. Fixed error codes only.
"""
from __future__ import annotations

import datetime as dt
import gzip
import hashlib
import hmac
import json
import os
import re
import shutil
import stat
import subprocess
import tarfile

SCHEMA_REVIEW = "qa525-g6-v2-review"
SCHEMA_RECEIPT = "qa525-g6-v2-receipt"
SCHEMA_RESTORE = "qa525-g6-v2-restore"
SCHEMA_IDENTITY = "qa525-g6-v2-identity"
SCHEMA_CAPTURE = "qa525-g6-v2-capture"
SCHEMA_CAPTURE_RECORD = "qa525-g6-v2-capture-record"
STATUS_RECEIPT = "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING"
RESULT_RESTORE = "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY"
ARCHIVE_FMT = "centenario-qa-g6-v2-CIFRADO-{stamp}.tar.gz"
RESTORE_FMT = "isolated-restore-v2-{stamp}.report.json"
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
EPOCH = re.compile(r"[a-f0-9]{32}\Z")
DRIVE_ID = re.compile(r"[A-Za-z0-9_-]{15,128}\Z")
SNAPSHOT = re.compile(r"[a-f0-9]{8,64}\Z")
ARCHIVE_RE = re.compile(r"centenario-qa-g6-v2-CIFRADO-(\d{8}T\d{6}Z)\.tar\.gz\Z")
MEMBER = re.compile(r"[A-Za-z0-9._-]+(/[A-Za-z0-9._-]+)*\Z")
MIN_KEY_BYTES = 32
MAX_ARCHIVE_BYTES = 8 * 1024 ** 3
MAX_EXTRACTED = 16 * 1024 ** 3
MAX_MEMBERS = 200000
CHUNK = 1 << 20
WINDOW = dt.timedelta(hours=48)
DATABASES = frozenset({"pmgm", "pmgm_keycloak"})
REQUIRED_STAGE = frozenset({"pmgm.dump", "pmgm_keycloak.dump", "globals.sql", "silo"})
# Only these names exist under the v2 root; no v1 name is ever touched.
REPO, KEY, STATE, EXPORTS, RESTORES, DOWNLOADS, STAGES = (
    "repo-v2", "key-v2", "state-v2", "exports-v2", "restores-v2", "downloads-v2",
    "stages-v2")
NAMES = (REPO, KEY, STATE, EXPORTS, RESTORES, DOWNLOADS, STAGES)


class Stop(Exception):
    """Fixed codes only; never echo paths, bytes or tool output."""


def canonical(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":"),
                       ensure_ascii=True, allow_nan=False) + "\n").encode("ascii")


def _clock(now):
    if type(now) is not dt.datetime or now.tzinfo is None or now.utcoffset() is None:
        raise Stop("G6P_CLOCK_INVALID")
    return now.astimezone(dt.timezone.utc)


def _stamp(now):
    return _clock(now).strftime("%Y%m%dT%H%M%SZ")


def _parse_stamp(text):
    try:
        return dt.datetime.strptime(text, "%Y%m%dT%H%M%SZ").replace(tzinfo=dt.timezone.utc)
    except (ValueError, TypeError):
        raise Stop("G6P_ARCHIVE_REF_INVALID") from None


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


def _open_regular(path, uid):
    """Open a regular, owned, 0600, single-link file; fstat must equal lstat."""
    try:
        before = os.lstat(path)
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    except OSError:
        raise Stop("G6P_FILE_UNSAFE") from None
    try:
        info = os.fstat(fd)
        if (not stat.S_ISREG(info.st_mode) or info.st_uid != uid
                or stat.S_IMODE(info.st_mode) != 0o600 or info.st_nlink != 1
                or (info.st_dev, info.st_ino) != (before.st_dev, before.st_ino)):
            raise Stop("G6P_FILE_UNSAFE")
    except BaseException:
        os.close(fd)
        raise
    return fd, info


def _safe_file(path, uid):
    fd, _ = _open_regular(path, uid)
    os.close(fd)


def _read_file(path, uid, limit=1 << 20):
    fd, info = _open_regular(path, uid)
    try:
        if not 0 < info.st_size <= limit:
            raise Stop("G6P_FILE_UNSAFE")
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
    if (type(root) is not str or not os.path.isabs(root) or type(uid) is not int
            or os.path.realpath(root) != os.path.normpath(root)):
        raise Stop("G6P_ROOT_INVALID")
    _safe_dir(root, uid)
    return {name: os.path.join(root, name) for name in NAMES}


def _ensure_dir(path, uid):
    if not os.path.lexists(path):
        try:
            os.mkdir(path, 0o700)
        except OSError:
            raise Stop("G6P_DIR_UNSAFE") from None
    _safe_dir(path, uid)


def _hash_fd(fd, cap):
    digest, total = hashlib.sha256(), 0
    while True:
        chunk = os.read(fd, CHUNK)
        if not chunk:
            break
        total += len(chunk)
        if total > cap:
            raise Stop("G6P_ARCHIVE_SIZE")
        digest.update(chunk)
    if total == 0:
        raise Stop("G6P_ARCHIVE_SIZE")
    return digest.hexdigest()


# ---------------------------------------------------------------- Restic port
class ResticCli:
    """Thin argv-only adapter. Secrets travel by file path, never by value.

    The binary is pinned by SHA-256 and re-hashed before every call. This is a
    best-effort check (a path can still be swapped between hash and exec); the
    root bootstrap must still authenticate the binary before running as root.
    """

    def __init__(self, binary, *, binary_sha256, timeout=3600):
        if (type(binary) is not str or not os.path.isabs(binary) or "\0" in binary
                or type(binary_sha256) is not str or HEX64.fullmatch(binary_sha256) is None):
            raise Stop("G6P_RESTIC_BINARY_INVALID")
        self._binary, self._pin, self._timeout = binary, binary_sha256, timeout

    def _pinned(self):
        try:
            fd = os.open(self._binary, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        except OSError:
            raise Stop("G6P_RESTIC_BINARY_INVALID") from None
        try:
            actual = _hash_fd(fd, 1 << 30)
        finally:
            os.close(fd)
        if not hmac.compare_digest(actual, self._pin):
            raise Stop("G6P_RESTIC_BINARY_INVALID")

    def _run(self, args, repo, keyfile, *, want_output=False):
        self._pinned()
        env = {"RESTIC_REPOSITORY": repo, "RESTIC_PASSWORD_FILE": keyfile,
               "PATH": "/usr/sbin:/usr/bin:/sbin:/bin", "HOME": "/nonexistent"}
        try:
            done = subprocess.run([self._binary, "--no-cache", *args], env=env, stdin=subprocess.DEVNULL,
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

    def backup(self, repo, keyfile, stage):
        """Snapshot exactly one stage directory; return the new snapshot id."""
        out = self._run(["backup", "--json", "--one-file-system", "--", stage],
                        repo, keyfile, want_output=True)
        snapshot = None
        try:
            for line in out.decode("utf-8").splitlines():
                doc = json.loads(line)
                if type(doc) is dict and doc.get("message_type") == "summary":
                    snapshot = doc.get("snapshot_id")
        except (ValueError, UnicodeError):
            raise Stop("G6P_SNAPSHOT_INVALID") from None
        if type(snapshot) is not str or SNAPSHOT.fullmatch(snapshot) is None:
            raise Stop("G6P_SNAPSHOT_INVALID")
        return snapshot

    def check(self, repo, keyfile):
        self._run(["check", "--quiet"], repo, keyfile)

    def snapshot_paths(self, repo, keyfile, snapshot_id):
        """Paths recorded in one exact snapshot (Restic keeps ABSOLUTE hierarchy)."""
        if type(snapshot_id) is not str or SNAPSHOT.fullmatch(snapshot_id) is None:
            raise Stop("G6P_SNAPSHOT_INVALID")
        out = self._run(["snapshots", "--json", snapshot_id], repo, keyfile,
                        want_output=True)
        try:
            rows = json.loads(out.decode("utf-8"))
            found = [r for r in rows if r["id"].startswith(snapshot_id)]
            paths = found[0]["paths"] if len(found) == 1 else None
        except (ValueError, KeyError, TypeError, UnicodeError, AttributeError, IndexError):
            raise Stop("G6P_SNAPSHOT_INVALID") from None
        if (type(paths) is not list
                or any(type(x) is not str or not x.startswith("/") for x in paths)):
            raise Stop("G6P_SNAPSHOT_INVALID")
        return paths

    def restore(self, repo, keyfile, target, snapshot_id, subfolder):
        """Restore ONLY ``snapshot:subfolder`` so its content lands at the target root."""
        if (type(snapshot_id) is not str or SNAPSHOT.fullmatch(snapshot_id) is None
                or type(subfolder) is not str or not subfolder.startswith("/")
                or "\0" in subfolder or ":" in subfolder):
            raise Stop("G6P_SNAPSHOT_INVALID")
        self._run(["restore", snapshot_id + ":" + subfolder, "--target", target],
                  repo, keyfile)


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


def _canonical_doc(raw, fields, schema, code):
    try:
        doc = json.loads(raw.decode("utf-8"))
    except (ValueError, UnicodeError):
        raise Stop(code) from None
    if (type(doc) is not dict or set(doc) != fields or doc["schema"] != schema
            or not hmac.compare_digest(raw, canonical(doc))):
        raise Stop(code)
    return doc


def _check_pins(expected_repo_id, expected_key_epoch):
    if (type(expected_repo_id) is not str or HEX64.fullmatch(expected_repo_id) is None
            or type(expected_key_epoch) is not str
            or EPOCH.fullmatch(expected_key_epoch) is None):
        raise Stop("G6P_PIN_INVALID")


def _load_identity(paths, *, runner, uid, expected_repo_id, expected_key_epoch):
    """BACKUP/RESTORE precondition: everything exists and equals independent pins."""
    _check_pins(expected_repo_id, expected_key_epoch)
    _safe_dir(paths[REPO], uid)
    _safe_dir(paths[STATE], uid)
    key = _read_file(paths[KEY], uid)
    raw = _read_file(os.path.join(paths[STATE], "identity.json"), uid)
    doc = _canonical_doc(raw, {"schema", "restic_repo_id", "key_epoch"},
                         SCHEMA_IDENTITY, "G6P_IDENTITY_INVALID")
    live_id = runner.repo_id(paths[REPO], paths[KEY])
    for have, want in ((doc["restic_repo_id"], expected_repo_id),
                       (live_id, expected_repo_id),
                       (doc["key_epoch"], expected_key_epoch),
                       (key_epoch(key), expected_key_epoch)):
        if type(have) is not str or not hmac.compare_digest(have, want):
            raise Stop("G6P_GENERATION_MISMATCH")


# ---------------------------------------------------------------- capture stage
def _verify_stage(stage, uid, allow_manifest=False):
    """Exactly the allowlisted entries; only regular files; hashes recorded."""
    try:
        names = set(os.listdir(stage))
    except OSError:
        raise Stop("G6P_STAGE_INVALID") from None
    if allow_manifest:
        names.discard("manifest.json")
    if names != REQUIRED_STAGE:
        raise Stop("G6P_STAGE_INVALID")
    entries = {}
    for name in sorted(REQUIRED_STAGE):
        full = os.path.join(stage, name)
        if name == "silo":
            _safe_dir(full, uid)
            before = len(entries)
            for base, dirs, files in os.walk(full, followlinks=False):
                for sub in dirs:
                    _safe_dir(os.path.join(base, sub), uid)
                for leaf in files:
                    rel = os.path.relpath(os.path.join(base, leaf), stage)
                    fd, _ = _open_regular(os.path.join(base, leaf), uid)
                    try:
                        entries[rel] = _hash_fd(fd, MAX_EXTRACTED)
                    finally:
                        os.close(fd)
            if len(entries) == before:
                raise Stop("G6P_STAGE_INVALID")  # empty silo needs an authenticated inventory
            continue
        fd, info = _open_regular(full, uid)
        try:
            if info.st_size <= 0:
                raise Stop("G6P_STAGE_INVALID")
            entries[name] = _hash_fd(fd, MAX_EXTRACTED)
        finally:
            os.close(fd)
    return entries


# ---------------------------------------------------------------- archive
class _Sink:
    """Counting, hashing writer on an exclusively created fd."""

    def __init__(self, fd, cap):
        self._fd, self._cap, self.total = fd, cap, 0
        self.digest = hashlib.sha256()

    def write(self, data):
        self.total += len(data)
        if self.total > self._cap:
            raise Stop("G6P_ARCHIVE_SIZE")
        self.digest.update(data)
        view = memoryview(data)
        while view:
            view = view[os.write(self._fd, view):]
        return len(data)

    def flush(self):
        pass


def _write_archive(repo_dir, key_path, out_path):
    """Stream a deterministic tar.gz of the CIPHERTEXT repo; bounded memory."""
    real_repo = os.path.realpath(repo_dir)
    if os.path.commonpath([real_repo, os.path.realpath(key_path)]) == real_repo:
        raise Stop("G6P_KEY_INSIDE_REPO")
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC
    try:
        out_fd = os.open(out_path, flags, 0o600)
    except FileExistsError:
        raise Stop("G6P_TARGET_EXISTS") from None
    except OSError:
        raise Stop("G6P_WRITE_FAILED") from None
    sink = _Sink(out_fd, MAX_ARCHIVE_BYTES)
    try:
        with gzip.GzipFile(fileobj=sink, mode="wb", mtime=0) as gz, \
                tarfile.open(fileobj=gz, mode="w|", format=tarfile.PAX_FORMAT) as tar:
            for base, dirs, files in os.walk(repo_dir, followlinks=False):
                dirs.sort()
                for name in sorted(dirs) + sorted(files):
                    full = os.path.join(base, name)
                    arc = os.path.relpath(full, repo_dir)
                    before = os.lstat(full)
                    entry = tarfile.TarInfo(arc)
                    entry.uid = entry.gid = entry.mtime = 0
                    entry.uname = entry.gname = ""
                    if stat.S_ISDIR(before.st_mode):
                        entry.type, entry.mode = tarfile.DIRTYPE, 0o700
                        tar.addfile(entry)
                    elif stat.S_ISREG(before.st_mode):
                        try:
                            fd = os.open(full, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
                        except OSError:
                            raise Stop("G6P_ARCHIVE_SPECIAL_FILE") from None
                        with os.fdopen(fd, "rb") as handle:
                            info = os.fstat(handle.fileno())
                            if ((info.st_dev, info.st_ino) != (before.st_dev, before.st_ino)
                                    or not stat.S_ISREG(info.st_mode)):
                                raise Stop("G6P_ARCHIVE_SPECIAL_FILE")
                            entry.size, entry.mode = info.st_size, 0o600
                            tar.addfile(entry, handle)
                    else:
                        raise Stop("G6P_ARCHIVE_SPECIAL_FILE")
        if not sink.total:
            raise Stop("G6P_ARCHIVE_SIZE")
        os.fsync(out_fd)
    except BaseException as exc:
        os.close(out_fd)
        try:
            os.unlink(out_path)
        except OSError:
            pass
        if isinstance(exc, Stop):
            raise
        if isinstance(exc, (OSError, tarfile.TarError, EOFError, ValueError)):
            raise Stop("G6P_ARCHIVE_FAILED") from None
        raise
    os.close(out_fd)
    return sink.digest.hexdigest()


class _HashingFd:
    """Reader over a held fd that hashes what is consumed."""

    def __init__(self, fd):
        self._fd, self.digest, self.total = fd, hashlib.sha256(), 0

    def read(self, size=-1):
        chunk = os.read(self._fd, CHUNK if size is None or size < 0 else min(size, CHUNK))
        self.total += len(chunk)
        if self.total > MAX_ARCHIVE_BYTES:
            raise Stop("G6P_ARCHIVE_SIZE")
        self.digest.update(chunk)
        return chunk

    def drain(self):
        while self.read(CHUNK):
            pass


def _extract_safe(fd, dest, expected_sha256):
    """Extract a regular-files-and-dirs tar.gz from a held fd, link-free."""
    os.lseek(fd, 0, os.SEEK_SET)
    reader = _HashingFd(fd)
    members, extracted = 0, 0
    try:
        with gzip.GzipFile(fileobj=reader, mode="rb") as gz, \
                tarfile.open(fileobj=gz, mode="r|") as tar:
            for member in tar:
                members += 1
                name = member.name
                if (members > MAX_MEMBERS or not MEMBER.fullmatch(name)
                        or any(part in (".", "..") for part in name.split("/"))):
                    raise Stop("G6P_EXTRACT_UNSAFE")
                target = os.path.join(dest, name)
                if member.isdir():
                    try:
                        os.mkdir(target, 0o700)
                    except OSError:
                        raise Stop("G6P_EXTRACT_UNSAFE") from None
                elif member.isreg():
                    extracted += member.size
                    if extracted > MAX_EXTRACTED:
                        raise Stop("G6P_ARCHIVE_SIZE")
                    source = tar.extractfile(member)
                    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC
                    try:
                        out = os.open(target, flags, 0o600)
                    except OSError:
                        raise Stop("G6P_EXTRACT_UNSAFE") from None
                    try:
                        left = member.size
                        while left:
                            chunk = source.read(min(CHUNK, left))
                            if not chunk:
                                raise Stop("G6P_EXTRACT_UNSAFE")
                            left -= len(chunk)
                            view = memoryview(chunk)
                            while view:
                                view = view[os.write(out, view):]
                    finally:
                        os.close(out)
                else:
                    raise Stop("G6P_EXTRACT_UNSAFE")
        reader.drain()
    except (tarfile.TarError, EOFError, OSError, ValueError):
        raise Stop("G6P_EXTRACT_UNSAFE") from None
    if not hmac.compare_digest(reader.digest.hexdigest(), expected_sha256):
        raise Stop("G6P_ARCHIVE_HASH_MISMATCH")


def _remove_stage(path):
    """Remove plaintext with proof of absence; failure is never a PASS."""
    try:
        shutil.rmtree(path)
        # rmtree can be patched/broken or return without actually cleaning.
        try:
            os.lstat(path)
        except FileNotFoundError:
            return
    except OSError:
        raise Stop("G6P_CLEANUP_FAILED") from None
    raise Stop("G6P_CLEANUP_FAILED")


def backup(root, *, runner, captor, expected_repo_id, expected_key_epoch, uid, now):
    """BACKUP: never initializes. Captures a verified stage, snapshots only it."""
    paths = _layout(root, uid)
    _load_identity(paths, runner=runner, uid=uid, expected_repo_id=expected_repo_id,
                   expected_key_epoch=expected_key_epoch)
    if not callable(captor):
        raise Stop("G6P_CAPTOR_INVALID")
    stamp = _stamp(now)
    _ensure_dir(paths[STAGES], uid)
    _ensure_dir(paths[EXPORTS], uid)
    stage = os.path.join(paths[STAGES], stamp)
    try:
        os.mkdir(stage, 0o700)
    except OSError:
        raise Stop("G6P_TARGET_EXISTS") from None
    name = ARCHIVE_FMT.format(stamp=stamp)
    try:
        captor(stage)
        entries = _verify_stage(stage, uid)
        manifest = canonical({"schema": SCHEMA_CAPTURE, "cut": stamp,
                              "entries": entries})
        manifest_sha = hashlib.sha256(manifest).hexdigest()
        _create_excl(os.path.join(stage, "manifest.json"), manifest)
        snapshot = runner.backup(paths[REPO], paths[KEY], stage)
        if type(snapshot) is not str or SNAPSHOT.fullmatch(snapshot) is None:
            raise Stop("G6P_SNAPSHOT_INVALID")
        runner.check(paths[REPO], paths[KEY])
        sha = _write_archive(paths[REPO], paths[KEY], os.path.join(paths[EXPORTS], name))
    finally:
        _remove_stage(stage)
    _create_excl(os.path.join(paths[STATE], "capture-" + stamp + ".json"), canonical(
        {"schema": SCHEMA_CAPTURE_RECORD, "cut": stamp, "snapshot_id": snapshot,
         "manifest_sha256": manifest_sha}))
    receipt = {"schema": SCHEMA_RECEIPT, "archive_name": name, "sha256": sha,
               "status": STATUS_RECEIPT, "restic_repo_id": expected_repo_id,
               "key_epoch": expected_key_epoch}
    _create_excl(os.path.join(paths[STATE], "receipt-" + stamp + ".json"),
                 canonical(receipt))
    return {"archive_name": name, "archive_sha256": sha, "snapshot_id": snapshot,
            "manifest_sha256": manifest_sha, "stamp": stamp}


def _verify_restored(data_dir, stamp, expected_manifest_sha256, uid):
    """The restored root must be EXACTLY the captured stage, bound to the manifest."""
    try:
        names = set(os.listdir(data_dir))
    except OSError:
        raise Stop("G6P_RESTORE_LAYOUT") from None
    if names != REQUIRED_STAGE | {"manifest.json"}:
        raise Stop("G6P_RESTORE_LAYOUT")
    raw = _read_file(os.path.join(data_dir, "manifest.json"), uid, limit=64 << 20)
    if not hmac.compare_digest(hashlib.sha256(raw).hexdigest(), expected_manifest_sha256):
        raise Stop("G6P_RESTORE_LAYOUT")
    manifest = _canonical_doc(raw, {"schema", "cut", "entries"}, SCHEMA_CAPTURE,
                              "G6P_RESTORE_LAYOUT")
    actual = _verify_stage(data_dir, uid, allow_manifest=True)
    if manifest["cut"] != stamp or manifest["entries"] != actual:
        raise Stop("G6P_RESTORE_LAYOUT")


def restore(root, *, runner, verifier, archive_name, archive_sha256, snapshot_id,
            expected_manifest_sha256, expected_repo_id, expected_key_epoch,
            forbidden_prefixes, uid, now):
    """RESTORE only from the DOWNLOADED archive copy. Needs no live repo or record.

    Identity comes from independent pins: the key file's epoch and the repo id of
    the repository EXTRACTED from the download.
    """
    paths = _layout(root, uid)
    _check_pins(expected_repo_id, expected_key_epoch)
    match = ARCHIVE_RE.fullmatch(archive_name if type(archive_name) is str else "")
    if (match is None or type(archive_sha256) is not str
            or HEX64.fullmatch(archive_sha256) is None
            or type(snapshot_id) is not str or SNAPSHOT.fullmatch(snapshot_id) is None
            or type(expected_manifest_sha256) is not str
            or HEX64.fullmatch(expected_manifest_sha256) is None):
        raise Stop("G6P_ARCHIVE_REF_INVALID")
    stamp = match.group(1)
    cut = _parse_stamp(stamp)
    clock = _clock(now)
    if clock < cut or clock >= cut + WINDOW:
        raise Stop("G6P_RESTORE_WINDOW")
    if not hmac.compare_digest(key_epoch(_read_file(paths[KEY], uid)), expected_key_epoch):
        raise Stop("G6P_GENERATION_MISMATCH")
    _ensure_dir(paths[STATE], uid)
    _ensure_dir(paths[RESTORES], uid)
    _safe_dir(paths[DOWNLOADS], uid)
    workdir = os.path.join(paths[RESTORES], stamp)
    real_work = os.path.realpath(workdir)
    if (type(forbidden_prefixes) is not list or not forbidden_prefixes
            or any(os.path.commonpath([real_work, os.path.realpath(p)])
                   == os.path.realpath(p) for p in forbidden_prefixes)):
        raise Stop("G6P_RESTORE_TARGET_FORBIDDEN")
    report_path = os.path.join(paths[STATE], RESTORE_FMT.format(stamp=stamp))
    if os.path.lexists(report_path):
        raise Stop("G6P_TARGET_EXISTS")
    fd, _ = _open_regular(os.path.join(paths[DOWNLOADS], archive_name), uid)
    created = False
    try:
        if not hmac.compare_digest(_hash_fd(fd, MAX_ARCHIVE_BYTES), archive_sha256):
            raise Stop("G6P_ARCHIVE_HASH_MISMATCH")
        try:
            os.mkdir(workdir, 0o700)
        except OSError:
            raise Stop("G6P_TARGET_EXISTS") from None
        created = True
        repo_dir, data_dir = (os.path.join(workdir, n) for n in ("repo", "data"))
        os.mkdir(repo_dir, 0o700)
        os.mkdir(data_dir, 0o700)
        _extract_safe(fd, repo_dir, archive_sha256)
        live_id = runner.repo_id(repo_dir, paths[KEY])
        if type(live_id) is not str or not hmac.compare_digest(live_id, expected_repo_id):
            raise Stop("G6P_GENERATION_MISMATCH")
        runner.check(repo_dir, paths[KEY])
        recorded = runner.snapshot_paths(repo_dir, paths[KEY], snapshot_id)
        if (type(recorded) is not list or len(recorded) != 1
                or not re.fullmatch(r"/(?:[^/\0:]+/)*stages-v2/" + stamp, recorded[0])):
            raise Stop("G6P_RESTORE_LAYOUT")
        if os.listdir(data_dir):
            raise Stop("G6P_TARGET_EXISTS")
        runner.restore(repo_dir, paths[KEY], data_dir, snapshot_id, recorded[0])
        _verify_restored(data_dir, stamp, expected_manifest_sha256, uid)
        tables, silo = verifier(data_dir)
        if (type(tables) is not dict or set(tables) != DATABASES
                or any(type(v) is not int or v <= 0 for v in tables.values())
                or type(silo) is not int or silo < 0):
            raise Stop("G6P_RESTORE_VERIFY_FAILED")
        report = {"schema": SCHEMA_RESTORE, "result": RESULT_RESTORE,
                  "archive_filename": archive_name, "archive_sha256": archive_sha256,
                  "restic_repo_id": expected_repo_id, "key_epoch": expected_key_epoch,
                  "postgres_tables_by_database": dict(tables),
                  "silo_objects_verified": silo, "production_touched": False}
        # Do NOT persist success before plaintext cleanup has been proven.
    finally:
        try:
            os.close(fd)
        finally:
            if created:  # fail closed if plaintext or private repo copy remains
                _remove_stage(workdir)
    _create_excl(report_path, canonical(report))
    return report


_RESTORE_FIELDS = {"schema", "result", "archive_filename", "archive_sha256",
                   "restic_repo_id", "key_epoch", "postgres_tables_by_database",
                   "silo_objects_verified", "production_touched"}


def finalize_review(root, *, archive_name, drive_file_id, expected_repo_id,
                    expected_key_epoch, uid):
    """Hash the DOWNLOADED file here and require a matching restore report."""
    paths = _layout(root, uid)
    _check_pins(expected_repo_id, expected_key_epoch)
    match = ARCHIVE_RE.fullmatch(archive_name if type(archive_name) is str else "")
    if (match is None or type(drive_file_id) is not str
            or DRIVE_ID.fullmatch(drive_file_id) is None):
        raise Stop("G6P_REVIEW_INPUT_INVALID")
    stamp = match.group(1)
    fd, _ = _open_regular(os.path.join(paths[DOWNLOADS], archive_name), uid)
    try:
        downloaded = _hash_fd(fd, MAX_ARCHIVE_BYTES)
    finally:
        os.close(fd)
    receipt = _canonical_doc(
        _read_file(os.path.join(paths[STATE], "receipt-" + stamp + ".json"), uid),
        {"schema", "archive_name", "sha256", "status", "restic_repo_id", "key_epoch"},
        SCHEMA_RECEIPT, "G6P_RECEIPT_INVALID")
    report = _canonical_doc(
        _read_file(os.path.join(paths[STATE], RESTORE_FMT.format(stamp=stamp)), uid),
        _RESTORE_FIELDS, SCHEMA_RESTORE, "G6P_RESTORE_REPORT_INVALID")
    for have in (receipt["sha256"], report["archive_sha256"]):
        if type(have) is not str or not hmac.compare_digest(have, downloaded):
            raise Stop("G6P_OFFHOST_HASH_MISMATCH")
    for doc in (receipt, report):
        if (doc.get("restic_repo_id") != expected_repo_id
                or doc.get("key_epoch") != expected_key_epoch):
            raise Stop("G6P_GENERATION_MISMATCH")
    if (receipt["archive_name"] != archive_name or report["archive_filename"] != archive_name
            or report["production_touched"] is not False
            or report["result"] != RESULT_RESTORE):
        raise Stop("G6P_RESTORE_REPORT_INVALID")
    review = {"schema": SCHEMA_REVIEW, "g6_archive_name": archive_name,
              "g6_archive_sha256": downloaded,
              "g6_restore_report_name": RESTORE_FMT.format(stamp=stamp),
              "g6_offhost_sha256_verified": True, "g6_drive_file_id": drive_file_id,
              "restic_repo_id": expected_repo_id, "key_epoch": expected_key_epoch}
    _create_excl(os.path.join(paths[STATE], "review-" + stamp + ".json"), canonical(review))
    return review


if __name__ == "__main__":
    raise SystemExit("QA525_G6_V2_PRODUCER_LIBRARY_ONLY_NO_EXECUTION")
