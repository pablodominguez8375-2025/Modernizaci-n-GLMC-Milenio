#!/usr/bin/env bash
# QA525 · RUTA A PAT — CANDIDATO SIN INSTALACIÓN AUTORIZADA · custodia de los 5 registros del intento cfe4 PENDING + UN solo intento de actualización a 6eed4.
# Derivado de Ruta A v3 (Claude), adaptado para PAT por ChatGPT. Revisión independiente distinta de ambos autores y autorización específica del Sponsor requeridas.
# NO ejecutar en glmdch hasta revisión independiente y distribución firmada de este exacto hash.
# Sin argumento: SOLO LECTURA (A0). Con EXECUTE_A1_A2: custodia bajo los 5 locks (A1) y un único intento (A2).
# Ejecutar siempre como archivo: `bash archivo [EXECUTE_A1_A2]` (stop() hace exit 2).
set -Eeuo pipefail
umask 077
SRC=6eed4f00b7917da3cca36bbda70e16d16328648d
DIGEST=sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2
PEND=cfe4e6573ece2ae81414cc252047d36cc56e5c73
PENDDIGEST=sha256:fab78644c2fe95d2f747794c0f85b21cb6ed438679bb11fd629e9a9e54b5d358
OLD=41d9c490d138419c7286445f37e7aeddf0d1b8d5
REVIEW='https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6604960072'
G6=/etc/pmgm/qa525-g6-reviewed-20261102T110545Z.json
L=/usr/local/sbin/pmgm-qa-update
STATE=/opt/centenario/qa525-signed-prepare
OWNER=0:0
MIN_RL=58
J="$STATE/dual-$PEND.json"; P="$STATE/prepared.json"; M="$STATE/expired-prepared-archive-pending.json"
E="$STATE/dual-events.log"; CUT=/etc/pmgm/qa525-supervised-cut.json
F=("$P" /etc/pmgm/qa525-sensitive-reviewed.json /etc/pmgm/qa525-single-operator-postdb-approved.json "$CUT" "$J")
LOCKS=(/opt/centenario/qa-api-refresh/operation.lock /opt/centenario/qa-auto-lite/web-update.lock /run/lock/pmgm-master-qa-604.lock /run/lock/pmgm-qa754-web-hotfix.lock /run/lock/pmgm-qa754-api-activation.lock)
# >>> FUNCTIONS (la prueba hermética carga exactamente este bloque)
stop() { printf 'QA525_ROUTE_A=STOP_%s_NO_RETRY\n' "$1"; exit 2; }
priv() { [[ "$(sudo stat -c '%u:%g %a %h' -- "$1" 2>/dev/null || :)" == "$OWNER 600 1" ]] && sudo test -f "$1" && sudo test ! -L "$1"; }
state_ok() {
  local f name line cid img st pj sv nonce rc names
  for f in "${F[@]}"; do priv "$f" || stop EVIDENCE_FILE_UNTRUSTED; done
  sudo test ! -e "$J.next" && sudo test ! -L "$J.next" || stop JOURNAL_NEXT_PRESENT
  [[ "$(sudo find "$STATE" -maxdepth 1 -name 'dual-*.json' | wc -l)" == 1 ]] || stop OTHER_OR_NO_JOURNAL
  sudo test ! -e "$M" && sudo test ! -L "$M" || stop ARCHIVE_MARKER_PRESENT
  sudo jq -e --arg s "$PEND" '.status=="PENDING" and .source_sha==$s' "$J" >/dev/null || stop JOURNAL_NOT_PENDING
  sudo jq -e --arg s "$PEND" --arg d "$PENDDIGEST" '.status=="PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT" and .source_sha==$s and .source_asset_sha256==$d and .schema_mode=="support00_postdb" and .review_mode=="single_operator_qa" and .may_apply==false and .images_built==true and .services_unchanged==true' "$P" >/dev/null || stop PREPARED_MISMATCH
  sudo jq -e --arg s "$PEND" --arg d "$PENDDIGEST" '.source_sha==$s and .release_asset_sha256==$d' /etc/pmgm/qa525-sensitive-reviewed.json >/dev/null || stop SENSITIVE_REVIEW_MISMATCH
  sudo jq -e --arg s "$PEND" --arg d "$PENDDIGEST" '.source_sha==$s and .release_asset_sha256==$d' /etc/pmgm/qa525-single-operator-postdb-approved.json >/dev/null || stop POSTDB_MISMATCH
  sudo jq -e --arg s "$PEND" --arg d "$PENDDIGEST" '.target_sha==$s and .release_asset_sha256==$d' "$CUT" >/dev/null || stop CUT_MISMATCH
  nonce=$(sudo jq -r '.nonce' "$CUT") || stop NONCE_READ
  [[ "$nonce" =~ ^[a-f0-9]{64}$ ]] || stop NONCE_INVALID
  priv "$STATE/approval-consumed-$nonce" || stop NONCE_MARKER_UNTRUSTED
  [[ "$(sudo cat -- "$STATE/approval-consumed-$nonce")" == "$PEND" ]] || stop NONCE_NOT_CONSUMED_FOR_CUT
  if sudo test -e "$E" || sudo test -L "$E"; then
    priv "$E" || stop EVENTS_UNTRUSTED
    rc=0; sudo grep -q " $PEND\$" "$E" || rc=$?
    case "$rc" in 0) stop TARGET_EVENT_PRESENT;; 1) : ;; *) stop EVENTS_READ_ERROR;; esac
  fi
  mapfile -t names < <(sudo jq -r '.baseline|keys[]' "$P") || stop BASELINE_KEYS
  [[ " ${names[*]} " == *" api "* && " ${names[*]} " == *" web "* ]] || stop BASELINE_API_WEB_MISSING
  for name in "${names[@]}"; do
    [[ "$name" =~ ^[a-z0-9_-]{1,40}$ ]] || stop BASELINE_NAME
    line=$(sudo docker inspect --format '{{.Id}} {{.Image}} {{.State.Status}} {{index .Config.Labels "com.docker.compose.project"}} {{index .Config.Labels "com.docker.compose.service"}}' "centenario-qa-lab2-$name-1") || stop BASELINE_INSPECT
    read -r cid img st pj sv <<<"$line"
    [[ "$st" == running && "$pj" == centenario-qa-lab2 && "$sv" == "$name" ]] || stop BASELINE_STATE
    sudo jq -e --arg n "$name" --arg c "$cid" --arg i "$img" '.baseline[$n].container_fingerprint==$c and .baseline[$n].image_id==$i' "$P" >/dev/null || stop BASELINE_DRIFT
  done
  for name in api web; do
    sudo jq -e --arg n "$name" --slurpfile p "$P" '.old_images[$n]==$p[0].baseline[$n].image_id and .new_images[$n]==$p[0][$n+"_image_id"]' "$J" >/dev/null || stop JOURNAL_PREPARED_BINDING
  done
}
custody() {
  local A f b lh mh files
  declare -A H
  state_ok
  A="$STATE/route-a-custody-$(date -u +%Y%m%dT%H%M%SZ)-$$"
  mkdir -m 700 -- "$A" || stop ARCHIVE_DIR
  for f in "${F[@]}"; do
    b=${f##*/}; [[ -z "${H[$b]:-}" ]] || stop BASENAME_COLLISION
    H[$b]=$(sha256sum -- "$f" | cut -d' ' -f1); [[ "${H[$b]}" =~ ^[a-f0-9]{64}$ ]] || stop HASH_BEFORE
  done
  files=$(for f in "${F[@]}"; do b=${f##*/}; printf '%s\t%s\t%s\n' "$b" "$f" "${H[$b]}"; done | jq -R -s -c 'split("\n")[:-1]|map(split("\t")|{name:.[0],from:.[1],sha256:.[2]})') || stop LEDGER_BUILD
  ( set -C; jq -n -c --arg s "$PEND" --arg a "${A##*/}" --argjson f "$files" '{status:"ROUTE_A_CUSTODY_LEDGER",source_sha:$s,archive:$a,files:$f}' > "$A/ledger.json" ) || stop LEDGER_WRITE
  priv "$A/ledger.json" || stop LEDGER_MODE
  sync -f "$A/ledger.json"; sync -f "$A"; sync -f "$STATE"
  lh=$(sha256sum -- "$A/ledger.json" | cut -d' ' -f1)
  ( set -C; jq -n -c --arg s "$PEND" --arg a "${A##*/}" --arg l "$lh" '{status:"ROUTE_A_CUSTODY_INCOMPLETE",source_sha:$s,archive:$a,ledger_sha256:$l}' > "$M" ) || stop MARKER_CREATE
  priv "$M" || stop MARKER_MODE
  sync -f "$M"; sync -f "$STATE"
  for f in "${F[@]}"; do
    b=${f##*/}
    mv -n -- "$f" "$A/$b" || stop MOVE
    [[ ! -e "$f" && ! -L "$f" && -f "$A/$b" && ! -L "$A/$b" ]] || stop MOVE_VERIFY
    [[ "$(sha256sum -- "$A/$b" | cut -d' ' -f1)" == "${H[$b]}" ]] || stop HASH_AFTER
    sync -f "$A/$b"; sync -f "$A"; sync -f "${f%/*}"
  done
  ( set -C; jq -n -c --arg s "$PEND" --arg l "$lh" --argjson f "$files" '{status:"ROUTE_A_CUSTODY_COMPLETE_NO_DEPLOYMENT",source_sha:$s,ledger_sha256:$l,files:$f,nonce_remains_consumed:true}' > "$A/receipt.json" ) || stop RECEIPT_WRITE
  priv "$A/receipt.json" || stop RECEIPT_MODE
  jq -e '.status=="ROUTE_A_CUSTODY_COMPLETE_NO_DEPLOYMENT" and (.files|length)==5' "$A/receipt.json" >/dev/null || stop RECEIPT_FORMAT
  sync -f "$A/receipt.json"; sync -f "$A"
  mh=$(sha256sum -- "$M" | cut -d' ' -f1)
  mv -n -- "$M" "$A/marker-cleared.json" || stop MARKER_CLEAR
  [[ ! -e "$M" && ! -L "$M" && "$(sha256sum -- "$A/marker-cleared.json" | cut -d' ' -f1)" == "$mh" ]] || stop MARKER_CLEAR_VERIFY
  sync -f "$A"; sync -f "$STATE"
  for f in "${F[@]}"; do b=${f##*/}; [[ ! -e "$f" && ! -L "$f" ]] || stop POST_ABSENT; [[ "$(sha256sum -- "$A/$b" | cut -d' ' -f1)" == "${H[$b]}" ]] || stop POST_HASH; done
  printf 'QA525_ROUTE_A_CUSTODY=PASS archive=%s\n' "${A##*/}"
}
lock_hook() { :; }  # punto de inyección SOLO para pruebas herméticas; en producción no hace nada
hold_locks() {
  local l fd want got n=0
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
run_custody_locked() {
  local l body rc=0
  for l in "${LOCKS[@]}"; do priv "$l" || stop LOCK_FILE_UNTRUSTED_OR_MISSING; done
  body="set -Eeuo pipefail; umask 077; $(declare -p PEND PENDDIGEST STATE OWNER J P M E CUT F LOCKS); $(declare -f stop priv state_ok custody lock_hook hold_locks); sudo() { \"\$@\"; }; hold_locks; custody"
  sudo bash -c "$body" || rc=$?
  [[ "$rc" == 0 ]] || stop CUSTODY_FAILED_SEE_ABOVE_MARKER_MAY_REMAIN
}
quota_ok() {
  local result limit rem rst
  result=$(sudo /usr/bin/python3 - <<'PY_AUTH_RATE'
import json
import os
from pathlib import Path
import re
import ssl
import stat
import sys
from urllib.request import (build_opener, ProxyHandler, HTTPSHandler,
                            HTTPRedirectHandler, Request)

TOKEN = Path('/etc/pmgm/qa525-github-readonly.pat')
URL = 'https://api.github.com/rate_limit'

class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

try:
    if os.geteuid() != 0:
        raise ValueError('root')
    parent = TOKEN.parent.lstat()
    if (not stat.S_ISDIR(parent.st_mode) or parent.st_uid != 0 or
            parent.st_mode & 0o022):
        raise ValueError('directory')
    fd = os.open(TOKEN, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    with os.fdopen(fd, 'rb') as handle:
        info = os.fstat(handle.fileno())
        if (not stat.S_ISREG(info.st_mode) or info.st_uid != 0 or
                info.st_gid != 0 or info.st_nlink != 1 or
                stat.S_IMODE(info.st_mode) != 0o600 or
                not 20 <= info.st_size <= 300):
            raise ValueError('metadata')
        raw = handle.read(301)
        if len(raw) != info.st_size:
            raise ValueError('changed')
    token = raw.decode('ascii').removesuffix('\n')
    if re.fullmatch(r'github_pat_[A-Za-z0-9_]{20,250}', token) is None:
        raise ValueError('format')
    opener = build_opener(ProxyHandler({}),
                          HTTPSHandler(context=ssl.create_default_context()),
                          NoRedirect())
    request = Request(URL, headers={
        'Authorization': 'Bearer ' + token,
        'Accept': 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28',
        'User-Agent': 'pmgm-qa525-pat-rate-v1',
    })
    with opener.open(request, timeout=20) as response:
        if response.geturl() != URL:
            raise ValueError('redirect')
        data = response.read(16385)
        if len(data) > 16384:
            raise ValueError('size')
    core = json.loads(data)['resources']['core']
    values = (core['limit'], core['remaining'], core['reset'])
    if any(type(x) is not int or x < 0 for x in values):
        raise ValueError('quota')
    print('|'.join(str(x) for x in values))
except Exception:
    sys.exit(2)
PY_AUTH_RATE
  ) || stop AUTH_RATE_READ
  IFS='|' read -r limit rem rst <<<"$result"
  [[ "$limit" =~ ^[0-9]+$ && "$rem" =~ ^[0-9]+$ && "$rst" =~ ^[0-9]+$ ]] || stop AUTH_RATE_PARSE
  (( limit > 60 && rem >= MIN_RL )) || stop AUTH_QUOTA_LOW_NO_RETRY
  printf 'QA525_GITHUB_AUTH_QUOTA remaining=%s limit=%s reset_utc=%s\n' "$rem" "$limit" "$(date -u -d "@$rst" +%FT%TZ)"
}
# <<< FUNCTIONS
[[ "$#" -le 1 ]] || stop BAD_ARGUMENT
MODE=${1:-READ_ONLY}
[[ "$MODE" == READ_ONLY || "$MODE" == EXECUTE_A1_A2 ]] || stop BAD_ARGUMENT
[[ "$REVIEW" =~ ^https://github\.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-[1-9][0-9]*$ ]] || stop REVIEW_URL_NOT_SET
[[ "$(hostname -s)" == glmdch && "$(id -un)" == soporte ]] || stop HOST_OR_USER
command -v jq >/dev/null && command -v curl >/dev/null || stop TOOLS
sudo -v || stop SUDO
# ---- A0 · solo lectura -------------------------------------------------------------
sudo test -f "$L" && sudo test ! -L "$L" || stop LAUNCHER_MISSING
printf '%s  %s\n' 61e3f1f7e7dd561567e5fac415a6fa9695c2f74fa86adde21dd8ed0330d03cd3 "$L" | sudo sha256sum -c - >/dev/null || stop LAUNCHER_HASH
priv "$G6" || stop G6_REVIEW_MODE
printf '%s  %s\n' 31100269647cc77c8d3daf37c260528ca9b136ccfcb889c46b82fd01e3783aea "$G6" | sudo sha256sum -c - >/dev/null || stop G6_REVIEW_HASH
sudo jq -e '.g6_offhost_sha256_verified==true and .g6_archive_name=="centenario-qa-g6-CIFRADO-20261102T110545Z.tar.gz" and .g6_archive_sha256=="772a645f09823648ed25a287f837653aef4cced2ba95413422d8f20f65a9d915"' "$G6" >/dev/null || stop G6_REVIEW_CONTENT
(( $(date -u +%s) < $(date -u -d '2026-11-04 10:45:00 UTC' +%s) )) || stop G6_TOO_CLOSE_TO_48H
quota_ok
for l in "${LOCKS[@]}"; do priv "$l" || stop LOCK_FILE_UNTRUSTED_OR_MISSING; done
s=$(sudo "$L" --status) || stop STATUS
printf '%s\n' "$s" | jq -e '.status=="QA525_UPDATE_STATUS_READ_ONLY" and .services_running==true and .prepared_exists==true and .archive_reconciliation_required==false and .may_apply==false' >/dev/null || stop STATUS_MISMATCH
state_ok
sql='BEGIN READ ONLY; SELECT count(*)::text || chr(124) || count(*) FILTER (WHERE "MigrationId" = '\''20261029120000_AddMemberHistoryProposals'\'')::text || chr(124) || count(*) FILTER (WHERE "MigrationId" = '\''20261030223000_SeedSupportWorkshop00'\'')::text FROM public."__EFMigrationsHistory"; COMMIT;'
ef=$(sudo docker exec centenario-qa-lab2-postgres-1 sh -c 'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"' qa525-readonly "$sql") || stop EF_READ
[[ "$ef" == '100|1|1' ]] || stop EF_HISTORY_DRIFT
for name in api web; do
  ids=$(sudo docker ps --filter label=com.docker.compose.project=centenario-qa-lab2 --filter "label=com.docker.compose.service=$name" --format '{{.ID}}') || stop CONTAINER_LIST
  [[ "$ids" =~ ^[a-f0-9]{12,64}$ ]] || stop CONTAINER_CARDINALITY
  [[ "$(sudo docker inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$ids")" == "$OLD" ]] || stop BASELINE_DRIFT
done
curl -fsS --max-time 20 --proto '=https' https://sistema.granlogiamixta.cl/qa-version.json | jq -e --arg o "$OLD" '.environment=="QA_SYNTHETIC" and .source_sha==$o' >/dev/null || stop PUBLIC_BASELINE_DRIFT
printf 'QA525_ROUTE_A_PREFLIGHT=PASS_PENDING_CFE4_BASELINE_INTACT\n'
[[ "$MODE" == EXECUTE_A1_A2 ]] || { printf 'QA525_ROUTE_A=READ_ONLY_DONE_NOTHING_CHANGED\n'; exit 0; }
# ---- A1 · custodia bajo los 5 locks (solo mueve evidencia; no toca runtime, BD ni Docker; nada se borra) ----
run_custody_locked
s=$(sudo "$L" --status) || stop POST_CUSTODY_STATUS
printf '%s\n' "$s" | jq -e '.services_running==true and .prepared_exists==false and .archive_reconciliation_required==false and .may_apply==false' >/dev/null || stop POST_CUSTODY_MISMATCH
quota_ok
# ---- A2 · UN solo intento. Única orden que despliega. Sin reintento. ----------------------
if result=$(sudo "$L" --review-and-run --source "$SRC" --digest "$DIGEST" --technical-review-reference "$REVIEW" --g6-review "$G6" --operator-name soporte@glmdch --schema-mode support00_postdb); then rc=0; else rc=$?; fi
printf 'QA525_GITHUB_AUTH_QUOTA_AFTER=PRE_A2_GATE_ALREADY_PASSED\n'
printf '%s\n' "$result" | jq -c '{status,source_sha,code,may_apply}' || true
[[ "$rc" == 0 ]] || stop LAUNCHER_BLOCKED_OR_INCOMPLETE
printf '%s\n' "$result" | jq -e --arg s "$SRC" '.status=="DUAL_API_WEB_APPLIED" and .source_sha==$s' >/dev/null || stop APPLY_RESPONSE_NOT_CONFIRMED
n="$STATE/dual-$SRC.json"
priv "$n" || stop JOURNAL_UNTRUSTED
sudo jq -e --arg s "$SRC" '.status=="APPLIED" and .source_sha==$s' "$n" >/dev/null || stop JOURNAL_NOT_APPLIED
for name in api web; do
  ids=$(sudo docker ps --filter label=com.docker.compose.project=centenario-qa-lab2 --filter "label=com.docker.compose.service=$name" --format '{{.ID}}') || stop POST_CONTAINER_LIST
  [[ "$ids" =~ ^[a-f0-9]{12,64}$ ]] || stop POST_CONTAINER_CARDINALITY
  [[ "$(sudo docker inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$ids")" == "$SRC" ]] || stop POST_IMAGE_REVISION
done
curl -fsS --max-time 30 --proto '=https' https://sistema.granlogiamixta.cl/qa-version.json | jq -e --arg s "$SRC" '.environment=="QA_SYNTHETIC" and .source_sha==$s' >/dev/null || stop POST_WEB_VERSION
for p in /health/ready /identity/realms/pmgm/.well-known/openid-configuration; do curl -fsS -o /dev/null --max-time 30 --proto '=https' "https://sistema.granlogiamixta.cl$p" || stop POST_HEALTH; done
printf 'QA525_ROUTE_A=PASS_DUAL_API_WEB_APPLIED\n'
