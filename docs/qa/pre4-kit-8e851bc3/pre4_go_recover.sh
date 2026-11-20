#!/bin/sh
# QA525 LAUNCHER_ONLY - RECUPERACION tras un STOP del launcher (Termius, glmdch, soporte). NO borra nada.
# 1) Diagnostico de solo lectura (sudo ls/cat/sha256sum/test): inbox, journal, lease, socket, procesos, shim.
# 2) Solo si escribes ARCHIVAR: MUEVE go.json y go.json.sig del inbox a un directorio NUEVO root 0700 fuera del inbox
#    (rename, mismo filesystem, sin copiar ni borrar) para poder emitir un GO NUEVO. No toca los 5 archivos pinneados.
set -eu
D=/var/lib/pmgm-qa525/inbox-launcher-8e851bc3
B=/var/lib/pmgm-qa525/launcher-8e851bc3
SOCK=/run/pmgm-qa525/launcher-8e851bc3.sock
SHIM=/usr/local/sbin/pmgm-qa-update
die() { echo "STOP $1"; exit 1; }
sudo -v
echo "== inbox"; sudo find "$D" -printf '%M %u:%g %s %p\n'
echo "== launcher dirs"; sudo find "$B" -maxdepth 2 -printf '%M %u:%g %s %p\n'
echo "== socket"; if sudo test -e "$SOCK" || sudo test -L "$SOCK"; then echo "SOCKET_PRESENT $SOCK"; else echo SOCKET_ABSENT; fi
echo "== procesos"; pgrep -af 'qa525-vnext-root-bootstrap|pre4_sender' || echo NO_PROCESS
echo "== shim (launcher instalado)"; sudo sha256sum "$SHIM"; echo "(viejo conocido: 67f0c3932fd2cde5c71c13dc912c81653af41182ad078d0490f7d1d2afb6bcae)"
sudo test -f "$D/go.json" || die NO_GO_IN_INBOX
NONCE=$(sudo python3 -I -c "import json;print(json.load(open('$D/go.json'))['nonce'])")
SPENT=$(printf '%s' "$NONCE" | sha256sum | cut -d' ' -f1).spent
echo "== nonce del GO en el inbox: $NONCE"
if sudo test -e "$B/journal/$SPENT"; then echo "NONCE_SPENT=YES ($SPENT)  -> el GO fue consumido; NO reutilizar"; else echo "NONCE_SPENT=NO (nadie lo consumio)"; fi
echo "== journal"; sudo ls -la "$B/journal"
printf 'Pega TODA esta salida en #525. Para MOVER (no borrar) go.json y go.json.sig a un directorio nuevo y poder firmar un GO nuevo, escribe ARCHIVAR (cualquier otra cosa termina): '
read -r ans; [ "$ans" = ARCHIVAR ] || { echo "SIN_CAMBIOS"; exit 0; }
A=/var/lib/pmgm-qa525/failed-go-$(date -u +%Y%m%dT%H%M%SZ)
sudo test ! -e "$A" || die ARCHIVE_EXISTS
sudo install -d -m 0700 -o root -g root "$A"
sudo sha256sum "$D/go.json" "$D/go.json.sig" | sudo tee "$A/SHA256SUMS.before" >/dev/null
sudo mv -n "$D/go.json" "$A/go.json"; sudo mv -n "$D/go.json.sig" "$A/go.json.sig"
sudo test ! -e "$D/go.json" && sudo test ! -e "$D/go.json.sig" || die MOVE_INCOMPLETE
( sudo sh -c "cd '$A' && sha256sum go.json go.json.sig" ) | awk '{print $1}' > /tmp/.qa525-after.$$
sudo awk '{print $1}' "$A/SHA256SUMS.before" | cmp - /tmp/.qa525-after.$$ || die ARCHIVE_HASH_MISMATCH
rm -f /tmp/.qa525-after.$$
echo "ARCHIVED dir=$A"; sudo find "$D" -printf '%M %u:%g %s %p\n'; echo "INBOX_READY_FOR_NEW_GO=PASS (deben quedar solo los 5 archivos pinneados)"
