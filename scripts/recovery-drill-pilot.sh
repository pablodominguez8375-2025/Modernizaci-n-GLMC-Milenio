#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/infrastructure/docker-compose.pilot.yml"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-$ROOT/infrastructure/.env.pilot.local}"
OUTPUT="${1:-$ROOT/backups/pilot-recovery-drill-$(date -u +%Y%m%dT%H%M%SZ)}"

[ -f "$ENV_FILE" ] || { echo "No existe $ENV_FILE" >&2; exit 1; }
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

[ "${PMGM_PILOT_RECOVERY_DRILL_ALLOWED:-false}" = "true" ] || {
  echo "Recovery drill bloqueado. Requiere PMGM_PILOT_RECOVERY_DRILL_ALLOWED=true." >&2
  exit 1
}
case "${PMGM_PILOT_HOSTNAME:-}" in
  localhost|127.0.0.1|::1) ;;
  *) echo "Recovery drill sólo puede ejecutarse contra un piloto localhost/CI." >&2; exit 1 ;;
esac
[ "${PMGM_PILOT_ALLOW_LOCALHOST:-false}" = "true" ] || { echo "Recovery drill requiere PMGM_PILOT_ALLOW_LOCALHOST=true." >&2; exit 1; }

compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")
probe_table="pmgm_recovery_probe_v032"
probe_value="before-backup"
probe_object="__pmgm_recovery_probe_v032__/probe.txt"

cleanup_probe() {
  for database in pmgm pmgm_keycloak; do
    "${compose[@]}" exec -T postgres psql -v ON_ERROR_STOP=1 -U pmgm_app -d "$database" -c "DROP TABLE IF EXISTS public.${probe_table};" >/dev/null 2>&1 || true
  done
  "${compose[@]}" run --rm -T --no-deps --entrypoint /bin/sh minio-init \
    -c "set -eu; mc alias set pmgm http://minio:9000 \"\$MINIO_ACCESS_KEY\" \"\$MINIO_SECRET_KEY\" >/dev/null; mc rm --force pmgm/pmgm-documents/${probe_object} >/dev/null 2>&1 || true" >/dev/null 2>&1 || true
}
trap cleanup_probe EXIT

echo "=== Recovery drill piloto v0.32 (localhost/CI) ==="
for database in pmgm pmgm_keycloak; do
  "${compose[@]}" exec -T postgres psql -v ON_ERROR_STOP=1 -U pmgm_app -d "$database" \
    -c "DROP TABLE IF EXISTS public.${probe_table}; CREATE TABLE public.${probe_table}(value text NOT NULL); INSERT INTO public.${probe_table}(value) VALUES ('${probe_value}');" >/dev/null
done

printf '%s\n' "$probe_value" | "${compose[@]}" run --rm -T --no-deps --entrypoint /bin/sh minio-init \
  -c "set -eu; mc alias set pmgm http://minio:9000 \"\$MINIO_ACCESS_KEY\" \"\$MINIO_SECRET_KEY\" >/dev/null; mc pipe pmgm/pmgm-documents/${probe_object} >/dev/null"

echo "Sondas temporales creadas; generando respaldo..."
bash "$ROOT/scripts/backup-pilot.sh" --output "$OUTPUT"
bash "$ROOT/scripts/verify-pilot-backup.sh" "$OUTPUT"

for database in pmgm pmgm_keycloak; do
  "${compose[@]}" exec -T postgres psql -v ON_ERROR_STOP=1 -U pmgm_app -d "$database" \
    -c "UPDATE public.${probe_table} SET value='after-backup';" >/dev/null
done
"${compose[@]}" run --rm -T --no-deps --entrypoint /bin/sh minio-init \
  -c 'set -eu; mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null; mc rm --recursive --force pmgm/pmgm-documents >/dev/null'

echo "Estado alterado; restaurando..."
bash "$ROOT/scripts/restore-pilot.sh" --backup "$OUTPUT" --yes

for database in pmgm pmgm_keycloak; do
  restored="$("${compose[@]}" exec -T postgres psql -At -U pmgm_app -d "$database" -c "SELECT value FROM public.${probe_table};" | tr -d '\r\n')"
  [ "$restored" = "$probe_value" ] || { echo "Restore de $database no recuperó la sonda: $restored" >&2; exit 1; }
done

object_value="$("${compose[@]}" run --rm -T --no-deps --entrypoint /bin/sh minio-init \
  -c "set -eu; mc alias set pmgm http://minio:9000 \"\$MINIO_ACCESS_KEY\" \"\$MINIO_SECRET_KEY\" >/dev/null; mc cat pmgm/pmgm-documents/${probe_object}" | tr -d '\r\n')"
[ "$object_value" = "$probe_value" ] || { echo "Restore MinIO no recuperó la sonda." >&2; exit 1; }

bash "$ROOT/scripts/smoke-pilot.sh"
cleanup_probe
trap - EXIT

echo "RECOVERY DRILL PILOTO OK"
