# QA525 GO-H-G6 v3 — ARTEFACTO=GLMDCH_OPERATIVO_CORTE_164a07ec — COMANDO=restore — en glmdch como soporte: `bash qa525-go-host-<sha8>.sh`. UNA sesión: recibe la línea firmada de Termux,
# la verifica completa, la instala SIN sobrescribir en el inbox root, emite el ZIP y ejecuta el bootstrap (pide el PIN).
# v3: el PIN se pide y se COTEJA con el manifiesto root ANTES de instalar el GO (un PIN equivocado no gasta el GO) y el GO instalado se aparta como usado ante cualquier salida.
# init, backup y restore piden después la clave de Restic por la misma terminal. No toca el launcher ni API/WEB. La validación con autoridad es la del launcher bajo PRE-root; esto es un filtro previo.
set -euo pipefail
SHA="164a07ecd36eb45428b6aeac4854a17e339ca525"
FP_EXPECTED="SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k"
PY="/usr/bin/python3.14"
PY_SHA="52e0a13e60a981d8c4b6478be2ba5176f69da07948a056bf49cf6f077e30cb41"
CMD="restore"
export QA_CMD="restore"
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
INSTALLED=0; ARCHIVED=no; NONCE=""
archive_go() { # el GO instalado nunca queda utilizable en el inbox: se aparta como usado (también ante señal o STOP tras instalarlo)
  [ "$INSTALLED" = "1" ] && [ "$ARCHIVED" != "si" ] || return 0
  STAMP="$(date -u +%Y%m%dT%H%M%SZ)-$$"
  U="$INBOX/.used-go-${NONCE:0:16}-$STAMP"
  if absent "$U.json" && absent "$U.json.sig" && sudo -n /usr/bin/mv -n -T "$INBOX/go.json" "$U.json" && sudo -n /usr/bin/mv -n -T "$INBOX/go.json.sig" "$U.json.sig" \
     && rootfile "$U.json" && rootfile "$U.json.sig" && absent "$INBOX/go.json" && absent "$INBOX/go.json.sig"; then ARCHIVED=si; fi
}
cleanup() { [ -z "$EMITTER" ] || kill "$EMITTER" 2>/dev/null || true; archive_go; rm -rf "$W"; }
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
trap 'exit 129' HUP
sudo -v || stop SUDO
echo "Pega el MANIFEST_PIN de este comando ($CMD) (el que imprimio KIT-G6-A de $CMD; NO el de otro comando) y pulsa Enter. No se muestra en pantalla:"
IFS= read -rs PIN; echo
PIN="$(printf '%s' "$PIN" | tr -d '[:space:]')"; PIN="${PIN#MANIFEST_PIN=}"
[ "${#PIN}" = "64" ] && [ -z "$(printf '%s' "$PIN" | tr -d '0-9a-f')" ] || stop PIN_SHAPE_NOTHING_SPENT
[ "$(sudo /usr/bin/sha256sum "$T/manifest-g6-$CMD.json" | cut -d' ' -f1)" = "$PIN" ] || stop PIN_IS_NOT_THE_MANIFEST_PIN_OF_THIS_COMMAND_NOTHING_SPENT
echo "PIN_COINCIDE_CON_MANIFIESTO_ROOT=si"
[ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F' "$INBOX")" = "0:0:700:directory" ] || stop INBOX_NOT_ROOT_0700
absent() { sudo /usr/bin/test ! -e "$1" -a ! -L "$1"; }   # ni archivo ni enlace (tampoco colgante)
rootfile() { [ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F:%h' "$1" 2>/dev/null)" = "0:0:600:regular file:1" ]; }
for g in go.json go.json.sig .go.json.new .go.json.sig.new; do absent "$INBOX/$g" || stop INBOX_ALREADY_HAS_A_GO_OR_LEFTOVER; done
[ "$(sudo /usr/bin/sha256sum "$INBOX/g6-operation.json" | cut -d' ' -f1)" = "${QA_OPERATION#sha256:}" ] || stop OPERATION_FILE_MISMATCH
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
if [ "$CMD" = "restore" ]; then # NO QUEMAR: todo lo que el restore exige de la copia descargada se mide ANTES de instalar el GO
  RESTORE_ARGV='["--restic-sha256","e0ba8fd58a7cb76707cbc064abb4ac72d5a91aa26041b9c4f763405684d00a41","--repo-id","b4df75c52a71050a4bbf493d25a7cd5dd5921d1fdb4ab8226eaf7b6b7ffb65ce","--key-epoch","75870a7a926569c94e853a2a44f0a580","--image","postgres@sha256:2d2b8998d31037bf721cfdf764d76ba74171b4fab3431b7f72c27c56ddbdf9e3","--archive-name","centenario-qa-g6-v2-CIFRADO-20261113T165400Z.tar.gz","--archive-sha256","b0c77c1c26ba48e1c6942abab3ea5d5b1a9aaa3228cbd65753bc274a775276fe","--snapshot-id","fb44c8dd093a7f8cb69491c4cb964b602942c0d565bafe01f39571b9595aa493","--manifest-sha256","51c4134db3d7c0c9dcd99a66303fdf7836b241a7396420b98d85971ef6c6f171"]'
  ARCH="$("$PY" -I -S -c 'import json,sys;a=json.loads(sys.argv[1]);print(dict(zip(a[0::2],a[1::2]))["--archive-name"])' "$RESTORE_ARGV")"
  ASHA="$("$PY" -I -S -c 'import json,sys;a=json.loads(sys.argv[1]);print(dict(zip(a[0::2],a[1::2]))["--archive-sha256"])' "$RESTORE_ARGV")"
  [[ "$ARCH" =~ ^centenario-qa-g6-v2-CIFRADO-([0-9]{8}T[0-9]{6}Z)\.tar\.gz$ ]] || stop ARCHIVE_NAME_SHAPE
  RSTAMP="${BASH_REMATCH[1]}"; [[ "$ASHA" =~ ^[0-9a-f]{64}$ ]] || stop ARCHIVE_SHA_SHAPE
  G6S="/var/lib/pmgm-qa525-g6-v2"; DLF="$G6S/downloads-v2/$ARCH"
  [ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F:%h' "$DLF" 2>/dev/null)" = "0:0:600:regular file:1" ] || stop DOWNLOAD_COPY_NOT_ROOT_0600_NOTHING_SPENT
  [ "$(sudo /usr/bin/sha256sum "$DLF" | cut -d' ' -f1)" = "$ASHA" ] || stop DOWNLOAD_COPY_HASH_MISMATCH_NOTHING_SPENT
  CUT="$(date -u -d "${RSTAMP:0:4}-${RSTAMP:4:2}-${RSTAMP:6:2} ${RSTAMP:9:2}:${RSTAMP:11:2}:${RSTAMP:13:2}" +%s)" || stop STAMP_DATE
  [ "$(date -u +%s)" -ge "$CUT" ] && [ "$(date -u +%s)" -lt $((CUT + 172800 - 1800)) ] || stop RESTORE_WINDOW_48H_NEARLY_OVER_NOTHING_SPENT
  absent "$G6S/restores-v2/$RSTAMP" || stop RESTORE_WORKDIR_ALREADY_EXISTS_NOTHING_SPENT
  absent "$G6S/state-v2/isolated-restore-v2-$RSTAMP.report.json" || stop RESTORE_REPORT_ALREADY_EXISTS_NOTHING_SPENT
  echo "COPIA_DESCARGADA_ROOT_0600_SHA_OK_VENTANA_48H_OK_SIN_RESTORE_PREVIO=si"
fi
INSTALLED=1  # desde aquí cualquier salida aparta el GO (archive_go)
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
set +e
# umask 022 SOLO para el proceso root del bootstrap: el verificador de autenticidad escribe bundle.json y
# trusted-root.jsonl con modo 0644 para que los lea el uid sin privilegios que ejecuta `gh`; con umask 077 heredado
# quedarían 0600 y la verificación APP falla (LAUNCH_AUTHENTICITY_DENIED). Los directorios siguen siendo root 0700.
echo "UMASK_ROOT_HEREDADO=$(sudo /bin/sh -c umask)"
printf '%s\n' "$PIN" | sudo /bin/sh -c 'umask 022; exec "$@"' sh "$PY" -I -S -u "$T/qa525-vnext-root-bootstrap.py" run "$T" "$T/manifest-g6-$CMD.json"
RC=$?; PIN=""
set -e
kill "$EMITTER" 2>/dev/null || true
EMITTER=""
echo "BOOTSTRAP_EXIT=$RC"
[ -e "$D/app-inner.zip.sent" ] && echo "FD_ENVIADO=si" || echo "FD_ENVIADO=no"
rm -f "$D/app-inner.zip.sent"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)-$$"
U="$INBOX/.used-go-${NONCE:0:16}-$STAMP"
ARCHIVED=no
if absent "$U.json" && absent "$U.json.sig" && sudo /usr/bin/mv -n -T "$INBOX/go.json" "$U.json" && sudo /usr/bin/mv -n -T "$INBOX/go.json.sig" "$U.json.sig" \
   && rootfile "$U.json" && rootfile "$U.json.sig" && absent "$INBOX/go.json" && absent "$INBOX/go.json.sig"; then ARCHIVED=si; fi
echo "GO_USADO_ARCHIVADO=$ARCHIVED"
# Veredicto y código de salida (nunca 0 salvo comando G6 terminado con 0 y GO usado archivado):
#   0 = el comando G6 terminó con 0 y el GO usado quedó archivado
#   3 = el comando G6 terminó con 0, pero el GO usado NO quedó archivado (incidente de custodia)
#   4 = el comando G6 no terminó con 0: copiar la salida, NO reintentar sin revisión (el nonce puede estar gastado)
if [ "$RC" = "0" ] && [ "$ARCHIVED" = "si" ]; then
  echo "QA525_GO_H_G6=${CMD}_COMMAND_EXIT_0_REVIEW_G6_LINES_ABOVE"
  echo "GO_H_G6_EXIT=0"
  exit 0
elif [ "$RC" = "0" ]; then
  echo "QA525_GO_H_G6=${CMD}_COMMAND_EXIT_0_BUT_USED_GO_NOT_ARCHIVED_CUSTODY_INCIDENT"
  echo "GO_H_G6_EXIT=3"
  exit 3
fi
echo "QA525_GO_H_G6=${CMD}_COMMAND_FAILED_DO_NOT_RETRY_WITHOUT_REVIEW"
[ "$ARCHIVED" = "si" ] || echo "AVISO=EL_GO_USADO_SIGUE_EN_EL_INBOX"
echo "GO_H_G6_EXIT=4"
exit 4
