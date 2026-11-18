#!/usr/bin/env python3
"""QA525 full-stack orchestrator (DB + API + WEB) — #1010. INACTIVE, hermetic.

Turns the manual supervised runbook that worked on the 17-nov-2026 cut (docs/qa525-runbook-actualizacion-manual-
supervisada.md) into ONE flow. It starts no process, opens no file/socket and imports no time module: every effect
goes through an injected `host` object (the future root adapters) and the clock/sleep are injected. It authorizes
nothing: GO, signature, policy and envelope are verified by the caller's `host.preflight()`/`host.supply()`.

Order (never changed): preflight -> supply -> classify migrations -> G6 evidence -> spend nonce -> locks -> journal
PENDING -> EF history before -> migrate ONCE -> validate schema+EF history -> lease -> API -> WEB -> journal APPLIED.
Rules encoded and tested:
* Anything not proven additive AND backward compatible with the running API (EF history + ordered-set hash, no
  arbitrary SQL, no unbounded backfill, no incompatible DDL locks, no dependency on API N+1) stops BEFORE any effect.
* The nonce is spent only after preflight, supply, classification and G6 all passed, and before the first effect.
* Verification of every recreate uses health.wait_until_ready (bounded, retried) — never a single probe.
* A migration that fails or leaves a divergent schema/EF history => DB_UNCERTAIN_BLOCKED: no re-run, no automatic
  restore; the journal becomes RECOVERY_MANUALLY_REQUIRED.
* A failed API/WEB step rolls back WEB then API with the same criterion; the additive schema stays (compatible).
* A crash (BaseException) after the first effect leaves the journal PENDING and returns NO receipt: never a false
  ACTIVE. APPLIED needs verified readiness AND schema AND EF history, never HTTP health alone.
"""
import importlib.util
from pathlib import Path

_spec = importlib.util.spec_from_file_location("qa525_auto_health", Path(__file__).with_name("qa525-vnext-health.py"))
health = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(health)

OUT_APPLIED = "DB_APPLIED_APIWEB_APPLIED"
OUT_ROLLED_BACK = "DB_APPLIED_APIWEB_ROLLED_BACK_COMPATIBLE"
OUT_STOP = "STOP_BEFORE_DB"
OUT_BLOCKED = "DB_UNCERTAIN_BLOCKED"
OUTCOMES = frozenset({OUT_APPLIED, OUT_ROLLED_BACK, OUT_STOP, OUT_BLOCKED})
HEALTH_BUDGET_S = 480
HEALTH_RETRY_S = 2


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def classify(plan):
    """'ADDITIVE' only when every migration is proven additive and backward compatible; anything else 'MANUAL'."""
    migrations = plan.get("migrations") if isinstance(plan, dict) else None
    if not isinstance(migrations, list) or not plan.get("ordered_set_sha256"):
        return "MANUAL"
    for m in migrations:
        if not isinstance(m, dict):
            return "MANUAL"
        if not (m.get("additive") is True and m.get("backward_compatible_with_api_n") is True
                and m.get("arbitrary_sql") is False and m.get("unbounded_backfill") is False
                and m.get("incompatible_ddl_lock") is False and m.get("needs_api_n_plus_1") is False):
            return "MANUAL"
    return "ADDITIVE"


def _stop(code, started, clock, journal=None):
    return {"outcome": OUT_STOP, "failure_class": code, "duration_s": clock() - started, "journal": journal}


def run(host, release, *, clock, sleep, budget=HEALTH_BUDGET_S, retry=HEALTH_RETRY_S):
    started = clock()
    # ---- pre-effect gates: any failure => STOP_BEFORE_DB, nothing spent, nothing touched ----
    if host.preflight() is not True:
        return _stop("PREFLIGHT_FAILED", started, clock)
    if host.supply() is not True:
        return _stop("SUPPLY_NOT_VERIFIED", started, clock)
    plan = host.migration_plan()
    if classify(plan) != "ADDITIVE":
        return _stop("MIGRATION_NOT_PROVEN_ADDITIVE", started, clock)
    g6 = host.g6_evidence()
    if not (isinstance(g6, dict) and g6.get("isolated_restore_ok") is True and g6.get("release_sha") == release["sha"]
            and g6.get("expires_at", 0) > clock() + budget):
        return _stop("G6_UNAVAILABLE_OR_EXPIRED", started, clock)
    if host.spend_nonce(release["nonce"]) is not True:
        return _stop("NONCE_NOT_SPENDABLE", started, clock)
    # ---- first effect boundary ----
    if host.acquire_locks() is not True:
        return _stop("LOCKS_NOT_ACQUIRED", started, clock)
    receipt = {"release_sha": release["sha"], "ef_set_sha256": plan["ordered_set_sha256"]}
    try:
        host.journal("PENDING")
        receipt["ef_history_before"] = host.ef_history_sha()
        already = host.migrations_already_applied(plan["ordered_set_sha256"])
        if not already:
            try:
                host.migrate_once()
            except Exception:
                return _blocked(host, receipt, "MIGRATION_FAILED_NO_RERUN", started, clock)
        receipt["ef_history_after"] = host.ef_history_sha()
        if host.schema_ok() is not True or host.ef_history_matches(plan["ordered_set_sha256"]) is not True:
            return _blocked(host, receipt, "SCHEMA_OR_EF_HISTORY_DIVERGENT", started, clock)
        if host.lease_alive() is not True:
            return _blocked(host, receipt, "LEASE_LOST_AFTER_MIGRATION", started, clock)
        applied = []
        failure = None
        for step in ("api", "web"):
            try:
                host.recreate(step, "target")
                applied.append(step)
                ok = health.wait_until_ready(lambda s=step: host.ready(s) is True, clock=clock, sleep=sleep,
                                             budget=budget, retry=retry)
            except Exception:
                ok = False
            if not ok:
                failure = "API_NOT_READY" if step == "api" else "WEB_NOT_READY"
                break
        if failure is None:
            if host.lease_alive() is not True or host.schema_ok() is not True:
                return _blocked(host, receipt, "FINAL_VERIFICATION_FAILED", started, clock)
            receipt.update(host.evidence())
            host.journal("APPLIED")
            receipt.update(outcome=OUT_APPLIED, duration_s=clock() - started)
            return receipt
        # ---- rollback: WEB then API, same bounded criterion; additive schema stays ----
        rolled = True
        for step in reversed(applied):
            try:
                host.recreate(step, "baseline")
                rolled = rolled and health.wait_until_ready(lambda s=step: host.ready(s) is True, clock=clock,
                                                            sleep=sleep, budget=budget, retry=retry)
            except Exception:
                rolled = False
        if not rolled:
            return _blocked(host, receipt, "ROLLBACK_NOT_VERIFIED_" + failure, started, clock)
        host.journal("ROLLED_BACK")
        receipt.update(outcome=OUT_ROLLED_BACK, failure_class=failure, duration_s=clock() - started)
        return receipt
    finally:
        host.release_locks()


def _blocked(host, receipt, code, started, clock):
    host.journal("RECOVERY_MANUALLY_REQUIRED")
    receipt.update(outcome=OUT_BLOCKED, failure_class=code, duration_s=clock() - started)
    return receipt
