#!/usr/bin/env python3
"""QA525 vNext observer glue -- PURE, INACTIVE, NO host access of its own.

Connects the injected state reader (rows/health providers supplied by the
future engine, see ``qa525-vnext-state-reader.py``) with the verifier and the
stage plan, producing exactly what the flight orchestrator expects:

* ``capture_plan(reader, target, nonce, admission_sha256)`` -> initial plan
  whose baseline is read ONCE, before any mutation;
* ``make_observe(reader, baseline, target)`` -> ``observe(kind)`` returning a
  verifier result (two samples) for API_SWITCHED / WEB_SWITCHED / APPLIED /
  ROLLED_BACK.

It never starts a process, opens a socket or reads the filesystem. The result
checksum is public integrity, not a signature; nothing here proves the rows or
health came from the real runtime.
"""
import copy
import importlib.util
from pathlib import Path

try:
    HERE = Path(__file__).resolve().parent
except NameError:  # PRE-4 Verified exec has no __file__: siblings are INJECTED by bind_dependencies, never read from disk
    HERE = None
KINDS = ("API_SWITCHED", "WEB_SWITCHED", "APPLIED", "ROLLED_BACK")


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def _load(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


verify = _load("qa525_vnext_verify_for_observer", "qa525-vnext-verify.py") if HERE is not None else None
plan_mod = _load("qa525_vnext_plan_for_observer", "qa525-vnext-baseline-plan.py") if HERE is not None else None

SIBLING_MODULES = {"verify": "qa525-vnext-verify.py", "plan_mod": "qa525-vnext-baseline-plan.py"}
_BOUND = []


def bind_dependencies(mapping):
    """PRE-4 ONLY: receive the exact sibling modules (one verified instance per file, shared by the whole flight)."""
    if (HERE is not None or _BOUND or type(mapping) is not dict or set(mapping) != set(SIBLING_MODULES)
            or any(type(m).__name__ != "module" or type(m).__module__ != "builtins" for m in mapping.values())):
        raise Stop("DEPENDENCY_BINDING_INVALID")
    globals().update(mapping)
    _BOUND.append(True)


def _reader(reader):
    if not (hasattr(reader, "observe") and hasattr(reader, "capture_baseline")):
        raise Stop("OBSERVER_READER_INVALID")
    return reader


def capture_plan(reader, target: dict, *, nonce: str, admission_sha256: str) -> dict:
    """Read the baseline once and prepare the initial in-memory plan."""
    _reader(reader)
    try:
        baseline = reader.capture_baseline()
    except Exception:
        raise Stop("OBSERVER_BASELINE_FAILED") from None
    try:
        return plan_mod.prepare(baseline, target, nonce=nonce,
                                admission_sha256=admission_sha256)
    except Exception:
        raise Stop("OBSERVER_PLAN_INVALID") from None


def make_observe(reader, baseline: dict, target: dict, samples: int = 2):
    """Return ``observe(kind)`` bound to this flight's baseline and target."""
    _reader(reader)
    base, goal = copy.deepcopy(baseline), copy.deepcopy(target)
    try:
        plan_mod.validate_baseline(base)
        plan_mod.validate_target(base, goal)
    except Exception:
        raise Stop("OBSERVER_CONTRACT_INVALID") from None

    def observe(kind):
        if type(kind) is not str or kind not in KINDS:
            raise Stop("OBSERVER_KIND_INVALID")
        try:
            return verify.verify(reader, base, kind,
                                 None if kind == "ROLLED_BACK" else copy.deepcopy(goal),
                                 samples=samples)
        except Exception:
            raise Stop("OBSERVER_VERIFY_FAILED") from None

    return observe


if __name__ == "__main__":
    raise SystemExit("QA525_OBSERVER_LIBRARY_ONLY_NO_EXECUTION")
