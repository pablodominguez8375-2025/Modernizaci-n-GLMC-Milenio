#!/usr/bin/env python3
"""QA525 offline PLAN/ADMISSION evidence contract: pure library, never a GO."""
import datetime as dt
import hashlib
import json
import re

HEX64 = re.compile(r"[0-9a-f]{64}\Z")
HEX40 = re.compile(r"[0-9a-f]{40}\Z")
SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")
UTC = re.compile(r"20\d\d-\d\d-\d\dT\d\d:\d\d:\d\dZ\Z")
SCOPE = "QA525_LAUNCHER_ONLY_NO_DOCKER_NO_IDENTITY_NO_MIGRATION"
GATES = ("ops_app", "pre_root_v3", "g6_db_globals", "g6_silo_offhost",
         "g6_isolated_restore", "minio", "python314_rootless")
PLAN_KEYS = {"schema", "state", "host", "environment", "scope", "operator",
             "source_sha", "app_archive_sha256", "old_launcher_sha256",
             "candidate_operator_sha256", "ops_receipt_sha256",
             "release_asset_sha256", "policy_hash"}
ADMISSION_KEYS = {"schema", "state", "plan_sha256", "source_sha",
                  "environment", "scope", "gates"}
GATE_KEYS = {"status", "evidence_sha256", "checked_at_utc", "reference"}

class Reject(ValueError):
    pass

def _need(condition, code):
    if not condition:
        raise Reject(code)

def _sha(value):
    return type(value) is str and SHA.fullmatch(value) is not None

def canonical(obj):
    """Stable ASCII/JSON bytes without newline, unlike a GO acta."""
    _need(type(obj) is dict, "OBJECT_REQUIRED")
    return json.dumps(obj, sort_keys=True, separators=(",", ":"),
                      ensure_ascii=True, allow_nan=False).encode("ascii")

def parse_exact(raw):
    _need(type(raw) is bytes and 0 < len(raw) <= 65536, "BYTES_INVALID")
    def pairs(items):
        out = {}
        for key, val in items:
            _need(key not in out, "DUPLICATE_KEY")
            out[key] = val
        return out
    try:
        obj = json.loads(raw.decode("ascii"), object_pairs_hook=pairs,
                         parse_constant=lambda _: (_ for _ in ()).throw(Reject("NON_FINITE")))
    except (UnicodeError, ValueError, RecursionError) as exc:
        raise Reject("JSON_INVALID") from exc
    _need(type(obj) is dict and canonical(obj) == raw, "NOT_CANONICAL")
    return obj

def check_plan(raw):
    p = parse_exact(raw)
    _need(set(p) == PLAN_KEYS, "PLAN_FIELDS")
    required = {"schema": "qa525-plan-launcher-only-v1", "state": "PENDING_REVIEW_NO_GO",
                "host": "glmdch", "environment": "QA_GLMDCH_ONLY", "scope": SCOPE,
                "operator": "soporte@glmdch"}
    _need(all(p[k] == v for k, v in required.items()), "PLAN_SCOPE")
    _need(type(p["source_sha"]) is str and HEX40.fullmatch(p["source_sha"]) is not None,
          "PLAN_SOURCE")
    _need(all(_sha(p[k]) for k in ("app_archive_sha256", "ops_receipt_sha256",
                                    "release_asset_sha256", "policy_hash")),
          "PLAN_DIGEST")
    _need(all(type(p[k]) is str and HEX64.fullmatch(p[k]) is not None for k in
              ("old_launcher_sha256", "candidate_operator_sha256")), "PLAN_LAUNCHER")
    return "sha256:" + hashlib.sha256(raw).hexdigest()

def check_admission(raw, plan_raw):
    plan_sha = check_plan(plan_raw)
    a = parse_exact(raw)
    _need(set(a) == ADMISSION_KEYS, "ADMISSION_FIELDS")
    _need(a["schema"] == "qa525-admission-launcher-only-v1"
          and a["state"] == "PENDING_EVIDENCE_NO_GO"
          and a["environment"] == "QA_GLMDCH_ONLY" and a["scope"] == SCOPE,
          "ADMISSION_SCOPE")
    _need(a["plan_sha256"] == plan_sha, "ADMISSION_PLAN_BINDING")
    _need(a["source_sha"] == parse_exact(plan_raw)["source_sha"],
          "ADMISSION_SOURCE_BINDING")
    _need(type(a["gates"]) is dict and set(a["gates"]) == set(GATES),
          "ADMISSION_GATES")
    for gate in GATES:
        e = a["gates"][gate]
        _need(type(e) is dict and set(e) == GATE_KEYS, "GATE_FIELDS")
        _need(type(e["status"]) is str and e["status"] in ("PENDING", "PASS", "STOP"),
              "GATE_STATUS")
        if e["status"] == "PASS":
            _need(_sha(e["evidence_sha256"]) and type(e["reference"]) is str
                  and 1 <= len(e["reference"]) <= 512
                  and type(e["checked_at_utc"]) is str
                  and UTC.fullmatch(e["checked_at_utc"]) is not None,
                  "GATE_EVIDENCE_REQUIRED")
            try:
                dt.datetime.strptime(e["checked_at_utc"], "%Y-%m-%dT%H:%M:%SZ")
            except ValueError as exc:
                raise Reject("GATE_TIMESTAMP") from exc
        else:
            _need(e["evidence_sha256"] is None and e["checked_at_utc"] is None
                  and e["reference"] is None, "GATE_UNVERIFIED_WITH_EVIDENCE")
    return {"plan_sha256": plan_sha,
            "admission_sha256": "sha256:" + hashlib.sha256(raw).hexdigest(),
            "pending_gates": [gate for gate in GATES if a["gates"][gate]["status"] != "PASS"],
            "ready_for_review": False, "may_apply": False, "go_signed": False}

if __name__ == "__main__":
    raise SystemExit("QA525_LIBRARY_ONLY_NO_GO")
