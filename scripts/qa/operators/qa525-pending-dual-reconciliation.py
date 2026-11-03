"""Explicit archival of an unactivated pending dual cut; never grants apply."""
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import time

ACTION = "ARCHIVE_PENDING_DUAL_NO_DEPLOYMENT"
MARKER = "pending-dual-reconciliation.json"


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def validate(review, prepared, journal, cut, *, now, hashes, baseline, consumed):
    sha = review.get("source_sha")
    require(type(sha) is str and re.fullmatch(r"[a-f0-9]{40}", sha),
            "RECONCILE_SHA_INVALID")
    require(review.get("schema") == "qa525-pending-reconciliation-v1"
            and review.get("action") == ACTION
            and review.get("operator_name") == "soporte@glmdch"
            and review.get("operator_reviewed") is True,
            "RECONCILE_APPROVAL_INVALID")
    issued, expires = review.get("approved_at"), review.get("expires_at")
    require(type(issued) is int and type(expires) is int
            and 0 <= now - issued < 900 and issued < expires <= issued + 900
            and now < expires, "RECONCILE_APPROVAL_EXPIRED")
    for key in ("sponsor_approval_reference", "technical_review_reference"):
        require(type(review.get(key)) is str and re.fullmatch(
            r"https://github\.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-[1-9][0-9]*",
            review[key]), "RECONCILE_REVIEW_REFERENCE_INVALID")
    require(review.get("prepared_sha256") == hashes["prepared.json"]
            and review.get("journal_sha256") == hashes["journal"],
            "RECONCILE_EVIDENCE_CHANGED")
    require(prepared.get("source_sha") == journal.get("source_sha") == cut.get("target_sha") == sha
            and prepared.get("status") == "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT"
            and journal.get("status") == "PENDING"
            and prepared.get("baseline") == baseline
            and consumed is True, "RECONCILE_RUNTIME_OR_STATE_MISMATCH")
    require(review.get("release_asset_sha256") == prepared.get("source_asset_sha256")
            == cut.get("release_asset_sha256")
            and re.fullmatch(r"sha256:[a-f0-9]{64}", review.get("release_asset_sha256", "")),
            "RECONCILE_DIGEST_MISMATCH")
    for name in ("api", "web"):
        require(journal.get("old_images", {}).get(name) == baseline[name]["image_id"]
                and journal.get("new_images", {}).get(name) == prepared.get(name + "_image_id"),
                "RECONCILE_IMAGE_BINDING_CHANGED")
    return sha


def sync_dir(path):
    fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def write_private(path, obj):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "w") as f:
        json.dump(obj, f, sort_keys=True)
        f.flush()
        os.fsync(f.fileno())


def reconcile(entry, modules, approval_path):
    """Caller MUST hold build.exclusive(). Approval is specific, fresh and root0600."""
    core, build, approval, _, _, adapter, _, _ = modules
    marker = entry.STATE / MARKER
    require(not os.path.lexists(marker), "RECONCILE_INTERRUPTED_REQUIRES_REVIEW")
    review = entry.private_json(approval_path)
    sha = review.get("source_sha")
    require(type(sha) is str and re.fullmatch(r"[a-f0-9]{40}", sha), "RECONCILE_SHA_INVALID")
    journal_path = entry.STATE / ("dual-" + sha + ".json")
    paths = [entry.STATE / "prepared.json", entry.REVIEW, entry.SINGLE_OPERATOR_REVIEW,
             approval.APPROVAL, journal_path]
    require(len(set(paths + [approval_path])) == 6, "RECONCILE_PATH_COLLISION")
    records = [entry.private_json(p) for p in paths]
    prepared, sensitive, post, cut, journal = records
    require(not os.path.lexists(journal_path.with_name(journal_path.name + ".next")),
            "RECONCILE_JOURNAL_WRITE_INCOMPLETE")
    for p in entry.STATE.glob("dual-*.json"):
        if p != journal_path:
            require(entry.private_json(p).get("status") == "APPLIED",
                    "RECONCILE_OTHER_JOURNAL_INCOMPLETE")
    for obj in (sensitive, post):
        require(obj.get("source_sha") == sha
                and obj.get("release_asset_sha256") == review.get("release_asset_sha256"),
                "RECONCILE_RECORD_CUT_MISMATCH")
    nonce = cut.get("nonce")
    require(type(nonce) is str and re.fullmatch(r"[a-f0-9]{64}", nonce),
            "RECONCILE_NONCE_INVALID")
    used = entry.STATE / ("approval-consumed-" + nonce)
    entry.trusted(used)
    require(used.read_text() == sha + "\n", "RECONCILE_NONCE_NOT_CONSUMED")
    entry.single_operator_runtime(adapter.docker)
    live = adapter.metadata()
    require(set(live) == set(core.TARGETS + core.DEPENDENCIES + core.ONESHOTS),
            "RECONCILE_SERVICE_SET_CHANGED")
    require(all(live[n][2] == "running" for n in core.TARGETS + core.DEPENDENCIES)
            and all(live[n][2] == "exited" and live[n][11] == 0 for n in core.ONESHOTS),
            "RECONCILE_SERVICES_UNHEALTHY")
    entry.reviewed_layers(core, {n: live[n][7].split(",") for n in core.TARGETS}, entry.WORKDIR)
    baseline = build.snapshot(adapter.docker)
    hashes = {p.name: digest(p) for p in paths}
    validate(review, prepared, journal, cut, now=int(time.time()),
             hashes={"prepared.json": hashes["prepared.json"], "journal": hashes[journal_path.name]},
             baseline=baseline, consumed=True)
    # Read only known event format; any target event requires a separate recovery path.
    events = entry.STATE / "dual-events.log"
    if os.path.lexists(events):
        entry.trusted(events)
        require(events.stat().st_size <= 1048576, "RECONCILE_EVENTS_TOO_LARGE")
        require(not any(line.split()[-1:] == [sha] for line in events.read_text().splitlines()),
                "RECONCILE_TARGET_EVENT_PRESENT")
    old_sha = adapter.docker(["image", "inspect", "--format",
        '{{index .Config.Labels "org.opencontainers.image.revision"}}', baseline["web"]["image_id"]])
    require(type(old_sha) is str and re.fullmatch(r"[a-f0-9]{40}", old_sha)
            and old_sha != sha and entry.health(old_sha), "RECONCILE_OLD_HEALTH_FAILED")
    # Recheck all evidence and runtime immediately before archival.
    require(build.snapshot(adapter.docker) == baseline and adapter.metadata() == live
            and all(digest(p) == hashes[p.name] for p in paths),
            "RECONCILE_FINAL_DRIFT")
    require(int(time.time()) < review["expires_at"], "RECONCILE_APPROVAL_EXPIRED")
    archive = entry.STATE / ("pending-dual-archive-" + secrets.token_hex(12))
    archive.mkdir(mode=0o700)
    # Never roll back or remove this blocker automatically after an interrupted move.
    write_private(marker, {"status": "PENDING_DUAL_ARCHIVE_INCOMPLETE",
                  "source_sha": sha, "archive_name": archive.name, "files_sha256": hashes})
    sync_dir(entry.STATE)
    for p in paths:
        p.rename(archive / p.name)
        sync_dir(p.parent)
        sync_dir(archive)
    write_private(archive / "reconciliation-approval.json", review)
    write_private(archive / "receipt.json", {
        "status": "PENDING_DUAL_ARCHIVED_BASELINE_UNCHANGED_NO_DEPLOYMENT",
        "source_sha": sha, "files_sha256": hashes,
        "nonce_remains_consumed": True, "archived_at": int(time.time())})
    sync_dir(archive)
    sync_dir(entry.STATE)
    marker.unlink()
    sync_dir(entry.STATE)
    return {"status": "PENDING_DUAL_ARCHIVED_BASELINE_UNCHANGED_NO_DEPLOYMENT",
            "source_sha": sha, "may_apply": False}
