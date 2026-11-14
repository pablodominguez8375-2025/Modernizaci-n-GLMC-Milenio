#!/usr/bin/env python3
"""QA525 vNext auto-web release envelope `pmgm-qa-auto-web-envelope-v1` (contract v1.2).

Schema, canonical builder (CI side) and PURE checker (host side). INACTIVE: no I/O, no network,
no signature. Authenticity (Sigstore attestation of these exact bytes by the pinned workflow) and
the Sponsor policy signature belong to the pre-root chain and to `qa525-vnext-policy.py`.

`check` never authorizes an apply. Its `expect` argument must come ONLY from host-owned sources:
the verified Sponsor policy (pins, sequence window) and fresh read-only host measurements
(`installed`). Nothing in the envelope is trusted to describe the host (contract §3, v1.1 P1-2).

Lesson "NO QUEMAR": this module consumes nothing. Callers must run every check here (and the
policy check) BEFORE spending a sequence, a release slot or a nonce.
"""
from __future__ import annotations

import datetime as dt
import hashlib
import hmac
import json
import re

SCHEMA = "pmgm-qa-auto-web-envelope-v1"
CONTRACT = "1.3"
MAX_BYTES = 262144
MAX_VALIDITY_S = 72 * 3600
ISSUER = "https://token.actions.githubusercontent.com"
SHA = re.compile(r"[0-9a-f]{40}\Z")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}\Z")
ISO = re.compile(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ\Z")

TOP = frozenset({"schema", "release_id", "repository", "source_repository_id", "environment",
                 "mode", "sequence", "installed", "candidate", "lineage", "classifier", "ci",
                 "issued_at", "not_before", "expires_at"})
INSTALLED = frozenset({"sha", "web_image_digest", "api_image_digest", "compose_bytes_sha256",
                       "identity_digest", "silo_image_digest"})
CANDIDATE = frozenset({"sha", "web_image_digest", "web_image_archive_sha256", "web_sbom_sha256",
                       "base_image_digests", "build_args_sha256", "oci_shape_sha256",
                       "rootfs_diff_ids_sha256", "base_diff_ids_sha256"})
BASES = frozenset({"build", "final"})
LINEAGE = frozenset({"merge_base", "ahead_by", "total_changed_files", "changed_files_complete",
                     "changed_files"})
CLASSIFIER = frozenset({"version", "sha256", "result", "reasons"})
CI = frozenset({"workflow", "workflow_ref", "run_id", "run_attempt", "issuer", "subject",
                "source_sha"})
EXPECT = frozenset({"repository", "source_repository_id", "installed", "classifier_sha256",
                    "workflow", "workflow_ref", "subject", "allowed_base_image_digests",
                    "build_args_sha256", "oci_shape_sha256", "base_diff_ids_sha256",
                    "content_layer_count", "first_sequence", "last_sequence", "last_applied_sequence"})
# The 7 ARG of frontend/Dockerfile:8-14, exactly (review 5904808610 P1-1).
BUILD_ARG_KEYS = frozenset({"VITE_API_BASE_URL", "VITE_USE_MOCKS", "VITE_OIDC_AUTHORITY",
                            "VITE_OIDC_CLIENT_ID", "VITE_OIDC_SCOPE", "VITE_OIDC_ALLOW_HTTP_LOCAL",
                            "VITE_OIDC_ALLOW_HTTP_QA"})
# OCI config keys that define how the WEB container runs; Labels are excluded (CI metadata).
SHAPE_KEYS = ("Cmd", "Entrypoint", "Env", "ExposedPorts", "Healthcheck", "StopSignal", "User",
              "Volumes", "WorkingDir")

# Decisions (not authorizations). Stop codes are `reject`.
AUTO_WEB_CANDIDATE = "AUTO_WEB_CANDIDATE_CHECKED_NO_APPLY"
MANUAL = "MANUAL_NO_AUTOMATIC_ACTION"
NO_OP_NOT_BEFORE = "NO_OP_BEFORE_NOT_BEFORE"


class Stop(Exception):
    """Static diagnostic code only; never echoes external content."""


def _unique(pairs):
    out = {}
    for key, val in pairs:
        if key in out:
            raise Stop("ENVELOPE_DUPLICATE_KEY")
        out[key] = val
    return out


def canonical(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":"), ensure_ascii=True,
                       allow_nan=False) + "\n").encode("ascii")


def release_id(record):
    body = {k: v for k, v in record.items() if k != "release_id"}
    return "sha256:" + hashlib.sha256(canonical(body)).hexdigest()


def build_args_sha256(args):
    """Digest of the EXACT 7 WEB build arguments (contract P1-2, same rules as qa-lite-web-build).

    Exact values (authority, ALLOW_HTTP_QA) are pinned by the Sponsor policy from the real
    compose (FUENTE ANTES QUE CODIGO); here only the invariant rules are enforced."""
    if (type(args) is not dict or set(args) != BUILD_ARG_KEYS
            or any(type(v) is not str or len(v) > 256 or any(c in v for c in "\r\n\x00")
                   for v in args.values())):
        raise Stop("BUILD_ARGS_INVALID")
    if args["VITE_USE_MOCKS"] != "false":
        raise Stop("BUILD_ARGS_MOCKS_FORBIDDEN")
    if (args["VITE_API_BASE_URL"] != "" or not args["VITE_OIDC_AUTHORITY"].startswith("https://")
            or args["VITE_OIDC_CLIENT_ID"] != "pmgm-web" or args["VITE_OIDC_SCOPE"] != "openid profile"
            or args["VITE_OIDC_ALLOW_HTTP_LOCAL"] != "false"
            or args["VITE_OIDC_ALLOW_HTTP_QA"] not in ("true", "false")):
        raise Stop("BUILD_ARGS_IDENTITY_CHANGED")
    return "sha256:" + hashlib.sha256(canonical(args)).hexdigest()


def _sha_of(obj):
    return "sha256:" + hashlib.sha256(canonical(obj)).hexdigest()


def image_binding(image_config, content_layer_count):
    """From an OCI image config (or `docker image inspect` record): shape and layer digests.

    Returns {oci_shape_sha256, rootfs_diff_ids_sha256, base_diff_ids_sha256}. The base layers are
    every diff_id except the last `content_layer_count` (COPY dist + chmod in frontend/Dockerfile).
    Used by CI to fill the envelope and by the host on the LOADED image (review P1-3)."""
    if type(image_config) is not dict or type(content_layer_count) is not int or content_layer_count < 1:
        raise Stop("IMAGE_CONFIG_INVALID")
    cfg = image_config.get("config", image_config.get("Config"))
    rootfs = image_config.get("rootfs", image_config.get("RootFS"))
    arch = image_config.get("architecture", image_config.get("Architecture"))
    osname = image_config.get("os", image_config.get("Os"))
    if type(cfg) is not dict or type(rootfs) is not dict or type(arch) is not str or type(osname) is not str:
        raise Stop("IMAGE_CONFIG_INVALID")
    diff_ids = rootfs.get("diff_ids", rootfs.get("Layers"))
    if (type(diff_ids) is not list or len(diff_ids) <= content_layer_count
            or any(type(d) is not str or DIGEST.fullmatch(d) is None for d in diff_ids)):
        raise Stop("IMAGE_CONFIG_INVALID")
    shape = {"architecture": arch, "os": osname, "config": {k: cfg.get(k) for k in SHAPE_KEYS}}
    try:
        shape_sha = _sha_of(shape)
    except (TypeError, ValueError):
        raise Stop("IMAGE_CONFIG_INVALID") from None
    return {"oci_shape_sha256": shape_sha, "rootfs_diff_ids_sha256": _sha_of(diff_ids),
            "base_diff_ids_sha256": _sha_of(diff_ids[:-content_layer_count])}


def verify_loaded_image(decision, image_config, content_layer_count):
    """Host side, after `docker load` and BEFORE spending: the loaded image is the attested one."""
    if type(decision) is not dict or decision.get("decision") != AUTO_WEB_CANDIDATE:
        raise Stop("LOADED_IMAGE_NO_CANDIDATE")
    got = image_binding(image_config, content_layer_count)
    for key in ("oci_shape_sha256", "rootfs_diff_ids_sha256", "base_diff_ids_sha256"):
        _eq(got[key], decision[key], "LOADED_IMAGE_MISMATCH")
    return True


def build(*, repository, source_repository_id, sequence, installed, candidate, lineage,
          classifier_module, classifier_sha256, ci, issued_at, not_before, expires_at):
    """CI-side builder. The classifier verdict is computed here, never copied from input."""
    files = sorted(lineage["changed_files"], key=lambda e: e["path"])
    verdict = classifier_module.classify(files, complete=lineage["changed_files_complete"],
                                         total=lineage["total_changed_files"])
    record = {
        "schema": SCHEMA, "repository": repository, "source_repository_id": source_repository_id,
        "environment": "qa", "mode": "auto-web", "sequence": sequence,
        "installed": dict(installed), "candidate": dict(candidate),
        "lineage": dict(lineage, changed_files=files),
        "classifier": {"version": verdict["version"], "sha256": classifier_sha256,
                       "result": verdict["result"], "reasons": verdict["reasons"]},
        "ci": dict(ci), "issued_at": issued_at, "not_before": not_before, "expires_at": expires_at}
    record["release_id"] = release_id(record)
    return canonical(record)


def _when(text):
    if type(text) is not str or ISO.fullmatch(text) is None:
        raise Stop("ENVELOPE_TIME_INVALID")
    try:
        return dt.datetime.strptime(text, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=dt.timezone.utc)
    except ValueError:
        raise Stop("ENVELOPE_TIME_INVALID") from None


def _eq(have, want, code):
    if type(have) is not str or type(want) is not str or not hmac.compare_digest(have, want):
        raise Stop(code)


def _keys(obj, keys, code):
    if type(obj) is not dict or set(obj) != keys:
        raise Stop(code)


def _pos_int(value, code):
    if type(value) is not int or value < 1:
        raise Stop(code)


def _digests(obj, keys, code):
    for key in keys:
        if type(obj[key]) is not str or DIGEST.fullmatch(obj[key]) is None:
            raise Stop(code)


def check(raw, *, expect, classifier_module, now):
    """Return a decision dict or raise Stop (= reject). `expect` is host-owned (see module doc)."""
    if type(expect) is not dict or set(expect) != EXPECT:
        raise Stop("EXPECT_INVALID")
    if type(now) is not dt.datetime or now.tzinfo is None:
        raise Stop("CLOCK_INVALID")
    if type(raw) is not bytes or not 0 < len(raw) <= MAX_BYTES:
        raise Stop("ENVELOPE_BYTES_INVALID")
    try:
        env = json.loads(raw.decode("ascii"), object_pairs_hook=_unique,
                         parse_float=lambda _: (_ for _ in ()).throw(Stop("ENVELOPE_FLOAT")),
                         parse_constant=lambda _: (_ for _ in ()).throw(Stop("ENVELOPE_FLOAT")))
    except (ValueError, UnicodeError):
        raise Stop("ENVELOPE_JSON_INVALID") from None
    _keys(env, TOP, "ENVELOPE_FIELDS")
    if canonical(env) != raw:
        raise Stop("ENVELOPE_NOT_CANONICAL")
    if env["release_id"] != release_id(env):
        raise Stop("ENVELOPE_RELEASE_ID")
    if (env["schema"] != SCHEMA or env["environment"] != "qa" or env["mode"] != "auto-web"):
        raise Stop("ENVELOPE_SCOPE")
    _eq(env["repository"], expect["repository"], "ENVELOPE_REPOSITORY_PIN")
    if type(env["source_repository_id"]) is not int or env["source_repository_id"] != expect["source_repository_id"]:
        raise Stop("ENVELOPE_REPOSITORY_PIN")

    inst, cand, lin, cls, ci = (env["installed"], env["candidate"], env["lineage"],
                                env["classifier"], env["ci"])
    _keys(inst, INSTALLED, "ENVELOPE_FIELDS")
    _keys(cand, CANDIDATE, "ENVELOPE_FIELDS")
    _keys(cand["base_image_digests"], BASES, "ENVELOPE_FIELDS")
    _keys(lin, LINEAGE, "ENVELOPE_FIELDS")
    _keys(cls, CLASSIFIER, "ENVELOPE_FIELDS")
    _keys(ci, CI, "ENVELOPE_FIELDS")
    for sha in (inst["sha"], cand["sha"], lin["merge_base"], ci["source_sha"]):
        if type(sha) is not str or SHA.fullmatch(sha) is None:
            raise Stop("ENVELOPE_SHA_INVALID")
    _digests(inst, INSTALLED - {"sha"}, "ENVELOPE_DIGEST_INVALID")
    _digests(cand, CANDIDATE - {"sha", "base_image_digests"}, "ENVELOPE_DIGEST_INVALID")
    _digests(cand["base_image_digests"], BASES, "ENVELOPE_DIGEST_INVALID")
    _digests(cls, {"sha256"}, "ENVELOPE_DIGEST_INVALID")

    # Workflow binding (the Sigstore verifier separately proves these bytes came from it).
    _eq(ci["workflow"], expect["workflow"], "ENVELOPE_WORKFLOW_PIN")
    _eq(ci["workflow_ref"], expect["workflow_ref"], "ENVELOPE_WORKFLOW_PIN")
    _eq(ci["subject"], expect["subject"], "ENVELOPE_WORKFLOW_PIN")
    _eq(ci["issuer"], ISSUER, "ENVELOPE_WORKFLOW_PIN")
    _pos_int(ci["run_id"], "ENVELOPE_CI_INVALID")
    _pos_int(ci["run_attempt"], "ENVELOPE_CI_INVALID")
    _eq(ci["source_sha"], cand["sha"], "ENVELOPE_SOURCE_SHA")

    # Host measurements, never envelope claims, describe the installed state.
    measured = expect["installed"]
    if type(measured) is not dict or set(measured) != INSTALLED:
        raise Stop("EXPECT_INVALID")
    for key in sorted(INSTALLED):
        _eq(inst[key], measured[key], "ENVELOPE_INSTALLED_DRIFT")

    # Lineage: strict fast-forward from the installed SHA.
    _eq(lin["merge_base"], inst["sha"], "ENVELOPE_NOT_DESCENDANT")
    if cand["sha"] == inst["sha"]:
        raise Stop("ENVELOPE_NOT_DESCENDANT")
    _pos_int(lin["ahead_by"], "ENVELOPE_NOT_DESCENDANT")
    if type(lin["changed_files_complete"]) is not bool or type(lin["total_changed_files"]) is not int:
        raise Stop("ENVELOPE_LINEAGE_INVALID")

    # Classifier: pinned hash, recomputed verdict must match byte for byte.
    _eq(cls["sha256"], expect["classifier_sha256"], "ENVELOPE_CLASSIFIER_PIN")
    verdict = classifier_module.classify(lin["changed_files"], complete=lin["changed_files_complete"],
                                         total=lin["total_changed_files"])
    if (cls["version"] != verdict["version"] or cls["result"] != verdict["result"]
            or cls["reasons"] != verdict["reasons"]):
        raise Stop("ENVELOPE_CLASSIFICATION_MISMATCH")

    # Sequence window from the verified policy; replay and regression are rejects.
    seq, first, last, applied = (env["sequence"], expect["first_sequence"], expect["last_sequence"],
                                 expect["last_applied_sequence"])
    if any(type(x) is not int for x in (seq, first, last, applied)):
        raise Stop("ENVELOPE_SEQUENCE_INVALID")
    if seq <= applied:
        raise Stop("ENVELOPE_SEQUENCE_REPLAY")
    if seq != applied + 1:
        raise Stop("ENVELOPE_SEQUENCE_GAP")
    if not first <= seq <= last:
        raise Stop("ENVELOPE_SEQUENCE_OUTSIDE_POLICY")

    issued, start, end = _when(env["issued_at"]), _when(env["not_before"]), _when(env["expires_at"])
    if not issued <= start < end or (end - start).total_seconds() > MAX_VALIDITY_S:
        raise Stop("ENVELOPE_WINDOW_INVALID")
    if now >= end:
        raise Stop("ENVELOPE_EXPIRED")

    base = {"release_id": env["release_id"], "sequence": seq, "candidate_sha": cand["sha"],
            "reasons": cls["reasons"], "contract": CONTRACT}
    if cls["result"] != "auto-web":
        return dict(base, decision=MANUAL)
    # Only auto-web candidates must satisfy the image binding (P1-1/P1-2).
    if cand["web_image_digest"] == inst["web_image_digest"]:
        raise Stop("ENVELOPE_IMAGE_NOT_NEW")
    allowed = expect["allowed_base_image_digests"]
    if type(allowed) is not dict or set(allowed) != BASES:
        raise Stop("EXPECT_INVALID")
    if cand["base_image_digests"] != allowed:
        return dict(base, decision=MANUAL, reasons=["BASE_IMAGE_DRIFT"])
    if type(expect["content_layer_count"]) is not int or expect["content_layer_count"] < 1:
        raise Stop("EXPECT_INVALID")
    _eq(cand["build_args_sha256"], expect["build_args_sha256"], "ENVELOPE_BUILD_ARGS")
    if cand["oci_shape_sha256"] != expect["oci_shape_sha256"]:
        return dict(base, decision=MANUAL, reasons=["OCI_SHAPE_CHANGED"])
    if cand["base_diff_ids_sha256"] != expect["base_diff_ids_sha256"]:
        return dict(base, decision=MANUAL, reasons=["BASE_LAYERS_DRIFT"])
    if now < start:
        return dict(base, decision=NO_OP_NOT_BEFORE)
    return dict(base, decision=AUTO_WEB_CANDIDATE, web_image_digest=cand["web_image_digest"],
                web_image_archive_sha256=cand["web_image_archive_sha256"],
                oci_shape_sha256=cand["oci_shape_sha256"],
                rootfs_diff_ids_sha256=cand["rootfs_diff_ids_sha256"],
                base_diff_ids_sha256=cand["base_diff_ids_sha256"],
                rollback_web_image_digest=inst["web_image_digest"])


if __name__ == "__main__":
    raise SystemExit("QA525_RELEASE_ENVELOPE_LIBRARY_ONLY_NO_EXECUTION")
