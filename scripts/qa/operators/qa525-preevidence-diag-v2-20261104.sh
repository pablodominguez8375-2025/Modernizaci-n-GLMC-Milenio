#!/usr/bin/env bash
# QA525 · DIAGNÓSTICO PRE-EVIDENCIA v2, SOLO LECTURA (v1 + réplica de install_review_packet y del preámbulo de --prepare-reviewed en sandbox privado).
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
MIN_NET=40
stop() { printf 'QA525_PREEVIDENCE=STOP_%s\n' "$1"; exit 2; }
[[ "$#" -le 1 ]] || stop BAD_ARGUMENT
MODE=${1:-FULL}
[[ "$MODE" == FULL || "$MODE" == LOCAL_ONLY ]] || stop BAD_ARGUMENT
[[ "$(hostname -s)" == glmdch && "$(id -un)" == soporte ]] || stop HOST_OR_USER
command -v python3 >/dev/null || stop TOOLS
sudo -v || stop SUDO
sudo test -f "$STAGE/qa525-update.py" && sudo test ! -L "$STAGE/qa525-update.py" || stop STAGE_MISSING
sudo /usr/bin/python3 -B -I - "$STAGE" "$SRC" "$DIGEST" "$REVIEW" "$G6" "$MODE" "$MIN_NET" <<'PY'
import importlib.util, json, os, re, shutil, sys, tempfile, time, traceback, urllib.request
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


def install_replay(entry, upd, modules, gate, source, decision, schema, g6, calls, fetch):
    core, build, approval, policy, signed, adapter, _g, _p = modules
    probe = step("I00_load_support00_probe", lambda: (entry.trusted(entry.HERE / "qa525-support00-post-db-readonly.py"),
                 entry.load("qa525_diag_probe", entry.HERE / "qa525-support00-post-db-readonly.py"))[1])
    if probe is None:
        return
    now = int(time.time())
    scope = getattr(gate, "SINGLE_OPERATOR_SCOPE", "POST_PR801_API_WEB_ONLY_NO_MIGRATION")
    comp = entry.private_json(entry.STATE / "support00-completed.json")
    review = dict(source_sha=source["source_sha"], release_asset_sha256=DIGEST, baseline_sha=entry.BASELINE,
                  reviewed_sections=sorted(entry.REVIEWED), sensitive_reasons=decision["reasons"], no_new_ef_migrations=False,
                  runtime_migration_flags_disabled=True, legacy_master_and_smtp_preserved=True, reviewed_at=now,
                  **{k: g6[k] for k in ("g6_archive_name", "g6_archive_sha256", "g6_drive_file_id", "g6_offhost_sha256_verified", "g6_restore_report_name")})
    post = dict(source_sha=source["source_sha"], release_asset_sha256=DIGEST, baseline_sha=entry.BASELINE, migration_id=gate.MIGRATION_ID,
                migration_blob_sha=gate.MIGRATION_BLOB, dbcontext_blob_sha=gate.CONTEXT_BLOB, migration_completed_at=comp["completed_at"],
                approved_at=now, additive_up_only=True, previous_api_forward_compatible=True, post_db_g6_archive_name=review["g6_archive_name"],
                post_db_g6_archive_sha256=review["g6_archive_sha256"], post_db_g6_restore_report_name=review["g6_restore_report_name"],
                review_mode="single_operator_qa", environment="QA_SYNTHETIC", host="glmdch", scope=scope,
                sponsor_approval_reference=entry.SINGLE_OPERATOR_DECISION, sponsor_accepts_single_operator_risk=True,
                independent_human_review=False, technical_review_kind="automated_ai", operator_name="soporte@glmdch", technical_review_reference=REVIEW)
    packet = {"schema_mode": "support00_postdb", "review": review, "post_db": post}
    step("I01_packet_contract", lambda: upd.packet_contract(packet, int(time.time())))
    step("I02_approved_review", lambda: entry.approved_review(review, source, decision, int(time.time()), schema_approved=True))
    step("I03_validate_single_operator_review", lambda: entry.validate_single_operator_review(post, scope=scope))
    step("I04_validate_g6_review", lambda: entry.validate_g6(review, int(time.time())))
    step("I05_single_operator_runtime", lambda: entry.single_operator_runtime(adapter.docker))
    live = step("I06_adapter_metadata", lambda: adapter.metadata())
    if live is None:
        return
    step("I07_target_journal_absent", lambda: req(not (entry.STATE / ("dual-" + source["source_sha"] + ".json")).exists(), "UPDATE_TARGET_ALREADY_ATTEMPTED"))
    step("I08_reviewed_layers", lambda: entry.reviewed_layers(core, {n: live[n][7].split(",") for n in core.TARGETS}, entry.WORKDIR))

    def journals():
        names = []
        for p in sorted(entry.STATE.glob("dual-*.json")):
            j = entry.private_json(p)
            names.append("%s=%s" % (p.name[:12], j.get("status")))
            req(j.get("status") == "APPLIED", "UPDATE_JOURNAL_REQUIRES_RECONCILIATION")
        print("QA525_PREEVIDENCE_INFO journals=%s" % (",".join(names) or "none"))
    step("I09_historical_journals_applied", journals)

    def prepared_state():
        r = entry.STATE / "prepared.json"
        present = r.exists() or r.is_symlink()
        print("QA525_PREEVIDENCE_INFO prepared_json_present=%s" % present)
        if present:
            prev = entry.private_json(r)
            j = entry.private_json(entry.STATE / ("dual-" + prev["source_sha"] + ".json"))
            upd.previous_prepared_is_applied(prev, j, live)
            req(prev["source_sha"] != source["source_sha"], "UPDATE_ALREADY_APPLIED")
            req(entry.health(prev["source_sha"]), "PREVIOUS_UPDATE_HEALTH_UNVERIFIED")
    step("I10_prepared_json_handling", prepared_state)
    step("I11_state_dir_writable_for_archive", lambda: req(os.access(entry.STATE, os.W_OK | os.X_OK), "STATE_NOT_WRITABLE"))
    # Sandbox: the three records are written to a private temp dir (root 0700) and the module paths are redirected.
    tmp = None
    saved = (entry.REVIEW, entry.SINGLE_OPERATOR_REVIEW, approval.APPROVAL)
    try:
        tmp = Path(tempfile.mkdtemp(prefix=".qa525-diag-", dir="/root"))
        entry.REVIEW, entry.SINGLE_OPERATOR_REVIEW, approval.APPROVAL = tmp / "r.json", tmp / "s.json", tmp / "c.json"
        cut = dict(target_sha=source["source_sha"], release_asset_sha256=source["release_asset_sha256"], scope=approval.SCOPE,
                   environment=approval.ENVIRONMENT, action=approval.ACTION, nonce=os.urandom(32).hex(), issued_at=int(time.time()),
                   expires_at=min(review["reviewed_at"], post["approved_at"]) + 1800, operator_reviewed=True)

        def write3():
            for p, obj in ((entry.REVIEW, review), (entry.SINGLE_OPERATOR_REVIEW, post), (approval.APPROVAL, cut)):
                fd = os.open(p, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
                with os.fdopen(fd, "w") as s:
                    json.dump(obj, s, sort_keys=True)
        step("I12_sandbox_write_three_records", write3)
        step("I13_validate_post_db_dba", lambda: entry.validate_post_db_dba(review, source, int(time.time()), adapter.docker, gate, probe, single_operator=True))
        step("I14_read_root_approval", lambda: approval.read_root_approval())
        step("I15_runtime_unchanged", lambda: req(adapter.metadata() == live, "UPDATE_RUNTIME_CHANGED"))
        # entry.main preamble replica (same checks that --prepare-reviewed repeats before building images)
        c0 = calls[0]

        def preamble():
            r = entry.private_json(entry.REVIEW, maximum=8192)
            s2 = signed.gate.evaluate(fetch, expected_sha=r.get("source_sha"))
            req(s2["source_sha"] == source["source_sha"], "SIGNATURE_RELEASE_TARGET_DRIFT")
            d2, st2 = entry.current_change_review(policy, signed, s2, gate)
            entry.validate_post_db_dba(r, s2, int(time.time()), adapter.docker, gate, probe, single_operator=True)
            entry.approved_review(r, s2, d2, int(time.time()), schema_approved=True)
            entry.validate_g6(r, int(time.time()))
            return s2
        s2 = step("P00_main_preamble_replica", preamble)
        print("QA525_PREEVIDENCE_GITHUB_CALLS main_preamble=%d" % (calls[0] - c0))
        ap = step("P01_approval_matches_prepare_contract", lambda: (
            req(approval.read_root_approval().get("target_sha") == source["source_sha"], "PREPARE_SUPERVISION_NOT_MATCHED"),
            req(0 <= int(time.time()) - cut["issued_at"] <= 1800 and int(time.time()) < cut["expires_at"], "PREPARE_SUPERVISION_EXPIRED")))
        step("P02_prepare_receipt_absent", lambda: req(not (entry.STATE / "prepared.json").exists(), "PREPARE_ALREADY_EXISTS"))

        def baseline():
            snap = build.snapshot(adapter.docker)
            req(all(x["running"] is True for x in snap.values()), "PREPARE_BASELINE_NOT_RUNNING")
        step("P03_baseline_running", baseline)
        print("QA525_PREEVIDENCE_INFO estimate_github_calls_full_run>=%d (make_review + preamble x2 + prepare + apply; limite anonimo 60/h)" % (calls[0] * 2))
    finally:
        entry.REVIEW, entry.SINGLE_OPERATOR_REVIEW, approval.APPROVAL = saved
        if tmp is not None:
            shutil.rmtree(tmp, ignore_errors=True)
        print("QA525_PREEVIDENCE_INFO sandbox_removed=%s" % (tmp is None or not tmp.exists()))


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
        readiness = signed.gate.readiness
        orig_fetch = readiness.fetch_public
        calls = [0]

        def fetch(*a, **k):
            calls[0] += 1
            return orig_fetch(*a, **k)

        readiness.fetch_public = fetch
        try:
            source = step("N10_release_gate_evaluate", lambda: signed.gate.evaluate(fetch, expected_sha=SRC))
            c_eval = calls[0]
            decision = schema = None
            if source is not None:
                step("N11_release_digest_matches", lambda: req(source.get("release_asset_sha256") == DIGEST, "DIGEST_MISMATCH"))
                step("N12_technical_review_reference", lambda: upd.verify_technical_reference(fetch, REVIEW, source, entry.REVIEWED))

                def change():
                    global decision, schema
                    decision, schema = entry.current_change_review(policy, signed, source, gate)
                    req(schema == getattr(gate, "SCHEMA_REVIEW_STATE", "PR801_REVIEWED_ADDITIVE_NEEDS_DBA"), "UNKNOWN_SCHEMA_STATE:" + str(schema)[:40])
                step("N13_current_change_review", change)
            print("QA525_PREEVIDENCE_GITHUB_CALLS make_review_packet=%d" % calls[0])
            if source is not None and decision is not None and g6 is not None:
                install_replay(entry, upd, modules, gate, source, decision, schema, g6, calls, fetch)
        finally:
            readiness.fetch_public = orig_fetch
        print("QA525_PREEVIDENCE_QUOTA_AFTER remaining=%s" % rate())
print("QA525_PREEVIDENCE_RESULT=%s" % ("ALL_PASS_NO_PRE_EVIDENCE_BLOCK_FOUND" if not first_fail else "FIRST_FAILURE " + first_fail[0]))
PY
printf 'QA525_PREEVIDENCE=READ_ONLY_DONE_NO_CHANGES\n'
