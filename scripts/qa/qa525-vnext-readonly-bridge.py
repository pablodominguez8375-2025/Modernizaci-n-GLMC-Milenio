#!/usr/bin/env python3
"""QA525 exact physical-shape preflight over injected READ-ONLY Docker effects.

INACTIVE library: no CLI, process launcher, credentials, Docker import or root
capability. Deliberately does NOT authorize applying any release. The future
signed host entrypoint owns the effects, OPS/APP provenance, G6 and approval.

A successful capture means only that a caller-injected Docker/health port
reported a stable 7 running + 3 exited-zero baseline with externally pinned
complete Compose layers (at least the historical API6/WEB5). It is not independent host evidence or a signature.
"""
import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent
MIN_LAYERS = {"api": 6, "web": 5}
MAX_LAYERS = 64
RESULT_STATUS = "QA525_VNEXT_READONLY_SHAPE_STABLE_NOT_AUTHORIZED"


class Stop(Exception):
    """Only fixed, safe failure codes; never leak external outputs."""


def _module(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


adapters = _module("qa525_bridge_host_adapters", "qa525-vnext-host-adapters.py")
runtime = _module("qa525_bridge_runtime", "qa525-vnext-runtime.py")
plan = _module("qa525_bridge_plan", "qa525-vnext-baseline-plan.py")
verify = _module("qa525_bridge_verify", "qa525-vnext-verify.py")


def inspect(*, docker_read, compose_adapter, health_check, served_web_revision,
            expected_layers, samples=3):
    """Validate a historical physical shape, without granting execution rights.

    docker_read: caller-provided argv-list -> stdout-string, not owned here.
    compose_adapter: existing qa-lite-compose-adapter module (metadata only).
    health_check / served_web_revision: injected, *not authenticated* sources.
    expected_layers: ordered Compose lists PINNED by a separate trusted review.
      Caller authentication of that pin is mandatory before any real execution.

    The two independent read-only command allowlists reject all Docker writes;
    baseline and samples are compared with the existing vNext verifier. No
    fallback to old ComposeWebAdapter.reverify or to 7-only inventory exists.
    """
    if (not callable(docker_read) or not callable(health_check)
            or not callable(served_web_revision)
            or not callable(getattr(compose_adapter, "metadata", None))):
        raise Stop("BRIDGE_PORT_INVALID")
    if type(samples) is not int or not 2 <= samples <= 5:
        raise Stop("BRIDGE_SAMPLES_INVALID")
    if (type(expected_layers) is not dict or set(expected_layers) != set(MIN_LAYERS)
            or any(type(expected_layers[name]) is not list
                   or not MIN_LAYERS[name] <= len(expected_layers[name]) <= MAX_LAYERS
                   or any(type(p) is not str for p in expected_layers[name])
                   for name in MIN_LAYERS)):
        raise Stop("BRIDGE_PIN_INVALID")
    try:
        # Exact argv allowlist, then the existing runtime's read-verb guard.
        read = adapters.read_only_docker(docker_read)
        reader = runtime.build_reader(compose_adapter, read, health_check,
                                      served_web_revision)
        baseline = reader.capture_baseline()
        digest = plan.validate_baseline(baseline)
    except Exception:
        raise Stop("BRIDGE_BASELINE_STOP") from None
    # Historical 6/5 is a minimum, not a ceiling for later reviewed cuts.
    # Every complete ordered layer must match the external pin exactly.
    if baseline["layers"] != expected_layers:
        raise Stop("BRIDGE_COMPOSE_ORDER_MISMATCH")
    try:
        observation = verify.verify(reader, baseline, "ROLLED_BACK", samples=samples)
        expected = verify.expected_target_sha256(baseline, "ROLLED_BACK")
        verify.require_ok(observation, digest, "ROLLED_BACK", expected)
    except Exception:
        raise Stop("BRIDGE_OBSERVATION_STOP") from None
    # No container IDs, endpoints, paths, manifest bytes or secrets in result.
    return {
        "status": RESULT_STATUS,
        "baseline_sha256": digest,
        "observation_sha256": observation["evidence_sha256"],
        "running_services_expected": 7,
        "exited_zero_oneshots_verified": 3,
        "api_compose_layers": len(baseline["layers"]["api"]),
        "web_compose_layers": len(baseline["layers"]["web"]),
        "samples": samples,
        "sourced_from_authenticated_host": False,
        "sponsor_approved_here": False,
        "g6_verified_here": False,
        "may_apply": False,
        "may_install": False,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_READONLY_LIBRARY_ONLY_NO_HOST_OR_APPLY")
