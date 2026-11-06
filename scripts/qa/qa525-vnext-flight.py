#!/usr/bin/env python3
"""QA525 vNext flight orchestrator -- INACTIVE library, NO real adapters.

Composes the already reviewed pieces (journal lease, durable stage store,
verifier evidence, terminal writer) around caller-injected callables. It
contains NO subprocess, network, Docker or host access and NO authentication:
the injected ``gate`` must be the authenticated bootstrap/locks/G6/Sponsor
check of the future engine, and the injected adapters are the only code that
could ever touch a host. Passing these tests proves ordering and fail-closed
behaviour with fakes, never permission to APPLY or a physical result.

Rules enforced here:
* ONE journal module instance owns every lease: the stage store and the
  terminal writer are rebound to it (they load private copies otherwise, and
  a lease from one copy is rejected by the other -- see PR #863 notes).
* A stage intent is persisted (fsync, hash chained) BEFORE its action runs and
  only after ``gate(stage)`` returned True.
* A verification stage only advances from a verifier result that passes
  ``require_ok`` for this plan (evidence glue), never from a caller claim.
* After any failure past the first persisted intent the engine tries ONE
  verified rollback; if that cannot be proven it persists RECOVERY_REQUIRED
  and stops. It never retries an action and never rolls back a flight whose
  only failure is the terminal write.
* EVERY mutation, rollback included, needs ``gate(stage)`` to return True. A
  denied rollback is never executed: RECOVERY_REQUIRED is persisted instead.
* An in-memory stage is never trusted to say whether a durable write happened:
  after any failed write the chain is re-read under the lease. If the durable
  tip is not exactly the previous plan the outcome is AMBIGUOUS and the engine
  persists RECOVERY_REQUIRED (when it still can) without running anything.
* If the terminal record cannot be written after a verified rollback, the
  plan stays at ROLLBACK_VERIFYING (the plan has no successor there) and the
  engine raises FLIGHT_ROLLBACK_VERIFIED_TERMINAL_UNRECORDED: the durable
  evidence is retained and the operator reconciles; nothing more is executed.
Exceptions are replaced by fixed codes; external text is never echoed.
"""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
STATUS = "FLIGHT_RESULT_NOT_AUTHENTICATED_NOT_AUTHORIZED_FOR_PHYSICAL_APPLY"


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def _load(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


journal = _load("qa525_vnext_journal_for_flight", "qa525-vnext-journal.py")
plan_mod = _load("qa525_vnext_plan_for_flight", "qa525-vnext-baseline-plan.py")
evidence = _load("qa525_vnext_evidence_for_flight", "qa525-vnext-evidence.py")
store = _load("qa525_vnext_stage_store_for_flight", "qa525-vnext-stage-store.py")
terminal = _load("qa525_vnext_terminal_for_flight", "qa525-vnext-terminal.py")

# Single journal owner: leases minted by journal.begin() here are the only
# ones the store and the terminal writer will accept.
store.journal = journal
terminal.journal = journal
if not (store.journal is terminal.journal is journal):  # pragma: no cover
    raise Stop("FLIGHT_JOURNAL_NOT_SHARED")


def _intent_proof(plan: dict, stage: str) -> dict:
    """Deterministic, flight-bound intent reference (NOT authenticated).

    Unique per plan state because it covers the current chain checksum."""
    raw = json.dumps({"chain": plan["chain_sha256"], "stage": stage,
                      "intent": True}, sort_keys=True).encode("utf-8")
    return {
        "kind": plan_mod.PROOF_KINDS[stage], "stage": stage,
        "nonce": plan["nonce"], "admission_sha256": plan["admission_sha256"],
        "baseline_sha256": plan["baseline_sha256"],
        "target_sha256": plan["target_sha256"],
        "evidence_sha256": "sha256:" + hashlib.sha256(raw).hexdigest(),
    }


def _tip_is(lease, plan):
    """True only if the durable chain ends exactly at ``plan``."""
    try:
        _root, _nonce, _admission, rows = store._chain(lease)
        return (bool(rows) and rows[-1][0]["plan"]["chain_sha256"] == plan["chain_sha256"]
                and len(rows) == len(plan["events"]) + 1)
    except Exception:
        return False


def _persist(lease, plan, checkpoint, prior):
    try:
        store.append(lease, plan, checkpoint)
    except Exception:
        # Never infer "nothing was written" from memory: re-read the chain.
        if _tip_is(lease, prior):
            raise Stop("FLIGHT_PERSIST_FAILED") from None
        raise Stop("FLIGHT_PERSIST_AMBIGUOUS") from None
    return plan


def _intent(lease, plan, stage, gate, checkpoint):
    """gate -> advance -> durable write. The action may run only afterwards."""
    try:
        allowed = gate(stage)
    except Exception:
        raise Stop("FLIGHT_GATE_DENIED") from None
    if allowed is not True:
        raise Stop("FLIGHT_GATE_DENIED")
    try:
        nxt = plan_mod.advance(plan, stage, _intent_proof(plan, stage))
    except Exception:
        raise Stop("FLIGHT_STAGE_REJECTED") from None
    return _persist(lease, nxt, checkpoint, plan)


def _verified(lease, plan, stage, report, checkpoint):
    try:
        nxt = evidence.advance_with_verification(plan, stage, report)
    except Exception:
        raise Stop("FLIGHT_VERIFICATION_REJECTED") from None
    return _persist(lease, nxt, checkpoint, plan)


def _call(function, code):
    if not callable(function):
        raise Stop("FLIGHT_ADAPTER_INVALID")
    try:
        return function()
    except Exception:
        raise Stop(code) from None


def _observe(observe, kind):
    if not callable(observe):
        raise Stop("FLIGHT_ADAPTER_INVALID")
    try:
        return observe(kind)
    except Exception:
        raise Stop("FLIGHT_OBSERVE_FAILED") from None


def _finish(lease, plan, kind, report, final_stage, now):
    target = None if kind == "ROLLED_BACK" else plan["target"]
    try:
        expected = evidence.verify.expected_target_sha256(plan["baseline"], kind, target)
        terminal.finish(lease, kind, report, plan["baseline_sha256"], expected,
                        final_stage, now)
    except Exception:
        raise Stop("FLIGHT_TERMINAL_FAILED") from None


def _stop(lease, plan, checkpoint):
    """Persist RECOVERY_REQUIRED when possible, then stop. Never retries."""
    if plan["stage"] == "ROLLBACK_VERIFYING":
        # The plan has no successor here: keep the durable evidence as it is.
        raise Stop("FLIGHT_ROLLBACK_VERIFIED_TERMINAL_UNRECORDED")
    try:
        if plan["stage"] != "RECOVERY_REQUIRED":
            nxt = plan_mod.advance(plan, "RECOVERY_REQUIRED",
                                   _intent_proof(plan, "RECOVERY_REQUIRED"))
            store.append(lease, nxt, checkpoint)
    except Exception:
        raise Stop("FLIGHT_RECOVERY_REQUIRED_UNPERSISTED") from None
    raise Stop("FLIGHT_RECOVERY_REQUIRED")


def _result(plan, status, cause=None):
    return {
        "schema": "qa525-flight-v1", "status": status, "result": STATUS,
        "stage": plan["stage"], "nonce": plan["nonce"],
        "baseline_sha256": plan["baseline_sha256"],
        "target_sha256": plan["target_sha256"], "cause": cause,
        "may_apply": False, "may_install": False,
        "signature_verified_here": False, "physical_runtime_verified": False,
    }


def _rollback(lease, plan, rollback, observe, gate, now, checkpoint):
    plan = _intent(lease, plan, "ROLLING_BACK", gate, checkpoint)
    _call(rollback, "FLIGHT_ROLLBACK_FAILED")
    report = _observe(observe, "ROLLED_BACK")
    plan = _verified(lease, plan, "ROLLBACK_VERIFYING", report, checkpoint)
    _finish(lease, plan, "ROLLED_BACK", report, "ROLLBACK_VERIFYING", now)
    return plan


def run_apply(lease, plan, *, switch_api, switch_web, rollback, observe,
              gate, now, checkpoint=None):
    """Run one APPLY flight with injected callables; see module docstring.

    ``plan`` must be the initial APPLYING plan of this lease's flight. Returns
    a non-authoritative result (APPLIED or ROLLED_BACK); raises Stop with a
    fixed code when the flight cannot be proven either way.
    """
    if not callable(gate):
        raise Stop("FLIGHT_GATE_REQUIRED")
    try:
        store.initialize(lease, plan, checkpoint)
    except Exception:
        raise Stop("FLIGHT_LEASE_OR_PLAN_REJECTED") from None
    current = copy.deepcopy(plan)
    try:
        current = _intent(lease, current, "API_SWITCHING", gate, checkpoint)
        _call(switch_api, "FLIGHT_API_SWITCH_FAILED")
        report = _observe(observe, "API_SWITCHED")
        current = _verified(lease, current, "API_VERIFIED", report, checkpoint)
        current = _intent(lease, current, "WEB_SWITCHING", gate, checkpoint)
        _call(switch_web, "FLIGHT_WEB_SWITCH_FAILED")
        report = _observe(observe, "WEB_SWITCHED")
        current = _verified(lease, current, "WEB_VERIFIED", report, checkpoint)
        report = _observe(observe, "APPLIED")
        current = _verified(lease, current, "VERIFYING", report, checkpoint)
    except Stop as exc:
        cause = str(exc)
        if cause == "FLIGHT_PERSIST_AMBIGUOUS":
            # A durable write may or may not have happened: run NOTHING more.
            _stop(lease, _latest(lease, current), checkpoint)
        if current["stage"] == "APPLYING":
            raise  # nothing mutated: no rollback, no recovery marker
        try:
            final = _rollback(lease, current, rollback, observe, gate, now, checkpoint)
        except Exception:
            _stop(lease, _latest(lease, current), checkpoint)
        return _result(final, "ROLLED_BACK", cause)
    try:
        _finish(lease, current, "APPLIED", report, "VERIFYING", now)
    except Stop:
        _stop(lease, current, checkpoint)  # verified but unrecorded: no mutation
    return _result(current, "APPLIED")


def _latest(lease, fallback):
    """Last durably persisted plan, falling back to the in-memory one."""
    try:
        _root, _nonce, _admission, rows = store._chain(lease)
        return copy.deepcopy(rows[-1][0]["plan"]) if rows else fallback
    except Exception:
        return fallback


if __name__ == "__main__":
    raise SystemExit("QA525_FLIGHT_LIBRARY_ONLY_NO_EXECUTION")
