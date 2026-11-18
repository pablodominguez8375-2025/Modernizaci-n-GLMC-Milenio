#!/usr/bin/env python3
"""QA525 successor baseline: pure, offline decision helpers (DRAFT, INACTIVE).

Nothing here touches Docker, the host, GitHub write APIs, files or the network.
It is NOT wired into any operator yet; it only fixes the three decisions that the
fixed 6ff4ae0d baseline got wrong for every update after the first one:

1. the analytical baseline of a new update is the LAST APPLIED, LIVE and HEALTHY
   cut (never a constant);
2. the diff baseline->target must be complete and ancestral: GitHub's compare API
   returns at most 300 files, so a response with 300 files (or more) is treated as
   TRUNCATED and stops the update instead of being "approved partially";
3. the target is FROZEN (sha + signed asset digest) by the caller before any
   preflight, so a moving ``dev`` can never change what is evaluated.

Every function raises ``Blocked`` with a fixed public code and never echoes input.
"""
import re

SHA = re.compile(r"[0-9a-f]{40}\Z")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")
IMAGE = re.compile(r"sha256:[0-9a-f]{64}\Z")
API_FILE_CAP = 300      # GitHub compare returns at most this many files
MAX_AHEAD = 250         # GitHub compare reports at most 250 commits
TARGETS = ("api", "web")


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def freeze_target(selected, expected_sha, expected_digest):
    """The caller pins sha+digest BEFORE preflight; the current signed release must equal them."""
    require(type(expected_sha) is str and SHA.fullmatch(expected_sha), "TARGET_SHA_INVALID")
    require(type(expected_digest) is str and DIGEST.fullmatch(expected_digest), "TARGET_DIGEST_INVALID")
    require(isinstance(selected, dict)
            and selected.get("status") == "READY_FOR_SIGNATURE_VERIFICATION_ONLY",
            "TARGET_RELEASE_NOT_READY")
    require(selected.get("source_sha") == expected_sha, "TARGET_MOVED_OR_NOT_FROZEN")
    require(selected.get("release_asset_sha256") == expected_digest, "TARGET_DIGEST_CHANGED")
    return {"source_sha": expected_sha, "release_asset_sha256": expected_digest}


def resolve_baseline(prepared, journals, live, health):
    """Return the SHA of the last applied cut, or STOP.

    prepared: parsed prepared.json (dict) of the predecessor
    journals: {sha: parsed dual-<sha>.json} for EVERY dual journal present
    live:     {"api": [container_id, image_id, status], "web": [...]} read-only Docker view
    health:   callable(sha) -> True only when the public QA version/health match that sha
    """
    require(isinstance(prepared, dict) and type(prepared.get("source_sha")) is str
            and SHA.fullmatch(prepared["source_sha"]), "BASELINE_PREPARED_INVALID")
    require(isinstance(journals, dict) and journals, "BASELINE_JOURNAL_MISSING")
    for sha, record in journals.items():
        require(type(sha) is str and SHA.fullmatch(sha) and isinstance(record, dict)
                and record.get("source_sha") == sha, "BASELINE_JOURNAL_INVALID")
        # Any journal that is not APPLIED (PENDING, ROLLED_BACK, RECOVERY...) needs a human.
        require(record.get("status") == "APPLIED", "BASELINE_JOURNAL_NOT_APPLIED")
    sha = prepared["source_sha"]
    journal = journals.get(sha)
    require(journal is not None, "BASELINE_JOURNAL_FOR_PREPARED_MISSING")
    require(prepared.get("status") == "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
            "BASELINE_PREPARED_STATUS_INVALID")
    new_images = journal.get("new_images")
    require(isinstance(new_images, dict) and isinstance(live, dict), "BASELINE_IMAGES_INVALID")
    for name in TARGETS:
        image = prepared.get(name + "_image_id")
        row = live.get(name)
        require(type(image) is str and IMAGE.fullmatch(image)
                and new_images.get(name) == image, "BASELINE_PREPARED_JOURNAL_MISMATCH")
        require(isinstance(row, (list, tuple)) and len(row) >= 3
                and row[1] == image and row[2] == "running", "BASELINE_NOT_LIVE")
    require(health(sha) is True, "BASELINE_HEALTH_UNVERIFIED")
    return sha


def guard_diff(baseline, target, diff):
    """Strict ancestry and completeness. Returns the validated file list or STOPs.

    A response that reaches the API cap is never trusted: the real diff may be larger.
    """
    require(type(baseline) is str and SHA.fullmatch(baseline), "BASELINE_SHA_INVALID")
    require(type(target) is str and SHA.fullmatch(target), "TARGET_SHA_INVALID")
    require(baseline != target, "BASELINE_EQUALS_TARGET")
    require(isinstance(diff, dict) and diff.get("status") == "ahead"
            and isinstance(diff.get("merge_base_commit"), dict)
            and diff["merge_base_commit"].get("sha") == baseline
            and type(diff.get("behind_by")) is int and diff["behind_by"] == 0,
            "DIFF_NOT_ANCESTRAL")
    ahead, total = diff.get("ahead_by"), diff.get("total_commits")
    require(type(ahead) is int and 0 < ahead < MAX_AHEAD
            and (total is None or total == ahead), "DIFF_COMMITS_INCOMPLETE")
    files = diff.get("files")
    require(isinstance(files, list) and files, "DIFF_FILES_MISSING")
    # len == cap means "possibly truncated": STOP, never approve a partial list.
    require(len(files) < API_FILE_CAP, "DIFF_TRUNCATED_OR_OVERSIZE")
    return files


def decide(selected, expected_sha, expected_digest, prepared, journals, live, health, diff, classify):
    """One call for the whole offline decision. ``classify`` is the existing change policy.

    Returns the classification of baseline->frozen target. Anything sensitive keeps its
    existing STOP/review path (schema, identity, bootstrap, infrastructure changes).
    """
    target = freeze_target(selected, expected_sha, expected_digest)
    baseline = resolve_baseline(prepared, journals, live, health)
    require(baseline != target["source_sha"], "TARGET_ALREADY_APPLIED")
    guard_diff(baseline, target["source_sha"], diff)
    result = classify(baseline, selected, diff)
    require(isinstance(result, dict) and result.get("analytical_baseline_sha") == baseline
            and result.get("source_sha") == target["source_sha"]
            and result.get("may_apply") is False, "CLASSIFICATION_EVIDENCE_INVALID")
    return result
