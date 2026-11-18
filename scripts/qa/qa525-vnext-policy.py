#!/usr/bin/env python3
"""QA525 vNext migration policy binding — pure, INACTIVE; no signature verification or host I/O.

The real adapter MUST authenticate the release and Sponsor approval independently,
then pass trusted_pins derived from those authenticated bytes, never from the plan.
This module only binds the exact migration plan to those pins. It does not certify
that a SQL migration is additive: those flags require separately reviewed evidence.
"""
import hashlib
import hmac
import json
import re

DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")
SOURCE = re.compile(r"[0-9a-f]{40}\Z")
MIGRATION_ID = re.compile(r"[0-9]{8,14}_[A-Za-z0-9_]{1,100}\Z")
FLAGS = ("additive", "backward_compatible_with_api_n", "arbitrary_sql",
         "unbounded_backfill", "incompatible_ddl_lock", "needs_api_n_plus_1")
FIELDS = frozenset(("id",) + FLAGS)
PLAN_FIELDS = frozenset(("ordered_set_sha256", "migrations"))


def canonical(obj):
    return (json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=True,
                       allow_nan=False) + "\n").encode("ascii")


def plan_digest(plan):
    """Canonical digest includes ordered IDs AND all compatibility flags."""
    if type(plan) is not dict or set(plan) != PLAN_FIELDS:
        raise ValueError("PLAN_FIELDS")
    migrations = plan["migrations"]
    if type(migrations) is not list or not migrations:
        raise ValueError("PLAN_MIGRATIONS")
    for m in migrations:
        if type(m) is not dict or set(m) != FIELDS:
            raise ValueError("PLAN_MIGRATION_FIELDS")
        if (type(m["id"]) is not str or not MIGRATION_ID.fullmatch(m["id"])
                or any(type(m[k]) is not bool for k in FLAGS)):
            raise ValueError("PLAN_MIGRATION_TYPES")
    if type(plan["ordered_set_sha256"]) is not str or not DIGEST.fullmatch(plan["ordered_set_sha256"]):
        raise ValueError("PLAN_SET_DIGEST")
    ids = [m["id"] for m in migrations]
    if ids != sorted(set(ids)):
        raise ValueError("PLAN_IDS_ORDER_OR_DUPLICATE")
    ordered_sha = "sha256:" + hashlib.sha256(("\n".join(ids) + "\n").encode("ascii")).hexdigest()
    if not hmac.compare_digest(plan["ordered_set_sha256"], ordered_sha):
        raise ValueError("PLAN_ORDERED_SET_MISMATCH")
    return "sha256:" + hashlib.sha256(canonical(plan)).hexdigest()


def plan_attested(plan, release, trusted_pins):
    """Fail closed; trusted_pins must originate from authenticated release, not caller input.

    trusted_pins = {release_sha, plan_sha256, baseline_images, expected_images}
    Binding the image pairs here prevents a caller from substituting unverified
    baseline/target digests after the release signature was checked.
    """
    try:
        if type(release) is not dict or type(trusted_pins) is not dict:
            return False
        if set(trusted_pins) != {"release_sha", "plan_sha256", "baseline_images", "expected_images"}:
            return False
        source = release.get("sha")
        if type(source) is not str or not SOURCE.fullmatch(source):
            return False
        if not hmac.compare_digest(source, trusted_pins["release_sha"]):
            return False
        for field in ("baseline_images", "expected_images"):
            got, want = release.get(field), trusted_pins[field]
            if type(got) is not dict or type(want) is not dict or set(got) != {"api", "web"} or set(want) != {"api", "web"}:
                return False
            for service in ("api", "web"):
                if (type(got[service]) is not str or type(want[service]) is not str
                        or not DIGEST.fullmatch(got[service])
                        or not hmac.compare_digest(got[service], want[service])):
                    return False
        expected = trusted_pins["plan_sha256"]
        return (type(expected) is str and DIGEST.fullmatch(expected) is not None
                and hmac.compare_digest(plan_digest(plan), expected))
    except (ValueError, TypeError, KeyError, UnicodeError, OverflowError):
        return False
