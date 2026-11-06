#!/usr/bin/env python3
"""QA525 vNext evidence glue -- PURE, INACTIVE, NOT an executor.

Ties the monotonic stage plan (``qa525-vnext-baseline-plan.py``) to the
read-only verifier (``qa525-vnext-verify.py``): a verification stage may only
advance with a proof DERIVED from a verifier result that passes ``require_ok``
for the plan's own baseline and target, with the kind the stage demands.

It performs no I/O, runs no process and trusts nothing it is handed: the
verifier result is a JSON with a PUBLIC checksum (integrity, not authenticity).
The engine must still obtain that result itself, under the held locks, from
the real adapter. Intent/stop stages are declarative and are NOT handled here.
"""
import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent
# Verification stage -> the verifier kind its evidence must have.
STAGE_KIND = {
    "API_VERIFIED": "API_SWITCHED",
    "WEB_VERIFIED": "WEB_SWITCHED",
    "VERIFYING": "APPLIED",
    "ROLLBACK_VERIFYING": "ROLLED_BACK",
}


class Stop(Exception):
    """Fail-closed diagnostic code; never carries external text."""


def _load(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


plan_mod = _load("qa525_vnext_plan_for_evidence", "qa525-vnext-baseline-plan.py")
verify = _load("qa525_vnext_verify_for_evidence", "qa525-vnext-verify.py")


def proof_from_verification(plan: dict, stage: str, result: dict) -> dict:
    """Return the plan proof for ``stage`` derived from ``result``.

    Raises ``Stop`` when the plan is invalid, the stage has no verifier kind,
    or the result is not an OK verification of exactly this plan's baseline,
    kind and target.
    """
    try:
        plan_mod.validate(plan)
    except plan_mod.Stop:
        raise Stop("EVIDENCE_PLAN_INVALID") from None
    if type(stage) is not str or stage not in STAGE_KIND:
        raise Stop("EVIDENCE_STAGE_NOT_VERIFIABLE")
    kind = STAGE_KIND[stage]
    target = None if kind == "ROLLED_BACK" else plan["target"]
    try:
        expected = verify.expected_target_sha256(plan["baseline"], kind, target)
        checked = verify.require_ok(result, plan["baseline_sha256"], kind, expected)
    except verify.Stop as exc:
        raise Stop("EVIDENCE_" + str(exc)) from None
    return {
        "kind": plan_mod.PROOF_KINDS[stage],
        "stage": stage,
        "nonce": plan["nonce"],
        "admission_sha256": plan["admission_sha256"],
        "baseline_sha256": plan["baseline_sha256"],
        "target_sha256": plan["target_sha256"],
        "evidence_sha256": checked["result_checksum_sha256"],
    }


def advance_with_verification(plan: dict, stage: str, result: dict) -> dict:
    """Advance ``plan`` to a verification ``stage`` using verifier evidence."""
    proof = proof_from_verification(plan, stage, result)
    try:
        return plan_mod.advance(plan, stage, proof)
    except plan_mod.Stop as exc:
        raise Stop("EVIDENCE_PLAN_" + str(exc)) from None


if __name__ == "__main__":
    raise SystemExit("QA525_EVIDENCE_LIBRARY_ONLY_NO_EXECUTION")
