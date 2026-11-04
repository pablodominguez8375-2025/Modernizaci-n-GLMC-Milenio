#!/usr/bin/env bash
# QA525 · INTENTO OBSERVADO · envoltorio "esperar ventana de cuota anónima fresca y ejecutar UNA vez" (hash fijo).
# No modifica ni relaja el guion revisado: lo descarga del commit inmutable,
# verifica SHA-256 y sintaxis ANTES de esperar, y lo ejecuta una sola vez cuando la cuota anónima de GitHub
# (/rate_limit, que NO consume cuota) vuelve a ser >= WAIT_MIN. Sin reintentos: cualquier STOP termina aquí.
# Uso: bash archivo               -> espera ventana y ejecuta SOLO A0 (lectura)
#      bash archivo EXECUTE_OBSERVED_ATTEMPT -> espera ventana y ejecuta A0 + UN intento observado (requiere autorización específica del Sponsor)
set -Eeuo pipefail
umask 077
COMMIT=eb5a764aef2bc6ad15c1a481c9516085a62aba93
SHA=bbae5c80e708c574f262a1ea55940052e97f6b874eccbcb9c388aedb5026b957
URL="https://raw.githubusercontent.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/$COMMIT/scripts/qa/operators/qa525-observed-attempt-6eed4-20261104.sh"
WAIT_MIN=59
POLL=20
DEADLINE=$(date -u -d '2026-11-06 09:55:00 UTC' +%s)
LAUNCH_BY=$(date -u -d '2026-11-06 09:50:00 UTC' +%s)
stop() { printf 'QA525_OBS_WAIT_RUN=STOP_%s_NO_RETRY\n' "$1"; exit 2; }
[[ "$#" -le 1 ]] || stop BAD_ARGUMENT
MODE=${1:-READ_ONLY}
[[ "$MODE" == READ_ONLY || "$MODE" == EXECUTE_OBSERVED_ATTEMPT ]] || stop BAD_ARGUMENT
[[ "$(hostname -s)" == glmdch && "$(id -un)" == soporte ]] || stop HOST_OR_USER
command -v curl >/dev/null && command -v jq >/dev/null && command -v sha256sum >/dev/null || stop TOOLS
sudo -v || stop SUDO
F=$(mktemp /home/soporte/.qa525-observed-XXXXXX.sh) || stop TMP
trap 'rm -f -- "$F"' EXIT
curl -fsS --max-time 60 --proto '=https' -o "$F" "$URL" || stop DOWNLOAD
printf '%s  %s\n' "$SHA" "$F" | sha256sum -c - >/dev/null || stop SCRIPT_HASH
bash -n "$F" || stop SCRIPT_SYNTAX
printf 'QA525_OBS_WAIT_RUN_SCRIPT=VERIFIED sha256=%s mode=%s\n' "$SHA" "$MODE"
RST0=''
while :; do
  (( $(date -u +%s) < DEADLINE )) || stop G6_DEADLINE_REACHED_RENEW_G6
  sudo -n -v || stop SUDO_EXPIRED
  rl=$(curl -fsS --max-time 20 --proto '=https' https://api.github.com/rate_limit) || stop RATE_LIMIT_READ
  rem=$(printf '%s' "$rl" | jq -er '.resources.core.remaining | select(type=="number")') || stop RATE_LIMIT_PARSE
  rst=$(printf '%s' "$rl" | jq -er '.resources.core.reset | select(type=="number")') || stop RATE_LIMIT_PARSE
  [[ -n "$RST0" ]] || RST0=$rst
  if (( rem >= WAIT_MIN )); then break; fi
  # una sola ventana: si ya pasó el primer reset (+2 min) y no hay cuota fresca, se termina; no se espera indefinidamente
  (( $(date -u +%s) < RST0 + 120 )) || stop NO_FRESH_WINDOW_ONE_WINDOW_ONLY
  printf 'QA525_OBS_WAIT_RUN_WAITING remaining=%s next_window_utc=%s\n' "$rem" "$(date -u -d "@$rst" +%FT%TZ)"
  sleep "$POLL"
done
(( $(date -u +%s) < LAUNCH_BY )) || stop TOO_CLOSE_TO_G6_DEADLINE_NOT_LAUNCHING
printf 'QA525_OBS_WAIT_RUN_WINDOW=OPEN remaining=%s\n' "$rem"
rc=0
if [[ "$MODE" == EXECUTE_OBSERVED_ATTEMPT ]]; then bash "$F" EXECUTE_OBSERVED_ATTEMPT || rc=$?; else bash "$F" || rc=$?; fi
printf 'QA525_OBS_WAIT_RUN_CHILD_EXIT=%s\n' "$rc"
exit "$rc"
