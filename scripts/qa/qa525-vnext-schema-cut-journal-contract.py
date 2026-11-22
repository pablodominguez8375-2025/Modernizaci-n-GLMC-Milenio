#!/usr/bin/env python3
"""QA525 schema-cut phase contract. Pure library; no filesystem or execution authority.

The actual root child must persist each event durably under the held lock plane,
including MIGRATION_STARTED before first DDL. This reducer only rejects illegal
order, replay, divergent bindings and false rollback claims. It never runs a
migration, grants, API/WEB or restore.
"""
import hashlib
import json
import re

SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")
NONCE = re.compile(r"[0-9a-f]{64}\Z")
STEPS = (
    "RESERVED", "BACKUP_VERIFIED", "WRITES_QUIESCED", "MIGRATION_STARTED",
    "SCHEMA_104", "GRANTS_VERIFIED", "POSTGATE_PASS", "APP_STARTED", "APP_APPLIED", "UAT_PASS",
)
TERMINAL = frozenset({"PRE_DDL_STOP", "PARTIAL_SCHEMA_STOP", "APP_PARTIAL_STOP", "UAT_STOP"})
FIELDS = frozenset({"seq", "phase", "nonce", "go_sha256", "plan_sha256", "prev_sha256"})
ZERO = "sha256:" + "0" * 64


class Stop(Exception):
    """Fixed code only, never journal contents."""


def _canonical(event):
    return (json.dumps(event, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + "\n").encode("ascii")


def _validate_event(event, seq, previous, binding):
    if (type(event) is not dict or set(event) != FIELDS
            or type(event["seq"]) is not int or isinstance(event["seq"], bool) or event["seq"] != seq
            or type(event["phase"]) is not str or event["phase"] not in STEPS + tuple(TERMINAL)
            or type(event["nonce"]) is not str or NONCE.fullmatch(event["nonce"]) is None
            or any(type(event[k]) is not str or SHA.fullmatch(event[k]) is None
                   for k in ("go_sha256", "plan_sha256", "prev_sha256"))
            or {k: event[k] for k in ("nonce", "go_sha256", "plan_sha256")} != binding
            or event["prev_sha256"] != previous):
        raise Stop("SCHEMA_JOURNAL_EVENT_INVALID")
    return "sha256:" + hashlib.sha256(_canonical(event)).hexdigest()


def _failure_for(phase):
    if phase in STEPS[:3]:
        return "PRE_DDL_STOP"
    if phase in STEPS[3:7]:
        return "PARTIAL_SCHEMA_STOP"
    if phase == "APP_STARTED":
        return "APP_PARTIAL_STOP"
    return "UAT_STOP"


def next_event(events, binding, phase):
    """Return the next canonical event bytes; caller must fsync file+directory before an effect."""
    if (type(events) is not list or type(binding) is not dict
            or set(binding) != {"nonce", "go_sha256", "plan_sha256"}
            or type(phase) is not str or phase not in STEPS + tuple(TERMINAL)):
        raise Stop("SCHEMA_JOURNAL_INPUT_INVALID")
    previous, last = ZERO, None
    for seq, raw in enumerate(events):
        if type(raw) is not bytes or not 0 < len(raw) <= 1024:
            raise Stop("SCHEMA_JOURNAL_EVENT_INVALID")
        try:
            value = json.loads(raw.decode("ascii"))
        except (ValueError, UnicodeError, RecursionError):
            raise Stop("SCHEMA_JOURNAL_EVENT_INVALID") from None
        if raw != _canonical(value):
            raise Stop("SCHEMA_JOURNAL_EVENT_INVALID")
        previous = _validate_event(value, seq, previous, binding)
        current = value["phase"]
        if seq == 0 and current != "RESERVED":
            raise Stop("SCHEMA_JOURNAL_ORDER_INVALID")
        if seq > 0:
            if last in TERMINAL or last == "UAT_PASS":
                raise Stop("SCHEMA_JOURNAL_TERMINAL")
            wanted = (STEPS[STEPS.index(last) + 1]
                      if last in STEPS and last != "UAT_PASS" else None)
            if current != wanted and current != _failure_for(last):
                raise Stop("SCHEMA_JOURNAL_ORDER_INVALID")
        last = current
    if last in TERMINAL or last == "UAT_PASS":
        raise Stop("SCHEMA_JOURNAL_TERMINAL")
    if not events:
        if phase != "RESERVED":
            raise Stop("SCHEMA_JOURNAL_ORDER_INVALID")
    else:
        wanted = STEPS[STEPS.index(last) + 1]
        if phase != wanted and phase != _failure_for(last):
            raise Stop("SCHEMA_JOURNAL_ORDER_INVALID")
    event = dict(binding, seq=len(events), phase=phase, prev_sha256=previous)
    digest = _validate_event(event, len(events), previous, binding)
    return {"event": _canonical(event), "event_sha256": digest,
            "failure_class_if_interrupted": _failure_for(phase),
            "may_apply": False}


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_JOURNAL_CONTRACT_ONLY_NO_ROOT")
