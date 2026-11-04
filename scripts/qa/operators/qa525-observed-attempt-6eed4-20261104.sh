#!/usr/bin/env bash
# QA525 · UN intento OBSERVADO de actualización API+WEB a 6eed4 (tras recuperación PASS y G6 nuevo).
# Autor: Claude. Revisor independiente requerido: ChatGPT. Requiere autorización NUEVA y específica del Sponsor para EXECUTE_OBSERVED_ATTEMPT.
# Sin argumento: SOLO LECTURA (A0). Con EXECUTE_OBSERVED_ATTEMPT: una sola `--review-and-run` del lanzador instalado (no se modifica),
# mientras un observador root de SOLO LECTURA graba eventos/estado/salud/logs de api y web en un directorio nuevo 0700 y, al terminar,
# imprime una línea de tiempo acotada y redactada que identifica la etapa del fallo. Sin reintento. Ejecutar siempre como archivo.
set -Eeuo pipefail
umask 077
SRC=6eed4f00b7917da3cca36bbda70e16d16328648d
DIGEST=sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2
OLD=41d9c490d138419c7286445f37e7aeddf0d1b8d5
REVIEW='https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6604960072'
# Pines del G6 NUEVO (los fija ChatGPT al cerrar el restore aislado; con los marcadores sin reemplazar el script NO ejecuta):
G6=__G6_ACTA_PATH__
G6SHA=__G6_ACTA_SHA256__
G6_ARCHIVE=centenario-qa-g6-CIFRADO-20261104T105358Z.tar.gz
G6_ARCHIVE_SHA=f7e2d142a62197323df5fb0efd936aaf96528aa85fe2aa7f3ec03bf4e65d9d9b
G6_CUTOFF='2026-11-06 10:00:00 UTC'   # archivo 10:53:58Z + 48 h = 11-06 10:53:58Z, con margen
L=/usr/local/sbin/pmgm-qa-update
STATE=/opt/centenario/qa525-signed-prepare
OBSROOT=/opt/centenario
OWNER=0:0
MIN_RL=58
INTERVAL=1
EVT=(/etc/pmgm/qa525-sensitive-reviewed.json /etc/pmgm/qa525-single-operator-postdb-approved.json /etc/pmgm/qa525-supervised-cut.json "$STATE/prepared.json" "$STATE/dual-$SRC.json" "$STATE/expired-prepared-archive-pending.json")
LOCKS=(/opt/centenario/qa-api-refresh/operation.lock /opt/centenario/qa-auto-lite/web-update.lock /run/lock/pmgm-master-qa-604.lock /run/lock/pmgm-qa754-web-hotfix.lock /run/lock/pmgm-qa754-api-activation.lock)
SITE=https://sistema.granlogiamixta.cl
# >>> FUNCTIONS (la prueba hermética carga exactamente este bloque)
stop() { printf 'QA525_OBSERVED=STOP_%s_NO_RETRY\n' "$1"; exit 2; }
priv() { [[ "$(sudo stat -c '%u:%g %a %h' -- "$1" 2>/dev/null || :)" == "$OWNER 600 1" ]] && sudo test -f "$1" && sudo test ! -L "$1"; }
quota_ok() {
  local rl rem rst
  rl=$(curl -fsS --max-time 20 --proto '=https' https://api.github.com/rate_limit) || stop RATE_LIMIT_READ
  rem=$(printf '%s' "$rl" | jq -er '.resources.core.remaining | select(type=="number")') || stop RATE_LIMIT_PARSE
  rst=$(printf '%s' "$rl" | jq -er '.resources.core.reset | select(type=="number")') || stop RATE_LIMIT_PARSE
  printf 'QA525_GITHUB_ANON_QUOTA remaining=%s reset_utc=%s\n' "$rem" "$(date -u -d "@$rst" +%FT%TZ)"
  (( rem >= MIN_RL )) || stop GITHUB_QUOTA_LOW_WAIT_FOR_RESET_THEN_RERUN
}
g6_ok() { (( $(date -u +%s) < $(date -u -d "$G6_CUTOFF" +%s) )) || stop G6_TOO_OLD_RENEW; }
pins_ok() {
  [[ "$G6" =~ ^/etc/pmgm/qa525-g6-reviewed-[0-9]{8}T[0-9]{6}Z\.json$ && "$G6SHA" =~ ^[a-f0-9]{64}$ ]] || stop G6_PINS_NOT_SET
  [[ "$G6" != "/etc/pmgm/qa525-g6-reviewed-20261102T110545Z.json" ]] || stop G6_OLD_ACTA
  [[ "$REVIEW" =~ ^https://github\.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-[1-9][0-9]*$ ]] || stop REVIEW_URL
}
clean_plane() {
  local f raw js b sv name ids
  for f in "${EVT[@]}"; do sudo test ! -e "$f" && sudo test ! -L "$f" || stop PLANE_NOT_CLEAN; done
  raw=$(sudo find "$STATE" -maxdepth 1 -name 'dual-*.json.next') || stop JOURNAL_NEXT_SCAN
  [[ -z "$raw" ]] || stop JOURNAL_NEXT_PRESENT
  raw=$(sudo find "$STATE" -maxdepth 1 -name 'dual-*.json') || stop JOURNAL_SCAN
  js=(); [[ -z "$raw" ]] || mapfile -t js <<<"$raw"
  for f in "${js[@]}"; do
    b=${f##*/}
    [[ "$b" =~ ^dual-([a-f0-9]{40})\.json$ ]] || stop JOURNAL_OTHER_NAME
    sv=${BASH_REMATCH[1]}
    priv "$f" || stop JOURNAL_OTHER_UNTRUSTED
    sudo jq -e --arg s "$sv" 'type=="object" and .status=="APPLIED" and .source_sha==$s' "$f" >/dev/null || stop JOURNAL_OTHER_NOT_APPLIED
  done
  for f in "${LOCKS[@]}"; do priv "$f" || stop LOCK_FILE_UNTRUSTED_OR_MISSING; done
  for name in api web; do
    ids=$(sudo docker ps --filter label=com.docker.compose.project=centenario-qa-lab2 --filter "label=com.docker.compose.service=$name" --format '{{.ID}}') || stop CONTAINER_LIST
    [[ "$ids" =~ ^[a-f0-9]{12,64}$ ]] || stop CONTAINER_CARDINALITY
    [[ "$(sudo docker inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$ids")" == "$OLD" ]] || stop RUNTIME_NOT_OLD
  done
  curl -fsS --max-time 30 --proto '=https' "$SITE/qa-version.json" | jq -e --arg s "$OLD" '.environment=="QA_SYNTHETIC" and .source_sha==$s' >/dev/null || stop PUBLIC_VERSION_NOT_OLD
}
events_loop() {   # root: docker events -> archivo (solo lectura)
  echo $$ > "$1/events.pid"
  exec docker events --since "$2" --filter type=container --format '{{.Time}} {{.Action}} {{index .Actor.Attributes "name"}} exit={{index .Actor.Attributes "exitCode"}}' > "$1/events.log" 2>&1
}
poll_loop() {     # root: estado/salud de api y web (solo cambios), journal, y logs de cada contenedor nuevo
  local n r id j lastj="" kids=() k
  declare -A seen=() last=()
  echo $$ > "$1/poll.pid"
  trap 'for k in "${kids[@]:-}"; do [[ -n "$k" ]] && kill "$k" 2>/dev/null || :; done; exit 0' TERM INT
  while :; do
    for n in api web; do
      r=$(docker inspect "centenario-qa-lab2-$n-1" 2>/dev/null | jq -c '{id:.[0].Id[0:12],st:.[0].State.Status,h:(.[0].State.Health.Status // null),ec:.[0].State.ExitCode,oom:.[0].State.OOMKilled,hl:((.[0].State.Health.Log // [])|last|{c:.ExitCode,o:(.Output[0:160])})}' 2>/dev/null) || r=""
      [[ -n "$r" ]] || r='{"id":null,"st":"absent"}'
      if [[ "${last[$n]:-}" != "$r" ]]; then printf '%s %s %s\n' "$(date -u +%FT%TZ)" "$n" "$r" >> "$1/poll.log"; last[$n]=$r; fi
      id=$(printf '%s' "$r" | jq -r '.id // empty')
      if [[ -n "$id" && -z "${seen[$n-$id]:-}" ]]; then
        seen[$n-$id]=1
        docker logs -f --timestamps "centenario-qa-lab2-$n-1" >> "$1/logs-$n-$id.log" 2>&1 &
        kids+=("$!")
      fi
    done
    j=$(jq -r '.status // empty' "$2" 2>/dev/null || :)
    if [[ "$j" != "$lastj" ]]; then printf '%s journal %s\n' "$(date -u +%FT%TZ)" "$j" >> "$1/poll.log"; lastj=$j; fi
    sleep "$3"
  done
}
observe_start() {   # root, SOLO LECTURA; todo a $OBS (0700 root, fuera de qa525-signed-prepare)
  local i=0
  sudo install -d -m 0700 -o root -g root -- "$OBS" || stop OBS_DIR
  sudo bash -c "umask 077; $(declare -f events_loop); events_loop \"\$@\"" _ "$OBS" "$(date -u +%FT%TZ)" &
  sudo bash -c "umask 077; $(declare -f poll_loop); poll_loop \"\$@\"" _ "$OBS" "$STATE/dual-$SRC.json" "$INTERVAL" &
  until sudo test -s "$OBS/events.pid" && sudo test -s "$OBS/poll.pid"; do i=$((i+1)); (( i < 50 )) || stop OBSERVER_NOT_STARTED; sleep 0.2; done
  sleep "$INTERVAL"
}
observe_stop() {
  local p pid
  for p in poll events; do
    pid=$(sudo cat "$OBS/$p.pid" 2>/dev/null || :)
    if [[ "$pid" =~ ^[0-9]+$ ]]; then
      if sudo kill "$pid" 2>/dev/null; then :; fi
    fi
  done
  sleep 1
  wait 2>/dev/null || :
}
summarize() {
  printf 'QA525_OBSERVED_TIMELINE dir=%s\n' "$OBS"
  printf -- '--- docker events api/web (hora accion contenedor exit) ---\n'
  sudo grep -aE 'centenario-qa-lab2-(api|web)-1' "$OBS/events.log" 2>/dev/null | cut -c1-200 | head -80 || :
  printf -- '--- estado/salud (solo cambios) ---\n'
  sudo cat "$OBS/poll.log" 2>/dev/null | cut -c1-330 | head -120 || :
  printf -- '--- journal final ---\n'
  sudo jq -c '{status,source_sha}' "$STATE/dual-$SRC.json" 2>/dev/null || printf 'sin journal\n'
  sudo tail -n 4 -- "$STATE/dual-events.log" 2>/dev/null | cut -c1-200 || :
  printf -- '--- lineas de error de los logs (filtradas y redactadas, max 60) ---\n'
  sudo sh -c 'cat "$1"/logs-*.log 2>/dev/null' _ "$OBS" | grep -aiE 'fail|error|exception|crit|fatal|unhealthy|unable|refused|denied|migrat|timeout|pending' | sed -E 's/(password|pwd|secret|token|apikey|api_key|authorization|bearer|connectionstring|conn[a-z]*|user id|username|host|server|key)[=: ]+[^ ,;"'\'']+/\1=[REDACTED]/Ig; s/[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+/[EMAIL]/g' | cut -c1-220 | head -60 || :
  printf -- '--- primera anomalia en un contenedor NUEVO ---\n'
  sudo awk -v old="$OLD_IDS" '$2 ~ /^(api|web)$/ { match($0, /"id":"[a-f0-9]+"/); id=substr($0, RSTART+6, RLENGTH-7); if (index(old, id)==0 && id!="" && ($0 ~ /"st":"(exited|dead|restarting)"/ || $0 ~ /"h":"unhealthy"/)) { print; found=1; exit } } END { if (!found) print "NO_NEW_CONTAINER_ANOMALY_SEEN" }' "$OBS/poll.log" 2>/dev/null || :
}
# <<< FUNCTIONS
[[ "$#" -le 1 ]] || stop BAD_ARGUMENT
MODE=${1:-READ_ONLY}
[[ "$MODE" == READ_ONLY || "$MODE" == EXECUTE_OBSERVED_ATTEMPT ]] || stop BAD_ARGUMENT
[[ "$(hostname -s)" == glmdch && "$(id -un)" == soporte ]] || stop HOST_OR_USER
command -v jq >/dev/null && command -v curl >/dev/null || stop TOOLS
pins_ok
sudo -v || stop SUDO
sudo test -f "$L" && sudo test ! -L "$L" || stop LAUNCHER_MISSING
printf '%s  %s\n' 61e3f1f7e7dd561567e5fac415a6fa9695c2f74fa86adde21dd8ed0330d03cd3 "$L" | sudo sha256sum -c - >/dev/null || stop LAUNCHER_HASH
priv "$G6" || stop G6_ACTA_MODE
printf '%s  %s\n' "$G6SHA" "$G6" | sudo sha256sum -c - >/dev/null || stop G6_ACTA_HASH
sudo jq -e --arg n "$G6_ARCHIVE" --arg h "$G6_ARCHIVE_SHA" '.g6_offhost_sha256_verified==true and .g6_archive_name==$n and .g6_archive_sha256==$h' "$G6" >/dev/null || stop G6_ACTA_CONTENT
g6_ok
quota_ok
s=$(sudo "$L" --status) || stop STATUS
printf '%s\n' "$s" | jq -e '.status=="QA525_UPDATE_STATUS_READ_ONLY" and .services_running==true and .prepared_exists==false and .archive_reconciliation_required==false and .may_apply==false' >/dev/null || stop STATUS_MISMATCH
clean_plane
sql='BEGIN READ ONLY; SELECT count(*)::text || chr(124) || count(*) FILTER (WHERE "MigrationId" = '\''20261029120000_AddMemberHistoryProposals'\'')::text || chr(124) || count(*) FILTER (WHERE "MigrationId" = '\''20261030223000_SeedSupportWorkshop00'\'')::text FROM public."__EFMigrationsHistory"; COMMIT;'
ef=$(sudo docker exec centenario-qa-lab2-postgres-1 sh -c 'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"' qa525-readonly "$sql") || stop EF_READ
[[ "$ef" == '100|1|1' ]] || stop EF_HISTORY_DRIFT
printf 'QA525_OBSERVED_A0=PASS_CLEAN_PLANE_RUNTIME_OLD\n'
if [[ "$MODE" == READ_ONLY ]]; then printf 'QA525_OBSERVED=READ_ONLY_PASS_NO_CHANGES\n'; exit 0; fi
# ---- UN intento observado -------------------------------------------------------------
OLD_IDS=$(for n in api web; do sudo docker ps -q --filter label=com.docker.compose.project=centenario-qa-lab2 --filter "label=com.docker.compose.service=$n" | cut -c1-12; done | tr '\n' ' ')
OBS="$OBSROOT/qa525-observe-$(date -u +%Y%m%dT%H%M%SZ)-$$"
trap 'observe_stop' EXIT
g6_ok
quota_ok
observe_start
if result=$(sudo "$L" --review-and-run --source "$SRC" --digest "$DIGEST" --technical-review-reference "$REVIEW" --g6-review "$G6" --operator-name soporte@glmdch --schema-mode support00_postdb); then rc=0; else rc=$?; fi
sleep "$INTERVAL"; sleep "$INTERVAL"
observe_stop
trap - EXIT
printf '%s\n' "$result" | jq -c '{status,source_sha,code,may_apply}' || true
summarize
[[ "$rc" == 0 ]] || stop LAUNCHER_BLOCKED_OR_INCOMPLETE_SEE_TIMELINE
printf '%s\n' "$result" | jq -e --arg s "$SRC" '.status=="DUAL_API_WEB_APPLIED" and .source_sha==$s' >/dev/null || stop APPLY_RESPONSE_NOT_CONFIRMED
n="$STATE/dual-$SRC.json"
priv "$n" || stop JOURNAL_UNTRUSTED
sudo jq -e --arg s "$SRC" '.status=="APPLIED" and .source_sha==$s' "$n" >/dev/null || stop JOURNAL_NOT_APPLIED
for name in api web; do
  ids=$(sudo docker ps --filter label=com.docker.compose.project=centenario-qa-lab2 --filter "label=com.docker.compose.service=$name" --format '{{.ID}}') || stop POST_CONTAINER_LIST
  [[ "$ids" =~ ^[a-f0-9]{12,64}$ ]] || stop POST_CONTAINER_CARDINALITY
  [[ "$(sudo docker inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$ids")" == "$SRC" ]] || stop POST_IMAGE_REVISION
done
curl -fsS --max-time 30 --proto '=https' "$SITE/qa-version.json" | jq -e --arg s "$SRC" '.environment=="QA_SYNTHETIC" and .source_sha==$s' >/dev/null || stop POST_WEB_VERSION
for p in /health/ready /identity/realms/pmgm/.well-known/openid-configuration; do curl -fsS -o /dev/null --max-time 30 --proto '=https' "$SITE$p" || stop POST_HEALTH; done
printf 'QA525_OBSERVED=PASS_DUAL_API_WEB_APPLIED\n'
