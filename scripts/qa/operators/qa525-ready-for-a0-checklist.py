#!/usr/bin/env python3
"""QA525 READY_FOR_A0 checklist: closed, auditable, never executes anything.

evaluate(inventory_text, inputs) -> (global, rows). Each row: (id, name, PASS|STOP, evidence_ref).
An unknown, missing or malformed input is STOP, never PASS. Global is READY_FOR_A0 only if EVERY row is PASS;
if the inventory or the params file is missing the global is STOP.
`inputs` are caller-supplied, already authenticated facts (strings/bools), no free text is rendered.
"""
import re
import sys

HEX40 = re.compile(r"[a-f0-9]{40}\Z")
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
ITEMS = (
    (1, "CI_EXACT_HEAD"), (2, "ADAPTER_PARAMS_A0_G6_V1"), (3, "INVENTORY_A0_G6"), (4, "MIGRATOR_PROVENANCE_IMAGE_A"),
    (5, "G6_COMPLETE"), (6, "ARCHIVE_LOCAL_OFFHOST"), (7, "REPO_ID_KEY_EPOCH_WINDOW"), (8, "FINAL_BACKUP_UNDER_QUIESCE"),
    (9, "BASE_ADELANTADA_RUNBOOK"), (10, "UAT_POSTFLIGHT"),
)


class Anomaly(Exception):
    pass


LINE = re.compile(r"(A0G6_[A-Za-z0-9_.-]{1,160})=([\x21-\x7e ]{0,400})\Z")
EVIDENCE = re.compile(r"sha256=[a-f0-9]{64} size=\d{1,9}( restic_repo_id=[a-f0-9]{64})?( key_epoch=[a-f0-9]{32})?\Z")
EV_ID = re.compile(r" restic_repo_id=([a-f0-9]{64})")
EV_EP = re.compile(r" key_epoch=([a-f0-9]{32})")


def parse(text):
    """Strict: every non-empty line must be a closed A0G6_KEY=VALUE; duplicates, STOP lines, contamination and
    malformed evidence raise Anomaly (=> global STOP)."""
    if type(text) is not str or len(text) > 1 << 20:
        raise Anomaly("TEXT")
    kv = {}
    for line in text.split("\n"):
        if not line:
            continue
        m = LINE.fullmatch(line)
        if m is None:
            raise Anomaly("LINE")
        k, v = m.groups()
        if k in kv:
            raise Anomaly("DUPLICATE")
        if k.startswith("A0G6_EVIDENCE_") and EVIDENCE.fullmatch(v) is None:
            raise Anomaly("EVIDENCE")
        kv[k] = v
    return kv


def _ok(d, key, rx):
    v = d.get(key)
    return type(v) is str and rx.fullmatch(v) is not None


def _evaluate(inventory_text, inputs):
    inv = parse(inventory_text)
    inputs = inputs if type(inputs) is dict else {}
    complete = inv.get("A0G6_INVENTORY_COMPLETE") == "1"
    ev = {k: v for k, v in inv.items() if k.startswith("A0G6_EVIDENCE_")}
    params = [k for k in ev if k.startswith("A0G6_EVIDENCE_qa525-a0-g6-params")]
    receipts = [k for k in ev if k.startswith("A0G6_EVIDENCE_receipt-")]
    reviews = [k for k in ev if k.startswith("A0G6_EVIDENCE_review-")]
    restores = [k for k in ev if k.startswith("A0G6_EVIDENCE_isolated-restore-v2-")]
    archives = [k for k in inv if k.startswith("A0G6_ARCHIVE_")]
    ids = {m.group(1) for v in ev.values() for m in EV_ID.finditer(v)}
    epochs = {m.group(1) for v in ev.values() for m in EV_EP.finditer(v)}
    rows = []

    def row(i, passed, ref):
        rows.append((i, dict(ITEMS)[i], "PASS" if passed else "STOP", ref if passed else "NO_EVIDENCE"))

    row(1, _ok(inputs, "ci_head_sha", HEX40) and inputs.get("ci_head_sha") == inputs.get("expected_head_sha")
        and inputs.get("ci_all_green") is True, "ci:" + str(inputs.get("ci_head_sha"))[:40])
    row(2, complete and len(params) == 1, "inventory:" + (params[0] if params else "-"))
    row(3, complete, "inventory:A0G6_INVENTORY_COMPLETE")
    row(4, _ok(inputs, "migrator_digest", HEX64) and _ok(inputs, "image_a_config_digest", HEX64)
        and inputs.get("migrator_provenance_verified") is True, "a0:migrator_provenance")
    row(5, complete and bool(receipts) and bool(reviews) and bool(restores), "inventory:receipt+restore+review")
    row(6, complete and bool(archives) and _ok(inputs, "offhost_sha256", HEX64)
        and inputs.get("offhost_matches_local") is True, "a0:offhost_vs_local")
    row(7, complete and len(ids) == 1 and len(epochs) == 1 and inputs.get("g6_window_valid") is True,
        "inventory:repo_id+key_epoch")
    row(8, inputs.get("final_backup_under_quiesce") is True, "g6:final_backup")
    row(9, inputs.get("base_adelantada_runbook_reviewed") is True, "review:BASE_ADELANTADA")
    row(10, inputs.get("uat_postflight_defined") is True, "review:UAT_POSTFLIGHT")
    glob = "READY_FOR_A0" if all(r[2] == "PASS" for r in rows) and complete and params else "STOP"
    return glob, rows


def evaluate(inventory_text, inputs):
    """Never raises: any anomaly or unexpected exception => every row STOP and global STOP."""
    try:
        return _evaluate(inventory_text, inputs)
    except Exception:  # noqa: BLE001 - closed STOP is the contract
        return "STOP", [(i, n, "STOP", "NO_EVIDENCE") for i, n in ITEMS]


def render(glob, rows):
    lines = ["READY_FOR_A0_%02d_%s=%s ref=%s" % (r[0], r[1], r[2], r[3])
             for r in rows]
    return "\n".join(lines + ["READY_FOR_A0_GLOBAL=" + glob]) + "\n"


if __name__ == "__main__":
    text = sys.stdin.read()
    g, r = evaluate(text, {})
    sys.stdout.write(render(g, r))
    sys.exit(0 if g == "READY_FOR_A0" else 1)
