#!/usr/bin/env python3
"""QA525 vNext assembled synthetic engine -- INACTIVE, NO host adapter.

This is the first composition boundary for the reviewed vNext libraries.  It
holds the synthetic five-lock plane plus the journal lease, captures one
baseline, durably records both child scope names, builds target and baseline
switchers, and runs one fail-closed flight.  Every effect remains injected.

There is intentionally no physical mode.  The separately authenticated root
bootstrap, real legacy-lock wrapper, runner, metadata/health providers and G6
+ Sponsor gate remain prerequisites of the future signed host entrypoint.
"""
import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent


class Stop(Exception):
    """Fixed diagnostic code; never includes caller or host text."""


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


flight = _load("qa525_vnext_flight_for_engine", "qa525-vnext-flight.py")
observer = _load("qa525_vnext_observer_for_engine", "qa525-vnext-observer.py")
switch = _load("qa525_vnext_switch_for_engine", "qa525-vnext-switch.py")
locks = _load("qa525_vnext_locks_for_engine", "qa525-vnext-locks.py")
scopes = _load("qa525_vnext_scope_store_for_engine", "qa525-vnext-scope-store.py")
bridge = _load("qa525_vnext_admission_bridge_for_engine", "qa525-vnext-admission-bridge.py")
g6_anchor = _load("qa525_vnext_g6_anchor_for_engine", "qa525-vnext-g6-anchor.py")

# Exact raw-evidence schema. Precomputed PASS/boolean is never an input.
G6_RAW_FIELDS = frozenset({"review_bytes", "receipt_bytes", "restore_bytes"})
G6_EVIDENCE_FIELDS = G6_RAW_FIELDS | frozenset({
    "pinned_admission_sha256", "pinned_approval_sha256",
    "pinned_review_sha256", "pinned_receipt_sha256",
    "pinned_restore_sha256", "observed_archive_sha256",
})
G6_FORWARD_STAGES = frozenset({"API_SWITCHING", "WEB_SWITCHING"})

# One journal module owns every lease consumer.
scopes.journal = flight.journal
if scopes.journal is not flight.journal:  # pragma: no cover
    raise Stop("ENGINE_JOURNAL_NOT_SHARED")


def run_synthetic(*, root, admission, admission_sha256, clock,
                  admission_bytes, approval_bytes, trusted_policy_hash,
                  synthetic_lock_dir, reader, target, project, workdir,
                  env_file, scope_factory, settle, external_gate,
                  runner=None, runner_factory=None, samples=3, checkpoint=None,
                  g6_evidence=None):
    """Run one fully assembled flight against injected synthetic adapters.

    ``external_gate(stage)`` stands in for the independently authenticated
    OPS/APP/Sponsor/G6 decision.  This function does not authenticate it and
    therefore every returned result remains non-authoritative.

    ``admission_bytes`` / ``approval_bytes`` must be the exact bytes of the
    admission file and of the externally AUTHENTICATED approval anchor; the
    admission-bridge binds them to ``trusted_policy_hash`` and re-checks the
    window at ``begin`` time, after the five locks are held. The digest that
    fixes plan, scopes and lease is the bound one, never a caller claim.

    ``clock()`` is read again by the bridge AFTER the five locks are held (and
    once more before the flight), so a window that closed while waiting for the
    locks is denied; there is no fixed ``now``. Pass exactly one of ``runner``
    or ``runner_factory``: the factory receives the HELD plane (for example
    ``lambda plane: adapters.held_runner(plane, run)``) so the process effect can
    only run while the locks and lease are held.

    g6_evidence carries raw review/receipt/restore bytes and the exact SHA
    pins and observed archive SHA. Its nine immutable values are copied at
    entry; a caller mutating its original dict after entry cannot change the
    evidence used within the flight. It is rechecked with a freshly read clock
    inside the held synthetic five-lock + root-lease plane before any factory,
    baseline or runner and before each forward API/WEB intent. Rollback stays
    guarded by locks and external_gate, not by G6 expiry. No authentication
    or physical permission is established here.

    If the plane cannot be closed cleanly AFTER the flight ended, the durable
    journal is the source of truth and the code says how the flight ended:
    ``ENGINE_CLOSE_FAILED_AFTER_APPLIED`` / ``_ROLLED_BACK`` / ``_FLIGHT_STOP``.
    """
    if not callable(clock) or (runner is None) == (runner_factory is None):
        raise Stop("ENGINE_ADAPTER_INVALID")
    # A newly added forward execution intent must not silently bypass G6.
    # Keep this contract aligned with the independently reviewed flight plan.
    if (frozenset(stage for stage, kind in flight.plan_mod.PROOF_KINDS.items()
                  if kind == "EXECUTION_INTENT") != G6_FORWARD_STAGES):
        raise Stop("ENGINE_FORWARD_STAGES_MISMATCH")
    if type(g6_evidence) is not dict:
        raise Stop("ENGINE_G6_EVIDENCE_INVALID")
    # Copy before inspecting: do not read the caller's mutable dict again.
    # Exact bytes/str types rule out mutable bytearray/list or custom values.
    g6_evidence = g6_evidence.copy()
    if (set(g6_evidence) != G6_EVIDENCE_FIELDS
            or any(type(g6_evidence[k]) is not bytes for k in G6_RAW_FIELDS)
            or any(type(g6_evidence[k]) is not str
                   for k in G6_EVIDENCE_FIELDS - G6_RAW_FIELDS)):
        raise Stop("ENGINE_G6_EVIDENCE_INVALID")
    for adapter in (runner or runner_factory, scope_factory, settle, external_gate):
        if not callable(adapter):
            raise Stop("ENGINE_ADAPTER_INVALID")
    try:
        bound = bridge.BoundJournal(
            flight.journal, admission_bytes, approval_bytes,
            trusted_policy_hash=trusted_policy_hash, clock=clock)
        first = clock()
    except Exception:
        raise Stop("ENGINE_ADMISSION_INVALID") from None
    ended = {"how": None}
    try:
        with locks.synthetic_flight(
                bound, root, admission, admission_sha256, first,
                synthetic_lock_dir=synthetic_lock_dir) as plane:
            plane.assert_held()
            lease = plane.lease
            if bound.last_binding is None or \
                    bound.last_binding["admission_sha256"] != admission_sha256:
                raise Stop("ENGINE_ADMISSION_UNBOUND")

            def check_held_g6():
                """Fresh, locked G6 byte consistency; NEVER host authority."""
                try:
                    plane.assert_held()
                    checked = g6_anchor.compare(
                        admission_bytes=admission_bytes,
                        approval_bytes=approval_bytes,
                        review_bytes=g6_evidence["review_bytes"],
                        receipt_bytes=g6_evidence["receipt_bytes"],
                        restore_bytes=g6_evidence["restore_bytes"],
                        pinned_admission_sha256=g6_evidence["pinned_admission_sha256"],
                        pinned_approval_sha256=g6_evidence["pinned_approval_sha256"],
                        pinned_review_sha256=g6_evidence["pinned_review_sha256"],
                        pinned_receipt_sha256=g6_evidence["pinned_receipt_sha256"],
                        pinned_restore_sha256=g6_evidence["pinned_restore_sha256"],
                        observed_archive_sha256=g6_evidence["observed_archive_sha256"],
                        trusted_policy_hash=trusted_policy_hash, now=clock())
                    plane.assert_held()
                    if (checked.get("status") != g6_anchor.STATUS
                            or checked.get("may_apply") is not False
                            or checked.get("may_install") is not False
                            or checked.get("admission_sha256") != admission_sha256
                            or checked.get("approval_sha256")
                            != bound.last_binding["external_anchor_sha256"]
                            or checked.get("nonce") != lease.nonce):
                        raise Stop("ENGINE_G6_EVIDENCE_DENIED")
                except Exception:
                    raise Stop("ENGINE_G6_EVIDENCE_DENIED") from None

            # Deny before a factory, baseline, scope manifest or runner.
            check_held_g6()
            if runner_factory is not None:
                try:
                    runner = runner_factory(plane)
                except Exception:
                    raise Stop("ENGINE_ADAPTER_INVALID") from None
                if not callable(runner):
                    raise Stop("ENGINE_ADAPTER_INVALID")
            try:
                plan = observer.capture_plan(
                    reader, target, nonce=lease.nonce,
                    admission_sha256=admission_sha256)
                observe = observer.make_observe(
                    reader, plan["baseline"], plan["target"], samples=2)
                units = [switch.containment.unit_name(lease.nonce, service)
                         for service in ("api", "web")]
                scopes.initialize(lease, admission_sha256, units, checkpoint)
            except Exception:
                raise Stop("ENGINE_PREPARE_FAILED") from None

            def held_gate(stage):
                try:
                    plane.assert_held()
                    allowed = external_gate(stage)
                    plane.assert_held()
                    if allowed is not True:
                        return False
                    # Read time AFTER the external gate, nearest to the next
                    # durable forward intent. No expiry check on rollback.
                    if stage in G6_FORWARD_STAGES:
                        check_held_g6()
                    plane.assert_held()
                    return True
                except Exception:
                    return False

            def record_unit(unit):
                plane.assert_held()
                digest = scopes.require_recorded(lease, admission_sha256, unit)
                plane.assert_held()
                return digest

            common = dict(nonce=lease.nonce, project=project, workdir=workdir,
                          env_file=env_file, runner=runner,
                          scope_factory=scope_factory, record_unit=record_unit,
                          observe=reader.observe, settle=settle, samples=samples)
            try:
                forward = switch.ServiceSwitcher.for_plan(plan, "target", **common)
                backward = switch.ServiceSwitcher.for_plan(plan, "baseline", **common)
                result = flight.run_apply(
                    lease, plan, switch_api=forward.switch_api,
                    switch_web=forward.switch_web,
                    rollback=backward.switch_all, observe=observe,
                    gate=held_gate, now=clock(), checkpoint=checkpoint)
                ended["how"] = result["status"]
                plane.assert_held()
                return result
            except flight.Stop:
                ended["how"] = ended["how"] or "FLIGHT_STOP"
                raise
            except Exception:
                ended["how"] = ended["how"] or "FLIGHT_STOP"
                raise Stop("ENGINE_FLIGHT_FAILED") from None
    except locks.Stop as exc:
        if str(exc) == "PLANE_JOURNAL_CLOSE_FAILED" and ended["how"] in (
                "APPLIED", "ROLLED_BACK", "FLIGHT_STOP"):
            raise Stop("ENGINE_CLOSE_FAILED_AFTER_" + ended["how"]) from None
        raise Stop("ENGINE_PLANE_DENIED") from None
    except (flight.Stop, Stop):
        raise
    except Exception:
        raise Stop("ENGINE_PLANE_DENIED") from None


if __name__ == "__main__":
    raise SystemExit("QA525_ENGINE_SYNTHETIC_ONLY_NO_EXECUTION")
