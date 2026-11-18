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
import re
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


SHA256 = re.compile(r"^sha256:[a-f0-9]{64}$")


def classify(plan):
    """'ADDITIVE' only when every migration is proven additive and backward compatible; anything else 'MANUAL'.
    An empty migration list or a malformed ordered-set hash is never ADDITIVE (fail closed)."""
    migrations = plan.get("migrations") if isinstance(plan, dict) else None
    sha = plan.get("ordered_set_sha256") if isinstance(plan, dict) else None
    if not isinstance(migrations, list) or not migrations or not isinstance(sha, str) or not SHA256.match(sha):
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
        baseline = host.evidence()
        # A step is recorded as ATTEMPTED *before* the effect: an adapter may change the physical state and then
        # raise, and the rollback must still cover it (#1034 review 6978339576 P1).
        attempted = []
        failure = None
        for step in ("api", "web"):
            attempted.append(step)
            try:
                host.recreate(step, "target")
                ok = (health.wait_until_ready(lambda s=step: host.ready(s) is True, clock=clock, sleep=sleep,
                                              budget=budget, retry=retry) and host.lease_alive() is True)
            except Exception:
                ok = False
            if not ok:
                failure = "API_NOT_READY" if step == "api" else "WEB_NOT_READY"
                break
        if failure is None:
            ev = host.evidence()
            if not _target_identity_ok(ev, baseline, release):
                return _blocked(host, receipt, "TARGET_IDENTITY_MISMATCH", started, clock)
            if host.lease_alive() is not True or host.schema_ok() is not True:
                return _blocked(host, receipt, "FINAL_VERIFICATION_FAILED", started, clock)
            receipt.update(images=ev["images"], containers=ev["containers"])
            host.journal("APPLIED")
            receipt.update(outcome=OUT_APPLIED, duration_s=clock() - started)
            return receipt
        # ---- rollback of EVERY attempted step, WEB then API; each step verified separately (no short-circuit) ----
        results = {}
        for step in reversed(attempted):
            try:
                host.recreate(step, "baseline")
                results[step] = (health.wait_until_ready(lambda s=step: host.ready(s) is True, clock=clock,
                                                         sleep=sleep, budget=budget, retry=retry)
                                 and host.lease_alive() is True)
            except Exception:
                results[step] = False
        receipt["rollback_results"] = dict(results)
        try:
            restored = all(results.values()) and _baseline_identity_ok(host.evidence(), baseline, attempted)
        except Exception:
            restored = False
        if not restored:
            return _blocked(host, receipt, "ROLLBACK_NOT_VERIFIED_" + failure, started, clock)
        host.journal("ROLLED_BACK")
        receipt.update(outcome=OUT_ROLLED_BACK, failure_class=failure, duration_s=clock() - started)
        return receipt
    finally:
        host.release_locks()


def _ids(ev):
    return ev.get("images") if isinstance(ev, dict) else None, ev.get("containers") if isinstance(ev, dict) else None


def _target_identity_ok(ev, baseline, release):
    """Exact target images, and NEW container ids (distinct from the baseline) for both services."""
    images, containers = _ids(ev)
    b_images, b_containers = _ids(baseline)
    expected = release.get("expected_images")
    if not (isinstance(images, dict) and isinstance(containers, dict) and isinstance(b_containers, dict)
            and isinstance(expected, dict) and set(expected) == {"api", "web"}):
        return False
    for step in ("api", "web"):
        cid = containers.get(step)
        if images.get(step) != expected[step] or not isinstance(cid, str) or not cid or cid == b_containers.get(step):
            return False
    return all(SHA256.match(v or "") for v in expected.values())


def _baseline_identity_ok(ev, baseline, attempted):
    """Every attempted service is back on the exact baseline image (readiness alone is not proof)."""
    images, _ = _ids(ev)
    b_images, _ = _ids(baseline)
    if not (isinstance(images, dict) and isinstance(b_images, dict)):
        return False
    return all(images.get(s) is not None and images.get(s) == b_images.get(s) for s in attempted)


def _blocked(host, receipt, code, started, clock):
    host.journal("RECOVERY_MANUALLY_REQUIRED")
    receipt.update(outcome=OUT_BLOCKED, failure_class=code, duration_s=clock() - started)
    return receipt
