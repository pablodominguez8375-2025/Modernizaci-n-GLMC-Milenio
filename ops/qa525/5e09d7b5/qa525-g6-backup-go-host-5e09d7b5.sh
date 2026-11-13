# QA525 GO-H-G6-BACKUP v2 — ARTEFACTO=GLMDCH_OPERATIVO_CORTE_5e09d7b5 — COMANDO=backup — en glmdch como soporte: `bash qa525-go-host-<sha8>.sh`. UNA sesión: recibe la línea firmada de Termux,
# la verifica completa, la instala SIN sobrescribir en el inbox root, emite el ZIP y ejecuta el bootstrap (pide el PIN).
# VENTANA MINIO: este guion arma el reinicio independiente (systemd), DETIENE MinIO cuando el respaldo lo pide y lo
# ARRANCA al terminar la copia del Silo (tope 600 s). Requiere la autorización escrita del Sponsor para ese día.
# Pide solo el PIN (la clave de Restic ya está en custodia root desde `init`). No toca el launcher ni API/WEB.
# v2: el éxito exige LECTURA FINAL real: MinIO running+healthy, reinicio independiente inactive, marcadores del respaldo y GO archivado. La validación con autoridad es la del launcher bajo PRE-root; esto es un filtro previo.
set -euo pipefail
SHA="5e09d7b5718309e31f639a1eb45890546e7b29cf"
FP_EXPECTED="SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k"
PY="/usr/bin/python3.14"
PY_SHA="52e0a13e60a981d8c4b6478be2ba5176f69da07948a056bf49cf6f077e30cb41"
CMD="backup"
export QA_CMD="backup"
MINIO="centenario-qa-lab2-minio-1"
WD="pmgm-g6-minio-restart"
DOCKER="/usr/bin/docker"
SYSTEMCTL="/usr/bin/systemctl"
SYSTEMD_RUN="/usr/bin/systemd-run"
NS="qa525-go-cut-v1@pmgm"
stop() { echo "QA525_GO_H_G6=STOP_$1"; echo "GO_FISICO=NO_GO"; exit 1; }
[ "$(hostname)" = "glmdch" ] && [ "$(id -un)" = "soporte" ] || stop HOST_USER
[ -t 0 ] || stop NEEDS_TERMINAL
[ "$(readlink -f "$PY")" = "$PY" ] && [ "$(sha256sum "$PY" | cut -d' ' -f1)" = "$PY_SHA" ] || stop INTERPRETER_PIN
umask 077
D="$HOME/qa525-corte-${SHA:0:8}"
T="/opt/centenario/qa525-root-source/$SHA"
[ -f "$D/app-inner.zip" ] && [ ! -L "$D/app-inner.zip" ] || stop NO_APP_ZIP
case "$CMD" in preflight|init|backup|restore|review) ;; *) stop COMMAND ;; esac
V="$D/kit-g6-$CMD/values.txt"
[ -f "$V" ] && [ ! -L "$V" ] && [ "$(stat -c '%u:%a' "$V")" = "$(id -u):600" ] || stop RUN_KIT_G6_A_FIRST
val() { sed -n "s/^$1=//p" "$V" | head -1; }
export QA_PLAN="$(val PLAN)" QA_ADMISSION="$(val ADMISSION)" QA_POLICY="$(val POLICY)" QA_OPERATION="$(val OPERATION)"
[ "$(val CMD)" = "$CMD" ] || stop VALUES_COMMAND
INBOX="$(val INBOX)"; SOCK="$(val SOCK)"
[ "$INBOX" = "/var/lib/pmgm-qa525/inbox-g6-$CMD" ] || stop VALUES_INBOX
case "$SOCK" in /run/pmgm-qa525/launcher.sock) ;; *) stop VALUES_SOCK ;; esac
PUBFILE="$HOME/.ssh/qa525-ops.pub"
[ -f "$PUBFILE" ] && [ ! -L "$PUBFILE" ] && [ "$(cut -d' ' -f1 "$PUBFILE")" = "ssh-ed25519" ] || stop SPONSOR_PUBKEY_FILE
PUBKEY="$(cut -d' ' -f2 "$PUBFILE")"
exec 9> "$D/.go-h.lock"
flock -n 9 || stop ANOTHER_GO_H_OR_EMITTER_RUNNING
W="$(mktemp -d)"
EMITTER=""
ARMED=""
minio_running() { [ "$(sudo -n "$DOCKER" inspect -f '{{.State.Running}}' "$MINIO" 2>/dev/null)" = "true" ]; }
minio_health() { sudo -n "$DOCKER" inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}sin-healthcheck{{end}}' "$MINIO" 2>/dev/null || echo desconocida; }
minio_healthy() { minio_running && [ "$(minio_health)" = "healthy" ]; }
wd_prop() { "$SYSTEMCTL" show --no-pager "$1" --property=ActiveState 2>/dev/null | sed -n 's/^ActiveState=//p' | head -1; }
wd_inactive() { [ "$(wd_prop "$WD.timer")" = "inactive" ] && [ "$(wd_prop "$WD.service")" = "inactive" ]; }  # lectura: vacío o raro = NO inactivo
SETTLED=""; MINIO_FINAL=no; WD_FINAL=desconocido
settle_minio() { # una vez: deja MinIO en marcha y SALUDABLE; solo entonces desarma el reinicio independiente y LO COMPRUEBA por lectura
  [ -n "$ARMED" ] && [ -z "$SETTLED" ] || return 0
  SETTLED=1
  if ! minio_running; then sudo -n "$DOCKER" start "$MINIO" >/dev/null 2>&1 || true; sleep 3; fi
  for _ in $(seq 1 24); do minio_healthy && break; sleep 5; done
  if minio_healthy; then
    MINIO_FINAL=si
    sudo -n "$SYSTEMCTL" stop "$WD.timer" >/dev/null 2>&1 || true
    if wd_inactive; then WD_FINAL=inactive; ARMED=""; else WD_FINAL=NO_CONFIRMADO_SIGUE_ARMADO_O_ILEGIBLE; fi
  else
    WD_FINAL=sigue_armado_porque_MinIO_no_esta_saludable
  fi
  echo "MINIO_FINAL_RUNNING_HEALTHY=$MINIO_FINAL"
  echo "REINICIO_INDEPENDIENTE_FINAL=$WD_FINAL"
}
GO_INSTALLED=""; ARCHIVED=no
cleanup() { [ -z "$EMITTER" ] || kill "$EMITTER" 2>/dev/null || true; settle_minio; [ -z "$GO_INSTALLED" ] || { archive_go; echo "GO_USADO_ARCHIVADO_AL_SALIR=$ARCHIVED"; }; rm -rf "$W"; }
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
trap 'exit 129' HUP
sudo -v || stop SUDO
[ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F' "$INBOX")" = "0:0:700:directory" ] || stop INBOX_NOT_ROOT_0700
absent() { sudo /usr/bin/test ! -e "$1" -a ! -L "$1"; }   # ni archivo ni enlace (tampoco colgante)
rootfile() { [ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F:%h' "$1" 2>/dev/null)" = "0:0:600:regular file:1" ]; }
archive_go() { # el GO instalado nunca queda utilizable en el inbox: se aparta como usado (también ante señal o STOP tras instalarlo)
  [ "$ARCHIVED" != "si" ] || return 0
  STAMP="$(date -u +%Y%m%dT%H%M%SZ)-$$"
  U="$INBOX/.used-go-${NONCE:0:16}-$STAMP"
  if absent "$U.json" && absent "$U.json.sig" && sudo -n /usr/bin/mv -n -T "$INBOX/go.json" "$U.json" && sudo -n /usr/bin/mv -n -T "$INBOX/go.json.sig" "$U.json.sig" \
     && rootfile "$U.json" && rootfile "$U.json.sig" && absent "$INBOX/go.json" && absent "$INBOX/go.json.sig"; then ARCHIVED=si; fi
}
for g in go.json go.json.sig .go.json.new .go.json.sig.new; do absent "$INBOX/$g" || stop INBOX_ALREADY_HAS_A_GO_OR_LEFTOVER; done
[ "$(sudo /usr/bin/sha256sum "$INBOX/g6-operation.json" | cut -d' ' -f1)" = "${QA_OPERATION#sha256:}" ] || stop OPERATION_FILE_MISMATCH
[ "$(sudo "$DOCKER" inspect -f '{{.State.Running}}' "$MINIO" 2>/dev/null)" = "true" ] || stop MINIO_IS_NOT_RUNNING_BEFORE_BACKUP
[ "$("$SYSTEMCTL" is-active "$WD.timer" 2>/dev/null || true)" != "active" ] || stop RESTART_TIMER_ALREADY_ARMED_REVIEW
[ "$("$SYSTEMCTL" is-active "$WD.service" 2>/dev/null || true)" != "active" ] || stop RESTART_UNIT_ALREADY_ACTIVE_REVIEW
echo "ATENCION: este guion DETENDRA el MinIO de QA ($MINIO) durante la copia del Silo (tope 10 min) y lo volvera a arrancar."
echo "Si autorizas la ventana ahora, escribe exactamente: AUTORIZO VENTANA MINIO"
IFS= read -r CONSENT
[ "$CONSENT" = "AUTORIZO VENTANA MINIO" ] || stop WINDOW_NOT_AUTHORIZED
echo "Pega la linea QA525GO1:... de Termux y pulsa Enter:"
IFS= read -r TOKEN
case "$TOKEN" in QA525GO1:*:*) ;; *) stop TOKEN_SHAPE ;; esac
REST="${TOKEN#QA525GO1:}"
printf '%s' "${REST%%:*}" | base64 -d > "$W/go.json" 2>/dev/null || stop TOKEN_B64
printf '%s' "${REST#*:}" | base64 -d > "$W/go.json.sig" 2>/dev/null || stop TOKEN_B64
printf 'ssh-ed25519 %s qa525-ops\n' "$PUBKEY" > "$W/key.pub"
[ "$(ssh-keygen -lf "$W/key.pub" | awk '{print $2}')" = "$FP_EXPECTED" ] || stop FINGERPRINT
printf 'qa525-ops namespaces="%s" ssh-ed25519 %s\n' "$NS" "$PUBKEY" > "$W/signers"
ssh-keygen -Y verify -f "$W/signers" -I qa525-ops -n "$NS" -s "$W/go.json.sig" < "$W/go.json" >/dev/null 2>&1 || stop SIGNATURE
APP="$(sha256sum "$D/app-inner.zip" | cut -d' ' -f1)"
NONCE="$(QA_GO="$W/go.json" QA_SHA="$SHA" QA_APP="$APP" "$PY" -I -S -c '
import calendar, json, os, re, sys, time
E = os.environ
raw = open(E["QA_GO"], "rb").read()
try:
    go = json.loads(raw)
    fixed = {"action": "g6-v2-%s-supervised" % E["QA_CMD"], "admission_sha256": E["QA_ADMISSION"],
             "app_archive_sha256": "sha256:" + E["QA_APP"], "app_source_sha": E["QA_SHA"],
             "environment": "QA_GLMDCH_ONLY", "host": "glmdch", "g6_command": E["QA_CMD"],
             "g6_operation_sha256": E["QA_OPERATION"], "operator": "soporte@glmdch", "ops_source_sha": E["QA_SHA"],
             "plan_sha256": E["QA_PLAN"], "policy_hash": E["QA_POLICY"],
             "schema": "qa525-go-g6-v1", "scope": "QA525_G6_V2_ONLY_NO_LAUNCHER_NO_API_WEB"}
    ok = (type(go) is dict and set(go) == set(fixed) | {"nonce", "issued_at", "expires_at"}
          and all(go[k] == v for k, v in fixed.items())
          and json.dumps(go, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode() + b"\n" == raw
          and type(go["nonce"]) is str and re.fullmatch(r"[0-9a-f]{64}", go["nonce"]) is not None)
    exp = calendar.timegm(time.strptime(go["expires_at"], "%Y-%m-%dT%H:%M:%SZ"))
    iss = calendar.timegm(time.strptime(go["issued_at"], "%Y-%m-%dT%H:%M:%SZ"))
    left = exp - int(time.time())
    ok = ok and 0 < exp - iss <= 900 and iss <= time.time() and left >= 180
except Exception:
    ok = False
if not ok:
    sys.exit(1)
sys.stderr.write("GO_SEGUNDOS_RESTANTES=%d\n" % left)
print(go["nonce"])
')" || stop GO_CONTENT_OR_EXPIRED
# -B: no escribir __pycache__ dentro de modules/ (rompería el conjunto exacto de 24 módulos que exigen los KIT-A siguientes).
# Reinicio independiente ANTES de instalar el GO: si no se puede armar o leer, no se gasta nada.
sudo -v || stop SUDO
ARMED=1   # desde aquí, cualquier salida pasa por settle_minio (aunque systemd-run falle a medias)
sudo "$SYSTEMD_RUN" --on-active=15min --unit="$WD" --collect "$DOCKER" start "$MINIO" >/dev/null 2>&1 || stop RESTART_TIMER_NOT_ARMED
QA_RUN="$D/modules/qa525-vnext-g6-v2-run.py" QA_WD="$WD" QA_DOCKER="$DOCKER" QA_MINIO="$MINIO" QA_SYSTEMCTL="$SYSTEMCTL" "$PY" -B -I -S -c '
import importlib.util, os, re, subprocess, sys, time
E = os.environ
try:
    spec = importlib.util.spec_from_file_location("g6run_precheck", E["QA_RUN"])
    run = importlib.util.module_from_spec(spec); spec.loader.exec_module(run)
    def show(unit, props):
        out = subprocess.run([E["QA_SYSTEMCTL"], "show", "--no-pager", unit, "--property=" + props], capture_output=True, timeout=15)
        return out.stdout if out.returncode == 0 else b""
    props = run._props(show(E["QA_WD"] + ".timer", "ActiveState,SubState,Unit,NextElapseUSecMonotonic"))
    ok = props.get("ActiveState") == "active" and props.get("SubState") == "waiting"
    found = re.search(r"argv\[\]=(.*?) ;", show(props.get("Unit", "x.service"), "ExecStart").decode("utf-8", "replace"))
    ok = ok and found is not None and found.group(1).split(" ") == [E["QA_DOCKER"], "start", E["QA_MINIO"]]
    remaining = run.usec_property_seconds(props["NextElapseUSecMonotonic"]) - time.monotonic()
    ok = ok and 780 <= remaining <= run.WATCHDOG_MAX_SECONDS
except Exception:
    ok = False
if not ok:
    sys.exit(1)
print("REINICIO_INDEPENDIENTE_ARMADO=si SEGUNDOS_RESTANTES=%d" % remaining)
' || stop RESTART_TIMER_PRECHECK_FAILED_NOTHING_SPENT
# install -T into a root 0700 directory that we just measured empty of GO files; cp -n refuses to overwrite.
sudo /usr/bin/install -m 0600 -o root -g root -T "$W/go.json" "$INBOX/.go.json.new" || stop INSTALL
sudo /usr/bin/install -m 0600 -o root -g root -T "$W/go.json.sig" "$INBOX/.go.json.sig.new" || stop INSTALL
# El inbox es root:root 0700 (medido arriba) y este proceso tiene el flock: nadie más crea entradas ahí.
# Aun así: publicación sin sobrescritura y, ante cualquier resultado parcial, se aparta TODO y se detiene.
retire() { # nunca deja un GO a medias en el inbox; conserva lo que haya con sello único
  R="$(date -u +%Y%m%dT%H%M%SZ)-$$"
  for g in go.json go.json.sig .go.json.new .go.json.sig.new; do
    absent "$INBOX/$g" || sudo /usr/bin/mv -n -T "$INBOX/$g" "$INBOX/.failed-$R-${g#.}" || true
  done
  for g in go.json go.json.sig .go.json.new .go.json.sig.new; do absent "$INBOX/$g" || { echo "QA525_GO_H_G6=STOP_PARTIAL_GO_STILL_IN_INBOX_DO_NOT_RETRY"; echo "GO_FISICO=NO_GO"; exit 1; }; done
  stop "$1_PARTIAL_GO_RETIRED_AS_.failed-$R"
}
rootfile "$INBOX/.go.json.new" && rootfile "$INBOX/.go.json.sig.new" || retire INSTALL
sudo /usr/bin/mv -n -T "$INBOX/.go.json.new" "$INBOX/go.json" || retire PUBLISH
sudo /usr/bin/mv -n -T "$INBOX/.go.json.sig.new" "$INBOX/go.json.sig" || retire PUBLISH
absent "$INBOX/.go.json.new" && absent "$INBOX/.go.json.sig.new" && rootfile "$INBOX/go.json" && rootfile "$INBOX/go.json.sig" || retire PUBLISH
[ "$(sudo /usr/bin/sha256sum "$INBOX/go.json" | cut -d' ' -f1)" = "$(sha256sum "$W/go.json" | cut -d' ' -f1)" ] || retire READBACK
[ "$(sudo /usr/bin/sha256sum "$INBOX/go.json.sig" | cut -d' ' -f1)" = "$(sha256sum "$W/go.json.sig" | cut -d' ' -f1)" ] || retire READBACK
GO_INSTALLED=1
echo "GO_SHA256=$(sha256sum "$W/go.json" | cut -d' ' -f1)"
echo "GO_INSTALADO=si NONCE=${NONCE:0:8}…"
QA_ZIP="$D/app-inner.zip" QA_NONCE="$NONCE" QA_APP="$APP" QA_SOCK="$SOCK" QA_PARENT="$$" "$PY" -I -S -c '
import array, fcntl, os, socket, sys, time
E = os.environ
fd = os.memfd_create("qa525-app", os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
with open(E["QA_ZIP"], "rb") as src:
    while True:
        block = src.read(1 << 20)
        if not block:
            break
        view = memoryview(block)
        while view:
            view = view[os.write(fd, view):]
fcntl.fcntl(fd, fcntl.F_ADD_SEALS, fcntl.F_SEAL_WRITE | fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_GROW | fcntl.F_SEAL_SEAL)
frame = b"QA525-BND-V1:" + E["QA_NONCE"].encode() + b":" + E["QA_APP"].encode()
end = time.time() + 600
while time.time() < end and os.getppid() == int(E["QA_PARENT"]):
    s = socket.socket(socket.AF_UNIX, socket.SOCK_SEQPACKET)
    try:
        s.connect(E["QA_SOCK"])
        s.sendmsg([frame], [(socket.SOL_SOCKET, socket.SCM_RIGHTS, array.array("i", [fd]).tobytes())])
        open(E["QA_ZIP"] + ".sent", "w").close()
        end = time.time() + 120
        while time.time() < end and os.getppid() == int(E["QA_PARENT"]):
            time.sleep(0.5)
        sys.exit(0)
    except OSError:
        s.close()
        time.sleep(0.2)
sys.exit(1)
' </dev/null >/dev/null 2>&1 9>&- &
EMITTER=$!
rm -f "$D/app-inner.zip.sent"
echo "Ahora pega el PIN del manifiesto (desde Bitwarden) y pulsa Enter. No se muestra en pantalla."
set +e
# umask 022 SOLO para el proceso root del bootstrap: el verificador de autenticidad escribe bundle.json y
# trusted-root.jsonl con modo 0644 para que los lea el uid sin privilegios que ejecuta `gh`; con umask 077 heredado
# quedarían 0600 y la verificación APP falla (LAUNCH_AUTHENTICITY_DENIED). Los directorios siguen siendo root 0700.
echo "UMASK_ROOT_HEREDADO=$(sudo /bin/sh -c umask)"
sudo -v || true
# -u: salida sin búfer, para ver STOP/START en el momento. Solo se canaliza stdout; PIN y clave siguen por la terminal.
OUT="$W/bootstrap.out"; : > "$OUT"
sudo /bin/sh -c 'umask 022; exec "$@"' sh "$PY" -I -S -u "$T/qa525-vnext-root-bootstrap.py" run "$T" "$T/manifest-g6-$CMD.json" | while IFS= read -r line; do
  printf '%s\n' "$line"; printf '%s\n' "$line" >> "$OUT"
  case "$line" in
    G6RUN_ACTION=STOP_MINIO_NOW)
      if sudo -n "$DOCKER" stop -t 30 "$MINIO" >/dev/null 2>&1; then L="GOH_MINIO_STOP=enviado"; else L="GOH_MINIO_STOP=FALLO_el_respaldo_se_detendra_solo"; fi
      echo "$L"; echo "$L" >> "$OUT" ;;
    G6RUN_ACTION=START_MINIO_NOW)
      if sudo -n "$DOCKER" start "$MINIO" >/dev/null 2>&1; then L="GOH_MINIO_START=enviado"; else L="GOH_MINIO_START=FALLO_el_reinicio_independiente_lo_arrancara"; fi
      echo "$L"; echo "$L" >> "$OUT" ;;
  esac
done
RC=${PIPESTATUS[0]}
set -e
kill "$EMITTER" 2>/dev/null || true
EMITTER=""
echo "BOOTSTRAP_EXIT=$RC"
settle_minio
echo "MINIO_SALUD_ACTUAL=$(minio_health)"
[ -e "$D/app-inner.zip.sent" ] && echo "FD_ENVIADO=si" || echo "FD_ENVIADO=no"
rm -f "$D/app-inner.zip.sent"
ARCHIVED=no
archive_go
echo "GO_USADO_ARCHIVADO=$ARCHIVED"
# Marcadores del respaldo, exactamente uno de cada uno y con la forma que imprime el runner; ventana observada de principio a fin.
one() { [ "$(grep -c "^$1=" "$OUT" 2>/dev/null || true)" = "1" ] && sed -n "s/^$1=//p" "$OUT"; }
MARKERS=no
AN="$(one ARCHIVE_NAME || true)"; AS="$(one ARCHIVE_SHA256 || true)"; SN="$(one SNAPSHOT_ID || true)"; MS="$(one MANIFEST_SHA256 || true)"; WS="$(one MINIO_WINDOW_SECONDS || true)"
if printf '%s' "$AN" | grep -Eq '^centenario-qa-g6-v2-CIFRADO-[0-9]{8}T[0-9]{6}Z\.tar\.gz$' && printf '%s' "$AS" | grep -Eq '^[a-f0-9]{64}$' \
   && printf '%s' "$SN" | grep -Eq '^[a-f0-9]{8,64}$' && printf '%s' "$MS" | grep -Eq '^[a-f0-9]{64}$' && printf '%s' "$WS" | grep -Eq '^[0-9]{1,3}$' && [ "$WS" -le 600 ] \
   && [ "$(grep -c '^G6RUN_ACTION=STOP_MINIO_NOW$' "$OUT")" = "1" ] && [ "$(grep -c '^G6RUN_ACTION=START_MINIO_NOW$' "$OUT")" = "1" ] \
   && grep -q '^GOH_MINIO_STOP=enviado$' "$OUT" && grep -q '^GOH_MINIO_START=enviado$' "$OUT" && ! grep -q '^G6RUN_STOP=' "$OUT"; then MARKERS=si; fi
echo "MARCADORES_RESPALDO_COMPLETOS=$MARKERS"
echo "MINIO_FINAL_RUNNING_HEALTHY=$MINIO_FINAL"
echo "REINICIO_INDEPENDIENTE_FINAL=$WD_FINAL"
# Veredicto y código de salida (el primero que aplique):
#   5 = MinIO NO quedó en marcha y saludable: prioridad absoluta, el reinicio independiente sigue armado
#   6 = MinIO sano pero el reinicio independiente NO se pudo comprobar inactivo (revisar a mano, no repetir)
#   4 = el respaldo no terminó con 0: copiar la salida, NO reintentar sin revisión (el nonce puede estar gastado)
#   3 = terminó con 0 pero el GO usado NO quedó archivado (incidente de custodia)
#   7 = terminó con 0 pero faltan/son raros los marcadores o la ventana (la copia NO se da por buena)
#   0 = respaldo con 0, GO archivado, marcadores completos, MinIO running+healthy y reinicio independiente inactive (leído)
if [ "$MINIO_FINAL" != "si" ]; then echo "QA525_GO_H_G6=backup_ENDED_BUT_MINIO_IS_NOT_RUNNING_AND_HEALTHY_CHECK_NOW"; echo "GO_H_G6_EXIT=5"; exit 5; fi
if [ "$WD_FINAL" != "inactive" ]; then echo "QA525_GO_H_G6=backup_MINIO_OK_BUT_RESTART_TIMER_NOT_CONFIRMED_INACTIVE_REVIEW"; echo "GO_H_G6_EXIT=6"; exit 6; fi
if [ "$RC" != "0" ]; then
  echo "QA525_GO_H_G6=backup_COMMAND_FAILED_DO_NOT_RETRY_WITHOUT_REVIEW"
  [ "$ARCHIVED" = "si" ] || echo "AVISO=EL_GO_USADO_SIGUE_EN_EL_INBOX"
  echo "GO_H_G6_EXIT=4"; exit 4
fi
if [ "$ARCHIVED" != "si" ]; then echo "QA525_GO_H_G6=backup_COMMAND_EXIT_0_BUT_USED_GO_NOT_ARCHIVED_CUSTODY_INCIDENT"; echo "GO_H_G6_EXIT=3"; exit 3; fi
if [ "$MARKERS" != "si" ]; then echo "QA525_GO_H_G6=backup_COMMAND_EXIT_0_BUT_MARKERS_OR_WINDOW_NOT_AS_EXPECTED_BACKUP_NOT_ACCEPTED"; echo "GO_H_G6_EXIT=7"; exit 7; fi
echo "QA525_GO_H_G6=backup_COMPLETE_MINIO_HEALTHY_RESTART_TIMER_INACTIVE_MARKERS_OK"
echo "GO_H_G6_EXIT=0"
exit 0
