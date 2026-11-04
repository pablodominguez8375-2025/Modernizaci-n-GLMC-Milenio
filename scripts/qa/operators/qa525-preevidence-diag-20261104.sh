#!/usr/bin/env bash
# QA525 · DIAGNÓSTICO PRE-EVIDENCIA, SOLO LECTURA. Reproduce paso a paso `make_review_packet` del lanzador instalado (cfe4)
# con SU MISMO código, para decir EXACTAMENTE qué paso colapsa a UPDATE_NOT_COMPLETED. No invoca el actualizador,
# no escribe en /etc/pmgm ni en qa525-signed-prepare, no usa Docker mutante, no modifica nada. Solo lecturas locales y (opcional)
# lecturas anónimas a api.github.com. Autor: Claude. Revisor independiente requerido: ChatGPT. Ejecutar como archivo.
# Uso: bash archivo            -> pasos locales + pasos de red (si la cuota anónima es suficiente)
#      bash archivo LOCAL_ONLY -> solo pasos locales (cero cuota)
set -Eeuo pipefail
umask 077
SRC=6eed4f00b7917da3cca36bbda70e16d16328648d
DIGEST=sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2
REVIEW='https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6604960072'
G6=/etc/pmgm/qa525-g6-reviewed-20261104T105358Z.json
STAGE=/opt/centenario/qa525-signed-stage/cfe4e6573ece2ae81414cc252047d36cc56e5c73/scripts/qa/operators
MIN_NET=45
stop() { printf 'QA525_PREEVIDENCE=STOP_%s\n' "$1"; exit 2; }
[[ "$#" -le 1 ]] || stop BAD_ARGUMENT
MODE=${1:-FULL}
[[ "$MODE" == FULL || "$MODE" == LOCAL_ONLY ]] || stop BAD_ARGUMENT
[[ "$(hostname -s)" == glmdch && "$(id -un)" == soporte ]] || stop HOST_OR_USER
command -v python3 >/dev/null || stop TOOLS
sudo -v || stop SUDO
sudo test -f "$STAGE/qa525-update.py" && sudo test ! -L "$STAGE/qa525-update.py" || stop STAGE_MISSING
sudo /usr/bin/python3 -B -I - "$STAGE" "$SRC" "$DIGEST" "$REVIEW" "$G6" "$MODE" "$MIN_NET" <<'PY'
import importlib.util, json, os, re, sys, time, traceback, urllib.request
from pathlib import Path

STAGE, SRC, DIGEST, REVIEW, G6, MODE, MIN_NET = Path(sys.argv[1]), *sys.argv[2:6], sys.argv[6], int(sys.argv[7])
first_fail = []


def short(e):
    m = str(e)
    if re.fullmatch(r"[A-Z][A-Z0-9_]{2,100}", m):
        return m
    return type(e).__name__ + ": " + re.sub(r"[A-Za-z0-9+/=_-]{32,}", "[LONG]", m)[:100]


def where(e):
    tb = traceback.extract_tb(e.__traceback__)
    return "%s:%d in %s" % (Path(tb[-1].filename).name, tb[-1].lineno, tb[-1].name) if tb else "?"


def step(name, fn):
    try:
        r = fn()
        print("QA525_PREEVIDENCE %s=PASS" % name)
        return r
    except BaseException as e:  # noqa: BLE001 - diagnóstico: captura todo
        print("QA525_PREEVIDENCE %s=FAIL class=%s.%s detail=%s at=%s" % (name, type(e).__module__, type(e).__name__, short(e), where(e)))
        first_fail.append(name)
        return None


def rate():
    try:
        with urllib.request.urlopen("https://api.github.com/rate_limit", timeout=15) as r:
            return json.load(r)["resources"]["core"]["remaining"]
    except Exception:  # noqa: BLE001
        return None


def req(cond, code):
    if not cond:
        raise RuntimeError(code)


spec = importlib.util.spec_from_file_location("qa525_update_diag", STAGE / "qa525-update.py")
upd = importlib.util.module_from_spec(spec)
spec.loader.exec_module(upd)
entry = step("L00_load_entry_and_trust", lambda: upd.load_entry())
if entry is None:
    print("QA525_PREEVIDENCE_RESULT=FIRST_FAILURE L00_load_entry_and_trust")
    raise SystemExit(0)
step("L01_state_dir_private", lambda: (entry.trusted(entry.STATE, directory=True), req((entry.STATE.stat().st_mode & 0o777) == 0o700, "STATE_NOT_0700")))
modules = step("L02_load_modules", lambda: [entry.load("qa525_diag_module_%d" % i, p) for i, p in enumerate(entry.MODULES)])
gate = step("L03_load_support00_gate", lambda: entry.load("qa525_diag_gate", entry.HERE / "qa525-support00-schema-gate.py"))
g6 = step("L10_g6_acta_private_json", lambda: entry.private_json(Path(G6)))
if g6 is not None:
    step("L11_g6_fields_exact", lambda: req(all(k in g6 for k in ("g6_archive_name", "g6_archive_sha256", "g6_drive_file_id", "g6_offhost_sha256_verified", "g6_restore_report_name")), "G6_FIELD_MISSING"))
    step("L12_validate_g6_archive_receipt_report_age", lambda: entry.validate_g6(g6, int(time.time())))
    step("L13_g6_offhost_true", lambda: req(g6.get("g6_offhost_sha256_verified") is True, "G6_OFFHOST_NOT_TRUE"))
if modules:
    adapter = modules[5]
    step("L20_single_operator_runtime_flags", lambda: entry.single_operator_runtime(adapter.docker))
if gate is not None:
    def completion():
        c = entry.private_json(entry.STATE / "support00-completed.json")
        req(c.get("status") == "SUPPORT00_EF100_VERIFIED" and c.get("migration_id") == gate.MIGRATION_ID
            and c.get("migration_blob_sha") == gate.MIGRATION_BLOB, "SUPPORT00_COMPLETION_MISMATCH")
        req(type(c.get("completed_at")) is int, "SUPPORT00_COMPLETED_AT_INVALID")
    step("L30_support00_completed_record", completion)
step("L40_archive_marker_absent", lambda: upd.require_reconciled_archive(entry))
if MODE == "FULL" and modules and gate is not None:
    before = rate()
    print("QA525_PREEVIDENCE_QUOTA_BEFORE remaining=%s" % before)
    if before is None or before < MIN_NET:
        print("QA525_PREEVIDENCE N_STEPS=SKIPPED_QUOTA_LOW_OR_UNKNOWN (reintente tras el reinicio de la ventana)")
    else:
        core, build, approval, policy, signed, adapter, _g, probe = modules
        fetch = signed.gate.readiness.fetch_public
        source = step("N10_release_gate_evaluate", lambda: signed.gate.evaluate(fetch, expected_sha=SRC))
        if source is not None:
            step("N11_release_digest_matches", lambda: req(source.get("release_asset_sha256") == DIGEST, "DIGEST_MISMATCH"))
            step("N12_technical_review_reference", lambda: upd.verify_technical_reference(fetch, REVIEW, source, entry.REVIEWED))
            def change():
                decision, state = entry.current_change_review(policy, signed, source, gate)
                req(state == getattr(gate, "SCHEMA_REVIEW_STATE", "PR801_REVIEWED_ADDITIVE_NEEDS_DBA"), "UNKNOWN_SCHEMA_STATE:" + str(state)[:40])
                return decision
            step("N13_current_change_review", change)
        print("QA525_PREEVIDENCE_QUOTA_AFTER remaining=%s" % rate())
print("QA525_PREEVIDENCE_RESULT=%s" % ("ALL_PASS_NO_PRE_EVIDENCE_BLOCK_FOUND" if not first_fail else "FIRST_FAILURE " + first_fail[0]))
PY
printf 'QA525_PREEVIDENCE=READ_ONLY_DONE_NO_CHANGES\n'
