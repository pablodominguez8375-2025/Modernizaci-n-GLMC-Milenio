#!/usr/bin/env python3
"""QA525 schema-cut G6 v2 evidence wiring. INACTIVE library; verified PRE-4 loading only, never CLI.

`make(authority, claims, read_evidence, clock)` returns `(verifier, archive_epoch)` for the reviewed ports
(`make_verify_backup`). The verifier delegates to the reviewed `qa525-vnext-g6-v2-evidence.inspect` with EVERY pin taken
from the authenticated `authority["g6"]` (never from the GO, the plan or the evidence files) and with the clock and the
admission expiry supplied here. `archive_epoch` is the start stamp of the G6 archive named by the pinned review file.
This is a CONSISTENCY check, exactly as `inspect` documents: physical archive/offhost/restore authenticity stays an
external physical gate. Fixed codes only; evidence text is never echoed.
"""
import calendar
import datetime as dt
import json
import re

SIBLING_MODULES = {"evidence": "qa525-vnext-g6-v2-evidence.py"}
_DEPS = {}
SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")
STAMP = re.compile(r"centenario-qa-g6-v2-CIFRADO-(\d{4})(\d{2})(\d{2})T(\d{2})(\d{2})(\d{2})Z\.tar\.gz\Z")
PIN_KEYS = ("pinned_review_sha256", "pinned_receipt_sha256", "pinned_restore_sha256",
            "observed_archive_sha256", "downloaded_offhost_sha256")
G6_KEYS = frozenset(PIN_KEYS + ("expected_restic_repo_id", "expected_key_epoch"))
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
EPOCH = re.compile(r"[a-f0-9]{32}\Z")


class Stop(Exception):
    """Fixed code only."""


def bind_dependencies(mods):
    if type(mods) is not dict or set(mods) != set(SIBLING_MODULES):
        raise Stop("SCHEMA_G6_INPUT_INVALID")
    _DEPS.clear()
    _DEPS.update(mods)


def _need(ok):
    if not ok:
        raise Stop("SCHEMA_G6_INPUT_INVALID")


def make(authority, claims, read_evidence, clock):
    g6 = authority.get("g6") if type(authority) is dict else None
    _need(type(g6) is dict and set(g6) == G6_KEYS
          and all(type(g6.get(k)) is str and SHA.fullmatch(g6[k]) for k in PIN_KEYS)
          and type(g6.get("expected_restic_repo_id")) is str and HEX64.fullmatch(g6["expected_restic_repo_id"])
          and type(g6.get("expected_key_epoch")) is str and EPOCH.fullmatch(g6["expected_key_epoch"])
          and type(claims) is dict and type(claims.get("expires_at")) is int and not isinstance(claims["expires_at"], bool)
          and callable(read_evidence) and callable(clock) and set(_DEPS) == set(SIBLING_MODULES)
          and callable(getattr(_DEPS["evidence"], "inspect", None)))
    evidence = _DEPS["evidence"]
    try:
        review = json.loads(read_evidence("review").decode("utf-8"))
        name = review["g6_archive_name"]
        m = STAMP.fullmatch(name)
        epoch = calendar.timegm(tuple(int(x) for x in m.groups()) + (0, 0, 0))
    except Exception:  # noqa: BLE001
        raise Stop("SCHEMA_G6_EVIDENCE_INVALID") from None
    pins = {"review_sha256": g6["pinned_review_sha256"], "receipt_sha256": g6["pinned_receipt_sha256"],
            "restore_sha256": g6["pinned_restore_sha256"]}

    def verifier(data):
        try:
            evidence.inspect(review_bytes=data["review"], receipt_bytes=data["receipt"],
                             restore_bytes=data["restore"], pins=dict(pins),
                             observed_archive_sha256=g6["observed_archive_sha256"],
                             downloaded_offhost_sha256=g6["downloaded_offhost_sha256"],
                             expected_restic_repo_id=g6["expected_restic_repo_id"],
                             expected_key_epoch=g6["expected_key_epoch"], now=clock(),
                             admission_expires_at=dt.datetime.fromtimestamp(claims["expires_at"], dt.timezone.utc))
        except Exception:  # noqa: BLE001
            return False
        return True
    return verifier, epoch


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_G6_LIBRARY_ONLY_NO_GO")
