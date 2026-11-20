#!/bin/sh
# QA525 LAUNCHER_ONLY - PASO TERMIUS (glmdch, soporte): instala el GO firmado en el inbox, arranca el sender y corre el launcher.
# Pide: sudo (una vez), confirmacion LANZAR y el PIN (oculto, TTY). No borra nada. STOP ante cualquier discrepancia.
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
RR=$HOME/qa525-release-8e851bc3d2f1615a94009591f0781f88d6d3fe6f
P=$HOME/qa525-prov-8e851bc3
if [ -f "$P/inner.zip" ]; then INNER=$P/inner.zip; else INNER=$HOME/qa525-inner-8e851bc3.zip; fi
[ -f "$INNER" ] || { echo "STOP INNER_ZIP_NOT_FOUND"; exit 1; }
D=/var/lib/pmgm-qa525/inbox-launcher-8e851bc3
SOCK=/run/pmgm-qa525/launcher-8e851bc3.sock
DEST=/opt/centenario/qa525-root-source/8e851bc3d2f1615a94009591f0781f88d6d3fe6f
NS=qa525-go-cut-v1@pmgm
PY=/usr/bin/python3.14
die() { echo "STOP $1"; exit 1; }
set -- "$RR"/go-in-*
[ -d "$1" ] || die GO_IN_NOT_FOUND
GOIN=$(ls -d "$RR"/go-in-* | sort | tail -1)
[ $# -gt 1 ] && echo "AVISO: hay $# directorios go-in-*; se usa el MAS NUEVO ($GOIN); los demas no se tocan"
GO=$GOIN/go.json; SIG=$GOIN/go.json.sig
for f in "$GO" "$SIG"; do [ -f "$f" ] && [ ! -L "$f" ] || die GO_FILE_NOT_REGULAR; done
[ "$(ssh-keygen -lf "$RR/po-admission/qa525-ops.pub" | awk '{print $2}')" = 'SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k' ] || die PUB_FP
BLOB=$(awk '{print $2}' "$RR/po-admission/qa525-ops.pub")
T=$(mktemp); printf 'qa525-ops namespaces="%s" ssh-ed25519 %s\n' "$NS" "$BLOB" > "$T"
ssh-keygen -Y verify -f "$T" -I qa525-ops -n "$NS" -s "$SIG" < "$GO" || { rm -f "$T"; die GO_SIGNATURE_INVALID; }
rm -f "$T"
python3 -I - "$GO" <<'PY' || exit 1
import calendar,hashlib,json,sys,time
raw=open(sys.argv[1],"rb").read(); d=json.loads(raw)
assert raw==(json.dumps(d,sort_keys=True,separators=(",",":"))).encode() or raw==(json.dumps(d,sort_keys=True,separators=(",",":"))+"\n").encode(), "GO_NOT_CANONICAL"
exp=calendar.timegm(time.strptime(d["expires_at"],"%Y-%m-%dT%H:%M:%SZ")); left=exp-int(time.time())
h=hashlib.sha256(raw).hexdigest()[:16]
print("SHORT_CODE=%s  SECONDS_LEFT=%d  schema=%s nonce=%s" % ("-".join(h[i:i+4] for i in range(0,16,4)),left,d["schema"],d["nonce"][:16]))
if d["schema"]!="qa525-go-launcher-v1": print("STOP GO_SCHEMA"); sys.exit(1)
if left<240: print("STOP GO_TOO_CLOSE_TO_EXPIRY (need >=240 s; sign a new GO)"); sys.exit(1)
PY
python3 -I "$HERE/pre4_kit.py" check-admission "$RR/po-admission/qa525-launcher-admission-8e851bc3.json"
printf 'Compara SHORT_CODE con Termux. Escribe LANZAR para instalar el GO y arrancar el launcher: '
read -r ans; [ "$ans" = LANZAR ] || die NOT_CONFIRMED
sudo -v
for f in go.json go.json.sig; do sudo test ! -e "$D/$f" || die "INBOX_ALREADY_HAS_$f"; done
sudo install -m 0600 -o root -g root -T "$GO" "$D/go.json"
sudo install -m 0600 -o root -g root -T "$SIG" "$D/go.json.sig"
A=$(sudo sha256sum "$D/go.json" "$D/go.json.sig" | awk '{print $1}'); B2=$(sha256sum "$GO" "$SIG" | awk '{print $1}')
[ -n "$A" ] && [ "$A" = "$B2" ] || die INBOX_COPY_HASH_MISMATCH
echo "GO_INBOX_COPY=PASS"
LOG=$HOME/qa525-sender-$(date -u +%H%M%S).log
"$PY" -I "$HERE/pre4_sender.py" --outer "$RR/release.zip" --inner "$INNER" --go "$GO" --socket "$SOCK" --wait 240 > "$LOG" 2>&1 &
echo "sender en segundo plano (log $LOG). Ahora el launcher: te pedira el PIN (oculto)."
set +e
sudo /bin/sh -c 'umask 022; exec "$@"' sh "$PY" -I -S -u "$DEST/qa525-vnext-root-bootstrap.py" run "$DEST" "$DEST/manifest-launcher.json"
rc=$?
set -e
sleep 1; echo "--- sender:"; cat "$LOG"
echo "LAUNCHER_RUN_EXIT=$rc  (0 = exito; cualquier otro valor: pega TODA la salida)"
