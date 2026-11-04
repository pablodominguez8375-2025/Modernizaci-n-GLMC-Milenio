#!/usr/bin/env bash
# QA525 · RECUPERACIÓN SIN DESPLIEGUE tras el intento A2 a 6eed4 (journal RECOVERY_MANUALLY_REQUIRED, runtime 41d9 restaurado).
# Autor: Claude. Revisor independiente requerido: ChatGPT. Requiere autorización NUEVA y específica del Sponsor para EXECUTE_RECOVERY.
# Sin argumento: SOLO LECTURA (A0 + diagnóstico acotado). Con EXECUTE_RECOVERY: archiva (mv, no borra) los 5 registros 6eed4 bajo los 5 locks.
# NO despliega, NO invoca el actualizador, NO toca Docker/BD/red/dev/main, NO toca la custodia cfe4 ni journals históricos.
# Ejecutar siempre como archivo: `bash archivo [EXECUTE_RECOVERY]`.
set -Eeuo pipefail
umask 077
SRC=6eed4f00b7917da3cca36bbda70e16d16328648d
DIGEST=sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2
PEND=cfe4e6573ece2ae81414cc252047d36cc56e5c73
OLD=41d9c490d138419c7286445f37e7aeddf0d1b8d5
CUSTODY_DIR=route-a-custody-20261104T012737Z-3429524
L=/usr/local/sbin/pmgm-qa-update
STATE=/opt/centenario/qa525-signed-prepare
OWNER=0:0
J="$STATE/dual-$SRC.json"; P="$STATE/prepared.json"; M="$STATE/expired-prepared-archive-pending.json"
E="$STATE/dual-events.log"; CUT=/etc/pmgm/qa525-supervised-cut.json
F=("$P" /etc/pmgm/qa525-sensitive-reviewed.json /etc/pmgm/qa525-single-operator-postdb-approved.json "$CUT" "$J")
LOCKS=(/opt/centenario/qa-api-refresh/operation.lock /opt/centenario/qa-auto-lite/web-update.lock /run/lock/pmgm-master-qa-604.lock /run/lock/pmgm-qa754-web-hotfix.lock /run/lock/pmgm-qa754-api-activation.lock)
SITE=https://sistema.granlogiamixta.cl
# >>> FUNCTIONS (la prueba hermética carga exactamente este bloque)
stop() { printf 'QA525_RECOVERY=STOP_%s_NO_RETRY\n' "$1"; exit 2; }
priv() { [[ "$(sudo stat -c '%u:%g %a %h' -- "$1" 2>/dev/null || :)" == "$OWNER 600 1" ]] && sudo test -f "$1" && sudo test ! -L "$1"; }
ev_has() { local rc=0; sudo grep -Eq "$1[: ]+$SRC\$" "$E" || rc=$?; case "$rc" in 0) return 0;; 1) return 1;; *) stop EVENTS_READ_ERROR;; esac; }
custody_ok() {
  local A="$STATE/$CUSTODY_DIR" f b h lh n
  [[ "$CUSTODY_DIR" =~ ^route-a-custody-[0-9TZ]+-[0-9]+$ ]] || stop CUSTODY_NAME
  [[ -d "$A" && ! -L "$A" && "$(sudo stat -c '%u:%g %a' -- "$A")" == "$OWNER 700" ]] || stop CUSTODY_DIR_UNTRUSTED
  for f in ledger.json receipt.json; do priv "$A/$f" || stop CUSTODY_RECORD_UNTRUSTED; done
  lh=$(sudo sha256sum -- "$A/ledger.json" | cut -d' ' -f1)
  sudo jq -e --arg s "$PEND" --arg l "$lh" '.status=="ROUTE_A_CUSTODY_COMPLETE_NO_DEPLOYMENT" and .source_sha==$s and .ledger_sha256==$l and (.files|length)==5' "$A/receipt.json" >/dev/null || stop CUSTODY_RECEIPT
  n=0
  while IFS=$'\t' read -r b h; do
    [[ "$b" =~ ^[A-Za-z0-9._-]+$ && "$h" =~ ^[a-f0-9]{64}$ ]] || stop CUSTODY_LEDGER_FORMAT
    priv "$A/$b" || stop CUSTODY_FILE_UNTRUSTED
    [[ "$(sudo sha256sum -- "$A/$b" | cut -d' ' -f1)" == "$h" ]] || stop CUSTODY_HASH
    n=$((n+1))
  done < <(sudo jq -r '.files[]|[.name,.sha256]|@tsv' "$A/ledger.json")
  [[ "$n" == 5 ]] || stop CUSTODY_COUNT
}
state_ok() {
  local f name line cid img st pj sv rev nonce names js raw b rc cnt fp im
  for f in "${F[@]}"; do priv "$f" || stop EVIDENCE_FILE_UNTRUSTED; done
  sudo test ! -e "$M" && sudo test ! -L "$M" || stop ARCHIVE_MARKER_PRESENT
  raw=$(sudo find "$STATE" -maxdepth 1 -name 'dual-*.json.next') || stop JOURNAL_NEXT_SCAN
  [[ -z "$raw" ]] || stop JOURNAL_NEXT_PRESENT
  raw=$(sudo find "$STATE" -maxdepth 1 -name 'dual-*.json') || stop JOURNAL_SCAN
  js=(); [[ -z "$raw" ]] || mapfile -t js <<<"$raw"
  cnt=0
  for f in "${js[@]}"; do
    if [[ "$f" == "$J" ]]; then cnt=$((cnt+1)); continue; fi
    b=${f##*/}
    [[ "$b" =~ ^dual-([a-f0-9]{40})\.json$ ]] || stop JOURNAL_OTHER_NAME
    sv=${BASH_REMATCH[1]}
    priv "$f" || stop JOURNAL_OTHER_UNTRUSTED
    sudo jq -e --arg s "$sv" 'type=="object" and .status=="APPLIED" and .source_sha==$s' "$f" >/dev/null || stop JOURNAL_OTHER_NOT_APPLIED
  done
  [[ "$cnt" == 1 ]] || stop JOURNAL_TARGET_NOT_UNIQUE
  custody_ok
  sudo jq -e --arg s "$SRC" '.status=="RECOVERY_MANUALLY_REQUIRED" and .source_sha==$s' "$J" >/dev/null || stop JOURNAL_NOT_RECOVERY_REQUIRED
  sudo jq -e --arg s "$SRC" --arg d "$DIGEST" '.status=="PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT" and .source_sha==$s and .source_asset_sha256==$d and .schema_mode=="support00_postdb" and .review_mode=="single_operator_qa" and .may_apply==false and .images_built==true and .services_unchanged==true' "$P" >/dev/null || stop PREPARED_MISMATCH
  sudo jq -e --arg s "$SRC" --arg d "$DIGEST" '.source_sha==$s and .release_asset_sha256==$d' /etc/pmgm/qa525-sensitive-reviewed.json >/dev/null || stop SENSITIVE_REVIEW_MISMATCH
  sudo jq -e --arg s "$SRC" --arg d "$DIGEST" '.source_sha==$s and .release_asset_sha256==$d' /etc/pmgm/qa525-single-operator-postdb-approved.json >/dev/null || stop POSTDB_MISMATCH
  sudo jq -e --arg s "$SRC" --arg d "$DIGEST" '.target_sha==$s and .release_asset_sha256==$d' "$CUT" >/dev/null || stop CUT_MISMATCH
  nonce=$(sudo jq -r '.nonce' "$CUT") || stop NONCE_READ
  [[ "$nonce" =~ ^[a-f0-9]{64}$ ]] || stop NONCE_INVALID
  priv "$STATE/approval-consumed-$nonce" || stop NONCE_MARKER_UNTRUSTED
  [[ "$(sudo cat -- "$STATE/approval-consumed-$nonce")" == "$SRC" ]] || stop NONCE_NOT_CONSUMED_FOR_CUT
  for name in api web; do
    sudo jq -e --arg n "$name" --slurpfile p "$P" '.old_images[$n]==$p[0].baseline[$n].image_id and .new_images[$n]==$p[0][$n+"_image_id"]' "$J" >/dev/null || stop JOURNAL_PREPARED_BINDING
  done
  priv "$E" || stop EVENTS_UNTRUSTED
  ev_has DUAL_API_WEB_RECOVERY_REQUIRED || stop RECOVERY_EVENT_MISSING
  ! ev_has DUAL_API_WEB_APPLIED || stop TARGET_APPLIED_EVENT
  ! ev_has DUAL_API_WEB_ROLLED_BACK || stop TARGET_ROLLED_BACK_EVENT
  mapfile -t names < <(sudo jq -r '.baseline|keys[]' "$P") || stop BASELINE_KEYS
  [[ " ${names[*]} " == *" api "* && " ${names[*]} " == *" web "* ]] || stop BASELINE_API_WEB_MISSING
  for name in "${names[@]}"; do
    [[ "$name" =~ ^[a-z0-9_-]{1,40}$ ]] || stop BASELINE_NAME
    line=$(sudo docker inspect --format '{{.Id}} {{.Image}} {{.State.Status}} {{index .Config.Labels "com.docker.compose.project"}} {{index .Config.Labels "com.docker.compose.service"}} {{index .Config.Labels "org.opencontainers.image.revision"}}' "centenario-qa-lab2-$name-1") || stop BASELINE_INSPECT
    read -r cid img st pj sv rev <<<"$line"
    [[ "$st" == running && "$pj" == centenario-qa-lab2 && "$sv" == "$name" ]] || stop BASELINE_STATE
    sudo jq -e --arg n "$name" --arg i "$img" '.baseline[$n].image_id==$i' "$P" >/dev/null || stop BASELINE_IMAGE_DRIFT
    if [[ "$name" == api || "$name" == web ]]; then
      [[ "$rev" == "$OLD" ]] || stop TARGET_NOT_OLD_REVISION
    else
      sudo jq -e --arg n "$name" --arg c "$cid" '.baseline[$n].container_fingerprint==$c' "$P" >/dev/null || stop DEPENDENCY_CONTAINER_CHANGED
    fi
  done
  for name in api web; do
    im=$(sudo jq -r --arg n "$name" '.new_images[$n]' "$J")
    [[ "$im" =~ ^sha256:[a-f0-9]{64}$ ]] || stop CANDIDATE_ID
    [[ "$(sudo docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$im")" == "$SRC" ]] || stop CANDIDATE_IMAGE_REVISION
  done
  curl -fsS --max-time 30 --proto '=https' "$SITE/qa-version.json" | jq -e --arg s "$OLD" '.environment=="QA_SYNTHETIC" and .source_sha==$s' >/dev/null || stop PUBLIC_VERSION_NOT_OLD
  for f in /health/ready /identity/realms/pmgm/.well-known/openid-configuration; do curl -fsS -o /dev/null --max-time 30 --proto '=https' "$SITE$f" || stop PUBLIC_HEALTH; done
}
recover() {
  local A f b lh mh files
  declare -A H
  state_ok
  A="$STATE/route-a-recovery-$(date -u +%Y%m%dT%H%M%SZ)-$$"
  mkdir -m 700 -- "$A" || stop ARCHIVE_DIR
  for f in "${F[@]}"; do
    b=${f##*/}; [[ -z "${H[$b]:-}" ]] || stop BASENAME_COLLISION
    H[$b]=$(sha256sum -- "$f" | cut -d' ' -f1); [[ "${H[$b]}" =~ ^[a-f0-9]{64}$ ]] || stop HASH_BEFORE
  done
  files=$(for f in "${F[@]}"; do b=${f##*/}; printf '%s\t%s\t%s\n' "$b" "$f" "${H[$b]}"; done | jq -R -s -c 'split("\n")[:-1]|map(split("\t")|{name:.[0],from:.[1],sha256:.[2]})') || stop LEDGER_BUILD
  ( set -C; jq -n -c --arg s "$SRC" --arg a "${A##*/}" --argjson f "$files" '{status:"RECOVERY_ARCHIVE_LEDGER",source_sha:$s,archive:$a,files:$f}' > "$A/ledger.json" ) || stop LEDGER_WRITE
  priv "$A/ledger.json" || stop LEDGER_MODE
  sync -f "$A/ledger.json"; sync -f "$A"; sync -f "$STATE"
  lh=$(sha256sum -- "$A/ledger.json" | cut -d' ' -f1)
  ( set -C; jq -n -c --arg s "$SRC" --arg a "${A##*/}" --arg l "$lh" '{status:"RECOVERY_ARCHIVE_INCOMPLETE",source_sha:$s,archive:$a,ledger_sha256:$l}' > "$M" ) || stop MARKER_CREATE
  priv "$M" || stop MARKER_MODE
  sync -f "$M"; sync -f "$STATE"
  for f in "${F[@]}"; do
    b=${f##*/}
    mv -n -- "$f" "$A/$b" || stop MOVE
    [[ ! -e "$f" && ! -L "$f" && -f "$A/$b" && ! -L "$A/$b" ]] || stop MOVE_VERIFY
    [[ "$(sha256sum -- "$A/$b" | cut -d' ' -f1)" == "${H[$b]}" ]] || stop HASH_AFTER
    sync -f "$A/$b"; sync -f "$A"; sync -f "${f%/*}"
  done
  ( set -C; jq -n -c --arg s "$SRC" --arg l "$lh" --argjson f "$files" '{status:"MANUAL_RECOVERY_CONFIRMED_BASELINE_RESTORED_NO_DEPLOYMENT",source_sha:$s,ledger_sha256:$l,files:$f,nonce_remains_consumed:true,events_and_candidate_images_preserved:true}' > "$A/receipt.json" ) || stop RECEIPT_WRITE
  priv "$A/receipt.json" || stop RECEIPT_MODE
  jq -e '.status=="MANUAL_RECOVERY_CONFIRMED_BASELINE_RESTORED_NO_DEPLOYMENT" and (.files|length)==5' "$A/receipt.json" >/dev/null || stop RECEIPT_FORMAT
  sync -f "$A/receipt.json"; sync -f "$A"
  mh=$(sha256sum -- "$M" | cut -d' ' -f1)
  mv -n -- "$M" "$A/marker-cleared.json" || stop MARKER_CLEAR
  [[ ! -e "$M" && ! -L "$M" && "$(sha256sum -- "$A/marker-cleared.json" | cut -d' ' -f1)" == "$mh" ]] || stop MARKER_CLEAR_VERIFY
  sync -f "$A"; sync -f "$STATE"
  for f in "${F[@]}"; do b=${f##*/}; [[ ! -e "$f" && ! -L "$f" ]] || stop POST_ABSENT; [[ "$(sha256sum -- "$A/$b" | cut -d' ' -f1)" == "${H[$b]}" ]] || stop POST_HASH; done
  printf 'QA525_RECOVERY=PASS_ARCHIVED archive=%s\n' "${A##*/}"
}
lock_hook() { :; }  # punto de inyección SOLO para pruebas herméticas; en producción no hace nada
hold_locks() {
  local l fd got n=0
  declare -gA WANT=()
  for l in "${LOCKS[@]}"; do
    [[ -f "$l" && ! -L "$l" && "$(stat -c '%u:%g %a %h' -- "$l")" == "$OWNER 600 1" ]] || stop LOCK_FILE_UNTRUSTED_OR_MISSING
    WANT[$l]=$(stat -c '%d:%i' -- "$l") || stop LOCK_STAT
  done
  for l in "${LOCKS[@]}"; do
    lock_hook pre_open "$l"
    { exec {fd}<"$l"; } 2>/dev/null || stop LOCK_OPEN_FAILED_NOT_CREATED
    lock_hook pre_acquire "$l"
    flock -n "$fd" || stop LOCKS_BUSY_ANOTHER_OPERATOR
    lock_hook post_acquire "$l"
    got=$(stat -L -c '%d:%i %u:%g %a %h' "/proc/self/fd/$fd") || stop LOCK_FD_STAT
    [[ "$got" == "${WANT[$l]} $OWNER 600 1" && -f "/proc/self/fd/$fd" ]] || stop LOCK_IDENTITY_CHANGED
    [[ ! -L "$l" && "$(stat -c '%d:%i' -- "$l")" == "${WANT[$l]}" ]] || stop LOCK_PATH_REPLACED
    n=$((n+1))
  done
  [[ "$n" == "${#LOCKS[@]}" ]] || stop LOCKS_NOT_ALL_HELD
}
run_recovery_locked() {
  local l body rc=0
  for l in "${LOCKS[@]}"; do priv "$l" || stop LOCK_FILE_UNTRUSTED_OR_MISSING; done
  body="set -Eeuo pipefail; umask 077; $(declare -p SRC DIGEST PEND OLD CUSTODY_DIR STATE OWNER J P M E CUT F LOCKS SITE); $(declare -f stop priv ev_has custody_ok state_ok recover lock_hook hold_locks); sudo() { \"\$@\"; }; hold_locks; recover"
  sudo bash -c "$body" || rc=$?
  [[ "$rc" == 0 ]] || stop RECOVERY_FAILED_SEE_ABOVE_MARKER_MAY_REMAIN
}
diag() {
  # SOLO LECTURA, campos acotados (sin entorno, sin logs de aplicación, sin secretos).
  printf 'QA525_DIAG journal=%s\n' "$(sudo jq -c '{status,source_sha,old_images,new_images}' "$J")"
  printf 'QA525_DIAG events_tail:\n'; sudo tail -n 12 -- "$E" | cut -c1-200
  printf 'QA525_DIAG docker_events_api_web (accion/hora/nombre/exit/health):\n'
  timeout 20 sudo docker events --since 2026-11-04T01:20:00Z --until "$(date -u +%FT%TZ)" --filter type=container --filter event=create --filter event=start --filter event=die --filter event=kill --filter event=destroy --filter event=health_status --format '{{.Time}} {{.Action}} {{index .Actor.Attributes "name"}} exit={{index .Actor.Attributes "exitCode"}}' 2>/dev/null | grep -E 'centenario-qa-lab2-(api|web)-1' | head -60 || :
}
# <<< FUNCTIONS
[[ "$#" -le 1 ]] || stop BAD_ARGUMENT
MODE=${1:-READ_ONLY}
[[ "$MODE" == READ_ONLY || "$MODE" == EXECUTE_RECOVERY ]] || stop BAD_ARGUMENT
[[ "$(hostname -s)" == glmdch && "$(id -un)" == soporte ]] || stop HOST_OR_USER
command -v jq >/dev/null && command -v curl >/dev/null || stop TOOLS
sudo -v || stop SUDO
sudo test -f "$L" && sudo test ! -L "$L" || stop LAUNCHER_MISSING
printf '%s  %s\n' 61e3f1f7e7dd561567e5fac415a6fa9695c2f74fa86adde21dd8ed0330d03cd3 "$L" | sudo sha256sum -c - >/dev/null || stop LAUNCHER_HASH
for l in "${LOCKS[@]}"; do priv "$l" || stop LOCK_FILE_UNTRUSTED_OR_MISSING; done
s=$(sudo "$L" --status) || stop STATUS
printf '%s\n' "$s" | jq -e '.status=="QA525_UPDATE_STATUS_READ_ONLY" and .services_running==true and .prepared_exists==true and .archive_reconciliation_required==false and .may_apply==false' >/dev/null || stop STATUS_MISMATCH
state_ok
sql='BEGIN READ ONLY; SELECT count(*)::text || chr(124) || count(*) FILTER (WHERE "MigrationId" = '\''20261029120000_AddMemberHistoryProposals'\'')::text || chr(124) || count(*) FILTER (WHERE "MigrationId" = '\''20261030223000_SeedSupportWorkshop00'\'')::text FROM public."__EFMigrationsHistory"; COMMIT;'
ef=$(sudo docker exec centenario-qa-lab2-postgres-1 sh -c 'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"' qa525-readonly "$sql") || stop EF_READ
[[ "$ef" == '100|1|1' ]] || stop EF_HISTORY_DRIFT
printf 'QA525_RECOVERY_A0=PASS_BASELINE_RESTORED_EVIDENCE_PENDING_ARCHIVE runtime=%s\n' "$OLD"
diag || :
if [[ "$MODE" == READ_ONLY ]]; then printf 'QA525_RECOVERY=READ_ONLY_PASS_NO_CHANGES\n'; exit 0; fi
run_recovery_locked
s=$(sudo "$L" --status) || stop POST_STATUS
printf '%s\n' "$s" | jq -e '.services_running==true and .prepared_exists==false and .archive_reconciliation_required==false and .may_apply==false' >/dev/null || stop POST_STATUS_MISMATCH
for name in api web; do
  ids=$(sudo docker ps --filter label=com.docker.compose.project=centenario-qa-lab2 --filter "label=com.docker.compose.service=$name" --format '{{.ID}}') || stop POST_CONTAINER_LIST
  [[ "$ids" =~ ^[a-f0-9]{12,64}$ ]] || stop POST_CONTAINER_CARDINALITY
  [[ "$(sudo docker inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$ids")" == "$OLD" ]] || stop POST_NOT_OLD
done
printf 'QA525_RECOVERY=PASS_BASELINE_RESTORED_ARCHIVED_NO_DEPLOYMENT\n'
