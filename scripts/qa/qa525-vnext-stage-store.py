#!/usr/bin/env python3
"""QA525 vNext durable *stage snapshots*, INACTIVE / NO-APPLY library.

A checkpoint is persisted under a genuine journal Lease before its caller may
attempt the corresponding external action. There is NO action/runner interface
here, and neither a checksum nor a stage can authorize installation or APPLIED.

The future trusted engine MUST use this module's journal.begin() (the SAME
module instance) for the entire flight, obtain independent authenticated
OPS/Sponsor/G6 approval and hold all legacy locks before any real mutation.
"""
from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import re

HERE = Path(__file__).resolve().parent
SCHEMA = "qa525-stage-snapshot-v1"
STATUS = "PERSISTED_STAGE_ONLY_NOT_AUTHORIZED_FOR_APPLY"
MAX_FILES = 13
MAX_SCAN = 10000
STAGE_FILE = re.compile(r"stage-([A-Za-z0-9_-]{24,96})-(\d{4})\.json\Z")
FIELDS = frozenset({
    "schema", "sequence", "nonce", "admission_sha256", "baseline_sha256",
    "target_sha256", "stage", "plan", "plan_sha256", "previous_record_sha256",
    "may_apply", "signature_verified_here",
})


class Stop(Exception):
    """Static fail-closed diagnostic; never includes external content."""


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


journal = _load("qa525_vnext_journal_for_stage_store", "qa525-vnext-journal.py")
plan_mod = _load("qa525_vnext_plan_for_stage_store", "qa525-vnext-baseline-plan.py")


def _canonical(value):
    try:
        return (json.dumps(value, sort_keys=True, separators=(",", ":"),
                           ensure_ascii=True, allow_nan=False) + "\n").encode("utf-8")
    except (TypeError, ValueError, RecursionError):
        raise Stop("STAGE_JSON_INVALID") from None


def _digest(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def _unique(pairs):
    out = {}
    for key, value in pairs:
        if key in out:
            raise Stop("STAGE_JSON_DUPLICATE")
        out[key] = value
    return out


def _validate_plan(value):
    try:
        stage = plan_mod.validate(value)
        raw = _canonical(value)
    except Exception:
        raise Stop("STAGE_PLAN_INVALID") from None
    if type(stage) is not str or type(value["events"]) is not list:
        raise Stop("STAGE_PLAN_INVALID")
    return stage, _digest(raw)


def _flight(lease):
    # The exact Lease must be minted by this copy of journal.begin(), not a
    # fabricated object, fd of another file or Lease from another import.
    if not journal.lease_is_held(lease):
        raise Stop("STAGE_LEASE_NOT_HELD")
    try:
        root = Path(lease.root)
        if journal.inspect(root, lease.nonce) != "APPLYING":
            raise Stop("STAGE_FLIGHT_NOT_APPLYING")
        marker, _entry, _recovery = journal._paths(root, lease.nonce)
        m = journal._object(marker)
        if (m.get("schema") != "qa525-nonce-v1"
                or m.get("nonce") != lease.nonce
                or type(m.get("admission_sha256")) is not str
                or journal.DIGEST.fullmatch(m["admission_sha256"]) is None):
            raise Stop("STAGE_MARKER_INVALID")
        return root, lease.nonce, m["admission_sha256"]
    except Stop:
        raise
    except (journal.Stop, OSError, AttributeError, TypeError):
        raise Stop("STAGE_JOURNAL_INVALID") from None


def _paths(root, nonce):
    try:
        entries = list(root.iterdir())
    except OSError:
        raise Stop("STAGE_SCAN_FAILED") from None
    if len(entries) > MAX_SCAN:
        raise Stop("STAGE_SCAN_LIMIT")
    prefix = "stage-" + nonce + "-"
    found = {}
    for entry in entries:
        name = entry.name
        if name.startswith(".qa525-next-"):
            # A crash between temp fsync, hardlink and directory fsync is
            # *ambiguous*: fail closed, retain evidence for operator recovery.
            raise Stop("STAGE_CRASH_RESIDUE")
        if name.startswith("stage-"):
            match = STAGE_FILE.fullmatch(name)
            if match is None:
                raise Stop("STAGE_UNRECOGNIZED_ENTRY")
            if not name.startswith(prefix):
                raise Stop("STAGE_FOREIGN_NONCE")
            idx = int(match.group(2))
            if idx in found or idx >= MAX_FILES:
                raise Stop("STAGE_SEQUENCE_INVALID")
            found[idx] = entry
    if found and sorted(found) != list(range(len(found))):
        raise Stop("STAGE_CHAIN_GAP")
    return [found[i] for i in range(len(found))]


def _read(path):
    try:
        raw = journal._read_private(path)  # no symlink, owner/mode/fstat checks
        parsed = json.loads(raw.decode("utf-8"), object_pairs_hook=_unique,
                            parse_constant=lambda _: (_ for _ in ()).throw(
                                Stop("STAGE_JSON_NONFINITE")))
        if type(parsed) is not dict or raw != _canonical(parsed):
            raise Stop("STAGE_NONCANONICAL")
        if set(parsed) != FIELDS or parsed["schema"] != SCHEMA:
            raise Stop("STAGE_FIELDS_INVALID")
        if (parsed["may_apply"] is not False
                or parsed["signature_verified_here"] is not False):
            raise Stop("STAGE_AUTHORITY_FORGED")
        return parsed, _digest(raw)
    except Stop:
        raise
    except (journal.Stop, UnicodeDecodeError, ValueError, TypeError, RecursionError):
        raise Stop("STAGE_UNTRUSTED_SNAPSHOT") from None


def _check_record(rec, digest, idx, previous, nonce, admission, anchor):
    plan = rec["plan"]
    stage, plan_hash = _validate_plan(plan)
    if (type(rec["sequence"]) is not int or rec["sequence"] != idx
            or rec["nonce"] != nonce or rec["admission_sha256"] != admission
            or rec["baseline_sha256"] != plan["baseline_sha256"]
            or rec["target_sha256"] != plan["target_sha256"]
            or rec["plan_sha256"] != plan_hash
            or rec["stage"] != stage
            or rec["previous_record_sha256"] != previous
            or len(plan["events"]) != idx):
        raise Stop("STAGE_CHAIN_MISMATCH")
    lineage = (plan["nonce"], plan["admission_sha256"],
               plan["baseline_sha256"], plan["target_sha256"])
    if lineage[0] != nonce or lineage[1] != admission:
        raise Stop("STAGE_FLIGHT_MISMATCH")
    if anchor is not None and lineage != anchor:
        raise Stop("STAGE_CUT_DRIFT")
    if idx == 0 and (stage != "APPLYING" or plan["events"]):
        raise Stop("STAGE_INITIAL_INVALID")
    return lineage


def _chain(lease):
    root, nonce, admission = _flight(lease)
    paths = _paths(root, nonce)
    prior_digest = None
    prior_plan = None
    anchor = None
    rows = []
    for idx, path in enumerate(paths):
        rec, fingerprint = _read(path)
        anchor = _check_record(rec, fingerprint, idx, prior_digest, nonce,
                               admission, anchor)
        if prior_plan is not None:
            events = rec["plan"]["events"]
            if events[:-1] != prior_plan["events"]:
                raise Stop("STAGE_EVENT_REPLAY_OR_DRIFT")
        prior_digest, prior_plan = fingerprint, rec["plan"]
        rows.append((rec, fingerprint))
    return root, nonce, admission, rows


def _record(plan, index, previous):
    _validate_plan(plan)
    return {
        "schema": SCHEMA, "sequence": index, "nonce": plan["nonce"],
        "admission_sha256": plan["admission_sha256"],
        "baseline_sha256": plan["baseline_sha256"],
        "target_sha256": plan["target_sha256"], "stage": plan["stage"],
        "plan": copy.deepcopy(plan), "plan_sha256": _digest(_canonical(plan)),
        "previous_record_sha256": previous, "may_apply": False,
        "signature_verified_here": False,
    }


def _summary(rec, fingerprint):
    return {
        "status": STATUS, "stage": rec["stage"],
        "sequence": rec["sequence"], "record_sha256": fingerprint,
        "nonce": rec["nonce"], "may_apply": False, "may_install": False,
        "signature_verified_here": False, "external_runtime_verified": False,
    }


def inspect(lease):
    """Read and validate the complete immutable chain under the same lease."""
    _root, _nonce, _admission, records = _chain(lease)
    if not records:
        raise Stop("STAGE_NOT_INITIALIZED")
    rec, fingerprint = records[-1]
    return _summary(rec, fingerprint)


def initialize(lease, plan, checkpoint=None):
    """Persist APPLYING baseline snapshot after journal.begin, before mutation."""
    root, nonce, admission, rows = _chain(lease)
    if rows:
        raise Stop("STAGE_ALREADY_INITIALIZED")
    stage, _hash = _validate_plan(plan)
    if (stage != "APPLYING" or plan["events"]
            or plan["nonce"] != nonce or plan["admission_sha256"] != admission):
        raise Stop("STAGE_INITIAL_MISMATCH")
    rec = _record(plan, 0, None)
    path = root / ("stage-" + nonce + "-0000.json")
    try:
        journal._atomic_new(path, rec, checkpoint)
    except journal.Stop:
        raise Stop("STAGE_WRITE_FAILED") from None
    return inspect(lease)


def append(lease, next_plan, checkpoint=None):
    """Persist ONE monotonic stage transition, using NOREPLACE+file/dir fsync.

    For a mutating stage (API_SWITCHING/WEB_SWITCHING/ROLLING_BACK), the future
    engine MUST call this BEFORE performing any external operation. This code
    itself never performs the operation and cannot authorize it.
    """
    root, nonce, admission, rows = _chain(lease)
    if not rows:
        raise Stop("STAGE_NOT_INITIALIZED")
    if len(rows) >= MAX_FILES:
        raise Stop("STAGE_HISTORY_FULL")
    current, previous_sha = rows[-1]
    stage, _hash = _validate_plan(next_plan)
    old = current["plan"]
    if (next_plan["nonce"] != nonce or next_plan["admission_sha256"] != admission
            or next_plan["baseline_sha256"] != old["baseline_sha256"]
            or next_plan["target_sha256"] != old["target_sha256"]
            or next_plan["baseline"] != old["baseline"]
            or next_plan["target"] != old["target"]
            or len(next_plan["events"]) != len(old["events"]) + 1
            or next_plan["events"][:-1] != old["events"]
            or next_plan["events"][-1]["from"] != old["stage"]
            or next_plan["events"][-1]["to"] != stage):
        raise Stop("STAGE_TRANSITION_MISMATCH")
    rec = _record(next_plan, len(rows), previous_sha)
    path = root / ("stage-" + nonce + "-%04d.json" % len(rows))
    try:
        journal._atomic_new(path, rec, checkpoint)
    except journal.Stop:
        raise Stop("STAGE_WRITE_FAILED") from None
    return inspect(lease)


if __name__ == "__main__":
    raise SystemExit("QA525_STAGE_STORE_LIBRARY_ONLY_NO_EXECUTION")
