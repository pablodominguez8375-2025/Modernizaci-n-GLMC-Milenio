#!/usr/bin/env python3
"""QA525 vNext terminal-state verifier -- READ-ONLY, INACTIVE, NOT an executor.

Decides whether the observed runtime equals either the sealed baseline
(ROLLED_BACK) or an explicit, different target (APPLIED), or a mid-transaction checkpoint (API_SWITCHED: api on target, web still baseline; WEB_SWITCHED: both on target). It only *reads*
through an injected adapter: no Docker, network, subprocess or host path is
imported or touched here. A future engine supplies a real adapter; a terminal
journal writer must accept only a result that passes ``require_ok`` for the
same baseline hash and kind.

Integrity, not authenticity: ``result_checksum_sha256`` is a PUBLIC checksum
that detects accidental corruption of the structure. It is not a signature
and does not prove who measured what. A terminal writer must hold the
transaction locks and invoke ``verify`` itself through a trusted adapter; it
must never accept a result JSON handed to it by another component.

``health`` in an observation is whatever the future adapter reports. That
adapter must check API and WEB health, OIDC, and the image/dependency/one-shot
state itself; two back-to-back samples here are not a soak period and nothing
in this module is physical evidence about any host.

Fail-closed: any adapter error, malformed observation, unstable sample,
missing/extra service, truthy-but-not-True flag or unexpected layer is a
non-OK result. This module never writes state and never declares success from
a status field written by someone else.
"""
import hashlib
import hmac
import json
import re

IMAGE_ID = re.compile(r"^(?:sha256:)?[a-f0-9]{64}$")
CONTAINER_ID = re.compile(r"^[a-f0-9]{12,64}$")
SHA40 = re.compile(r"^[a-f0-9]{40}$")
TARGETS = ("api", "web")
KINDS = ("APPLIED", "ROLLED_BACK", "API_SWITCHED", "WEB_SWITCHED")
TARGET_KINDS = ("APPLIED", "API_SWITCHED", "WEB_SWITCHED")
MAX_SERVICES = 32
MAX_LAYERS = 64
RUNTIME_CODES = frozenset(
    ["OK", "OBSERVATION_INVALID", "VERIFY_INDETERMINATE", "UNSTABLE_BETWEEN_SAMPLES",
     "DEPENDENCY_CHANGED", "ONESHOT_CHANGED", "HEALTH_FAILED",
     "LAYERS_MISMATCH_API", "LAYERS_MISMATCH_WEB",
     "IMAGE_MISMATCH_API", "IMAGE_MISMATCH_WEB",
     "SERVICE_NOT_RUNNING_API", "SERVICE_NOT_RUNNING_WEB"])


class Stop(Exception):
    """Fail-closed diagnostic code; never carries external text."""


def canonical(value) -> bytes:
    return (json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n").encode()


def digest(value) -> str:
    return "sha256:" + hashlib.sha256(canonical(value)).hexdigest()


def _exact(value, kind, code):
    if type(value) is not kind:
        raise Stop(code)
    return value


def _names(mapping, code, required=None):
    _exact(mapping, dict, code)
    if not 0 < len(mapping) <= MAX_SERVICES:
        raise Stop(code)
    for key in mapping:
        if type(key) is not str or re.fullmatch(r"[a-z][a-z0-9_-]{0,40}", key) is None:
            raise Stop(code)
    if required is not None and set(mapping) != set(required):
        raise Stop(code)


def _layers(layers, code):
    _names(layers, code, TARGETS)
    for paths in layers.values():
        _exact(paths, list, code)
        if not 0 < len(paths) <= MAX_LAYERS or len(set(paths)) != len(paths):
            raise Stop(code)
        for path in paths:
            if (type(path) is not str or not path.startswith("/") or "\0" in path
                    or any(part in ("", ".", "..") for part in path[1:].split("/"))):
                raise Stop(code)


def validate_baseline(baseline: dict) -> str:
    """Strict schema check; returns the baseline digest used to bind results."""
    _exact(baseline, dict, "BASELINE_INVALID")
    if set(baseline) != {"images", "dependencies", "oneshots", "layers", "web_sha"}:
        raise Stop("BASELINE_INVALID")
    _names(baseline["images"], "BASELINE_INVALID", TARGETS)
    for image in baseline["images"].values():
        if type(image) is not str or IMAGE_ID.fullmatch(image) is None:
            raise Stop("BASELINE_INVALID")
    _names(baseline["dependencies"], "BASELINE_INVALID")
    for ident in baseline["dependencies"].values():
        if type(ident) is not str or CONTAINER_ID.fullmatch(ident) is None:
            raise Stop("BASELINE_INVALID")
    _names(baseline["oneshots"], "BASELINE_INVALID")
    for row in baseline["oneshots"].values():
        _exact(row, dict, "BASELINE_INVALID")
        if (set(row) != {"id", "exit_code"} or type(row["id"]) is not str
                or CONTAINER_ID.fullmatch(row["id"]) is None
                or type(row["exit_code"]) is not int):
            raise Stop("BASELINE_INVALID")
    _layers(baseline["layers"], "BASELINE_INVALID")
    if type(baseline["web_sha"]) is not str or SHA40.fullmatch(baseline["web_sha"]) is None:
        raise Stop("BASELINE_INVALID")
    return digest(baseline)


def validate_target(baseline: dict, kind: str, target: dict) -> dict:
    """Return the exact expectation for ``kind``.

    ROLLED_BACK ignores caller-supplied expectations: it is always the
    sealed baseline. APPLIED needs an explicit target whose images differ from
    the baseline; dependencies and one-shots must stay untouched (the dual
    transaction recreates only api/web with --no-deps).
    """
    if kind not in KINDS:
        raise Stop("KIND_INVALID")
    if kind == "ROLLED_BACK":
        if target is not None:
            raise Stop("ROLLBACK_TARGET_MUST_BE_BASELINE")
        return {"images": baseline["images"], "layers": baseline["layers"],
                "web_sha": baseline["web_sha"]}
    if kind not in TARGET_KINDS:
        raise Stop("KIND_INVALID")
    _exact(target, dict, "TARGET_INVALID")
    if set(target) != {"images", "layers", "web_sha"}:
        raise Stop("TARGET_INVALID")
    _names(target["images"], "TARGET_INVALID", TARGETS)
    for image in target["images"].values():
        if type(image) is not str or IMAGE_ID.fullmatch(image) is None:
            raise Stop("TARGET_INVALID")
    _layers(target["layers"], "TARGET_INVALID")
    if type(target["web_sha"]) is not str or SHA40.fullmatch(target["web_sha"]) is None:
        raise Stop("TARGET_INVALID")
    if any(target["images"][name] == baseline["images"][name] for name in TARGETS):
        raise Stop("APPLIED_TARGET_EQUALS_BASELINE")
    if kind == "API_SWITCHED":
        # Mid-transaction checkpoint: api already on the target, web still the
        # sealed baseline (image, layers and served web_sha).
        return {"images": {"api": target["images"]["api"], "web": baseline["images"]["web"]},
                "layers": {"api": target["layers"]["api"], "web": baseline["layers"]["web"]},
                "web_sha": baseline["web_sha"]}
    return target  # APPLIED and WEB_SWITCHED: both services on the target


def _observation(adapter) -> dict:
    """One strict observation. Every field must have the exact type."""
    try:
        obs = adapter.observe()
    except Exception:  # any adapter exception, even a Stop it raises, is untrusted text
        raise Stop("VERIFY_INDETERMINATE") from None
    _exact(obs, dict, "OBSERVATION_INVALID")
    if set(obs) != {"images", "running", "dependencies", "oneshots", "layers", "health"}:
        raise Stop("OBSERVATION_INVALID")
    _names(obs["images"], "OBSERVATION_INVALID", TARGETS)
    _names(obs["running"], "OBSERVATION_INVALID", TARGETS)
    for name in TARGETS:
        if type(obs["images"][name]) is not str or IMAGE_ID.fullmatch(obs["images"][name]) is None:
            raise Stop("OBSERVATION_INVALID")
        if type(obs["running"][name]) is not bool:
            raise Stop("OBSERVATION_INVALID")
    _names(obs["dependencies"], "OBSERVATION_INVALID")
    for ident in obs["dependencies"].values():
        if type(ident) is not str or CONTAINER_ID.fullmatch(ident) is None:
            raise Stop("OBSERVATION_INVALID")
    _names(obs["oneshots"], "OBSERVATION_INVALID")
    for row in obs["oneshots"].values():
        if (type(row) is not dict or set(row) != {"id", "exit_code"}
                or type(row["id"]) is not str or CONTAINER_ID.fullmatch(row["id"]) is None
                or type(row["exit_code"]) is not int):
            raise Stop("OBSERVATION_INVALID")
    _layers(obs["layers"], "OBSERVATION_INVALID")
    health = obs["health"]
    if (type(health) is not dict or set(health) != {"ok", "web_sha"}
            or type(health["ok"]) is not bool or type(health["web_sha"]) is not str):
        raise Stop("OBSERVATION_INVALID")
    return obs


def _compare(baseline: dict, expect: dict, obs: dict) -> None:
    for name in TARGETS:
        if obs["images"][name] != expect["images"][name]:
            raise Stop("IMAGE_MISMATCH_" + name.upper())
        if obs["running"][name] is not True:
            raise Stop("SERVICE_NOT_RUNNING_" + name.upper())
    if obs["dependencies"] != baseline["dependencies"]:
        raise Stop("DEPENDENCY_CHANGED")
    if obs["oneshots"] != baseline["oneshots"]:
        raise Stop("ONESHOT_CHANGED")
    # Exact lists, same order: an orphan image-only layer (the 6eed4 residue)
    # or a missing layer is a mismatch, not "close enough".
    for name in TARGETS:
        if obs["layers"][name] != expect["layers"][name]:
            raise Stop("LAYERS_MISMATCH_" + name.upper())
    if obs["health"]["ok"] is not True or obs["health"]["web_sha"] != expect["web_sha"]:
        raise Stop("HEALTH_FAILED")


def verify(adapter, baseline: dict, kind: str, target: dict = None, samples: int = 2) -> dict:
    """Return a result dict (public checksum, not a signature); ok=False on any problem (never raises on
    runtime evidence). Programming errors in baseline/target still raise Stop
    because the caller must not proceed on a malformed contract."""
    baseline_sha = validate_baseline(baseline)
    expect = validate_target(baseline, kind, target)
    if type(samples) is not int or not 2 <= samples <= 5:
        raise Stop("SAMPLES_INVALID")
    code = "OK"
    evidence = None
    try:
        first = None
        for _ in range(samples):
            obs = _observation(adapter)
            _compare(baseline, expect, obs)
            if first is None:
                first = obs
            elif obs != first:
                raise Stop("UNSTABLE_BETWEEN_SAMPLES")
        evidence = digest(first)
    except Stop as exc:
        code = str(exc)
    except Exception:  # unexpected failure: indeterminate, never success
        code = "VERIFY_INDETERMINATE"
    if code not in RUNTIME_CODES:  # never let outside text reach a report
        code = "VERIFY_INDETERMINATE"
    body = {"schema": "qa525-verify-result-v1", "kind": kind, "ok": code == "OK",
            "code": code, "baseline_sha256": baseline_sha,
            "target_sha256": digest(expect), "evidence_sha256": evidence}
    body["result_checksum_sha256"] = digest(body)
    return body


def expected_target_sha256(baseline: dict, kind: str, target: dict = None) -> str:
    """Digest of the exact expectation the caller wants verified.

    For ROLLED_BACK it is derived from the (trusted) baseline alone; for
    APPLIED it is derived from the explicit target the caller approved.
    """
    validate_baseline(baseline)
    return digest(validate_target(baseline, kind, target))


def _same(left, right) -> bool:
    return (type(left) is str and type(right) is str
            and hmac.compare_digest(left.encode(), right.encode()))


def require_ok(result: dict, baseline_sha256: str, kind: str, expected_target: str) -> dict:
    """Structural gate for a future terminal writer (NOT authentication).

    Re-derives the public checksum, then binds the result to the baseline,
    the kind AND the exact target the caller expects, so an OK result for a
    different target of the same baseline cannot be reused.
    """
    _exact(result, dict, "RESULT_INVALID")
    fields = {"schema", "kind", "ok", "code", "baseline_sha256", "target_sha256",
              "evidence_sha256", "result_checksum_sha256"}
    if set(result) != fields or result["schema"] != "qa525-verify-result-v1":
        raise Stop("RESULT_INVALID")
    body = {key: value for key, value in result.items() if key != "result_checksum_sha256"}
    if not _same(digest(body), result["result_checksum_sha256"]):
        raise Stop("RESULT_CHECKSUM_INVALID")
    if kind not in KINDS or result["kind"] != kind:
        raise Stop("RESULT_KIND_MISMATCH")
    if not _same(result["baseline_sha256"], baseline_sha256):
        raise Stop("RESULT_BASELINE_MISMATCH")
    if not _same(result["target_sha256"], expected_target):
        raise Stop("RESULT_TARGET_MISMATCH")
    if result["ok"] is not True or result["code"] != "OK" or result["evidence_sha256"] is None:
        raise Stop("RESULT_NOT_OK")
    return result
