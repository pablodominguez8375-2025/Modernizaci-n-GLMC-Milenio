#!/usr/bin/env python3
"""QA525 launcher reversal transaction on an explicitly disposable tmp sandbox.

No privileged entrypoint. Never accepts glmdch, /opt, /usr/local or a path outside
a fresh direct child of the OS temp directory. This models an independently
approved reversal of a *committed* launcher swap, not an interrupted journal
repair and not permission to deploy or restore API/WEB.
"""
from contextlib import contextmanager
import importlib.util
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import tempfile
import time

PREFIX = "qa525-launcher-recovery-test-"
SOURCE = re.compile(r"[0-9a-f]{40}\Z")
HASH = re.compile(r"[0-9a-f]{64}\Z")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")
CUT_RECEIPT_KEYS = frozenset({
    "status", "source_sha", "release_asset_sha256", "nonce",
    "old_launcher_sha256", "new_launcher_sha256", "candidate_operator_sha256",
})
APPROVAL_KEYS = frozenset({
    "action", "scope", "operator", "approved", "cut_nonce", "nonce",
    "source_sha", "release_asset_sha256", "old_launcher_sha256",
    "new_launcher_sha256", "issued_at", "expires_at",
})
SCOPE = "QA525_LAUNCHER_RECOVERY_SANDBOX_ONLY_NO_HOST_OR_API_WEB"


class Blocked(Exception):
    """Deterministic refusal code; never reveals recovered payloads."""


def require(value, reason):
    if not value:
        raise Blocked(reason)


def sha(value):
    return hashlib.sha256(value).hexdigest()


def trusted_file(path, mode, limit=4096):
    before = path.lstat()
    require(stat.S_ISREG(before.st_mode) and before.st_nlink == 1
            and stat.S_IMODE(before.st_mode) == mode
            and 0 < before.st_size <= limit, "RECOVERY_SANDBOX_FILE_UNTRUSTED")
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        after = os.fstat(fd)
        require((after.st_dev, after.st_ino, after.st_size, after.st_nlink) ==
                (before.st_dev, before.st_ino, before.st_size, before.st_nlink),
                "RECOVERY_SANDBOX_SOURCE_CHANGED")
        with os.fdopen(fd, "rb") as stream:
            fd = -1
            raw = stream.read(limit + 1)
    finally:
        if fd >= 0:
            os.close(fd)
    require(len(raw) == before.st_size, "RECOVERY_SANDBOX_SIZE_DRIFT")
    return raw


def unique_fields(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "RECOVERY_DUPLICATE_JSON_FIELD")
        result[key] = value
    return result


def sync_dir(directory):
    fd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def durable(path, payload, *, mode):
    fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY |
                 os.O_NOFOLLOW | os.O_CLOEXEC, mode)
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


def encode(data):
    return (json.dumps(data, sort_keys=True, separators=(",", ":")) + "\n").encode()


def trust(root):
    root = Path(root)
    tmp = Path(tempfile.gettempdir()).resolve()
    require(root.is_absolute() and root.parent == tmp
            and root.name.startswith(PREFIX) and len(root.name) <= 128,
            "RECOVERY_SANDBOX_OUTSIDE_DISPOSABLE_ROOT")
    for path in (root, root / "live", root / "state"):
        info = path.lstat()
        require(stat.S_ISDIR(info.st_mode) and
                stat.S_IMODE(info.st_mode) == 0o700 and
                info.st_uid == os.geteuid(),
                "RECOVERY_SANDBOX_DIRECTORY_UNTRUSTED")
    return root


@contextmanager
def exclusive_lock(path):
    before = path.lstat()
    require(stat.S_ISREG(before.st_mode) and before.st_nlink == 1
            and stat.S_IMODE(before.st_mode) == 0o600,
            "RECOVERY_SANDBOX_LOCK_UNTRUSTED")
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        after = os.fstat(fd)
        require((after.st_dev, after.st_ino) ==
                (before.st_dev, before.st_ino),
                "RECOVERY_SANDBOX_LOCK_REPLACED")
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise Blocked("RECOVERY_SANDBOX_LOCK_BUSY") from None
        yield
    finally:
        os.close(fd)


def validate_approval(approval, receipt, *, now):
    require(type(approval) is dict and set(approval) == APPROVAL_KEYS
            and approval["action"] == "rollback-root-launcher-supervised"
            and approval["scope"] == SCOPE
            and approval["operator"] == "soporte@glmdch"
            and approval["approved"] is True,
            "RECOVERY_SEPARATE_APPROVAL_REQUIRED")
    for key in ("source_sha", "release_asset_sha256",
                "old_launcher_sha256", "new_launcher_sha256"):
        require(approval[key] == receipt[key],
                "RECOVERY_APPROVAL_IDENTITY_MISMATCH")
    require(type(approval["cut_nonce"]) is str
            and approval["cut_nonce"] == receipt["nonce"]
            and type(approval["nonce"]) is str
            and HASH.fullmatch(approval["nonce"]) is not None
            and approval["nonce"] != receipt["nonce"],
            "RECOVERY_DISTINCT_NONCE_REQUIRED")
    issued, expires = approval["issued_at"], approval["expires_at"]
    require(type(now) is int and type(issued) is int
            and type(expires) is int and issued <= now < expires
            and 0 < expires - issued <= 900,
            "RECOVERY_APPROVAL_EXPIRED")
    return approval["nonce"]


def rollback(root, approval, *, now=None, checkpoint=lambda stage: None,
             sandbox_gate=None):
    """Simulate independently authorized reversal, with no auto retry.

    A pending original cut or any unrecognized journal entry is a hard STOP.
    The original signed receipt and old launcher backup remain immutable.
    """
    root = trust(root)
    state, live_dir = root / "state", root / "live"
    live = live_dir / "pmgm-qa-update"
    with exclusive_lock(state / "recovery.lock"):
        names = {p.name for p in state.iterdir()}
        require(len(names) == 3 and "recovery.lock" in names
                and len([n for n in names if n.startswith("receipt-")]) == 1
                and len([n for n in names if n.startswith("old-")]) == 1,
                "RECOVERY_UNFINISHED_OR_UNKNOWN_JOURNAL")
        cut = next(n[len("receipt-"):-len(".json")]
                   for n in names if n.startswith("receipt-")
                   and n.endswith(".json"))
        require(HASH.fullmatch(cut) is not None
                and names == {"recovery.lock", "receipt-" + cut + ".json",
                              "old-" + cut + ".bin"},
                "RECOVERY_CUT_JOURNAL_UNTRUSTED")
        try:
            receipt = json.loads(trusted_file(
                state / ("receipt-" + cut + ".json"), 0o600),
                object_pairs_hook=unique_fields)
        except (ValueError, UnicodeError, TypeError):
            raise Blocked("RECOVERY_RECEIPT_JSON_INVALID") from None
        require(type(receipt) is dict and set(receipt) == CUT_RECEIPT_KEYS
                and receipt["status"] == "LAUNCHER_ATOMIC_SWAP_COMMITTED"
                and receipt["nonce"] == cut
                and type(receipt["source_sha"]) is str
                and SOURCE.fullmatch(receipt["source_sha"]) is not None
                and type(receipt["release_asset_sha256"]) is str
                and DIGEST.fullmatch(receipt["release_asset_sha256"]) is not None
                and all(type(receipt[k]) is str
                        and HASH.fullmatch(receipt[k]) is not None
                        for k in ("old_launcher_sha256", "new_launcher_sha256",
                                  "candidate_operator_sha256"))
                and receipt["old_launcher_sha256"] !=
                    receipt["new_launcher_sha256"],
                "RECOVERY_RECEIPT_FIELDS_INVALID")
        old = trusted_file(state / ("old-" + cut + ".bin"), 0o700)
        new = trusted_file(live, 0o700)
        require(sha(old) == receipt["old_launcher_sha256"]
                and sha(new) == receipt["new_launcher_sha256"],
                "RECOVERY_BACKUP_OR_LIVE_HASH_DRIFT")
        timestamp = int(time.time()) if now is None else now
        nonce = validate_approval(approval, receipt, now=timestamp)
        if sandbox_gate is not None:
            sandbox_gate(receipt, sha(new), sha(old), timestamp)
        approved_snapshot = dict(approval)
        intent = state / ("rollback-intent-" + nonce + ".json")
        current = state / ("rollback-new-" + nonce + ".bin")
        final = state / ("rollback-receipt-" + nonce + ".json")
        stage = live_dir / (".rollback-" + nonce + ".tmp")
        require(all(not p.exists() and not p.is_symlink()
                    for p in (intent, current, final, stage)),
                "RECOVERY_REPLAY_BLOCKED")
        checkpoint("before_intent")
        record = {"status": "ROLLBACK_INTENT_NO_DEPLOYMENT",
                  "cut_nonce": cut, "recovery_nonce": nonce,
                  "source_sha": receipt["source_sha"],
                  "release_asset_sha256": receipt["release_asset_sha256"],
                  "old_launcher_sha256": sha(old),
                  "new_launcher_sha256": sha(new)}
        durable(intent, encode(record), mode=0o600)
        checkpoint("after_intent")
        durable(current, new, mode=0o700)
        require(sha(trusted_file(current, 0o700)) == sha(new),
                "RECOVERY_DURABLE_NEW_BACKUP_DRIFT")
        checkpoint("after_backup")
        durable(stage, old, mode=0o700)
        require(sha(trusted_file(live, 0o700)) == sha(new)
                and sha(trusted_file(stage, 0o700)) == sha(old),
                "RECOVERY_PRE_SWAP_DRIFT")
        last_nonce = validate_approval(
            approval, receipt, now=int(time.time()) if now is None else now)
        require(approval == approved_snapshot and last_nonce == nonce,
                "RECOVERY_APPROVAL_CHANGED_DURING_TRANSACTION")
        if sandbox_gate is not None:
            sandbox_gate(receipt, sha(new), sha(old),
                         int(time.time()) if now is None else now)
        checkpoint("before_swap")
        os.replace(stage, live)
        sync_dir(live_dir)
        checkpoint("after_swap")
        require(sha(trusted_file(live, 0o700)) == sha(old),
                "RECOVERY_POST_SWAP_DRIFT")
        durable(final, encode({**record, "status": "ROLLBACK_ATOMIC_SWAP_COMMITTED"}),
                mode=0o600)
        checkpoint("after_receipt")
        intent.unlink()
        sync_dir(state)
        return {"status": "SIMULATED_LAUNCHER_ROLLBACK_PASS_NO_HOST_CHANGE",
                "old_launcher_sha256": sha(old),
                "recovery_nonce_sha256": sha(nonce.encode()),
                "may_install": False, "may_apply": False,
                "production_touched": False}


def simulate_delayed_dual_recovery(root, recovery_claim, dual_approval, *,
                                   now=None, checkpoint=lambda stage: None):
    """ONLY /tmp simulation; an untrusted contract result NEVER grants root authority.

    The existing fsync+swap sandbox is reused *after* validating original
    receipt A and separate recovery claim B under the same sandbox lock.
    Both sources/consent are rechecked before swap. This is deliberately NOT a
    privileged bootstrap, Sigstore verifier, approval or host recovery tool.
    """
    require(type(recovery_claim) is dict and type(dual_approval) is dict,
            "DELAYED_SANDBOX_INPUT_INVALID")
    spec = importlib.util.spec_from_file_location(
        "qa525_sandbox_delayed_contract",
        Path(__file__).with_name("qa525-launcher-replacement-contract.py"))
    require(spec is not None and spec.loader is not None,
            "DELAYED_SANDBOX_CONTRACT_MISSING")
    policy = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(policy)
    fields = {
        "action": "rollback-root-launcher-supervised",
        "scope": SCOPE,
        "operator": "soporte@glmdch",
        "approved": dual_approval.get("approved"),
        "cut_nonce": dual_approval.get("cut_nonce"),
        "nonce": dual_approval.get("nonce"),
        "source_sha": dual_approval.get("cut_source_sha"),
        "release_asset_sha256": dual_approval.get("cut_release_asset_sha256"),
        "old_launcher_sha256": dual_approval.get("old_launcher_sha256"),
        "new_launcher_sha256": dual_approval.get("new_launcher_sha256"),
        "issued_at": dual_approval.get("issued_at"),
        "expires_at": dual_approval.get("expires_at"),
    }
    # Snapshot immutable input identity; do not rely on the mutable act alone.
    frozen_claim = encode(recovery_claim)
    frozen_approval = encode(dual_approval)

    def review_gate(receipt, current_sha, old_sha, stamp):
        require(encode(recovery_claim) == frozen_claim and
                encode(dual_approval) == frozen_approval,
                "DELAYED_SANDBOX_CONSENT_OR_CLAIM_CHANGED")
        try:
            review = policy.assess_delayed_recovery_plan(
                receipt, recovery_claim, dual_approval,
                live_sha256=current_sha, backup_sha256=old_sha,
                pending=False, now=stamp)
        except policy.Blocked:
            raise Blocked("DELAYED_SANDBOX_POLICY_REJECTED") from None
        require(review.get("status") ==
                    "REVIEWED_DELAYED_DUAL_PROVENANCE_NO_EXECUTION" and
                review.get("claims_authenticated") is False and
                review.get("may_recover") is False,
                "DELAYED_SANDBOX_MUST_NOT_AUTHORIZE_ROOT")

    report = rollback(root, fields, now=now, checkpoint=checkpoint,
                      sandbox_gate=review_gate)
    return {**report, "status":
            "SIMULATED_DELAYED_DUAL_PROVENANCE_NO_HOST_CHANGE",
            "recovery_claim_authenticated": False,
            "approved_for_host_recovery": False}


if __name__ == "__main__":
    raise SystemExit("RECOVERY_SANDBOX_LIBRARY_ONLY_NO_HOST_ENTRYPOINT")
