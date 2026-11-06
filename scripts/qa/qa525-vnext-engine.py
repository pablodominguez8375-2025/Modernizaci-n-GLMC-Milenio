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

# One journal module owns every lease consumer.
scopes.journal = flight.journal
if scopes.journal is not flight.journal:  # pragma: no cover
    raise Stop("ENGINE_JOURNAL_NOT_SHARED")


def run_synthetic(*, root, admission, admission_sha256, now,
                  admission_bytes, approval_bytes, trusted_policy_hash,
                  synthetic_lock_dir, reader, target, project, workdir,
                  env_file, runner, scope_factory, settle, external_gate,
                  samples=3, checkpoint=None):
    """Run one fully assembled flight against injected synthetic adapters.

    ``external_gate(stage)`` stands in for the independently authenticated
    OPS/APP/Sponsor/G6 decision.  This function does not authenticate it and
    therefore every returned result remains non-authoritative.

    ``admission_bytes`` / ``approval_bytes`` must be the exact bytes of the
    admission file and of the externally AUTHENTICATED approval anchor; the
    admission-bridge binds them to ``trusted_policy_hash`` and re-checks the
    window at ``begin`` time, after the five locks are held. The digest that
    fixes plan, scopes and lease is the bound one, never a caller claim.
    """
    for adapter in (runner, scope_factory, settle, external_gate):
        if not callable(adapter):
            raise Stop("ENGINE_ADAPTER_INVALID")
    try:
        bound = bridge.BoundJournal(
            flight.journal, admission_bytes, approval_bytes,
            trusted_policy_hash=trusted_policy_hash)
    except Exception:
        raise Stop("ENGINE_ADMISSION_INVALID") from None
    try:
        with locks.synthetic_flight(
                bound, root, admission, admission_sha256, now,
                synthetic_lock_dir=synthetic_lock_dir) as plane:
            plane.assert_held()
            lease = plane.lease
            if bound.last_binding is None or \
                    bound.last_binding["admission_sha256"] != admission_sha256:
                raise Stop("ENGINE_ADMISSION_UNBOUND")
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
                    return allowed is True
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
                    gate=held_gate, now=now, checkpoint=checkpoint)
                plane.assert_held()
                return result
            except flight.Stop:
                raise
            except Exception:
                raise Stop("ENGINE_FLIGHT_FAILED") from None
    except (flight.Stop, Stop):
        raise
    except Exception:
        raise Stop("ENGINE_PLANE_DENIED") from None


if __name__ == "__main__":
    raise SystemExit("QA525_ENGINE_SYNTHETIC_ONLY_NO_EXECUTION")
