#!/usr/bin/env python3
"""QA525 vNext INERT deterministic sequence simulator; no execution authority.

Uses only caller-supplied in-memory synthetic observations and structural
proofs. It invokes the existing pure planner/verifier libraries (injected by
the offline tests). It has NO journal writer, locks, Docker/host/network,
filesystem access, signature verification, authenticated G6 or approval.
A passing simulation is never evidence for physical QA or permission to APPLY.
"""
import copy

# VERIFYING/ROLLBACK_VERIFYING are NOT intents: they advance only with a proof
# derived from the verifier result (same rule as qa525-vnext-evidence.py).
APPLY_INTENTS = ("API_SWITCHING", "WEB_SWITCHING")
ROLLBACK_INTENTS = ("API_SWITCHING", "ROLLING_BACK")
SIMULATED = "SIMULATED_ONLY_NOT_AUTHORIZED_FOR_APPLY"


class Stop(Exception):
    """Only stable, sanitized diagnostic codes are emitted."""


class _MemorySamples:
    """Two snapshots supplied as values; no callbacks, subprocess or I/O."""

    def __init__(self, samples):
        self.samples = copy.deepcopy(samples)
        self.index = 0

    def observe(self):
        if self.index >= len(self.samples):
            raise Stop("DRYRUN_MISSING_SAMPLE")
        value = copy.deepcopy(self.samples[self.index])
        self.index += 1
        return value


def _step(planner, plan, stage, supplied_proof):
    try:
        return planner.advance(plan, stage, supplied_proof)
    except Exception:
        raise Stop("DRYRUN_STAGE_REJECTED") from None


def _check(planner, verifier, plan, base, target, kind, samples, next_stage=None):
    # This checksum is deliberately NOT a signature; the samples are synthetic.
    try:
        checked_target = target if kind != "ROLLED_BACK" else None
        report = verifier.verify(_MemorySamples(samples), base, kind,
                                 checked_target, samples=2)
        expected = verifier.expected_target_sha256(base, kind, checked_target)
        verifier.require_ok(report, plan["baseline_sha256"], kind, expected)
        if report["ok"] is not True:
            raise Stop("DRYRUN_NON_OK")
    except Exception:
        raise Stop("DRYRUN_VERIFICATION_REJECTED") from None
    if next_stage is None:
        return plan
    # Structurally bound to the flight and checker result, not authenticated.
    proof = {
        "kind": planner.PROOF_KINDS[next_stage],
        "stage": next_stage,
        "nonce": plan["nonce"],
        "admission_sha256": plan["admission_sha256"],
        "baseline_sha256": plan["baseline_sha256"],
        "target_sha256": plan["target_sha256"],
        "evidence_sha256": report["result_checksum_sha256"],
    }
    return _step(planner, plan, next_stage, proof)


def replay(planner, verifier, baseline, target, initial_plan,
           *, mode, observations, intents):
    """Replay fake API/WEB or rollback checkpoints WITHOUT physical mutation.

    'apply' requires 2 synthetic samples each for api, web and final.
    'rollback' requires 2 samples for api and rollback. Execution-intent
    proofs (API/WEB switching, rolling back) are supplied; verification stages
    are derived from the verifier result and never from a supplied claim. Proofs are supplied, never created or authenticated by this module.
    All external inputs remain non-authoritative, including successful
    verifier checksums. No APPLIED/ROLLED_BACK journal record is ever made.
    """
    if type(mode) is not str or mode not in ("apply", "rollback"):
        raise Stop("DRYRUN_MODE_INVALID")
    needed = ("api", "web", "final") if mode == "apply" else ("api", "rollback")
    stages = APPLY_INTENTS if mode == "apply" else ROLLBACK_INTENTS
    if (type(observations) is not dict or set(observations) != set(needed)
            or type(intents) is not dict or set(intents) != set(stages)):
        raise Stop("DRYRUN_INPUT_SET_INVALID")
    for name in needed:
        values = observations[name]
        if (type(values) is not list or len(values) != 2
                or any(type(value) is not dict for value in values)):
            raise Stop("DRYRUN_SAMPLES_INVALID")
    try:
        b, t, p = (copy.deepcopy(baseline), copy.deepcopy(target),
                   copy.deepcopy(initial_plan))
        if (planner.validate(p) != "APPLYING"
                or p["baseline_sha256"] != planner.validate_baseline(b)
                or p["target_sha256"] != planner.validate_target(b, t)
                or p["may_apply"] is not False
                or p["external_runtime_verified"] is not False):
            raise Stop("DRYRUN_NOT_INITIAL")
    except Exception:
        raise Stop("DRYRUN_PLAN_INVALID") from None

    p = _step(planner, p, "API_SWITCHING", intents["API_SWITCHING"])
    p = _check(planner, verifier, p, b, t, "API_SWITCHED",
               observations["api"], next_stage="API_VERIFIED")
    checked = ["API_SWITCHED"]
    if mode == "apply":
        p = _step(planner, p, "WEB_SWITCHING", intents["WEB_SWITCHING"])
        p = _check(planner, verifier, p, b, t, "WEB_SWITCHED",
                   observations["web"], next_stage="WEB_VERIFIED")
        checked.append("WEB_SWITCHED")
        p = _check(planner, verifier, p, b, t, "APPLIED",
                   observations["final"], next_stage="VERIFYING")
        checked.append("APPLIED")
        end = "VERIFYING"
    else:
        p = _step(planner, p, "ROLLING_BACK", intents["ROLLING_BACK"])
        p = _check(planner, verifier, p, b, t, "ROLLED_BACK",
                   observations["rollback"], next_stage="ROLLBACK_VERIFYING")
        checked.append("ROLLED_BACK")
        end = "ROLLBACK_VERIFYING"
    try:
        if (planner.validate(p) != end or p["may_apply"] is not False
                or p["external_runtime_verified"] is not False):
            raise Stop("DRYRUN_INVARIANT_REJECTED")
    except Exception:
        raise Stop("DRYRUN_INVARIANT_REJECTED") from None
    return {
        "schema": "qa525-inert-dryrun-v1",
        "status": SIMULATED,
        "mode": mode,
        "stage": end,
        "checked_kinds": checked,
        "nonce": p["nonce"],
        "baseline_sha256": p["baseline_sha256"],
        "target_sha256": p["target_sha256"],
        "simulated_plan_checksum_sha256": p["chain_sha256"],
        "may_apply": False,
        "may_install": False,
        "physical_runtime_verified": False,
        "signatures_verified": False,
        "journal_written": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_INERT_DRYRUN_LIBRARY_ONLY_NO_APPLY")
