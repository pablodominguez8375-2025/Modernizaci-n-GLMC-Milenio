#!/usr/bin/env python3
"""QA525 successor update: bounded baseline injection for the historical operator (DRAFT).

Activated ONLY by ``qa525-update.py --successor``. Without the flag nothing here runs and the
historical 6ff4 flow is byte-for-byte unchanged. It reuses the historical entrypoint
(core.transact, reviewed_layers, retain_rollback_images, validate_g6, health, post-DB gate,
Sigstore re-verification, locks and dual rollback) and replaces exactly four decisions:

1. ``entry.BASELINE``: the last APPLIED, LIVE and HEALTHY cut (never a constant);
2. ``entry.current_change_review``: complete+ancestral diff (<300 files), policy classification
   against that baseline, NO sensitive path in the diff, and the three pinned schema blobs of
   the target equal the pinned ones (schema identical to the predecessor);
3. ``entry.approved_review``: also accepts ELIGIBLE with ``sensitive_reasons == []``;
4. ``entry.validate_live_ef_history``: live EF history equals a root-pinned expected set.

No new authority: a successor never touches schema, identity, bootstrap or infrastructure.
Any such change keeps the historical STOP. Errors use fixed public codes and never echo input.
"""
import importlib.util
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SHA = re.compile(r"[0-9a-f]{40}\Z")
EF_PIN = "successor-ef-history.json"


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def load_baseline_module(entry):
    path = HERE / "qa525-successor-baseline.py"
    entry.trusted(path)
    spec = importlib.util.spec_from_file_location("qa525_successor_baseline_rt", path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def read_state(entry, adapter):
    """prepared.json, EVERY dual journal and the Docker view, read from the host (never declared)."""
    prepared = entry.private_json(entry.STATE / "prepared.json")
    journals = {}
    for path in sorted(entry.STATE.glob("dual-*.json")):
        sha = path.name[len("dual-"):-len(".json")]
        require(SHA.fullmatch(sha), "BASELINE_JOURNAL_INVALID")
        journals[sha] = entry.private_json(path)
    rows = adapter.metadata()
    live = {name: [rows[name][0], rows[name][1], rows[name][2]] for name in ("api", "web")}
    return prepared, journals, live


def pinned_blobs(gate):
    member = gate.member
    return {member.MIGRATION_PATH: member.MIGRATION_BLOB, gate.CONTEXT_PATH: gate.CONTEXT_BLOB,
            gate.MIGRATION_PATH: gate.MIGRATION_BLOB}


def make_current_change_review(succ, baseline):
    def current_change_review(policy, signed, source, schema_gate):
        fetch = signed.gate.readiness.fetch_public
        target = source["source_sha"]
        diff = fetch("/compare/" + baseline + "..." + target)
        files = succ.guard_diff(baseline, target, diff)
        decision = policy.classify(baseline, source, diff)
        require(decision.get("analytical_baseline_sha") == baseline
                and decision.get("source_sha") == target and decision.get("may_apply") is False,
                "CLASSIFICATION_EVIDENCE_INVALID")
        names = set()
        for row in files:
            require(isinstance(row, dict) and type(row.get("filename")) is str, "DIFF_INVALID")
            names.add(row["filename"].lower())
            if row.get("status") == "renamed":
                require(type(row.get("previous_filename")) is str, "DIFF_INVALID")
                names.add(row["previous_filename"].lower())
        # A successor never carries schema work: any migration/DbContext path stops here.
        require(not any("/migrations/" in n or "dbcontext" in n for n in names),
                "SUCCESSOR_SCHEMA_CHANGE_NOT_ALLOWED")
        blobs = {}
        for path in pinned_blobs(schema_gate):
            detail = fetch("/contents/" + path + "?ref=" + target)
            require(isinstance(detail, dict) and type(detail.get("sha")) is str,
                    "MIGRATION_SOURCE_BLOB_UNAVAILABLE")
            blobs[path] = detail["sha"]
        require(blobs == pinned_blobs(schema_gate), "SUCCESSOR_SCHEMA_BLOBS_CHANGED")
        return decision, schema_gate.SCHEMA_REVIEW_STATE
    return current_change_review


def make_approved_review(original):
    def approved_review(review, source, policy, now, *, schema_approved=False):
        if policy.get("status") == "ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY":
            require(policy.get("reasons") == [] and review.get("sensitive_reasons") == [],
                    "SENSITIVE_REVIEW_NOT_CURRENT_OR_INCOMPLETE")
            # Same checks as the historical function, with the status it expects.
            policy = dict(policy, status="BLOCKED_REVIEW_REQUIRED")
        return original(review, source, policy, now, schema_approved=schema_approved)
    return approved_review


def make_live_ef_check(entry, expected_loader=None):
    def validate_live_ef_history(docker):
        pin = (expected_loader or (lambda: entry.private_json(entry.STATE / EF_PIN)))()
        expected = pin.get("migrations") if isinstance(pin, dict) else None
        require(type(expected) is list and expected
                and all(type(x) is str and re.fullmatch(r"[A-Za-z0-9_]+", x) for x in expected)
                and len(set(expected)) == len(expected), "EF_BASELINE_MIGRATIONS_INVALID")
        query = ('BEGIN READ ONLY; SELECT "MigrationId" FROM '
                 'public."__EFMigrationsHistory" ORDER BY 1; COMMIT;')
        raw = docker(["exec", "centenario-qa-lab2-postgres-1", "sh", "-c",
                      'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"',
                      "qa525-readonly", query])
        actual = raw.splitlines()
        require(len(actual) == len(expected) and len(set(actual)) == len(actual)
                and set(actual) == set(expected), "EF_MIGRATION_HISTORY_DRIFT")
        return True
    return validate_live_ef_history


def install(entry, adapter, *, health=None, state=None, expected_loader=None):
    """Patch ``entry`` in place for ONE successor update. Returns the resolved baseline SHA."""
    succ = load_baseline_module(entry)
    prepared, journals, live = state if state is not None else read_state(entry, adapter)
    baseline = succ.resolve_baseline(prepared, journals, live, health or entry.health)
    entry.BASELINE = baseline
    entry.current_change_review = make_current_change_review(succ, baseline)
    entry.approved_review = make_approved_review(entry.approved_review)
    entry.validate_live_ef_history = make_live_ef_check(entry, expected_loader)
    return baseline
