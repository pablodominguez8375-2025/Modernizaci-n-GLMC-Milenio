#!/usr/bin/env python3
"""QA525 vNext terminal writer -- INACTIVE library, NOT an executor.

Writes ``terminal-<nonce>.json`` (APPLIED or ROLLED_BACK) for a flight whose
journal is still APPLYING, and ONLY when a verifier result from
``qa525-vnext-verify.py`` passes ``require_ok`` for the same baseline, kind and
EXPECTED target, while the caller still holds the flight's lease (the
transaction lock). It performs no Docker, network, process or host action.

Honest limits:
* ``final_stage`` is still the caller's claim: the journal and plan do not yet
  persist stages (durable plan with nonce/admission/baseline/target SHA + fsync
  is #851 work). Do not connect this writer to a launcher before that exists.
* The flock is advisory: ``lease_is_held`` proves the lease object owns it for
  cooperating processes only.
* ``require_ok`` checks a public checksum, not a signature or the origin of the
  measurement. The engine must run ``verify`` itself under the held lock with a
  trusted adapter and pass the result straight here; never a result received
  from another component or read from a file.
* The journal does not yet record ``baseline_sha256``/``target_sha256`` at
  APPLYING, so this module cannot compare them with the journal. They are
  written into the terminal record for the evidence trail only until the
  journal carries them.
* No APPLIED may follow a RECOVERY_REQUIRED flight: that path needs an
  operator procedure, not this writer.
"""
import datetime as dt
import importlib.util
import os
from pathlib import Path
import re

try:
    HERE = Path(__file__).resolve().parent
except NameError:  # PRE-4 Verified exec has no __file__: siblings are INJECTED by bind_dependencies, never read from disk
    HERE = None
KINDS = {"APPLIED": "VERIFYING", "ROLLED_BACK": "ROLLBACK_VERIFYING"}
DIGEST = re.compile(r"^sha256:[a-f0-9]{64}$")


class Stop(Exception):
    """Fail-closed diagnostic code; never carries external text."""


def _load(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


journal = _load("qa525_vnext_journal_for_terminal", "qa525-vnext-journal.py") if HERE is not None else None
verify = _load("qa525_vnext_verify_for_terminal", "qa525-vnext-verify.py") if HERE is not None else None

SIBLING_MODULES = {"journal": "qa525-vnext-journal.py", "verify": "qa525-vnext-verify.py"}
_BOUND = []


def bind_dependencies(mapping):
    """PRE-4 ONLY: receive the exact sibling modules (one verified instance per file, shared by the whole flight)."""
    if (HERE is not None or _BOUND or type(mapping) is not dict or set(mapping) != set(SIBLING_MODULES)
            or any(type(m).__name__ != "module" or type(m).__module__ != "builtins" for m in mapping.values())):
        raise Stop("DEPENDENCY_BINDING_INVALID")
    globals().update(mapping)
    _BOUND.append(True)


def _require_lock_held(lease) -> None:
    """The lease must be a live one minted by ``begin`` of this journal copy.

    A fabricated Lease, a fd of another file, or a lease from another module
    instance is refused. Advisory-flock limit: see ``journal.lease_is_held``.
    """
    if not journal.lease_is_held(lease):
        raise Stop("LEASE_NOT_HELD")


def finish(lease, kind: str, result: dict, baseline_sha256: str,
           expected_target_sha256: str, final_stage: str, now: dt.datetime) -> str:
    """Create the terminal record; returns ``kind``. Raises Stop otherwise."""
    _require_lock_held(lease)
    if kind not in KINDS:
        raise Stop("KIND_INVALID")
    if final_stage != KINDS[kind]:
        raise Stop("FINAL_STAGE_MISMATCH")
    for value in (baseline_sha256, expected_target_sha256):
        if type(value) is not str or DIGEST.fullmatch(value) is None:
            raise Stop("DIGEST_INVALID")
    try:
        when = journal._clock(now)
    except journal.Stop:
        raise Stop("CLOCK_INVALID") from None
    try:
        checked = verify.require_ok(result, baseline_sha256, kind, expected_target_sha256)
    except verify.Stop as exc:
        raise Stop("VERIFY_" + str(exc)) from None
    try:
        status = journal.inspect(lease.root, lease.nonce)
        if status != "APPLYING":
            raise Stop("FLIGHT_NOT_APPLYING")
        marker, _journal_path, _recovery = journal._paths(lease.root, lease.nonce)
        record = {
            "schema": "qa525-terminal-v1", "status": kind, "nonce": lease.nonce,
            "admission_sha256": journal._object(marker)["admission_sha256"],
            "final_stage": final_stage,
            "baseline_sha256": baseline_sha256, "target_sha256": expected_target_sha256,
            "verify_result_checksum_sha256": checked["result_checksum_sha256"],
            "evidence_sha256": checked["evidence_sha256"],
            "finished_at": when.strftime("%Y-%m-%dT%H:%M:%SZ"),
            "authenticated_by": "CHECKSUM_ONLY_NOT_A_SIGNATURE",
        }
        journal._atomic_new(journal._terminal_path(lease.root, lease.nonce), record)
    except journal.Stop as exc:
        raise Stop("JOURNAL_" + str(exc)) from None
    return kind
