#!/usr/bin/env python3
"""QA525 vNext PURE baseline + monotonic stage plan (NOT durable/NOT an APPLY).

No host access, locks, subprocess, Docker, file I/O or terminal writer.
The caller MUST obtain a trusted baseline under physical locks, persist it
durably before any mutating call, and authenticate every external proof.
This library only validates structured observations and plans transitions.
"""
from __future__ import annotations

import copy
import hashlib
import json
import re

TARGETS = ("api", "web")
DEPENDENCIES = ("keycloak", "postgres", "minio", "gateway", "clamav")
ONESHOTS = ("db-grants", "migrator", "minio-init")
STAGES = ("APPLYING", "API_SWITCHING", "API_VERIFIED",
          "WEB_SWITCHING", "WEB_VERIFIED", "VERIFYING")
SHA = re.compile(r"[a-f0-9]{40}\Z")
IMAGE = re.compile(r"sha256:[a-f0-9]{64}\Z")
CONTAINER = re.compile(r"[a-f0-9]{12,64}\Z")
DIGEST = re.compile(r"sha256:[a-f0-9]{64}\Z")
MAX_HISTORY = 8
MAX_LAYERS = 16


class Stop(Exception):
    """Fixed diagnostic codes, never external content."""


def canonical(doc: dict) -> bytes:
    return (json.dumps(doc, sort_keys=True, separators=(",", ":")) + "\n").encode()


def digest(doc: dict) -> str:
    return "sha256:" + hashlib.sha256(canonical(doc)).hexdigest()


def _has_keys(doc, keys, code):
    if type(doc) is not dict or set(doc) != set(keys):
        raise Stop(code)


def _layers(value, code):
    _has_keys(value, TARGETS, code)
    first = None
    for name in TARGETS:
        paths = value[name]
        if type(paths) is not list or not 0 < len(paths) <= MAX_LAYERS:
            raise Stop(code)
        if any(type(p) is not str or not p.startswith("/") or
               "//" in p or "\x00" in p or "." in p.split("/")
               or ".." in p.split("/") or p.endswith("/") for p in paths):
            raise Stop(code)
        if len(set(paths)) != len(paths):
            raise Stop(code)
        if first is None:
            first = paths[0]
        elif paths[0] != first:
            raise Stop(code)


def validate_baseline(base):
    """Conforms to the fields consumed by the separate Claude verifier PR #848."""
    _has_keys(base, ("images", "dependencies", "oneshots", "layers", "web_sha"),
              "BASELINE_FIELDS_INVALID")
    _has_keys(base["images"], TARGETS, "BASELINE_IMAGES_INVALID")
    if any(type(v) is not str or IMAGE.fullmatch(v) is None
           for v in base["images"].values()):
        raise Stop("BASELINE_IMAGES_INVALID")
    _has_keys(base["dependencies"], DEPENDENCIES, "BASELINE_DEPENDENCIES_INVALID")
    if any(type(v) is not str or CONTAINER.fullmatch(v) is None
           for v in base["dependencies"].values()):
        raise Stop("BASELINE_DEPENDENCIES_INVALID")
    _has_keys(base["oneshots"], ONESHOTS, "BASELINE_ONESHOTS_INVALID")
    for row in base["oneshots"].values():
        _has_keys(row, ("id", "exit_code"), "BASELINE_ONESHOTS_INVALID")
        if (type(row["id"]) is not str or CONTAINER.fullmatch(row["id"]) is None
                or type(row["exit_code"]) is not int or row["exit_code"] != 0):
            raise Stop("BASELINE_ONESHOTS_INVALID")
    _layers(base["layers"], "BASELINE_LAYERS_INVALID")
    if type(base["web_sha"]) is not str or SHA.fullmatch(base["web_sha"]) is None:
        raise Stop("BASELINE_WEB_SHA_INVALID")
    return digest(base)


def validate_target(base, target):
    validate_baseline(base)
    _has_keys(target, ("images", "layers", "web_sha"), "TARGET_FIELDS_INVALID")
    _has_keys(target["images"], TARGETS, "TARGET_IMAGES_INVALID")
    for name in TARGETS:
        new, old = target["images"][name], base["images"][name]
        if type(new) is not str or IMAGE.fullmatch(new) is None or new == old:
            raise Stop("TARGET_IMAGE_NOT_NEW")
    _layers(target["layers"], "TARGET_LAYERS_INVALID")
    if type(target["web_sha"]) is not str or SHA.fullmatch(target["web_sha"]) is None:
        raise Stop("TARGET_WEB_SHA_INVALID")
    if target["web_sha"] == base["web_sha"]:
        raise Stop("TARGET_WEB_SHA_NOT_NEW")
    return digest(target)


def _seal(body):
    return digest(body)


def _verified(plan):
    if type(plan) is not dict or set(plan) != {
        "schema", "stage", "baseline", "baseline_sha256", "target",
        "target_sha256", "events", "chain_sha256", "may_apply",
        "external_runtime_verified",
    }:
        raise Stop("PLAN_SCHEMA_INVALID")
    if (plan["schema"] != "qa525-offline-stage-plan-v1"
            or plan["may_apply"] is not False
            or plan["external_runtime_verified"] is not False):
        raise Stop("PLAN_AUTHORITY_INVALID")
    if plan["baseline_sha256"] != validate_baseline(plan["baseline"]):
        raise Stop("PLAN_BASELINE_CHANGED")
    if plan["target_sha256"] != validate_target(plan["baseline"], plan["target"]):
        raise Stop("PLAN_TARGET_CHANGED")
    events = plan["events"]
    if type(events) is not list or len(events) > MAX_HISTORY:
        raise Stop("PLAN_EVENTS_INVALID")
    stage = "APPLYING"
    for i, event in enumerate(events):
        _has_keys(event, ("from", "to", "evidence_sha256"), "PLAN_EVENT_INVALID")
        if event["from"] != stage or event["to"] not in STAGES[1:] + ("RECOVERY_REQUIRED",):
            raise Stop("PLAN_EVENT_ORDER_INVALID")
        if stage == "RECOVERY_REQUIRED":
            raise Stop("PLAN_TERMINAL_BLOCK")
        expected = (STAGES[STAGES.index(stage) + 1]
                    if stage in STAGES[:-1] else None)
        if event["to"] not in {expected, "RECOVERY_REQUIRED"}:
            raise Stop("PLAN_SKIP_OR_REPLAY")
        if type(event["evidence_sha256"]) is not str or not DIGEST.fullmatch(event["evidence_sha256"]):
            raise Stop("PLAN_PROOF_INVALID")
        stage = event["to"]
        if stage == "RECOVERY_REQUIRED" and i != len(events) - 1:
            raise Stop("PLAN_RECOVERY_NOT_TERMINAL")
    if plan["stage"] != stage:
        raise Stop("PLAN_STAGE_MISMATCH")
    body = {k: v for k, v in plan.items() if k != "chain_sha256"}
    if plan["chain_sha256"] != _seal(body):
        raise Stop("PLAN_CHECKSUM_MISMATCH")
    return stage


def prepare(base: dict, target: dict) -> dict:
    """Return a plan requiring OUTSIDE durable storage before any host action."""
    b, t = copy.deepcopy(base), copy.deepcopy(target)
    bsha = validate_baseline(b)
    tsha = validate_target(b, t)
    plan = {
        "schema": "qa525-offline-stage-plan-v1",
        "stage": "APPLYING", "baseline": b, "baseline_sha256": bsha,
        "target": t, "target_sha256": tsha, "events": [],
        "may_apply": False, "external_runtime_verified": False,
    }
    plan["chain_sha256"] = _seal(plan)
    return plan


def advance(plan: dict, next_stage: str, evidence_sha256: str) -> dict:
    """Pure monotonic plan only, NO file writer; terminal writer is separate."""
    stage = _verified(plan)
    if stage == "RECOVERY_REQUIRED":
        raise Stop("PLAN_RECOVERY_NOT_REPLAYABLE")
    if type(next_stage) is not str:
        raise Stop("PLAN_STAGE_INVALID")
    expected = (STAGES[STAGES.index(stage) + 1]
                if stage in STAGES[:-1] else None)
    if next_stage not in {expected, "RECOVERY_REQUIRED"}:
        raise Stop("PLAN_SKIP_OR_REPLAY")
    if type(evidence_sha256) is not str or DIGEST.fullmatch(evidence_sha256) is None:
        raise Stop("PLAN_PROOF_INVALID")
    successor = copy.deepcopy(plan)
    successor["events"].append({"from": stage, "to": next_stage,
                                "evidence_sha256": evidence_sha256})
    successor["stage"] = next_stage
    successor["chain_sha256"] = _seal({
        k: v for k, v in successor.items() if k != "chain_sha256"
    })
    _verified(successor)
    return successor


def validate(plan: dict) -> str:
    """Fail-closed structure/step verification, not proof of durable commits."""
    return _verified(plan)


if __name__ == "__main__":
    raise SystemExit("QA525_BASELINE_PLAN_LIBRARY_ONLY_NO_APPLY")
