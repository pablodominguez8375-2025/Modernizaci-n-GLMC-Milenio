#!/usr/bin/env python3
"""QA525 launcher replacement *sandbox* transaction and crash-state model.

This module deliberately has NO production installation entrypoint. It only
accepts fresh root directories directly below the process temp directory,
named qa525-launcher-swap-test-*. It can never modify /usr/local/sbin or
/opt/centenario. Intended for hermetic adversarial QA525 tests (Issue #822).

The future signed installer must separately authenticate the candidate, lock
the real QA operators, validate root ownership, authorization and live state.
NEVER call this simulator as a deployment shortcut.
"""
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import tempfile

TEST_PREFIX = "qa525-launcher-swap-test-"
LIVE_REL = Path("live/pmgm-qa-update")
SIGNED_REL = Path("signed/qa525-update.py")
STATE_REL = Path("state")
SHA = re.compile(r"[a-f0-9]{40}")
DIGEST = re.compile(r"sha256:[a-f0-9]{64}")
HASH = re.compile(r"[a-f0-9]{64}")
NO_DEPLOY = "SANDBOX_ONLY_NO_HOST_INSTALL_NO_API_WEB_NO_IDENTITY"


class Blocked(Exception):
    """Non-sensitive fail-closed classification."""


def require(value, code):
    if not value:
        raise Blocked(code)


def valid(value, pattern):
    return type(value) is str and pattern.fullmatch(value) is not None


def trust_sandbox(root):
    """Reject path escapes, links and unsafe temp roots before any writes."""
    root = Path(root)
    parent = Path(tempfile.gettempdir()).resolve()
    require(root.is_absolute() and root.parent == parent
            and root.name.startswith(TEST_PREFIX)
            and len(root.name) <= 128,
            "SANDBOX_ROOT_NOT_WHITELISTED")
    for path in (root, root / "live", root / "signed", root / "state"):
        try:
            st = path.lstat()
        except OSError:
            raise Blocked("SANDBOX_DIRECTORY_MISSING") from None
        require(stat.S_ISDIR(st.st_mode) and not stat.S_ISLNK(st.st_mode)
                and stat.S_IMODE(st.st_mode) == 0o700,
                "SANDBOX_DIRECTORY_UNTRUSTED")
    for file in (root / LIVE_REL, root / SIGNED_REL):
        try:
            st = file.lstat()
        except OSError:
            raise Blocked("SANDBOX_CANDIDATE_OR_LAUNCHER_MISSING") from None
        require(stat.S_ISREG(st.st_mode) and not stat.S_ISLNK(st.st_mode)
                and st.st_nlink == 1 and stat.S_IMODE(st.st_mode) == 0o700,
                "SANDBOX_FILE_UNTRUSTED")
    return root


def read_exact(path, limit=2_000_000):
    before = path.lstat()
    require(stat.S_ISREG(before.st_mode) and before.st_nlink == 1
            and not stat.S_ISLNK(before.st_mode)
            and 0 < before.st_size < limit, "SANDBOX_SOURCE_UNTRUSTED")
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        statfd = os.fstat(fd)
        require(statfd.st_dev == before.st_dev and statfd.st_ino == before.st_ino
                and statfd.st_size == before.st_size and statfd.st_nlink == 1,
                "SANDBOX_SOURCE_CHANGED")
        with os.fdopen(fd, "rb") as stream:
            fd = -1
            payload = stream.read(limit + 1)
    finally:
        if fd >= 0:
            os.close(fd)
    require(len(payload) == before.st_size and len(payload) < limit,
            "SANDBOX_SIZE_CHANGED")
    return payload


def sha(data):
    return hashlib.sha256(data).hexdigest()


def sync_dir(directory):
    fd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def create_durable(path, payload, *, mode=0o600):
    """O_EXCL+O_NOFOLLOW, fsync file and parent; never overwrite."""
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW,
                 mode)
    try:
        os.fchmod(fd, mode)
        with os.fdopen(fd, "wb") as stream:
            fd = -1
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
    finally:
        if fd >= 0:
            os.close(fd)
    sync_dir(path.parent)


def json_bytes(data):
    return (json.dumps(data, sort_keys=True, separators=(",", ":")) + "\n").encode()


def inspect(root, nonce, *, old_hash, new_hash, source_sha, digest):
    """Classify persisted evidence. No mutation or automatic reconciliation."""
    root = trust_sandbox(root)
    require(valid(nonce, HASH) and valid(old_hash, HASH)
            and valid(new_hash, HASH) and old_hash != new_hash
            and valid(source_sha, SHA) and valid(digest, DIGEST),
            "SANDBOX_INSPECTION_IDENTITY_INVALID")
    state = root / STATE_REL
    pending = state / ("intent-" + nonce + ".json")
    backup = state / ("old-" + nonce + ".py")
    receipt = state / ("receipt-" + nonce + ".json")
    live = root / LIVE_REL
    for item in (pending, backup, receipt):
        require(not item.is_symlink(), "SANDBOX_EVIDENCE_SYMLINK_FORBIDDEN")
    if pending.exists():
        require(pending.is_file() and pending.stat().st_nlink == 1,
                "SANDBOX_PENDING_MARKER_UNTRUSTED")
        record = json.loads(read_exact(pending))
        require(record.get("status") == "SWAP_INTENT_NO_DEPLOYMENT"
                and record.get("old_launcher_sha256") == old_hash
                and record.get("new_launcher_sha256") == new_hash
                and record.get("nonce") == nonce
                and record.get("source_sha") == source_sha
                and record.get("release_asset_sha256") == digest,
                "SANDBOX_INTENT_IDENTITY_INVALID")
    live_hash = sha(read_exact(live))
    if backup.exists():
        require(sha(read_exact(backup)) == old_hash,
                "SANDBOX_BACKUP_HASH_DRIFT")
    if receipt.exists():
        require(receipt.is_file(), "SANDBOX_RECEIPT_INVALID")
        record = json.loads(read_exact(receipt))
        require(record.get("status") == "LAUNCHER_ATOMIC_SWAP_COMMITTED"
                and record.get("old_launcher_sha256") == old_hash
                and record.get("new_launcher_sha256") == new_hash
                and record.get("nonce") == nonce
                and record.get("source_sha") == source_sha
                and record.get("release_asset_sha256") == digest,
                "SANDBOX_COMMIT_RECEIPT_INVALID")
    if pending.exists():
        if not backup.exists():
            status = "PENDING_BEFORE_DURABLE_BACKUP_MANUAL_RECONCILIATION"
        elif live_hash == old_hash:
            status = "PENDING_OLD_LAUNCHER_MANUAL_RECONCILIATION"
        elif live_hash == new_hash:
            status = "PENDING_NEW_LAUNCHER_MANUAL_RECONCILIATION"
        else:
            status = "PENDING_UNKNOWN_LAUNCHER_MANUAL_RECONCILIATION"
    elif receipt.exists() and backup.exists() and live_hash == new_hash:
        status = "SIMULATED_LAUNCHER_COMMITTED_NO_DEPLOYMENT"
    elif receipt.exists():
        status = "RECEIPT_DIVERGED_MANUAL_RECONCILIATION"
    elif live_hash == old_hash and not backup.exists():
        status = "SIMULATION_NOT_STARTED_NO_AUTHORIZATION"
    else:
        status = "UNACCOUNTED_SIMULATION_STATE_MANUAL_RECONCILIATION"
    return {
        "status": status,
        "scope": NO_DEPLOY,
        "pending_intent": pending.exists(),
        "backup_exists": backup.exists(),
        "receipt_exists": receipt.exists(),
        "live_sha256": live_hash,
        "may_apply": False,
        "may_install": False,
        "may_recover_automatically": False,
    }


def simulate(root, *, source_sha, digest, nonce, old_hash, new_hash,
             checkpoint=lambda phase: None):
    """Exercise journal/backup/swap/receipt, with injectable crash boundaries.

    The simulator NEVER accepts glmdch production paths or claims a real
    signature/approval. The caller must inspect the result after any error.
    """
    root = trust_sandbox(root)
    require(valid(source_sha, SHA) and valid(digest, DIGEST)
            and valid(nonce, HASH) and valid(old_hash, HASH)
            and valid(new_hash, HASH) and old_hash != new_hash,
            "SANDBOX_TRANSACTION_INPUT_INVALID")
    live = root / LIVE_REL
    candidate = root / SIGNED_REL
    state = root / STATE_REL
    expected_old, expected_new = read_exact(live), read_exact(candidate)
    require(sha(expected_old) == old_hash and sha(expected_new) == new_hash,
            "SANDBOX_SIGNED_CANDIDATE_HASH_MISMATCH")
    pending = state / ("intent-" + nonce + ".json")
    backup = state / ("old-" + nonce + ".py")
    receipt = state / ("receipt-" + nonce + ".json")
    staged = root / "live" / (".stage-" + nonce + ".py")
    require(all(not p.exists() and not p.is_symlink()
                for p in (pending, backup, receipt, staged)),
            "SANDBOX_REPLAY_OR_PENDING_BLOCKED")
    checkpoint("before_intent")
    create_durable(pending, json_bytes({
        "status": "SWAP_INTENT_NO_DEPLOYMENT",
        "source_sha": source_sha,
        "release_asset_sha256": digest,
        "old_launcher_sha256": old_hash,
        "new_launcher_sha256": new_hash,
        "nonce": nonce,
    }))
    checkpoint("after_durable_intent")
    create_durable(backup, expected_old, mode=0o700)
    require(sha(read_exact(backup)) == old_hash, "SANDBOX_BACKUP_VERIFY_FAILED")
    checkpoint("after_durable_backup")
    create_durable(staged, expected_new, mode=0o700)
    require(sha(read_exact(staged)) == new_hash, "SANDBOX_STAGE_VERIFY_FAILED")
    require(sha(read_exact(live)) == old_hash, "SANDBOX_LIVE_DRIFT_BEFORE_SWAP")
    checkpoint("before_atomic_swap")
    os.replace(staged, live)
    sync_dir(live.parent)
    checkpoint("after_atomic_swap")
    require(sha(read_exact(live)) == new_hash, "SANDBOX_LIVE_DRIFT_AFTER_SWAP")
    create_durable(receipt, json_bytes({
        "status": "LAUNCHER_ATOMIC_SWAP_COMMITTED",
        "source_sha": source_sha,
        "release_asset_sha256": digest,
        "old_launcher_sha256": old_hash,
        "new_launcher_sha256": new_hash,
        "nonce": nonce,
    }))
    checkpoint("after_durable_receipt")
    pending.unlink()
    checkpoint("after_intent_unlink")
    sync_dir(state)
    return inspect(root, nonce, old_hash=old_hash, new_hash=new_hash,
                   source_sha=source_sha, digest=digest)


if __name__ == "__main__":
    raise SystemExit("SANDBOX_LIBRARY_ONLY_NO_PRODUCTION_ENTRYPOINT")
