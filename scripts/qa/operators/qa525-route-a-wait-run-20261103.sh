#!/usr/bin/env bash
# QA525 · RUTA A · envoltorio "esperar ventana de cuota anónima fresca y ejecutar UNA vez" (hash fijo).
# No modifica ni relaja el guion revisado (GO_SCRIPT_EXACT_HASH #6607929043): lo descarga del commit inmutable,
# verifica SHA-256 y sintaxis ANTES de esperar, y lo ejecuta una sola vez cuando la cuota anónima de GitHub
# (/rate_limit, que NO consume cuota) vuelve a ser >= WAIT_MIN. Sin reintentos: cualquier STOP termina aquí.
# Uso: bash archivo               -> espera ventana y ejecuta SOLO A0 (lectura)
#      bash archivo EXECUTE_A1_A2 -> espera ventana y ejecuta A0 + custodia + UN intento (requiere autorización específica del Sponsor)
set -Eeuo pipefail
umask 077
COMMIT=3db21f5d2768617b99184b2574fe735dba4ac6b2
SHA=63214566a641bb516c0a0bd1419b968e67a7988d02a1e2be4f0dc4d2b1096b51
URL="https://raw.githubusercontent.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/$COMMIT/scripts/qa/operators/qa525-route-a-6eed4-20261103.sh"
WAIT_MIN=59
POLL=20
DEADLINE=$(date -u -d '2026-11-04 10:15:00 UTC' +%s)
stop() { printf 'QA525_WAIT_RUN=STOP_%s_NO_RETRY\n' "$1"; exit 2; }
[[ "$#" -le 1 ]] || stop BAD_ARGUMENT
MODE=${1:-READ_ONLY}
[[ "$MODE" == READ_ONLY || "$MODE" == EXECUTE_A1_A2 ]] || stop BAD_ARGUMENT
[[ "$(hostname -s)" == glmdch && "$(id -un)" == soporte ]] || stop HOST_OR_USER
command -v curl >/dev/null && command -v jq >/dev/null && command -v sha256sum >/dev/null || stop TOOLS
sudo -v || stop SUDO
F=$(mktemp /home/soporte/.qa525-route-a-XXXXXX.sh) || stop TMP
trap 'rm -f -- "$F"' EXIT
curl -fsS --max-time 60 --proto '=https' -o "$F" "$URL" || stop DOWNLOAD
printf '%s  %s\n' "$SHA" "$F" | sha256sum -c - >/dev/null || stop SCRIPT_HASH
bash -n "$F" || stop SCRIPT_SYNTAX
printf 'QA525_WAIT_RUN_SCRIPT=VERIFIED sha256=%s mode=%s\n' "$SHA" "$MODE"
while :; do
  (( $(date -u +%s) < DEADLINE )) || stop G6_DEADLINE_REACHED_RENEW_G6
  sudo -n -v || stop SUDO_EXPIRED
  rl=$(curl -fsS --max-time 20 --proto '=https' https://api.github.com/rate_limit) || stop RATE_LIMIT_READ
  rem=$(printf '%s' "$rl" | jq -er '.resources.core.remaining | select(type=="number")') || stop RATE_LIMIT_PARSE
  rst=$(printf '%s' "$rl" | jq -er '.resources.core.reset | select(type=="number")') || stop RATE_LIMIT_PARSE
  if (( rem >= WAIT_MIN )); then break; fi
  printf 'QA525_WAIT_RUN_WAITING remaining=%s next_window_utc=%s\n' "$rem" "$(date -u -d "@$rst" +%FT%TZ)"
  sleep "$POLL"
done
printf 'QA525_WAIT_RUN_WINDOW=OPEN remaining=%s\n' "$rem"
rc=0
if [[ "$MODE" == EXECUTE_A1_A2 ]]; then bash "$F" EXECUTE_A1_A2 || rc=$?; else bash "$F" || rc=$?; fi
printf 'QA525_WAIT_RUN_CHILD_EXIT=%s\n' "$rc"
exit "$rc"
