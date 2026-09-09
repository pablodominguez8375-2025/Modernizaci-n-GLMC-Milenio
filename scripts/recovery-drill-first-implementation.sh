#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/infrastructure/docker-compose.first.yml"
ENV_FILE="${PMGM_FIRST_ENV_FILE:-$ROOT/infrastructure/.env.first.local}"
OUTPUT="${1:-}"

command -v python3 >/dev/null 2>&1 || { echo "Python 3 es obligatorio." >&2; exit 1; }
if [ -z "$OUTPUT" ]; then
  OUTPUT="$ROOT/backups/recovery-drill-$(date -u +%Y%m%dT%H%M%SZ)"
fi
OUTPUT="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$OUTPUT")"

compose=(docker compose -f "$COMPOSE_FILE")
if [ -f "$ENV_FILE" ]; then
  set -a
  # shellcheck disable=SC1090
  . "$ENV_FILE"
  set +a
  compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")
fi

: "${PMGM_FIRST_DB_PASSWORD:?PMGM_FIRST_DB_PASSWORD es obligatorio}"
: "${PMGM_FIRST_MINIO_USER:?PMGM_FIRST_MINIO_USER es obligatorio}"
: "${PMGM_FIRST_MINIO_PASSWORD:?PMGM_FIRST_MINIO_PASSWORD es obligatorio}"
: "${PMGM_FIRST_KEYCLOAK_ADMIN_PASSWORD:?PMGM_FIRST_KEYCLOAK_ADMIN_PASSWORD es obligatorio}"
: "${PMGM_QA_USER_PASSWORD:?PMGM_QA_USER_PASSWORD es obligatorio}"

echo "=== Recovery drill PMGM v0.31 ==="
bash "$ROOT/scripts/backup-first-implementation.sh" --output "$OUTPUT"
bash "$ROOT/scripts/verify-first-backup.sh" "$OUTPUT"

object_count="$(find "$OUTPUT/objects/pmgm-documents" -type f | wc -l | tr -d ' ')"
if [ "$object_count" -lt 1 ]; then
  echo "El recovery drill requiere al menos un documento ficticio en MinIO." >&2
  exit 1
fi

echo "Simulando corrupción controlada de datos ficticios..."
"${compose[@]}" exec -T postgres psql \
  -v ON_ERROR_STOP=1 \
  -U pmgm_app \
  -d pmgm \
  -c 'UPDATE core.members SET "InstitutionalNumber" = '\''GLM-QA-BROKEN'\'' WHERE "InstitutionalNumber" = '\''GLM-QA-0230'\'';' >/dev/null

corrupted_count="$("${compose[@]}" exec -T postgres psql -At -U pmgm_app -d pmgm -c 'SELECT count(*) FROM core.members WHERE "InstitutionalNumber" = '\''GLM-QA-BROKEN'\'';' | tr -d '[:space:]')"
[ "$corrupted_count" = "1" ] || { echo "No fue posible aplicar la mutación controlada PostgreSQL." >&2; exit 1; }

"${compose[@]}" run --rm -T --no-deps --entrypoint /bin/sh minio-init \
  -c 'set -eu; mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null; mc rm --recursive --force pmgm/pmgm-documents >/dev/null'

remaining_objects="$("${compose[@]}" run --rm -T --no-deps --entrypoint /bin/sh minio-init \
  -c 'set -eu; mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null; mc find pmgm/pmgm-documents --type f | wc -l' | tr -d '[:space:]')"
[ "$remaining_objects" = "0" ] || { echo "La simulación MinIO no dejó el bucket vacío." >&2; exit 1; }

echo "Restaurando desde respaldo verificado..."
bash "$ROOT/scripts/restore-first-implementation.sh" --backup "$OUTPUT" --yes

echo "Validando funcionalmente el entorno recuperado..."
bash "$ROOT/scripts/smoke-first-implementation.sh"

echo "RECOVERY DRILL OK"
echo "Respaldo utilizado: $OUTPUT"
