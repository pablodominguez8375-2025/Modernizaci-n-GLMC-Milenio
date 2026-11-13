# QA525 KIT-G6-B v3 — COMANDO=restore — ARTEFACTO=GLMDCH_OPERATIVO_CORTE_164a07ec — como soporte@glmdch, CON sudo de binarios del sistema (stat, test, install, sha256sum). Instala en custodia
# root lo que generó KIT-A. No ejecuta código del proyecto, no instala ningún GO y no sobrescribe nada distinto.
(
set -euo pipefail
SHA="164a07ecd36eb45428b6aeac4854a17e339ca525"
BASE="/var/lib/pmgm-qa525"
SOCK="/run/pmgm-qa525/launcher.sock"
CMD="restore"
stop() { echo "QA525_KIT_G6_B=STOP_$1"; echo "GO_FISICO=NO_GO"; exit 1; }
[ "$(hostname)" = "glmdch" ] && [ "$(id -un)" = "soporte" ] || stop HOST_USER
D="$HOME/qa525-corte-${SHA:0:8}"
case "$CMD" in preflight|init|backup|restore|review) ;; *) stop COMMAND ;; esac
K="$D/kit-g6-$CMD"
OPS_JSON="$D/qa525-ops-v3-${SHA:0:8}.json"
OPS_SIG="$OPS_JSON.sig"
[ -f "$OPS_JSON" ] && [ ! -L "$OPS_JSON" ] && [ -f "$OPS_SIG" ] && [ ! -L "$OPS_SIG" ] || stop OPS_FILES
for f in app-bundle.json trusted-root.jsonl; do [ -f "$D/$f" ] && [ ! -L "$D/$f" ] || stop RUN_CORTE_A_FIRST; done
T="/opt/centenario/qa525-root-source/$SHA"
[ -d "$K" ] && [ ! -L "$K" ] && [ "$(stat -c '%u:%a' "$K")" = "$(id -u):700" ] || stop KIT_DIR_NOT_MINE_0700
for f in plan.json admission.json app-subject.json g6-operation.json "sponsor-config-g6-$CMD.json" "manifest-g6-$CMD.json"; do [ -f "$K/$f" ] && [ ! -L "$K/$f" ] || stop RUN_KIT_A_FIRST; done
[ -t 0 ] || stop NEEDS_TERMINAL
# PIN: lo teclea el Sponsor desde Bitwarden (fuente independiente de estos archivos). Se exige ANTES del primer write root.
echo "Pega el MANIFEST_PIN guardado en Bitwarden (64 hex) y pulsa Enter (no se muestra en pantalla):"
IFS= read -rs PIN; echo   # sin eco; se toleran espacios, saltos de línea y el prefijo MANIFEST_PIN= pegados por error
PIN="$(printf '%s' "$PIN" | tr -d '[:space:]')"; PIN="${PIN#MANIFEST_PIN=}"
[ "${#PIN}" = "64" ] && [ -z "$(printf '%s' "$PIN" | tr -d '0-9a-f')" ] || stop PIN_SHAPE
[ "$(sha256sum "$K/manifest-g6-$CMD.json" | cut -d' ' -f1)" = "$PIN" ] || stop PIN_DOES_NOT_MATCH_KIT_MANIFEST_NOTHING_INSTALLED
sudo -v || stop SUDO
[ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F' "$BASE")" = "0:0:700:directory" ] || stop BASE_NOT_ROOT_0700
[ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F' "$T")" = "0:0:700:directory" ] || stop STAGE_NOT_ROOT_0700
mkd() { # dir mode
  if sudo /usr/bin/test -e "$1" -o -L "$1"; then
    [ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F' "$1")" = "0:0:$2:directory" ] || stop "DIR_UNEXPECTED"
  else
    sudo /usr/bin/install -d -m "0$2" -o root -g root "$1" || stop MKDIR
  fi
}
put() { # src dst : create, or accept only an identical root 0600 regular file
  if sudo /usr/bin/test -e "$2" -o -L "$2"; then
    [ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F:%h' "$2")" = "0:0:600:regular file:1" ] || stop TARGET_UNEXPECTED
    [ "$(sudo /usr/bin/sha256sum "$2" | cut -d' ' -f1)" = "$(sha256sum "$1" | cut -d' ' -f1)" ] || stop TARGET_DIFFERS_NO_OVERWRITE
  else
    sudo /usr/bin/install -m 0600 -o root -g root -T "$1" "$2" || stop INSTALL
    [ "$(sudo /usr/bin/sha256sum "$2" | cut -d' ' -f1)" = "$(sha256sum "$1" | cut -d' ' -f1)" ] || stop READBACK
  fi
}
I="$BASE/inbox-g6-$CMD"
[ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F' /var/lib/pmgm-qa525-g6-v2)" = "0:0:700:directory" ] || stop G6_STATE_ROOT_NOT_ROOT_0700
mkd "$I" 700; mkd "$BASE/tmp" 700; mkd "$BASE/journal" 700; mkd "$BASE/lease" 700; mkd "$BASE/pre-root-trust" 700
mkd "$(dirname "$SOCK")" 755
if sudo /usr/bin/test -e "$BASE/lease/.launcher-only.lease"; then
  [ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F:%h' "$BASE/lease/.launcher-only.lease")" = "0:0:600:regular empty file:1" ] || stop LEASE_UNEXPECTED
else
  sudo /usr/bin/install -m 0600 -o root -g root -T /dev/null "$BASE/lease/.launcher-only.lease" || stop LEASE
fi
put "$OPS_JSON" "$I/ops-v3.json"
put "$OPS_SIG" "$I/ops-v3.json.sig"
put "$K/app-subject.json" "$I/app-subject.json"
put "$D/app-bundle.json" "$I/app-bundle.json"
put "$D/trusted-root.jsonl" "$I/trusted-root.jsonl"
put "$K/g6-operation.json" "$I/g6-operation.json"
put "$K/plan.json" "$T/plan-g6-$CMD.json"
put "$K/admission.json" "$T/admission-g6-$CMD.json"
put "$K/sponsor-config-g6-$CMD.json" "$T/sponsor-config-g6-$CMD.json"
put "$K/manifest-g6-$CMD.json" "$T/manifest-g6-$CMD.json"
for g in go.json go.json.sig; do sudo /usr/bin/test ! -e "$I/$g" -a ! -L "$I/$g" || stop INBOX_HAS_A_GO; done
[ "$(sudo /usr/bin/find "$I" -mindepth 1 -maxdepth 1 ! -name '.used-go-*' ! -name '.failed-*' | wc -l)" = "6" ] || stop INBOX_SET_UNEXPECTED
[ "$(sudo /usr/bin/sha256sum "$T/manifest-g6-$CMD.json" | cut -d' ' -f1)" = "$PIN" ] || stop ROOT_MANIFEST_IS_NOT_THE_PIN
echo "MANIFEST_PIN_MATCHES_ROOT_COPY=si"
echo "INBOX=$I"
echo "QA525_KIT_G6_B=ROOT_CUSTODY_READY_NO_GO_INSTALLED"
echo "ROOT_CODE_EXECUTED=no"
echo "GO_FISICO=NO_GO"
)
