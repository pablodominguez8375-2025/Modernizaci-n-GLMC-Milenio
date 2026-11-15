#!/bin/sh
# QA525 LAUNCHER_ONLY - PASO TERMUX: arma el GO (ventana 900 s), lo firmas con TU llave, lo envias a glmdch.
# AUTORIDAD: la ejecuta el Sponsor. Firmar este GO es la autorizacion de lanzar el launcher QA (solo QA glmdch).
# No imprime ni copia la llave privada. Ejecutar desde la copia del kit en el commit fijado.
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
B=${QA525_B:-$HOME/qa525-deviceB-a3451816}
H=soporte@sistema.granlogiamixta.cl
RR=qa525-release-8e851bc3d2f1615a94009591f0781f88d6d3fe6f
KEY=$HOME/.ssh/qa525-ops; PUB=$KEY.pub
EXPECTED_FP='SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k'
NS=qa525-go-cut-v1@pmgm
die() { echo "STOP $1"; exit 1; }
[ -f "$KEY" ] && [ -f "$PUB" ] || die KEY_MISSING
[ "$(ssh-keygen -lf "$PUB" | awk '{print $2}')" = "$EXPECTED_FP" ] || die PUBLIC_KEY_FINGERPRINT_MISMATCH
[ -f "$B/outer.zip" ] && [ -f "$B/launch-config.json" ] || die DEVICE_B_FILES_MISSING
STAMP=$(date -u +%Y%m%dT%H%M%SZ)
G=$HOME/qa525-go-$STAMP
python3 -I "$HERE/pre4_gosheet.py" --outer "$B/outer.zip" --config "$B/launch-config.json" --out "$G"
printf '\nCompara el SHORT_CODE con el que mostrara glmdch. Escribe FIRMO para firmar este GO (900 s): '
read -r ans; [ "$ans" = FIRMO ] || die NOT_CONFIRMED
ssh-keygen -q -Y sign -f "$KEY" -n "$NS" "$G/go.json" || die SIGN_FAILED
BLOB=$(awk '{print $2}' "$PUB")
printf 'qa525-ops namespaces="%s" ssh-ed25519 %s\n' "$NS" "$BLOB" > "$G/allowed.tmp"
ssh-keygen -Y verify -f "$G/allowed.tmp" -I qa525-ops -n "$NS" -s "$G/go.json.sig" < "$G/go.json" >/dev/null 2>&1 || die SIGNATURE_VERIFY_FAILED
rm -f "$G/allowed.tmp"
echo "GO_SIGNATURE_LOCAL=PASS"
ssh -p 22107 "$H" "mkdir -m 700 $RR/go-in-$STAMP" || die REMOTE_MKDIR_FAILED
scp -P 22107 "$G/go.json" "$G/go.json.sig" "$H:$RR/go-in-$STAMP/" || die SCP_FAILED
echo "GO_SENT dir=go-in-$STAMP  AHORA: pega el bloque de Termius (el GO vence segun expires_at de arriba)"
