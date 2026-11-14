#!/usr/bin/env python3
"""QA525 auto-web F1 simulator (contract v1.2, table §6). PURE and INACTIVE.

Models one oneshot tick of the future `qa525-vnext-auto.py` WITHOUT touching anything: it
composes the real envelope checker and classifier with a scripted host (`events`) and returns
the outcome, the final state and what was spent. It is the executable form of the F1 table so
the orchestrator (ChatGPT) can be tested against the same vectors.

Invariants encoded here (lessons from the GO/G6 incidents, contract §8):
* NO QUEMAR: nothing is spent (sequence, release slot) unless every precheck passed and the
  durable APPLYING intent is the next step. reject / manual / no-op never spend.
* BLOCKED is sticky; only a new Sponsor policy generation or a manual GO clears it.
* One rollback at most; a failed rollback is BLOCKED with critical=true.
* A crash in APPLYING/VERIFYING is never retried automatically.
"""
from __future__ import annotations

import importlib.util
from pathlib import Path

_HERE = Path(__file__).resolve().parent


def _load(name):
    spec = importlib.util.spec_from_file_location("sim_" + name.replace("-", "_"), _HERE / (name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


ENVELOPE = _load("qa525-vnext-release-envelope")
CLASSIFIER = _load("qa525-vnext-release-classifier")

STATES = frozenset({"IDLE", "BLOCKED"})
EVENTS = frozenset({"lock_busy", "network_drop", "asset_count", "asset_oversize", "policy_ok",
                    "attestation_ok", "loaded_digest_ok", "image_shape_ok", "invariants_pre_ok",
                    "crash", "health_ok", "invariants_post_ok", "rollback_ok"})
DEFAULT_EVENTS = {"lock_busy": False, "network_drop": False, "asset_count": 1,
                  "asset_oversize": False, "policy_ok": True, "attestation_ok": True,
                  "loaded_digest_ok": True, "image_shape_ok": True, "invariants_pre_ok": True,
                  "crash": None, "health_ok": True, "invariants_post_ok": True, "rollback_ok": True}


def _out(outcome, code, state, spent=False, critical=False):
    return {"outcome": outcome, "code": code, "state": state, "spent": spent, "critical": critical}


def tick(*, state, raw, expect, now, events=None):
    """One tick. Returns {outcome, code, state, spent, critical}.

    outcome in {auto-web, manual, reject, no-op, BLOCKED}. `spent` means the sequence and one
    release slot of the policy were consumed (only after the durable APPLYING intent).
    """
    ev = dict(DEFAULT_EVENTS)
    if events:
        if not set(events) <= EVENTS:
            raise ValueError("unknown event")
        ev.update(events)
    if state not in STATES:
        raise ValueError("bad state")
    if ev["lock_busy"]:
        return _out("no-op", "LOCK_BUSY", state)
    if state == "BLOCKED":
        return _out("no-op", "BLOCKED_STICKY", "BLOCKED")
    if ev["network_drop"]:
        return _out("no-op", "DOWNLOAD_INTERRUPTED", "IDLE")
    if ev["asset_count"] != 1 or ev["asset_oversize"]:
        return _out("reject", "ASSET_INVALID", "IDLE")
    if not ev["policy_ok"]:
        return _out("reject", "POLICY_INVALID", "IDLE")
    if not ev["attestation_ok"]:
        return _out("reject", "ATTESTATION_INVALID", "IDLE")
    try:
        decision = ENVELOPE.check(raw, expect=expect, classifier_module=CLASSIFIER, now=now)
    except ENVELOPE.Stop as exc:
        return _out("reject", str(exc), "IDLE")
    if decision["decision"] == ENVELOPE.MANUAL:
        return _out("manual", ",".join(decision["reasons"]), "IDLE")
    if decision["decision"] == ENVELOPE.NO_OP_NOT_BEFORE:
        return _out("no-op", "NOT_BEFORE", "IDLE")
    # Host checks that need the loaded image and live state; still before spending anything.
    if not ev["invariants_pre_ok"]:
        return _out("reject", "INVARIANT_DRIFT_PRE", "IDLE")
    if not ev["loaded_digest_ok"]:
        return _out("reject", "LOADED_DIGEST_MISMATCH", "IDLE")
    if not ev["image_shape_ok"]:
        return _out("manual", "IMAGE_SHAPE_CHANGED", "IDLE")
    # Durable APPLYING intent written: from here the slot is spent.
    if ev["crash"] in ("APPLYING", "VERIFYING"):
        return _out("BLOCKED", "RECOVERY_REQUIRED_" + ev["crash"], "BLOCKED", spent=True)
    if ev["health_ok"] and ev["invariants_post_ok"]:
        return _out("auto-web", "ACTIVE", "IDLE", spent=True)
    if ev["rollback_ok"]:
        return _out("BLOCKED", "ROLLED_BACK_ONCE", "BLOCKED", spent=True)
    return _out("BLOCKED", "ROLLBACK_FAILED", "BLOCKED", spent=True, critical=True)


if __name__ == "__main__":
    raise SystemExit("QA525_AUTOWEB_SIMULATOR_LIBRARY_ONLY_NO_EXECUTION")
