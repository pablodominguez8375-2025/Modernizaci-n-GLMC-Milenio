#!/usr/bin/env bash
# QA525 · CONCILIACIÓN DE CAPAS COMPOSE 6eed4 (residuo del rollback A2). Autor: Claude. Revisor independiente requerido: ChatGPT.
# Causa (diagnóstico v2, I08): los contenedores api/web activos llevan en `com.docker.compose.project.config_files` la capa image-only
# qa525-<svc>-6eed4…-<imagen>.json creada por el ROLLBACK de A2, pero su journal dual-6eed4….json fue archivado en la recuperación, por lo que
# `reviewed_layers` del lanzador instalado (cfe4) no la acepta (ROOT_SOURCE_MISSING). NO se crea ni falsifica ningún journal.
# Qué hace: sin argumento = SOLO LECTURA (A0): inventario y simulación de `reviewed_layers` sobre la lista sin la capa 6eed4.
#   EXECUTE_RECONCILE: bajo el lock exclusivo QA525, recrea api y luego web con la MISMA imagen y la lista de capas SIN la capa 6eed4
#   (mismas banderas que la transacción dual: up --detach --no-deps --no-build --pull never --force-recreate --wait). Verifica imagen idéntica,
#   dependencias intactas, health/OIDC/qa-version del SHA vigente y `reviewed_layers` sobre las etiquetas nuevas. Si algo falla: rollback a la lista
#   original (web y luego api). Los JSON de capa 6eed4 quedan intactos como evidencia. No inicia ningún update. Ejecutar como archivo.
set -Eeuo pipefail
umask 077
STAGE=/opt/centenario/qa525-signed-stage/cfe4e6573ece2ae81414cc252047d36cc56e5c73/scripts/qa/operators
SRC=6eed4f00b7917da3cca36bbda70e16d16328648d
stop() { printf 'QA525_RECONCILE=STOP_%s\n' "$1"; exit 2; }
[[ "$#" -le 1 ]] || stop BAD_ARGUMENT
MODE=${1:-READ_ONLY}
[[ "$MODE" == READ_ONLY || "$MODE" == EXECUTE_RECONCILE ]] || stop BAD_ARGUMENT
[[ "$(hostname -s)" == glmdch && "$(id -un)" == soporte ]] || stop HOST_OR_USER
command -v python3 >/dev/null || stop TOOLS
sudo -v || stop SUDO
sudo test -f "$STAGE/qa525-update.py" && sudo test ! -L "$STAGE/qa525-update.py" || stop STAGE_MISSING
sudo /usr/bin/python3 -B -I - "$STAGE" "$SRC" "$MODE" <<'PY'
import importlib.util, json, os, re, sys, time, traceback
from pathlib import Path

STAGE, SRC, MODE = Path(sys.argv[1]), sys.argv[2], sys.argv[3]
LAYER = re.compile(r"qa525-(api|web)-([a-f0-9]{40})-([a-f0-9]{64})\.json")
SIZES = {"api": 3, "web": 2}


class Stop(Exception):
    pass


def req(cond, code):
    if not cond:
        raise Stop(code)


def plan(name, files, live_image, read_json, state):
    """Pure decision: drop exactly the one 6eed4 layer, which must be last, image-only and equal to the running image."""
    req(type(files) is list and len(files) == len(set(files)) and SIZES[name] <= len(files) <= 128, "LAYERS_INVALID_" + name)
    target = [f for f in files if (m := LAYER.fullmatch(Path(f).name)) and m.group(2) == SRC]
    req(len(target) == 1 and files[-1] == target[0], "TARGET_LAYER_NOT_UNIQUE_LAST_" + name)
    m = LAYER.fullmatch(Path(target[0]).name)
    req(m.group(1) == name and Path(target[0]).parent == state, "TARGET_LAYER_BINDING_" + name)
    image = "sha256:" + m.group(3)
    req(read_json(target[0]) == {"services": {name: {"image": image}}}, "TARGET_LAYER_NOT_IMAGE_ONLY_" + name)
    req(image == live_image, "TARGET_LAYER_IMAGE_NOT_RUNNING_" + name)
    cleaned = files[:-1]
    req(len(cleaned) >= SIZES[name], "CLEANED_TOO_SHORT_" + name)
    return cleaned


def args_for(core, workdir, files, service):
    a = ["compose", "--project-name", core.PROJECT, "--project-directory", workdir, "--env-file", core.ENV_FILE]
    for f in files:
        a += ["--file", f]
    return a + ["up", "--detach", "--no-deps", "--no-build", "--pull", "never", "--force-recreate", "--wait", "--wait-timeout", "90", service]


def main():
    spec = importlib.util.spec_from_file_location("qa525_update_reconcile", STAGE / "qa525-update.py")
    upd = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(upd)
    entry = upd.load_entry()
    entry.trusted(entry.STATE, directory=True)
    modules = [entry.load("qa525_rec_module_%d" % i, p) for i, p in enumerate(entry.MODULES)]
    core, build, _a, _p, _s, adapter = modules[:6]
    workdir = str(entry.WORKDIR)

    def inventory():
        live = adapter.metadata()
        files = {n: live[n][7].split(",") for n in core.TARGETS}
        return live, files

    def checks():
        upd.require_reconciled_archive(entry)
        entry.single_operator_runtime(adapter.docker)
        live, files = inventory()
        req(all(live[n][2] == "running" for n in core.TARGETS), "SERVICES_NOT_RUNNING")
        for p in entry.STATE.glob("dual-*.json*"):
            j = entry.private_json(p) if p.suffix == ".json" else {"status": "NEXT"}
            req(j.get("status") == "APPLIED", "JOURNAL_NOT_APPLIED")
        req(not (entry.STATE / ("dual-" + SRC + ".json")).exists(), "TARGET_JOURNAL_PRESENT")
        cleaned = {n: plan(n, files[n], live[n][1], entry.private_json, entry.STATE) for n in core.TARGETS}
        # simulacro del validador instalado sobre la lista limpia:
        entry.reviewed_layers(core, cleaned, entry.WORKDIR)
        print("QA525_RECONCILE A0_API_LAYERS_NOW=%d CLEANED=%d" % (len(files["api"]), len(cleaned["api"])))
        print("QA525_RECONCILE A0_WEB_LAYERS_NOW=%d CLEANED=%d" % (len(files["web"]), len(cleaned["web"])))
        return live, files, cleaned

    with build.exclusive():
        live, files, cleaned = checks()
        print("QA525_RECONCILE A0=PASS (reviewed_layers aceptaria la lista sin capa 6eed4; imagen de capa == imagen activa)")
        if MODE == "READ_ONLY":
            print("QA525_RECONCILE=READ_ONLY_DONE_NO_CHANGES")
            return
        before = build.snapshot(adapter.docker)
        web_sha = adapter.docker(["image", "inspect", "--format", '{{index .Config.Labels "org.opencontainers.image.revision"}}', live["web"][1]])
        req(re.fullmatch(r"[a-f0-9]{40}", web_sha or ""), "WEB_PROVENANCE")
        req(entry.health(web_sha), "HEALTH_BEFORE_FAILED")

        def run(name, fl):
            adapter.docker(args_for(core, workdir, fl, name), cwd=workdir, timeout=240)

        def audit(ev):
            fd = os.open(entry.STATE / "dual-events.log", os.O_WRONLY | os.O_APPEND | os.O_CREAT | os.O_NOFOLLOW, 0o600)
            with os.fdopen(fd, "w") as f:
                f.write("%d %s %s\n" % (int(time.time()), ev, SRC))
        audit("LAYERS_RECONCILE_6EED4_STARTED")
        try:
            run("api", cleaned["api"])
            run("web", cleaned["web"])
            now = build.snapshot(adapter.docker)
            for n in core.TARGETS:
                req(now[n]["image_id"] == before[n]["image_id"] and now[n]["running"] is True, "IMAGE_CHANGED_" + n)
            for n in core.DEPENDENCIES:
                req(now[n] == before[n], "DEPENDENCY_CHANGED_" + n)
            req(entry.health(web_sha), "HEALTH_AFTER_FAILED")
            _l, nf = inventory()
            req(nf == cleaned, "LABELS_NOT_AS_PLANNED")
            entry.reviewed_layers(core, nf, entry.WORKDIR)
            audit("LAYERS_RECONCILE_6EED4_DONE")
            print("QA525_RECONCILE=RECONCILED_LABELS_CLEAN_IMAGES_UNCHANGED_HEALTH_PASS")
        except BaseException as e:  # noqa: BLE001
            print("QA525_RECONCILE FAIL %s" % (str(e) if isinstance(e, Stop) else type(e).__name__))
            try:
                run("web", files["web"])
                run("api", files["api"])
                req(entry.health(web_sha), "ROLLBACK_HEALTH_FAILED")
                audit("LAYERS_RECONCILE_6EED4_ROLLED_BACK")
                print("QA525_RECONCILE=ROLLED_BACK_TO_ORIGINAL_LAYERS_HEALTH_PASS")
            except BaseException as e2:  # noqa: BLE001
                audit("LAYERS_RECONCILE_6EED4_RECOVERY_REQUIRED")
                print("QA525_RECONCILE=ROLLBACK_FAILED_MANUAL_RECOVERY_REQUIRED %s" % type(e2).__name__)
            raise SystemExit(3)


try:
    main()
except Stop as e:
    print("QA525_RECONCILE=STOP_%s" % e)
    raise SystemExit(2)
except SystemExit:
    raise
except BaseException as e:  # noqa: BLE001
    tb = traceback.extract_tb(e.__traceback__)
    print("QA525_RECONCILE=STOP_UNEXPECTED %s.%s at=%s:%d" % (type(e).__module__, type(e).__name__, Path(tb[-1].filename).name, tb[-1].lineno) if tb else "?")
    raise SystemExit(2)
PY
printf 'QA525_RECONCILE_WRAPPER_DONE mode=%s\n' "$MODE"
