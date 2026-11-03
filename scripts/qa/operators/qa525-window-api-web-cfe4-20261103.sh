#!/usr/bin/env bash
# QA525 · single supervised API+WEB window, SHA-pinned, fail closed.
# Only run on glmdch AFTER independent wrapper review and sponsor approval.
set -Eeuo pipefail
umask 077
SRC=cfe4e6573ece2ae81414cc252047d36cc56e5c73
OLD=41d9c490d138419c7286445f37e7aeddf0d1b8d5
DIGEST=sha256:fab78644c2fe95d2f747794c0f85b21cb6ed438679bb11fd629e9a9e54b5d358
REVIEW=https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6601046369
G6=/etc/pmgm/qa525-g6-reviewed-20261102T110545Z.json
L=/usr/local/sbin/pmgm-qa-update
STATE=/opt/centenario/qa525-signed-prepare
stop() { printf 'QA525_WINDOW=STOP_%s_NO_RETRY\n' "$1"; exit 2; }
[[ "$(hostname -s)" == glmdch && "$(id -un)" == soporte ]] || stop HOST_OR_USER
sudo -v || stop SUDO
sudo test -f "$L" && sudo test ! -L "$L" || stop LAUNCHER_MISSING
printf '%s  %s\n' 61e3f1f7e7dd561567e5fac415a6fa9695c2f74fa86adde21dd8ed0330d03cd3 "$L" | sudo sha256sum -c - >/dev/null || stop LAUNCHER_HASH
sudo test -f "$G6" && sudo test ! -L "$G6" || stop G6_REVIEW_MISSING
[[ "$(sudo stat -c '%u:%g %a %h' "$G6")" == '0:0 600 1' ]] || stop G6_REVIEW_MODE
printf '%s  %s\n' 31100269647cc77c8d3daf37c260528ca9b136ccfcb889c46b82fd01e3783aea "$G6" | sudo sha256sum -c - >/dev/null || stop G6_REVIEW_HASH
sudo jq -e '.g6_offhost_sha256_verified==true and .g6_archive_name=="centenario-qa-g6-CIFRADO-20261102T110545Z.tar.gz" and .g6_archive_sha256=="772a645f09823648ed25a287f837653aef4cced2ba95413422d8f20f65a9d915"' "$G6" >/dev/null || stop G6_REVIEW_CONTENT
now=$(date -u +%s)
cutoff=$(date -u -d '2026-11-04 11:05:45 UTC' +%s)
(( now < cutoff )) || stop G6_48H_EXPIRED
s=$(sudo "$L" --status) || stop STATUS
printf '%s\n' "$s" | jq -e '.status=="QA525_UPDATE_STATUS_READ_ONLY" and .services_running==true and .prepared_exists==false and .archive_reconciliation_required==false and .may_apply==false' >/dev/null || stop STATUS_MISMATCH
sudo test -f "$STATE/support00-completed.json" && sudo test ! -L "$STATE/support00-completed.json" || stop SUPPORT00_RECEIPT_MISSING
[[ "$(sudo stat -c '%u:%g %a %h' "$STATE/support00-completed.json")" == '0:0 600 1' ]] || stop SUPPORT00_RECEIPT_MODE
sudo jq -e '.status=="SUPPORT00_EF100_VERIFIED" and .migration_id=="20261030223000_SeedSupportWorkshop00" and .migration_blob_sha=="8eb86e632009acd0bf252e8ea8abee1eabee6c6b"' "$STATE/support00-completed.json" >/dev/null || stop SUPPORT00_RECEIPT_FIELDS
sql='BEGIN READ ONLY; SELECT count(*)::text || chr(124) || count(*) FILTER (WHERE "MigrationId" = '\''20261029120000_AddMemberHistoryProposals'\'')::text || chr(124) || count(*) FILTER (WHERE "MigrationId" = '\''20261030223000_SeedSupportWorkshop00'\'')::text FROM public."__EFMigrationsHistory"; COMMIT;'
ef=$(sudo docker exec centenario-qa-lab2-postgres-1 sh -c 'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"' qa525-readonly "$sql") || stop EF_READ
[[ "$ef" == '100|1|1' ]] || stop EF_HISTORY_DRIFT
org_sql='BEGIN READ ONLY; SELECT count(*)::text || chr(124) || count(*) FILTER (WHERE "Number"='\''00'\'' AND lower("Type")='\''support'\'')::text || chr(124) || count(*) FILTER (WHERE "Number"='\''00'\'' AND lower("Type")<>'\''support'\'')::text || chr(124) || count(*) FILTER (WHERE lower("Type")='\''support'\'' AND "Number"<>'\''00'\'')::text FROM core.organizations WHERE "Number"='\''00'\'' OR lower("Type")='\''support'\''; COMMIT;'
org=$(sudo docker exec centenario-qa-lab2-postgres-1 sh -c 'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"' qa525-readonly "$org_sql") || stop SUPPORT00_READ
[[ "$org" == '1|1|0|0' ]] || stop SUPPORT00_DRIFT
for name in api web; do
  ids=$(sudo docker ps --filter label=com.docker.compose.project=centenario-qa-lab2 --filter "label=com.docker.compose.service=$name" --format '{{.ID}}') || stop CONTAINER_LIST
  [[ "$ids" =~ ^[a-f0-9]{12,64}$ ]] || stop CONTAINER_CARDINALITY
  rev=$(sudo docker inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$ids") || stop CONTAINER_INSPECT
  [[ "$rev" == "$OLD" ]] || stop API_WEB_BASELINE_DRIFT
done
web=$(curl -fsS --max-time 20 --proto '=https' https://sistema.granlogiamixta.cl/qa-version.json) || stop PUBLIC_VERSION
printf '%s\n' "$web" | jq -e --arg old "$OLD" '.environment=="QA_SYNTHETIC" and .source_sha==$old' >/dev/null || stop PUBLIC_BASELINE_DRIFT
printf 'QA525_WINDOW_PREFLIGHT=PASS_SOURCE_PINNED_G6_FRESH_DB100\n'
# This is the ONLY mutating/deploying command in this script. The trusted
# launcher rechecks GitHub, CI, Sigstore, schema, backup, identity and locks.
if result=$(sudo "$L" --review-and-run \
    --source "$SRC" --digest "$DIGEST" \
    --technical-review-reference "$REVIEW" \
    --g6-review "$G6" --operator-name soporte@glmdch \
    --schema-mode support00_postdb); then
  printf '%s\n' "$result" | jq -c '{status,source_sha,code,may_apply}' || stop RESULT_PARSE
else
  printf '%s\n' "$result" | jq -c '{status,source_sha,code,may_apply}' || true
  stop LAUNCHER_BLOCKED_OR_INCOMPLETE
fi
printf '%s\n' "$result" | jq -e --arg sha "$SRC" '.status=="DUAL_API_WEB_APPLIED" and .source_sha==$sha' >/dev/null || stop APPLY_RESPONSE_NOT_CONFIRMED
journal="$STATE/dual-$SRC.json"
sudo test -f "$journal" && sudo test ! -L "$journal" || stop JOURNAL_MISSING
[[ "$(sudo stat -c '%u:%g %a %h' "$journal")" == '0:0 600 1' ]] || stop JOURNAL_MODE
sudo jq -e --arg sha "$SRC" '.status=="APPLIED" and .source_sha==$sha' "$journal" >/dev/null || stop JOURNAL_NOT_APPLIED
s=$(sudo "$L" --status) || stop POST_STATUS
printf '%s\n' "$s" | jq -e '.services_running==true and .prepared_exists==true and .archive_reconciliation_required==false and .may_apply==false' >/dev/null || stop POST_STATUS_MISMATCH
for name in api web; do
  ids=$(sudo docker ps --filter label=com.docker.compose.project=centenario-qa-lab2 --filter "label=com.docker.compose.service=$name" --format '{{.ID}}') || stop POST_CONTAINER_LIST
  [[ "$ids" =~ ^[a-f0-9]{12,64}$ ]] || stop POST_CONTAINER_CARDINALITY
  rev=$(sudo docker inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$ids") || stop POST_CONTAINER_INSPECT
  [[ "$rev" == "$SRC" ]] || stop POST_IMAGE_REVISION
done
web=$(curl -fsS --max-time 30 --proto '=https' https://sistema.granlogiamixta.cl/qa-version.json) || stop POST_PUBLIC_VERSION
printf '%s\n' "$web" | jq -e --arg sha "$SRC" '.environment=="QA_SYNTHETIC" and .source_sha==$sha' >/dev/null || stop POST_WEB_VERSION
for path in /health/ready /identity/realms/pmgm/.well-known/openid-configuration; do
  curl -fsS -o /dev/null --max-time 30 --proto '=https' "https://sistema.granlogiamixta.cl$path" || stop POST_HEALTH
done
printf 'QA525_WINDOW=PASS_DUAL_API_WEB_APPLIED\n'
