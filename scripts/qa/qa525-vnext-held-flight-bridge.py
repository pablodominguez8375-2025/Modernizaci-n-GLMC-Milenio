#!/usr/bin/env python3
"""QA525 B7 HELD root-flight composition. INACTIVE LIBRARY; NO CLI, NO HOST EFFECTS.

This joins already reviewed vNext components over ONE RootPlane made by the
caller-injected root_locks.root_flight. In a future signed entrypoint, the
injected runner MAY perform effects; consequently it must never be connected
until independent pre-root OPS/APP origin, program/child/Compose SHA pins,
Sponsor consent, G6 custody, host and systemd cgroup are authenticated.

This module DOES NOT check signatures or grant authority. It only requires
fresh bound admission/G6 metadata, the exact B7 record and five held locks +
journal lease before and throughout each transition, including rollback.
Every effect (inspector, runtime reader, process runner, scopes and clock)
is injected; importing this file never opens a resource or runs a command.
"""
from copy import deepcopy
from types import MappingProxyType
import re

SHA256 = re.compile(r"sha256:[0-9a-f]{64}\Z")
STATUS = "QA525_HELD_FLIGHT_ASSEMBLED_EXTERNAL_AUTH_NOT_VERIFIED_HERE"
G6_BYTES = frozenset({"review_bytes", "receipt_bytes", "restore_bytes"})
G6_PINS = frozenset({
    "pinned_admission_sha256", "pinned_approval_sha256",
    "pinned_review_sha256", "pinned_receipt_sha256",
    "pinned_restore_sha256", "observed_archive_sha256",
    "downloaded_offhost_sha256",
})
G6_IDS = {"expected_restic_repo_id": re.compile(r"[0-9a-f]{64}\Z"),
          "expected_key_epoch": re.compile(r"[0-9a-f]{32}\Z")}
FORWARD = frozenset({"API_SWITCHING", "WEB_SWITCHING"})


class Stop(Exception):
    """Fixed diagnostic codes; never echo host or private inputs."""


def _callable(obj, name):
    return callable(getattr(obj, name, None))


def _inputs(inputs, g6_evidence, modules):
    required = {
        "clock", "external_gate", "runner_factory", "scope_factory", "settle",
        "inspector", "reader", "port", "root", "admission",
        "admission_sha256", "admission_bytes", "approval_bytes", "policy_hash",
        "baseline_sha256", "target", "project", "workdir", "env_file",
    }
    if set(inputs) != required:
        raise Stop("HELDFLIGHT_INPUT_FIELDS_INVALID")
    for key in ("clock", "external_gate", "runner_factory", "scope_factory",
                "settle", "inspector"):
        if not callable(inputs[key]):
            raise Stop("HELDFLIGHT_ADAPTER_REQUIRED")
    if not (_callable(inputs["reader"], "capture_baseline")
            and _callable(inputs["reader"], "observe")
            and type(inputs["target"]) is dict):
        raise Stop("HELDFLIGHT_READER_INVALID")
    if (type(inputs["admission_bytes"]) is not bytes
            or type(inputs["approval_bytes"]) is not bytes
            or any(type(inputs[k]) is not str or SHA256.fullmatch(inputs[k]) is None
                   for k in ("admission_sha256", "baseline_sha256", "policy_hash"))):
        raise Stop("HELDFLIGHT_PINS_INVALID")
    for name in ("project", "workdir", "env_file"):
        if type(inputs[name]) is not str or not inputs[name]:
            raise Stop("HELDFLIGHT_PATH_INVALID")
    if (type(g6_evidence) is not dict or set(g6_evidence) != G6_BYTES | G6_PINS | set(G6_IDS)
            or any(type(g6_evidence[k]) is not bytes for k in G6_BYTES)
            or any(type(g6_evidence[k]) is not str
                   or SHA256.fullmatch(g6_evidence[k]) is None for k in G6_PINS)
            or any(type(g6_evidence[k]) is not str
                   or G6_IDS[k].fullmatch(g6_evidence[k]) is None for k in G6_IDS)):
        raise Stop("HELDFLIGHT_G6_INPUT_INVALID")
    for name, method in (
            ("root_locks", "root_flight"), ("bridge", "BoundJournal"),
            ("boundary", "Boundary"), ("b7", "inspect_inside_held_plane"),
            ("observer", "capture_plan"), ("scopes", "initialize"),
            ("switch", "ServiceSwitcher"), ("flight", "run_apply"),
            ("g6_anchor", "compare")):
        if not _callable(modules.get(name), method):
            raise Stop("HELDFLIGHT_MODULE_INVALID")
    try:
        stages = {stage for stage, kind in
                  modules["flight"].plan_mod.PROOF_KINDS.items()
                  if kind == "EXECUTION_INTENT"}
    except Exception:
        raise Stop("HELDFLIGHT_INTENT_SCHEMA_INVALID") from None
    if stages != FORWARD:
        raise Stop("HELDFLIGHT_INTENT_SCHEMA_INVALID")


def run_held(*, root_locks, bridge, boundary, b7, observer, scopes, switch,
             flight, g6_anchor, g6_evidence, checkpoint=None, **inputs):
    """Compose ONE journal-bound physical RootPlane with injected effects.

    This is deliberately NOT a runnable updater and MUST NOT be called by an
    untrusted root shell. A signed entrypoint must supply authenticated bytes,
    root-owned pinned Python modules and trusted effects. A truthy arbitrary
    external_gate does not prove a Sponsor signature; no authority is conferred
    by returning a successful outcome. It cannot be used to skip issue #822.
    """
    modules = dict(root_locks=root_locks, bridge=bridge, boundary=boundary,
                   b7=b7, observer=observer, scopes=scopes, switch=switch,
                   flight=flight, g6_anchor=g6_anchor)
    _inputs(inputs, g6_evidence, modules)
    frozen = MappingProxyType(dict(g6_evidence))
    try:
        # IMPORTANT: stage and scope manifests must share the ONE journal
        # module instance that minted the lease; independent importlib copies
        # reject foreign lease objects and would jeopardize crash recovery.
        if not _callable(getattr(flight, "journal", None), "begin"):
            raise Stop("HELDFLIGHT_JOURNAL_NOT_SHARED")
        scopes.journal = flight.journal
        if scopes.journal is not flight.journal:
            raise Stop("HELDFLIGHT_JOURNAL_NOT_SHARED")
        journal = bridge.BoundJournal(
            flight.journal, inputs["admission_bytes"], inputs["approval_bytes"],
            trusted_policy_hash=inputs["policy_hash"], clock=inputs["clock"])
        first = inputs["clock"]()
    except Stop:
        raise
    except Exception:
        raise Stop("HELDFLIGHT_ADMISSION_INVALID") from None
    ended = {"outcome": None}
    try:
        with root_locks.root_flight(
                inputs["port"], journal, inputs["root"], inputs["admission"],
                inputs["admission_sha256"], first) as plane:
            held = boundary.Boundary(
                root_locks=root_locks, plane=plane, nonce=plane.lease.nonce,
                admission_sha256=inputs["admission_sha256"])
            if (journal.last_binding is None
                    or journal.last_binding.get("admission_sha256")
                    != inputs["admission_sha256"]):
                raise Stop("HELDFLIGHT_ADMISSION_UNBOUND")
            lease = plane.lease

            def check_g6():
                """Pure byte-integrity and freshness, NOT backup custody proof."""
                held._check()
                if type(g6_evidence) is not dict or g6_evidence != frozen:
                    raise Stop("HELDFLIGHT_G6_CHANGED")
                try:
                    result = g6_anchor.compare(
                        admission_bytes=inputs["admission_bytes"],
                        approval_bytes=inputs["approval_bytes"],
                        review_bytes=frozen["review_bytes"],
                        receipt_bytes=frozen["receipt_bytes"],
                        restore_bytes=frozen["restore_bytes"],
                        pinned_admission_sha256=frozen["pinned_admission_sha256"],
                        pinned_approval_sha256=frozen["pinned_approval_sha256"],
                        pinned_review_sha256=frozen["pinned_review_sha256"],
                        pinned_receipt_sha256=frozen["pinned_receipt_sha256"],
                        pinned_restore_sha256=frozen["pinned_restore_sha256"],
                        observed_archive_sha256=frozen["observed_archive_sha256"],
                        downloaded_offhost_sha256=frozen["downloaded_offhost_sha256"],
                        expected_restic_repo_id=frozen["expected_restic_repo_id"],
                        expected_key_epoch=frozen["expected_key_epoch"],
                        trusted_policy_hash=inputs["policy_hash"],
                        now=inputs["clock"]())
                except Exception:
                    raise Stop("HELDFLIGHT_G6_DENIED") from None
                held._check()
                if (type(g6_evidence) is not dict or g6_evidence != frozen
                        or type(result) is not dict
                        or result.get("status") != g6_anchor.STATUS
                        or result.get("may_apply") is not False
                        or result.get("may_install") is not False
                        or result.get("admission_sha256")
                        != inputs["admission_sha256"]
                        or result.get("approval_sha256")
                        != journal.last_binding.get("external_anchor_sha256")
                        or result.get("nonce") != lease.nonce):
                    raise Stop("HELDFLIGHT_G6_DENIED")

            # NOTHING may be prepared for mutation until G6 and B7 pass.
            check_g6()
            try:
                report = held.inspect_once(
                    b7=b7, inspector=inputs["inspector"],
                    baseline_sha256=inputs["baseline_sha256"])
                held.require_current_record(report)
            except Exception:
                raise Stop("HELDFLIGHT_B7_DENIED") from None
            check_g6()
            try:
                plan = observer.capture_plan(
                    inputs["reader"], inputs["target"], nonce=lease.nonce,
                    admission_sha256=inputs["admission_sha256"])
                if (type(plan) is not dict
                        or flight.plan_mod.validate(plan) != "APPLYING"
                        or plan.get("baseline_sha256")
                        != report["baseline_sha256"]):
                    raise Stop("HELDFLIGHT_BASELINE_CHANGED")
                units = [switch.containment.unit_name(lease.nonce, service)
                         for service in ("api", "web")]
                scopes.initialize(lease, inputs["admission_sha256"], units, checkpoint)
                held.require_current_record(report)
            except Stop:
                raise
            except Exception:
                raise Stop("HELDFLIGHT_PREPARE_DENIED") from None

            # The process effect must be produced by the SIGNED entrypoint.
            # Never import host subprocess/Docker here and never provide a
            # default runner. The exact APPLYING plan is deep-copied AFTER B7,
            # G6, baseline match and durable scope names; the factory must
            # derive its 4-command allowlist from this very same plan, not
            # re-read an unfenced Docker baseline or trust a separate claim.
            # Never share the mutable original plan held by the flight.
            try:
                factory_plan = deepcopy(plan)
                held.require_current_record(report)
                runner = inputs["runner_factory"](
                    plane, held, report, factory_plan)
                if not callable(runner):
                    raise Stop("HELDFLIGHT_RUNNER_INVALID")
                if (factory_plan != plan
                        or flight.plan_mod.validate(plan) != "APPLYING"):
                    raise Stop("HELDFLIGHT_RUNNER_PLAN_MUTATED")
                held.require_current_record(report)
                check_g6()
            except Stop:
                raise
            except Exception:
                raise Stop("HELDFLIGHT_RUNNER_INVALID") from None

            def stage_gate(stage):
                try:
                    held.require_current_record(report)
                    if inputs["external_gate"](stage) is not True:
                        return False
                    held.require_current_record(report)
                    if stage in FORWARD:
                        check_g6()
                    held.require_current_record(report)
                    return True
                except Exception:
                    return False

            def record_unit(unit):
                held.require_current_record(report)
                digest = scopes.require_recorded(
                    lease, inputs["admission_sha256"], unit)
                held.require_current_record(report)
                return digest

            def guarded_run(argv):
                held.require_current_record(report)
                try:
                    code = runner(argv)
                except Exception:
                    raise Stop("HELDFLIGHT_CHILD_FAILED") from None
                held.require_current_record(report)
                if type(code) is not int:
                    raise Stop("HELDFLIGHT_CHILD_INVALID")
                return code

            common = dict(nonce=lease.nonce, project=inputs["project"],
                          workdir=inputs["workdir"], env_file=inputs["env_file"],
                          runner=guarded_run, scope_factory=inputs["scope_factory"],
                          record_unit=record_unit, observe=inputs["reader"].observe,
                          settle=inputs["settle"])
            try:
                forward = switch.ServiceSwitcher.for_plan(plan, "target", **common)
                rollback = switch.ServiceSwitcher.for_plan(plan, "baseline", **common)
                result = flight.run_apply(
                    lease, plan, switch_api=forward.switch_api,
                    switch_web=forward.switch_web,
                    rollback=rollback.switch_all,
                    observe=observer.make_observe(
                        inputs["reader"], plan["baseline"], plan["target"],
                        samples=2),
                    gate=stage_gate, now=inputs["clock"](),
                    checkpoint=checkpoint)
                ended["outcome"] = result.get("status")
                held.require_current_record(report)
                if ended["outcome"] not in ("APPLIED", "ROLLED_BACK"):
                    raise Stop("HELDFLIGHT_TERMINAL_UNKNOWN")
                return {
                    "status": STATUS,
                    "terminal_outcome": ended["outcome"],
                    "nonce": lease.nonce,
                    "baseline_sha256": report["baseline_sha256"],
                    "flight_context_sha256": report["flight_context_sha256"],
                    "may_apply": False, "may_install": False,
                    "signature_verified_here": False,
                    "sponsor_approved_here": False,
                    "g6_custody_verified_here": False,
                    "physical_host_authenticated_here": False,
                }
            except flight.Stop:
                ended["outcome"] = ended["outcome"] or "FLIGHT_STOP"
                raise
            except Stop:
                ended["outcome"] = ended["outcome"] or "FLIGHT_STOP"
                raise
            except Exception:
                ended["outcome"] = ended["outcome"] or "FLIGHT_STOP"
                raise Stop("HELDFLIGHT_EXECUTION_STOP") from None
    except root_locks.Stop as exc:
        if (str(exc) == "ROOTLOCK_JOURNAL_CLOSE_FAILED"
                and ended["outcome"] in ("APPLIED", "ROLLED_BACK", "FLIGHT_STOP")):
            raise Stop("HELDFLIGHT_CLOSE_FAILED_AFTER_" + ended["outcome"]) from None
        raise Stop("HELDFLIGHT_PLANE_DENIED") from None
    except (Stop, flight.Stop):
        raise
    except Exception:
        raise Stop("HELDFLIGHT_PLANE_DENIED") from None


if __name__ == "__main__":
    raise SystemExit("QA525_HELD_FLIGHT_LIBRARY_ONLY_NO_HOST_EXECUTION")
