#!/usr/bin/env bash
# QA525 · APLICACIÓN MANUAL SUPERVISADA API+WEB da274d65 (ruta B de contingencia). Autor: Claude. Revisor independiente requerido: ChatGPT.
# Requiere autorización NUEVA y específica del Sponsor para EXECUTE_MANUAL_APPLY. Sin argumento: SOLO LECTURA (A0 + prerequisitos).
# Aplica SOLO las imágenes ya construidas y firmadas de da274d65 (api/web) con los mismos override y capas que usa el operador, pero
# ESPERA la salud real (la API tarda ~6 s en escuchar) antes de verificar, y revierte ambos servicios ante cualquier fallo.
# NO toca el journal del operador, nonces, eventos, BD, dev/main, ni dependencias. Escribe su propio recibo manual-apply-*.json.
# Ejecutar siempre como archivo: `bash archivo [EXECUTE_MANUAL_APPLY]`.
set -Eeuo pipefail
umask 077
SRC=da274d651d8e9e056525413406c0aab62131504f
DIGEST=sha256:f869f3e12e048d70aaecf5e4e2f5b348d6378cf02aaa9fc0e5ee3243460f2e82
OLD=6eed4f00b7917da3cca36bbda70e16d16328648d
G6_END=1795129839
WORKDIR=/opt/centenario/qa-lab-firstboot/recovery-v2
ENVF=/opt/centenario/qa-auto-lite/empty.env
PROJ=centenario-qa-lab2
DEPS=(keycloak postgres minio gateway clamav)
ONES=(db-grants migrator minio-init)
HPATHS=(/ /health/web /health/ready /identity/realms/pmgm/.well-known/openid-configuration)
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
  [[ " ${names[*]} " == *" api "* && " ${names[*]} " == *" web "* && "${#names[@]}" == 7 ]] || stop BASELINE_API_WEB_MISSING
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
prereq_ok() {
  local n k f s img p ls
  for n in api web; do
    mapfile -t ls < <(sudo jq -r --arg n "$n" '.original_layers[$n][]' "$J") || stop LAYERS_READ
    [[ "${#ls[@]}" -ge 2 ]] || stop LAYERS_EMPTY
    for f in "${ls[@]}"; do
      [[ "$f" == /* && "$f" != *..* ]] || stop LAYER_PATH
      sudo test -f "$f" && sudo test ! -L "$f" && [[ "$(sudo stat -c '%u' -- "$f")" == 0 && -z "$(sudo find "$f" -maxdepth 0 -perm /022)" ]] || stop LAYER_UNTRUSTED
    done
  done
  for s in api web; do
    for k in new old; do
      img=$(sudo jq -r --arg k "${k}_images" --arg s "$s" '.[$k][$s]' "$J") || stop IMAGE_READ
      [[ "$img" =~ ^sha256:[a-f0-9]{64}$ ]] || stop IMAGE_ID_FORMAT
      p="$STATE/qa525-$s-$SRC-${img#sha256:}.json"
      priv "$p" || stop OVERRIDE_MISSING_OR_UNTRUSTED
      [[ "$(sudo cat -- "$p")" == "$(printf '{"services": {"%s": {"image": "%s"}}}' "$s" "$img")" ]] || stop OVERRIDE_CONTENT
    done
  done
}
apply_manual() {
  local n i f b out ts R RS new_api new_web old_api old_web ov line cid
  declare -A LAY BEFORE
  state_ok
  [[ "$(date -u +%s)" -lt $((G6_END-3600)) ]] || stop G6_WINDOW_TOO_CLOSE_OR_EXPIRED
  new_api=$(jq -r '.new_images.api' "$J"); new_web=$(jq -r '.new_images.web' "$J")
  old_api=$(jq -r '.old_images.api' "$J"); old_web=$(jq -r '.old_images.web' "$J")
  for i in "$new_api" "$new_web" "$old_api" "$old_web"; do [[ "$i" =~ ^sha256:[a-f0-9]{64}$ ]] || stop IMAGE_ID_FORMAT; done
  [[ "$new_api" != "$new_web" && "$new_api" != "$old_api" && "$new_web" != "$old_web" ]] || stop IMAGE_IDS_NOT_DISTINCT
  for n in api web; do
    LAY[$n]=$(jq -r --arg n "$n" '.original_layers[$n][]' "$J") || stop LAYERS_READ
    [[ -n "${LAY[$n]}" ]] || stop LAYERS_EMPTY
    while IFS= read -r f; do
      [[ "$f" == /* && "$f" != *..* ]] || stop LAYER_PATH
      [[ -f "$f" && ! -L "$f" && "$(stat -c '%u' -- "$f")" == 0 && -z "$(find "$f" -maxdepth 0 -perm /022)" ]] || stop LAYER_UNTRUSTED
    done <<<"${LAY[$n]}"
  done
  ov() { local s=$1 img=$2 p="$STATE/qa525-$1-$SRC-${2#sha256:}.json"
    [[ -f "$p" && ! -L "$p" && "$(stat -c '%u:%g %a %h' -- "$p")" == "$OWNER 600 1" ]] || stop OVERRIDE_MISSING_OR_UNTRUSTED >&2
    [[ "$(cat -- "$p")" == "$(printf '{"services": {"%s": {"image": "%s"}}}' "$s" "$img")" ]] || stop OVERRIDE_CONTENT >&2
    printf '%s' "$p"; }
  local OV_NEW_API OV_NEW_WEB OV_OLD_API OV_OLD_WEB
  OV_NEW_API=$(ov api "$new_api"); OV_NEW_WEB=$(ov web "$new_web"); OV_OLD_API=$(ov api "$old_api"); OV_OLD_WEB=$(ov web "$old_web")
  info() { docker inspect --format '{{.Id}} {{.Image}} {{.State.Status}} {{index .Config.Labels "com.docker.compose.project"}} {{index .Config.Labels "com.docker.compose.service"}} {{index .Config.Labels "org.opencontainers.image.revision"}}' "$PROJ-$1-1"; }
  for n in "${DEPS[@]}" "${ONES[@]}"; do line=$(info "$n") || stop BEFORE_INSPECT; read -r cid _ <<<"$line"; BEFORE[$n]=$cid; done
  compose() { # service override
    local s=$1 e=$2 args=(compose --project-name "$PROJ" --project-directory "$WORKDIR" --env-file "$ENVF") f
    while IFS= read -r f; do args+=(--file "$f"); done <<<"${LAY[$s]}"
    args+=(--file "$e" up --detach --no-deps --no-build --pull never --force-recreate --wait --wait-timeout 90 "$s")
    ( cd "$WORKDIR" && docker "${args[@]}" >/dev/null 2>&1 ); }
  ready() { # espera salud real y versión pública $1 (hasta ~150 s)
    local k p c
    for k in $(seq 1 75); do
      c=0
      for p in "${HPATHS[@]}"; do curl -fsS -o /dev/null --max-time 12 --proto '=https' "$SITE$p" 2>/dev/null || c=1; done
      if [[ "$c" == 0 ]] && curl -fsS --max-time 12 --proto '=https' "$SITE/qa-version.json" 2>/dev/null | jq -e --arg s "$1" '.environment=="QA_SYNTHETIC" and .source_sha==$s' >/dev/null; then return 0; fi
      sleep 2
    done; return 1; }
  verify() { # api_img web_img
    local n line cid img st
    for n in api web; do line=$(info "$n") || return 1; read -r cid img st _ <<<"$line"; [[ "$st" == running ]] || return 1
      [[ "$n" == api && "$img" == "$1" || "$n" == web && "$img" == "$2" ]] || return 1; done
    for n in "${DEPS[@]}" "${ONES[@]}"; do line=$(info "$n") || return 1; read -r cid img st _ <<<"$line"; [[ "$cid" == "${BEFORE[$n]}" ]] || return 1; done
    for n in "${DEPS[@]}"; do line=$(info "$n") || return 1; read -r cid img st _ <<<"$line"; [[ "$st" == running ]] || return 1; done; }
  forward() { compose api "$OV_NEW_API" && ready_api && compose web "$OV_NEW_WEB" && ready "$SRC" && verify "$new_api" "$new_web"; }
  ready_api() { local k; for k in $(seq 1 45); do curl -fsS -o /dev/null --max-time 12 --proto '=https' "$SITE/health/ready" 2>/dev/null && return 0; sleep 2; done; return 1; }
  rollback() { compose web "$OV_OLD_WEB" || :; compose api "$OV_OLD_API" || :; ready "$OLD" && verify "$old_api" "$old_web"; }
  ts=$(date -u +%Y%m%dT%H%M%SZ); RS="$STATE/manual-apply-$SRC-$ts.started.json"; R="$STATE/manual-apply-$SRC-$ts.result.json"
  receipt() { # status path
    ( set -C; jq -n -c --arg s "$SRC" --arg st "$1" --arg t "$ts" --arg na "$new_api" --arg nw "$new_web" --arg oa "$old_api" --arg ow "$old_web" '{status:$st,source_sha:$s,started:$t,new_images:{api:$na,web:$nw},old_images:{api:$oa,web:$ow},operator_journal_untouched:true}' > "$2" ) && chmod 600 "$2" && sync -f "$2"; }
  receipt MANUAL_APPLY_STARTED "$RS" || stop RECEIPT_START
  if forward; then receipt MANUAL_APPLY_VERIFIED_API_WEB "$R" || stop RECEIPT_RESULT; printf 'QA525_MANUAL_APPLY=PASS_VERIFIED api=%s web=%s\n' "$new_api" "$new_web"; return 0; fi
  if rollback; then receipt MANUAL_APPLY_ROLLED_BACK_VERIFIED "$R" || :; stop MANUAL_APPLY_FAILED_ROLLED_BACK_OK; fi
  receipt MANUAL_APPLY_ROLLBACK_FAILED_MANUAL_RECOVERY "$R" || :; stop MANUAL_APPLY_ROLLBACK_FAILED_MANUAL_RECOVERY
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
run_apply_locked() {
  local l body rc=0
  for l in "${LOCKS[@]}"; do priv "$l" || stop LOCK_FILE_UNTRUSTED_OR_MISSING; done
  body="set -Eeuo pipefail; umask 077; $(declare -p SRC DIGEST OLD G6_END WORKDIR ENVF PROJ DEPS ONES HPATHS STATE OWNER J P M E CUT F LOCKS SITE); $(declare -f stop priv ev_has state_ok prereq_ok lock_hook hold_locks apply_manual); sudo() { \"\$@\"; }; hold_locks; apply_manual"
  sudo bash -c "$body" || rc=$?
  [[ "$rc" == 0 ]] || stop MANUAL_APPLY_FAILED_SEE_ABOVE
}
# <<< FUNCTIONS
[[ "$#" -le 1 ]] || stop BAD_ARGUMENT
MODE=${1:-READ_ONLY}
[[ "$MODE" == READ_ONLY || "$MODE" == EXECUTE_MANUAL_APPLY ]] || stop BAD_ARGUMENT
[[ "$(hostname -s)" == glmdch && "$(id -un)" == soporte ]] || stop HOST_OR_USER
command -v jq >/dev/null && command -v curl >/dev/null || stop TOOLS
sudo -v || stop SUDO
sudo test -f "$L" && sudo test ! -L "$L" || stop LAUNCHER_MISSING
printf '%s  %s\n' 7495fd65a51ecf83ded149b9467180e7470ceaee278e33a2ff7bd7e5b6faa562 "$L" | sudo sha256sum -c - >/dev/null || stop LAUNCHER_HASH
for l in "${LOCKS[@]}"; do priv "$l" || stop LOCK_FILE_UNTRUSTED_OR_MISSING; done
s=$(sudo "$L" --status) || stop STATUS
printf '%s\n' "$s" | jq -e '.status=="QA525_UPDATE_STATUS_READ_ONLY" and .services_running==true and .prepared_exists==true and .archive_reconciliation_required==false and .may_apply==false' >/dev/null || stop STATUS_MISMATCH
state_ok
prereq_ok
[[ "$(date -u +%s)" -lt $((G6_END-3600)) ]] || stop G6_WINDOW_TOO_CLOSE_OR_EXPIRED
printf 'QA525_MANUAL_A0=PASS_BASELINE_RESTORED_READY_TO_APPLY runtime=%s\n' "$OLD"
if [[ "$MODE" == READ_ONLY ]]; then printf 'QA525_MANUAL_APPLY=READ_ONLY_PASS_NO_CHANGES\n'; exit 0; fi
run_apply_locked
printf 'QA525_MANUAL_APPLY=DONE\n'
