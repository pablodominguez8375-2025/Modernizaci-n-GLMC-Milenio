#!/usr/bin/env python3
"""QA525 offline journal/nonce component -- NOT an APPLY executor.

This module has no CLI, network, Docker, or host-action integration.
The caller MUST separately validate OPS provenance, G6, Sponsor approval,
API/WEB baseline and dual-runtime rollback before using it for any real QA.
It never WRITES APPLIED or ROLLED_BACK. A terminal record may only be created
by the separate ``qa525-vnext-terminal.py`` after a verifier result; here it is
only READ (``inspect``/``reconcile_after_crash``) so that a finished flight is
never mistaken for a crashed one.
"""
import datetime as dt
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import tempfile

SHA = re.compile(r"^[a-f0-9]{40}$")
DIGEST = re.compile(r"^sha256:[a-f0-9]{64}$")
NONCE = re.compile(r"^[A-Za-z0-9_-]{24,96}$")
MAX_JSON = 64 * 1024


class Stop(Exception):
    """Fail-closed diagnostic codes; no external text or secrets."""


def _canonical(value: dict) -> bytes:
    return (json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n").encode()


def _clock(now: dt.datetime) -> dt.datetime:
    if not isinstance(now, dt.datetime) or now.tzinfo is None:
        raise Stop("CLOCK_NOT_UTC")
    return now.astimezone(dt.timezone.utc)


def _timestamp(value: object) -> dt.datetime:
    if not isinstance(value, str) or not re.fullmatch(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ", value):
        raise Stop("ADMISSION_TIME_INVALID")
    try:
        parsed = dt.datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ")
    except ValueError:
        raise Stop("ADMISSION_TIME_INVALID") from None
    return parsed.replace(tzinfo=dt.timezone.utc)


def _root(directory: Path) -> None:
    """Requires an existing private directory; ancestor/installer trust is external."""
    if not directory.is_absolute() or ".." in directory.parts:
        raise Stop("JOURNAL_ROOT_INVALID")
    try:
        st = directory.lstat()
    except OSError:
        raise Stop("JOURNAL_ROOT_MISSING") from None
    if (not stat.S_ISDIR(st.st_mode) or st.st_uid != os.geteuid()
            or st.st_gid != os.getegid() or stat.S_IMODE(st.st_mode) != 0o700):
        raise Stop("JOURNAL_ROOT_UNTRUSTED")


def _fsync_dir(directory: Path) -> None:
    try:
        fd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
        try:
            os.fsync(fd)
        finally:
            os.close(fd)
    except OSError:
        raise Stop("JOURNAL_DIRECTORY_FSYNC_FAILED") from None


def _abandoned_journal_link_is_trusted(path: Path, st: os.stat_result) -> bool:
    """Recognize exactly one crash-left temporary link to the same journal.

    os.link(NOREPLACE) followed by unlink(temp) leaves nlink=2 if abruptly
    interrupted between those calls. The second name must be a private
    .qa525-next-* entry in the same locked root directory.
    """
    if not path.name.startswith(("journal-", "terminal-")) or st.st_nlink != 2:
        return False
    try:
        entries = list(path.parent.iterdir())
        if len(entries) > 10000:
            return False
        copies = []
        for candidate in entries:
            if candidate.name.startswith(".qa525-next-"):
                copy = candidate.lstat()
                if (stat.S_ISREG(copy.st_mode) and
                        copy.st_dev == st.st_dev and copy.st_ino == st.st_ino and
                        copy.st_uid == st.st_uid and copy.st_gid == st.st_gid and
                        stat.S_IMODE(copy.st_mode) == 0o600):
                    copies.append(candidate)
        return len(copies) == 1
    except OSError:
        return False


def _read_private(path: Path) -> bytes:
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        with os.fdopen(fd, "rb") as file:
            before = os.fstat(file.fileno())
            if (not stat.S_ISREG(before.st_mode) or before.st_uid != os.geteuid()
                    or before.st_gid != os.getegid()
                    or stat.S_IMODE(before.st_mode) != 0o600
                    or (before.st_nlink != 1 and
                        not _abandoned_journal_link_is_trusted(path, before))
                    or not 0 < before.st_size <= MAX_JSON):
                raise Stop("JOURNAL_FILE_UNTRUSTED")
            raw = file.read(MAX_JSON + 1)
            after = os.fstat(file.fileno())
            if (len(raw) != before.st_size or (before.st_dev, before.st_ino, before.st_mtime_ns,
                    before.st_size) != (after.st_dev, after.st_ino, after.st_mtime_ns, after.st_size)):
                raise Stop("JOURNAL_FILE_CHANGED")
        return raw
    except Stop:
        raise
    except OSError:
        raise Stop("JOURNAL_FILE_UNAVAILABLE") from None


def _object(path: Path) -> dict:
    try:
        obj = json.loads(_read_private(path))
    except (ValueError, UnicodeDecodeError):
        raise Stop("JOURNAL_JSON_INVALID") from None
    if type(obj) is not dict:
        raise Stop("JOURNAL_JSON_INVALID")
    return obj


def _write_exclusive(path: Path, obj: dict, checkpoint=None, label="") -> None:
    payload = _canonical(obj)
    if len(payload) > MAX_JSON:
        raise Stop("JOURNAL_PAYLOAD_TOO_LARGE")
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "wb") as file:
            file.write(payload)
            file.flush()
            os.fsync(file.fileno())
        if checkpoint:
            checkpoint("after_" + label + "_fsync")
        _fsync_dir(path.parent)
        if checkpoint:
            checkpoint("after_" + label + "_dirsync")
    except FileExistsError:
        raise Stop("JOURNAL_ALREADY_EXISTS") from None
    except Stop:
        raise
    except OSError:
        raise Stop("JOURNAL_WRITE_FAILED") from None


def _atomic_new(path: Path, obj: dict, checkpoint=None) -> None:
    """Atomically link into an absent pathname; never overwrite a live journal."""
    if path.exists() or path.is_symlink():
        raise Stop("JOURNAL_ALREADY_EXISTS")
    content = _canonical(obj)
    if len(content) > MAX_JSON:
        raise Stop("JOURNAL_PAYLOAD_TOO_LARGE")
    fd = None
    try:
        fd, name = tempfile.mkstemp(prefix=".qa525-next-", dir=str(path.parent))
        os.fchmod(fd, 0o600)
        with os.fdopen(fd, "wb") as out:
            fd = None
            out.write(content)
            out.flush()
            os.fsync(out.fileno())
        if checkpoint:
            checkpoint("after_journal_temp_fsync")
        # os.link is NOREPLACE even if a non-cooperating process created path.
        # os.replace could erase another journal between check and rename.
        try:
            os.link(name, path, follow_symlinks=False)
        except FileExistsError:
            raise Stop("JOURNAL_ALREADY_EXISTS") from None
        if checkpoint:
            checkpoint("after_journal_link")
        os.unlink(name)
        _fsync_dir(path.parent)
        if checkpoint:
            checkpoint("after_journal_dirsync")
    except Stop:
        raise
    except OSError:
        raise Stop("JOURNAL_WRITE_FAILED") from None
    finally:
        if fd is not None:
            os.close(fd)
        # Never remove an abandoned .qa525-next-* file; retain crash evidence.


class Lease:
    """Keeps the exclusive lock held until the external orchestrator exits."""

    def __init__(self, fd: int, root: Path, nonce: str):
        self.fd, self.root, self.nonce = fd, root, nonce

    def close(self):
        if self.fd is not None:
            os.close(self.fd)
            self.fd = None

    def __enter__(self):
        return self

    def __exit__(self, _type, _value, _trace):
        self.close()


def _lock(directory: Path) -> int:
    _root(directory)
    try:
        fd = os.open(directory / ".qa525-transaction.lock",
                     os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW | os.O_CLOEXEC, 0o600)
        st = os.fstat(fd)
        if (not stat.S_ISREG(st.st_mode) or st.st_uid != os.geteuid()
                or stat.S_IMODE(st.st_mode) != 0o600 or st.st_nlink != 1):
            raise Stop("JOURNAL_LOCK_UNTRUSTED")
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise Stop("JOURNAL_OPERATION_ACTIVE") from None
        return fd
    except Stop:
        if "fd" in locals():
            os.close(fd)
        raise
    except OSError:
        if "fd" in locals():
            os.close(fd)
        raise Stop("JOURNAL_LOCK_FAILED") from None


def _paths(directory: Path, nonce: str) -> tuple[Path, Path, Path]:
    if type(nonce) is not str or NONCE.fullmatch(nonce) is None:
        raise Stop("JOURNAL_NONCE_INVALID")
    return (directory / ("nonce-consumed-" + nonce + ".json"),
            directory / ("journal-" + nonce + ".json"),
            directory / ("recovery-" + nonce + ".json"))


def _terminal_path(directory: Path, nonce: str) -> Path:
    _paths(directory, nonce)  # validates the nonce shape
    return directory / ("terminal-" + nonce + ".json")


def _terminal_status(nonce: str, marker: Path, journal: Path, recovery: Path,
                     terminal: Path) -> str:
    """Strict read of a terminal record; contradictions fail closed."""
    if recovery.exists() or recovery.is_symlink():
        raise Stop("TERMINAL_AND_RECOVERY_CONFLICT")
    if not (marker.exists() and journal.exists()) or marker.is_symlink() or journal.is_symlink():
        raise Stop("TERMINAL_WITHOUT_FLIGHT")
    m, j, t = _object(marker), _object(journal), _object(terminal)
    if (t.get("schema") != "qa525-terminal-v1" or t.get("status") not in ("APPLIED", "ROLLED_BACK")
            or t.get("nonce") != nonce or m.get("nonce") != nonce
            or j.get("status") != "APPLYING"
            or t.get("admission_sha256") != m.get("admission_sha256")
            or j.get("admission_sha256") != m.get("admission_sha256")):
        raise Stop("TERMINAL_RECORD_INVALID")
    return t["status"]


def _guard_no_other_flights(root: Path, nonce: str) -> None:
    """Fail closed if any prior vNext nonce or unfinished legacy dual exists.

    This is only a mechanical interlock, NOT authentication of the legacy
    journal or proof of an applied runtime. We intentionally do not permit
    a second vNext nonce after any earlier one: there is no externally
    verified terminal writer in the inactive journal library yet.
    """
    try:
        entries = sorted(root.iterdir(), key=lambda item: item.name)
    except OSError:
        raise Stop("JOURNAL_SCAN_FAILED") from None
    if len(entries) > 10000:
        raise Stop("JOURNAL_SCAN_LIMIT")
    for child in entries:
        name = child.name
        if re.fullmatch(r"(?:nonce-consumed|journal|recovery|terminal)-[A-Za-z0-9_-]{24,96}\.json", name):
            raise Stop("PRIOR_NONCE_UNRESOLVED")
        if any(name.startswith(part) and name.endswith(".json")
               for part in ("nonce-consumed-", "journal-", "recovery-", "terminal-")):
            raise Stop("JOURNAL_UNKNOWN_RESIDUE")
        if name.startswith(".qa525-next-"):
            raise Stop("JOURNAL_ORPHAN_TEMP_UNRESOLVED")
        if name.startswith("dual-") and name.endswith(".json"):
            if re.fullmatch(r"dual-[a-f0-9]{40}\.json", name) is None:
                raise Stop("LEGACY_DUAL_NAME_INVALID")
            legacy = _object(child)
            if legacy.get("status") != "APPLIED":
                raise Stop("LEGACY_DUAL_NOT_APPLIED")
            # APPLIED here is a precondition for the mechanical guard only.
            # An independent signed verify_state check is still required.


def _check_admission(data: dict, now: dt.datetime) -> None:
    mandatory = {"schema": "qa525-admission-v2", "status": "ADMITTED",
                 "nonce_consumed": False, "nonce_consume_transition": "APPLYING",
                 "network_allowed": False}
    if type(data) is not dict or any(data.get(k) != v for k, v in mandatory.items()):
        raise Stop("ADMISSION_INVALID")
    if (type(data.get("ops_source_sha")) is not str or SHA.fullmatch(data["ops_source_sha"]) is None
            or type(data.get("app_source_sha")) is not str or SHA.fullmatch(data["app_source_sha"]) is None
            or type(data.get("policy_hash")) is not str or DIGEST.fullmatch(data["policy_hash"]) is None
            or type(data.get("artifact_sha256")) is not str or DIGEST.fullmatch(data["artifact_sha256"]) is None
            or type(data.get("signed_installable_sha256")) is not str
            or DIGEST.fullmatch(data["signed_installable_sha256"]) is None
            or type(data.get("artifact_id")) is not int or data["artifact_id"] <= 0
            or type(data.get("silo_workflow_run")) is not int or data["silo_workflow_run"] <= 0):
        raise Stop("ADMISSION_BINDING_INVALID")
    _paths(Path("/unused"), data.get("nonce"))
    admitted, expires, g6 = (_timestamp(data.get("admitted_at")),
                            _timestamp(data.get("valid_until")),
                            _timestamp(data.get("g6_expires_at")))
    if not admitted <= now < min(expires, g6):
        raise Stop("ADMISSION_EXPIRED")
    if expires - admitted > dt.timedelta(minutes=30):
        raise Stop("ADMISSION_WINDOW_TOO_LONG")


def begin(root: Path, admission_path: Path, expected_sha256: str, now: dt.datetime,
          checkpoint=None) -> Lease:
    """Reserve a nonce and persist APPLYING; no runtime action follows here.

    Upstream must authenticate the admission hash and revalidate G6, Sponsor,
    baseline, OPS signature and runtime policy in the SAME held lock window.
    This unit ONLY implements durable ordering/at-most-once mechanics.
    """
    now = _clock(now)
    if type(expected_sha256) is not str or DIGEST.fullmatch(expected_sha256) is None:
        raise Stop("ADMISSION_DIGEST_INVALID")
    fd = _lock(root)
    try:
        raw = _read_private(admission_path)
        actual = "sha256:" + hashlib.sha256(raw).hexdigest()
        if actual != expected_sha256:
            raise Stop("ADMISSION_HASH_MISMATCH")
        try:
            data = json.loads(raw)
        except (ValueError, UnicodeDecodeError):
            raise Stop("ADMISSION_INVALID") from None
        _check_admission(data, now)
        nonce = data["nonce"]
        marker, journal, recovery = _paths(root, nonce)
        if (marker.exists() or marker.is_symlink() or journal.exists() or journal.is_symlink()
                or recovery.exists() or recovery.is_symlink()):
            raise Stop("NONCE_ALREADY_CONSUMED")
        _guard_no_other_flights(root, nonce)
        payload = {"schema": "qa525-nonce-v1", "nonce": nonce,
                   "admission_sha256": actual, "journal_path": str(journal),
                   "ops_source_sha": data["ops_source_sha"],
                   "app_source_sha": data["app_source_sha"],
                   "policy_hash": data["policy_hash"],
                   "consumed_at": now.strftime("%Y-%m-%dT%H:%M:%SZ")}
        if checkpoint:
            checkpoint("before_nonce")
        _write_exclusive(marker, payload, checkpoint, "nonce")
        if checkpoint:
            checkpoint("before_journal")
        applying = {**payload, "schema": "qa525-journal-v1", "status": "APPLYING",
                    "created_at": payload["consumed_at"],
                    "terminal_verification": "REQUIRED_EXTERNAL_VERIFIER"}
        _atomic_new(journal, applying, checkpoint)
        return Lease(fd, root, nonce)
    except BaseException:
        os.close(fd)
        raise


def inspect(root: Path, nonce: str) -> str:
    """Read-only classification; a live APPLYING session is NOT success."""
    _root(root)
    marker, journal, recovery = _paths(root, nonce)
    terminal = _terminal_path(root, nonce)
    if terminal.exists() or terminal.is_symlink():
        return _terminal_status(nonce, marker, journal, recovery, terminal)
    if recovery.exists() or recovery.is_symlink():
        state = _object(recovery)
        if state.get("status") != "RECOVERY_REQUIRED":
            raise Stop("RECOVERY_JOURNAL_INVALID")
        return "RECOVERY_REQUIRED"
    if marker.exists() or marker.is_symlink():
        m = _object(marker)
        if m.get("journal_path") != str(journal) or m.get("nonce") != nonce:
            raise Stop("JOURNAL_MARKER_MISMATCH")
        if not (journal.exists() or journal.is_symlink()):
            return "MARKER_ONLY_RECOVERY_REQUIRED"
        state = _object(journal)
        if (state.get("status") not in {"APPLYING", "RECOVERY_REQUIRED"}
                or state.get("admission_sha256") != m.get("admission_sha256")):
            raise Stop("JOURNAL_UNVERIFIED_TERMINAL")
        return state["status"]
    if journal.exists() or journal.is_symlink():
        raise Stop("JOURNAL_WITHOUT_NONCE")
    return "NOT_STARTED"


def reconcile_after_crash(root: Path, nonce: str, now: dt.datetime) -> str:
    """Fail-closed crash reconciliation; never retries or runs APPLY.

    Calling this while a live orchestrator owns the lock returns ACTIVE.
    The caller must separately verify the process really died and preserve
    images, oneshots and the old baseline for operator recovery.
    """
    now = _clock(now)
    fd = _lock(root)
    try:
        marker, journal, recovery = _paths(root, nonce)
        status = inspect(root, nonce)
        if status == "NOT_STARTED":
            return "NOT_STARTED"
        if status in {"RECOVERY_REQUIRED", "APPLIED", "ROLLED_BACK"}:
            return status  # a finished flight is never rewritten as a crash
        if status not in {"APPLYING", "MARKER_ONLY_RECOVERY_REQUIRED"}:
            raise Stop("RECOVERY_STATE_INVALID")
        m = _object(marker)
        payload = {"schema": "qa525-recovery-v1", "status": "RECOVERY_REQUIRED",
                   "nonce": nonce, "admission_sha256": m["admission_sha256"],
                   "journal_path": str(journal), "reason": "ABANDONED_" + status,
                   "reconciled_at": now.strftime("%Y-%m-%dT%H:%M:%SZ"),
                   "external_verified_rollback": False,
                   "retry_allowed": False}
        _write_exclusive(recovery, payload)
        return "RECOVERY_REQUIRED"
    finally:
        os.close(fd)


# There is deliberately NO APPLIED / ROLLED_BACK writer here (see qa525-vnext-terminal.py), no network client, CLI,
# Docker API, SSH, systemctl or shell command in this module.
