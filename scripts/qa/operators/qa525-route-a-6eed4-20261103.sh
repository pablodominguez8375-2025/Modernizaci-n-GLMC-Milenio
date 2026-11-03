#!/usr/bin/env bash
# QA525 · RUTA A · custodia de los 5 registros del intento cfe4 PENDING + UN solo intento de actualización a 6eed4.
# Autor: Claude. Revisor independiente requerido: ChatGPT. Autorización específica del Sponsor requerida.
# Sin argumento: SOLO LECTURA (A0). Con EXECUTE_A1_A2: custodia (A1) y un único intento de actualización (A2).
# Ejecutar siempre como archivo: `bash archivo [EXECUTE_A1_A2]` (stop() hace exit 2).
set -Eeuo pipefail
umask 077
SRC=6eed4f00b7917da3cca36bbda70e16d16328648d
DIGEST=sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2
PEND=cfe4e6573ece2ae81414cc252047d36cc56e5c73
OLD=41d9c490d138419c7286445f37e7aeddf0d1b8d5
REVIEW='__REVIEW_URL__'
G6=/etc/pmgm/qa525-g6-reviewed-20261102T110545Z.json
L=/usr/local/sbin/pmgm-qa-update
STATE=/opt/centenario/qa525-signed-prepare
MIN_RL=58
stop() { printf 'QA525_ROUTE_A=STOP_%s_NO_RETRY\n' "$1"; exit 2; }
priv() { [[ "$(sudo stat -c '%u:%g %a %h' -- "$1" 2>/dev/null || :)" == '0:0 600 1' ]] && sudo test -f "$1" && sudo test ! -L "$1"; }
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
rl=$(curl -fsS --max-time 20 --proto '=https' https://api.github.com/rate_limit) || stop RATE_LIMIT_READ
rem=$(printf '%s' "$rl" | jq -r '.resources.core.remaining'); rst=$(printf '%s' "$rl" | jq -r '.resources.core.reset')
[[ "$rem" =~ ^[0-9]+$ && "$rst" =~ ^[0-9]+$ ]] || stop RATE_LIMIT_PARSE
printf 'QA525_GITHUB_ANON_QUOTA remaining=%s reset_utc=%s\n' "$rem" "$(date -u -d "@$rst" +%FT%TZ)"
(( rem >= MIN_RL )) || stop "GITHUB_QUOTA_LOW_WAIT_FOR_RESET_THEN_RERUN"
s=$(sudo "$L" --status) || stop STATUS
printf '%s\n' "$s" | jq -e '.status=="QA525_UPDATE_STATUS_READ_ONLY" and .services_running==true and .prepared_exists==true and .archive_reconciliation_required==false and .may_apply==false' >/dev/null || stop STATUS_MISMATCH
J="$STATE/dual-$PEND.json"; P="$STATE/prepared.json"
F=("$STATE/prepared.json" /etc/pmgm/qa525-sensitive-reviewed.json /etc/pmgm/qa525-single-operator-postdb-approved.json /etc/pmgm/qa525-supervised-cut.json "$J")
for f in "${F[@]}"; do priv "$f" || stop "EVIDENCE_FILE_UNTRUSTED"; done
sudo test ! -e "$J.next" || stop JOURNAL_NEXT_PRESENT
[[ "$(sudo find "$STATE" -maxdepth 1 -name 'dual-*.json' | wc -l)" == 1 ]] || stop OTHER_OR_NO_JOURNAL
sudo test ! -e "$STATE/expired-prepared-archive-pending.json" || stop ARCHIVE_MARKER_PRESENT
sudo jq -e --arg s "$PEND" '.status=="PENDING" and .source_sha==$s' "$J" >/dev/null || stop JOURNAL_NOT_PENDING
sudo jq -e --arg s "$PEND" '.status=="PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT" and .source_sha==$s and .may_apply==false' "$P" >/dev/null || stop PREPARED_MISMATCH
if sudo test -e "$STATE/dual-events.log"; then ! sudo grep -q " $PEND\$" "$STATE/dual-events.log" || stop TARGET_EVENT_PRESENT; fi
sql='BEGIN READ ONLY; SELECT count(*)::text || chr(124) || count(*) FILTER (WHERE "MigrationId" = '\''20261029120000_AddMemberHistoryProposals'\'')::text || chr(124) || count(*) FILTER (WHERE "MigrationId" = '\''20261030223000_SeedSupportWorkshop00'\'')::text FROM public."__EFMigrationsHistory"; COMMIT;'
ef=$(sudo docker exec centenario-qa-lab2-postgres-1 sh -c 'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"' qa525-readonly "$sql") || stop EF_READ
[[ "$ef" == '100|1|1' ]] || stop EF_HISTORY_DRIFT
for name in api web; do
  ids=$(sudo docker ps --filter label=com.docker.compose.project=centenario-qa-lab2 --filter "label=com.docker.compose.service=$name" --format '{{.ID}}') || stop CONTAINER_LIST
  [[ "$ids" =~ ^[a-f0-9]{12,64}$ ]] || stop CONTAINER_CARDINALITY
  [[ "$(sudo docker inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$ids")" == "$OLD" ]] || stop BASELINE_DRIFT
  img=$(sudo docker inspect --format '{{.Image}}' "$ids") || stop CONTAINER_INSPECT
  sudo jq -e --arg n "$name" --arg i "$img" '.old_images[$n]==$i' "$J" >/dev/null || stop JOURNAL_IMAGE_MISMATCH
done
curl -fsS --max-time 20 --proto '=https' https://sistema.granlogiamixta.cl/qa-version.json | jq -e --arg o "$OLD" '.environment=="QA_SYNTHETIC" and .source_sha==$o' >/dev/null || stop PUBLIC_BASELINE_DRIFT
printf 'QA525_ROUTE_A_PREFLIGHT=PASS_PENDING_CFE4_BASELINE_INTACT\n'
[[ "$MODE" == EXECUTE_A1_A2 ]] || { printf 'QA525_ROUTE_A=READ_ONLY_DONE_NOTHING_CHANGED\n'; exit 0; }
# ---- A1 · custodia (solo mueve evidencia; no toca runtime, BD ni Docker; nada se borra) ----
M="$STATE/expired-prepared-archive-pending.json"
sudo sh -c 'set -C; umask 077; printf "{\"status\":\"ROUTE_A_CUSTODY_INCOMPLETE\",\"source_sha\":\"%s\"}\n" "$1" > "$2"' sh "$PEND" "$M" || stop MARKER_CREATE
A="$STATE/route-a-custody-$(date -u +%Y%m%dT%H%M%SZ)-$$"
sudo mkdir -m 700 -- "$A" || stop ARCHIVE_DIR
declare -A H
for f in "${F[@]}"; do
  b=$(basename "$f"); [[ -z "${H[$b]:-}" ]] || stop BASENAME_COLLISION
  H[$b]=$(sudo sha256sum -- "$f" | cut -d' ' -f1) || stop HASH_BEFORE
done
sudo sync -f "$STATE"
for f in "${F[@]}"; do
  b=$(basename "$f")
  sudo mv -n -- "$f" "$A/$b" || stop MOVE
  sudo test ! -e "$f" && sudo test -f "$A/$b" || stop MOVE_VERIFY
  [[ "$(sudo sha256sum -- "$A/$b" | cut -d' ' -f1)" == "${H[$b]}" ]] || stop HASH_AFTER
  sudo sync -f "$A"; sudo sync -f "$(dirname "$f")"
done
{ printf '{"status":"ROUTE_A_CUSTODY_COMPLETE_NO_DEPLOYMENT","source_sha":"%s","files":{' "$PEND"
  sep=''; for b in "${!H[@]}"; do printf '%s"%s":"%s"' "$sep" "$b" "${H[$b]}"; sep=','; done; printf '}}\n'; } | sudo tee "$A/receipt.json" >/dev/null || stop RECEIPT
sudo mv -n -- "$M" "$A/marker-cleared.json" || stop MARKER_CLEAR
sudo sync -f "$A"; sudo sync -f "$STATE"
s=$(sudo "$L" --status) || stop POST_CUSTODY_STATUS
printf '%s\n' "$s" | jq -e '.services_running==true and .prepared_exists==false and .archive_reconciliation_required==false and .may_apply==false' >/dev/null || stop POST_CUSTODY_MISMATCH
printf 'QA525_ROUTE_A_CUSTODY=PASS archive=%s\n' "$(basename "$A")"
# ---- A2 · UN solo intento. Única orden que despliega. Sin reintento. ----------------------
if result=$(sudo "$L" --review-and-run --source "$SRC" --digest "$DIGEST" --technical-review-reference "$REVIEW" --g6-review "$G6" --operator-name soporte@glmdch --schema-mode support00_postdb); then rc=0; else rc=$?; fi
rl=$(curl -fsS --max-time 20 --proto '=https' https://api.github.com/rate_limit 2>/dev/null || :)
printf 'QA525_GITHUB_ANON_QUOTA_AFTER remaining=%s\n' "$(printf '%s' "$rl" | jq -r '.resources.core.remaining // "unknown"' 2>/dev/null || echo unknown)"
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
