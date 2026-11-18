#!/usr/bin/env bash
# QA525 · CONCILIACIÓN DEL JOURNAL tras la aplicación manual verificada (ruta B v4) de da274d65.
# Contexto: el operador dejó dual-<SRC>.json en RECOVERY_MANUALLY_REQUIRED; la API/WEB corren ya en da274d65 por el apply manual
# (recibo manual-apply-<SRC>-*.result.json = MANUAL_APPLY_VERIFIED_API_WEB). El operador exige, para toda capa de imagen heredada,
# un journal en APPLIED (reviewed_layers/COMPOSE_HISTORY_NOT_SUCCESSFULLY_APPLIED). Archivar el journal rompería el próximo corte.
# Este script SOLO cambia "status" del journal de RECOVERY_MANUALLY_REQUIRED a APPLIED (mismo mecanismo atómico del operador: .next + replace),
# tras verificar el estado físico y el recibo, con respaldo del original. NO toca prepared/aprobaciones/nonce/eventos/Docker/BD/red/dev/main.
# Autor: Claude. Revisor independiente requerido: ChatGPT. Requiere autorización NUEVA y específica del Sponsor para EXECUTE_RECONCILE.
# Ejecutar siempre como archivo: `bash archivo [EXECUTE_RECONCILE]`. Sin argumento: SOLO LECTURA.
set -Eeuo pipefail
umask 077
SRC=da274d651d8e9e056525413406c0aab62131504f
OLDAPI=sha256:c620fd26212edc943b368da7a5d5e0fbe90ae8391ee8e73b98d818fd6d2171ed
OLDWEB=sha256:664c0e37bf6e6b801fd5617f4d327f5e027358817d488a92905338d992c1255a
NEWAPI=sha256:09b66869936d505a08c53b44ad9d22c86c0ffad83696ac5f1ea65c7bfff9a0cb
NEWWEB=sha256:fb15b5c17ae57b149b5d3d2883ba41a84514a3b88cc2bc051207ae0f67507f34
JSHA=4fe6d3b9039d89586f9aefc6a56e494f39821f3406264e17a50c9e157f4c2e44
APPLY_SHA=dc0c470c234214145d4b841dfbf4400c61aa1dc005f62cba591406131dff44df
L=/usr/local/sbin/pmgm-qa-update
STATE=/opt/centenario/qa525-signed-prepare
OWNER=0:0
J="$STATE/dual-$SRC.json"; P="$STATE/prepared.json"; E="$STATE/dual-events.log"; CUT=/etc/pmgm/qa525-supervised-cut.json
LOCKS=(/opt/centenario/qa-api-refresh/operation.lock /opt/centenario/qa-auto-lite/web-update.lock /run/lock/pmgm-master-qa-604.lock /run/lock/pmgm-qa754-web-hotfix.lock /run/lock/pmgm-qa754-api-activation.lock)
SITE=https://sistema.granlogiamixta.cl
# >>> FUNCTIONS (la prueba hermética carga exactamente este bloque)
stop() { printf 'QA525_RECONCILE=STOP_%s_NO_RETRY\n' "$1"; exit 2; }
priv() { [[ "$(sudo stat -c '%u:%g %a %h' -- "$1" 2>/dev/null || :)" == "$OWNER 600 1" ]] && sudo test -f "$1" && sudo test ! -L "$1"; }
ev_has() { local rc=0; sudo grep -Eq "$1[: ]+$SRC\$" "$E" || rc=$?; case "$rc" in 0) return 0;; 1) return 1;; *) stop EVENTS_READ_ERROR;; esac; }
state_ok() {
  local f name line cid img st pj sv rev nonce names raw b cnt want lab nd R S ip
  priv "$J" || stop JOURNAL_UNTRUSTED
  priv "$P" || stop PREPARED_UNTRUSTED
  priv "$CUT" || stop CUT_UNTRUSTED
  priv "$E" || stop EVENTS_UNTRUSTED
  raw=$(sudo find "$STATE" -maxdepth 1 \( -name 'dual-*.json.next' -o -name 'reconcile-*' -o -name 'route-a-recovery-*' -o -name 'expired-prepared-archive-pending.json' \)) || stop STATE_SCAN
  [[ -z "$raw" ]] || stop STALE_OR_PRIOR_RECONCILE_ARTIFACT
  raw=$(sudo find "$STATE" -maxdepth 1 -name 'dual-*.json') || stop JOURNAL_SCAN
  cnt=0
  while IFS= read -r f; do
    [[ -n "$f" ]] || continue
    if [[ "$f" == "$J" ]]; then cnt=$((cnt+1)); continue; fi
    b=${f##*/}
    [[ "$b" =~ ^dual-([a-f0-9]{40})\.json$ ]] || stop JOURNAL_OTHER_NAME
    sv=${BASH_REMATCH[1]}
    priv "$f" || stop JOURNAL_OTHER_UNTRUSTED
    sudo jq -e --arg s "$sv" 'type=="object" and .status=="APPLIED" and .source_sha==$s' "$f" >/dev/null || stop JOURNAL_OTHER_NOT_APPLIED
  done <<<"$raw"
  [[ "$cnt" == 1 ]] || stop JOURNAL_TARGET_NOT_UNIQUE
  [[ "$(sudo sha256sum -- "$J" | cut -d' ' -f1)" == "$JSHA" ]] || stop JOURNAL_HASH_CHANGED
  sudo jq -e --arg s "$SRC" --arg oa "$OLDAPI" --arg ow "$OLDWEB" --arg na "$NEWAPI" --arg nw "$NEWWEB" '.status=="RECOVERY_MANUALLY_REQUIRED" and .source_sha==$s and .old_images=={api:$oa,web:$ow} and .new_images=={api:$na,web:$nw} and (.original_layers.api|length)==6 and (.original_layers.web|length)==5 and ([.[]]|length)==5' "$J" >/dev/null || stop JOURNAL_CONTENT
  # recibo de la aplicación manual: exactamente uno, verificado, ligado a este script y a este journal
  raw=$(sudo find "$STATE" -maxdepth 1 -name "manual-apply-$SRC-*.result.json") || stop RECEIPT_SCAN
  [[ -n "$raw" && "$(wc -l <<<"$raw")" == 1 ]] || stop RECEIPT_NOT_UNIQUE
  R=$raw
  S=$(sudo find "$STATE" -maxdepth 1 -name "manual-apply-$SRC-*.started.json") || stop STARTED_SCAN
  [[ -n "$S" && "$(wc -l <<<"$S")" == 1 ]] || stop STARTED_NOT_UNIQUE
  priv "$R" || stop RECEIPT_UNTRUSTED
  priv "$S" || stop STARTED_UNTRUSTED
  sudo jq -e --arg s "$SRC" --arg a "$APPLY_SHA" --arg j "$JSHA" --arg oa "$OLDAPI" --arg ow "$OLDWEB" --arg na "$NEWAPI" --arg nw "$NEWWEB" '.status=="MANUAL_APPLY_VERIFIED_API_WEB" and .source_sha==$s and .script_sha256==$a and .operator_journal_sha256==$j and .operator_journal_untouched==true and .no_second_apply_without_approved_reconciliation==true and .new_images=={api:$na,web:$nw} and .old_images=={api:$oa,web:$ow} and .public_version.source_sha==$s and .public_version.environment=="QA_SYNTHETIC"' "$R" >/dev/null || stop RECEIPT_CONTENT
  nonce=$(sudo jq -r '.nonce' "$CUT") || stop NONCE_READ
  [[ "$nonce" =~ ^[a-f0-9]{64}$ ]] || stop NONCE_INVALID
  priv "$STATE/approval-consumed-$nonce" || stop NONCE_MARKER_UNTRUSTED
  [[ "$(sudo cat -- "$STATE/approval-consumed-$nonce")" == "$SRC" ]] || stop NONCE_NOT_CONSUMED_FOR_CUT
  ev_has DUAL_API_WEB_RECOVERY_REQUIRED || stop RECOVERY_EVENT_MISSING
  ! ev_has DUAL_API_WEB_APPLIED || stop TARGET_APPLIED_EVENT
  ! ev_has DUAL_API_WEB_ROLLED_BACK || stop TARGET_ROLLED_BACK_EVENT
  # overrides: solo imagen, exactos, ligados al hash del recibo
  for want in "api $NEWAPI" "web $NEWWEB" "api $OLDAPI" "web $OLDWEB"; do
    read -r name img <<<"$want"
    f="$STATE/qa525-$name-$SRC-${img#sha256:}.json"
    priv "$f" || stop OVERRIDE_UNTRUSTED
    sudo jq -e --arg n "$name" --arg i "$img" '.=={services:{($n):{image:$i}}}' "$f" >/dev/null || stop OVERRIDE_CONTENT
    [[ "$(sudo sha256sum -- "$f" | cut -d' ' -f1)" == "$(sudo jq -r --arg k "${f##*/}" '.overrides_sha256[$k]' "$R")" ]] || stop OVERRIDE_HASH
  done
  # estado físico vivo
  mapfile -t names < <(sudo jq -r '.baseline|keys[]' "$P") || stop BASELINE_KEYS
  [[ " ${names[*]} " == *" api "* && " ${names[*]} " == *" web "* && "${#names[@]}" == 7 ]] || stop BASELINE_API_WEB_MISSING
  for name in "${names[@]}"; do
    [[ "$name" =~ ^[a-z0-9_-]{1,40}$ ]] || stop BASELINE_NAME
    line=$(sudo docker inspect --format '{{.Id}} {{.Image}} {{.State.Status}} {{index .Config.Labels "com.docker.compose.project"}} {{index .Config.Labels "com.docker.compose.service"}} {{index .Config.Labels "org.opencontainers.image.revision"}}' "centenario-qa-lab2-$name-1") || stop BASELINE_INSPECT
    read -r cid img st pj sv rev <<<"$line"
    [[ "$st" == running && "$pj" == centenario-qa-lab2 && "$sv" == "$name" ]] || stop BASELINE_STATE
    if [[ "$name" == api || "$name" == web ]]; then
      [[ "$rev" == "$SRC" ]] || stop TARGET_NOT_NEW_REVISION
      [[ "$img" == "$( [[ $name == api ]] && echo "$NEWAPI" || echo "$NEWWEB")" ]] || stop TARGET_IMAGE_NOT_NEW
      [[ "$cid" == "$(sudo jq -r --arg n "$name" '.current_containers[$n]' "$R")" ]] || stop TARGET_CONTAINER_CHANGED_SINCE_RECEIPT
      nd=${img#sha256:}
      want=$(sudo jq -r --arg n "$name" --arg p "$STATE/qa525-$name-$SRC-$nd.json" '(.original_layers[$n] + [$p]) | join(",")' "$J") || stop LAYERS_EXPECTED
      lab=$(sudo docker inspect --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}' "centenario-qa-lab2-$name-1") || stop LAYERS_INSPECT
      [[ "$lab" == "$want" ]] || stop COMPOSE_LAYERS_MISMATCH
    else
      sudo jq -e --arg n "$name" --arg c "$cid" '.baseline[$n].container_fingerprint==$c and .baseline[$n].image_id=="'"$img"'"' "$P" >/dev/null || stop DEPENDENCY_CHANGED
    fi
  done
  # sondeo HTTP DIRECTO a las instancias (mismo criterio del postflight de v4), antes de la salud pública
  ip=$(sudo docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}' "centenario-qa-lab2-api-1" | awk '{print $1}') || stop API_IP
  [[ "$ip" =~ ^[0-9.]+$ ]] || stop API_IP_INVALID
  curl -fsS -o /dev/null --max-time 12 --proto '=http' "http://$ip:8080/health/ready" || stop API_DIRECT_NOT_READY
  ip=$(sudo docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}' "centenario-qa-lab2-web-1" | awk '{print $1}') || stop WEB_IP
  [[ "$ip" =~ ^[0-9.]+$ ]] || stop WEB_IP_INVALID
  curl -fsS -o /dev/null --max-time 12 --proto '=http' "http://$ip:80/health/web" || stop WEB_DIRECT_NOT_HEALTHY
  curl -fsS --max-time 12 --proto '=http' "http://$ip:80/qa-version.json" | jq -e --arg s "$SRC" '.environment=="QA_SYNTHETIC" and .source_sha==$s' >/dev/null || stop WEB_DIRECT_VERSION_NOT_NEW
  curl -fsS --max-time 30 --proto '=https' "$SITE/qa-version.json" | jq -e --arg s "$SRC" '.environment=="QA_SYNTHETIC" and .source_sha==$s' >/dev/null || stop PUBLIC_VERSION_NOT_NEW
  for f in /health/ready /identity/realms/pmgm/.well-known/openid-configuration; do curl -fsS -o /dev/null --max-time 30 --proto '=https' "$SITE$f" || stop PUBLIC_HEALTH; done
}
reconcile() {
  local D oh nh rh
  state_ok
  D="$STATE/reconcile-$(date -u +%Y%m%dT%H%M%SZ)-$$"
  mkdir -m 700 -- "$D" || stop BACKUP_DIR
  cp --no-clobber -- "$J" "$D/dual-$SRC.json.orig" || stop BACKUP_COPY
  chmod 600 -- "$D/dual-$SRC.json.orig"
  oh=$(sha256sum -- "$D/dual-$SRC.json.orig" | cut -d' ' -f1)
  [[ "$oh" == "$JSHA" ]] || stop BACKUP_HASH
  sync -f "$D/dual-$SRC.json.orig"; sync -f "$D"; sync -f "$STATE"
  python3 -I - "$J" <<'PY' || stop JOURNAL_REWRITE
import json, os, sys
path = sys.argv[1]
temp = path + ".next"
with open(path) as f:
    value = json.load(f)
if value.get("status") != "RECOVERY_MANUALLY_REQUIRED":
    raise SystemExit(1)
value["status"] = "APPLIED"
fd = os.open(temp, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
with os.fdopen(fd, "w") as out:
    json.dump(value, out, sort_keys=True)
    out.flush()
    os.fsync(out.fileno())
os.replace(temp, path)
folder = os.open(os.path.dirname(path), os.O_RDONLY | os.O_DIRECTORY)
try:
    os.fsync(folder)
finally:
    os.close(folder)
PY
  priv "$J" || stop JOURNAL_MODE_AFTER
  [[ ! -e "$J.next" && ! -L "$J.next" ]] || stop NEXT_LEFT
  jq -e --arg s "$SRC" '.status=="APPLIED" and .source_sha==$s' "$J" >/dev/null || stop JOURNAL_STATUS_AFTER
  [[ "$(jq -S 'del(.status)' "$J")" == "$(jq -S 'del(.status)' "$D/dual-$SRC.json.orig")" ]] || stop JOURNAL_OTHER_FIELDS_CHANGED
  nh=$(sha256sum -- "$J" | cut -d' ' -f1)
  rh=$(sha256sum -- "$(printf '%s\n' "$STATE"/manual-apply-"$SRC"-*.result.json)" | cut -d' ' -f1)
  ( set -C; jq -n -c --arg s "$SRC" --arg o "$oh" --arg n "$nh" --arg r "$rh" '{status:"JOURNAL_RECONCILED_APPLIED_FROM_VERIFIED_MANUAL_RECEIPT",source_sha:$s,journal_before_sha256:$o,journal_after_sha256:$n,manual_apply_receipt_sha256:$r,only_status_field_changed:true,prepared_nonce_events_docker_db_untouched:true}' > "$D/receipt.json" ) || stop RECEIPT_WRITE
  priv "$D/receipt.json" || stop RECEIPT_MODE
  sync -f "$D/receipt.json"; sync -f "$D"
  printf 'QA525_RECONCILE_JOURNAL=APPLIED backup=%s\n' "${D##*/}"
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
run_reconcile_locked() {
  local l body rc=0
  for l in "${LOCKS[@]}"; do priv "$l" || stop LOCK_FILE_UNTRUSTED_OR_MISSING; done
  body="set -Eeuo pipefail; umask 077; $(declare -p SRC OLDAPI OLDWEB NEWAPI NEWWEB JSHA APPLY_SHA STATE OWNER J P E CUT LOCKS SITE); $(declare -f stop priv ev_has state_ok reconcile lock_hook hold_locks); sudo() { \"\$@\"; }; hold_locks; reconcile"
  sudo bash -c "$body" || rc=$?
  [[ "$rc" == 0 ]] || stop RECONCILE_FAILED_SEE_ABOVE_BACKUP_MAY_REMAIN
}
# <<< FUNCTIONS
[[ "$#" -le 1 ]] || stop BAD_ARGUMENT
MODE=${1:-READ_ONLY}
[[ "$MODE" == READ_ONLY || "$MODE" == EXECUTE_RECONCILE ]] || stop BAD_ARGUMENT
[[ "$(hostname -s)" == glmdch && "$(id -un)" == soporte ]] || stop HOST_OR_USER
command -v jq >/dev/null && command -v curl >/dev/null && command -v python3 >/dev/null || stop TOOLS
sudo -v || stop SUDO
sudo test -f "$L" && sudo test ! -L "$L" || stop LAUNCHER_MISSING
printf '%s  %s\n' 7495fd65a51ecf83ded149b9467180e7470ceaee278e33a2ff7bd7e5b6faa562 "$L" | sudo sha256sum -c - >/dev/null || stop LAUNCHER_HASH
for l in "${LOCKS[@]}"; do priv "$l" || stop LOCK_FILE_UNTRUSTED_OR_MISSING; done
s=$(sudo "$L" --status) || stop STATUS
printf '%s\n' "$s" | jq -e '.status=="QA525_UPDATE_STATUS_READ_ONLY" and .services_running==true and .may_apply==false' >/dev/null || stop STATUS_MISMATCH
state_ok
printf 'QA525_RECONCILE_A0=PASS_APPLIED_STATE_VERIFIED_JOURNAL_PENDING_RECONCILE runtime=%s\n' "$SRC"
if [[ "$MODE" == READ_ONLY ]]; then printf 'QA525_RECONCILE=READ_ONLY_PASS_NO_CHANGES\n'; exit 0; fi
run_reconcile_locked
s=$(sudo "$L" --status) || stop POST_STATUS
printf '%s\n' "$s" | jq -e '.status=="QA525_UPDATE_STATUS_READ_ONLY" and .services_running==true and .may_apply==false' >/dev/null || stop POST_STATUS_MISMATCH
printf 'QA525_RECONCILE=PASS_JOURNAL_APPLIED\n'
