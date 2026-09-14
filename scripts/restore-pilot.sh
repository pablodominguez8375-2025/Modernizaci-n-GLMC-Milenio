#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/infrastructure/docker-compose.pilot.yml"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-$ROOT/infrastructure/.env.pilot.local}"
BACKUP_DIR=""
CONFIRMED="false"

while [ "$#" -gt 0 ]; do
  case "$1" in
    --backup) BACKUP_DIR="${2:?Falta valor para --backup}"; shift 2 ;;
    --yes) CONFIRMED="true"; shift ;;
    -h|--help)
      echo "Uso: bash scripts/restore-pilot.sh --backup RUTA --yes"
      exit 0 ;;
    *) echo "Argumento no reconocido: $1" >&2; exit 2 ;;
  esac
done

[ -n "$BACKUP_DIR" ] || { echo "Debe indicar --backup RUTA." >&2; exit 2; }
[ "$CONFIRMED" = "true" ] || { echo "Agregue --yes para confirmar la restauración destructiva." >&2; exit 2; }
[ -f "$ENV_FILE" ] || { echo "No existe $ENV_FILE" >&2; exit 1; }
command -v docker >/dev/null 2>&1 || { echo "Docker es obligatorio." >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "Python 3 es obligatorio." >&2; exit 1; }

BACKUP_DIR="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$BACKUP_DIR")"
bash "$ROOT/scripts/verify-pilot-backup.sh" "$BACKUP_DIR"

set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a
compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")

running="$("${compose[@]}" ps --status running --services 2>/dev/null || true)"
resume=()
for service in gateway web api keycloak; do
  if printf '%s\n' "$running" | grep -qx "$service"; then resume+=("$service"); fi
done

if [ "${#resume[@]}" -gt 0 ]; then
  echo "Deteniendo acceso al piloto..."
  "${compose[@]}" stop "${resume[@]}" >/dev/null
fi

echo "Preparando PostgreSQL y MinIO..."
"${compose[@]}" up -d postgres minio >/dev/null
ready=false
for _ in $(seq 1 30); do
  if "${compose[@]}" exec -T postgres pg_isready -U pmgm_app -d pmgm >/dev/null 2>&1; then ready=true; break; fi
  sleep 2
done
[ "$ready" = "true" ] || { echo "PostgreSQL no quedó disponible; los servicios públicos permanecen detenidos." >&2; exit 1; }

for database in pmgm pmgm_keycloak; do
  echo "Restaurando PostgreSQL: $database..."
  tmp="/tmp/${database}-pilot-restore.dump"
  "${compose[@]}" exec -T postgres rm -f "$tmp"
  "${compose[@]}" cp "$BACKUP_DIR/database/${database}.dump" "postgres:$tmp" >/dev/null
  if ! "${compose[@]}" exec -T postgres pg_restore \
    --clean --if-exists --no-owner --no-privileges --exit-on-error \
    -U pmgm_app -d "$database" "$tmp"; then
    "${compose[@]}" exec -T postgres rm -f "$tmp" >/dev/null 2>&1 || true
    echo "Falló restore de $database; gateway/web/api/keycloak permanecen detenidos." >&2
    exit 1
  fi
  "${compose[@]}" exec -T postgres rm -f "$tmp"
done

echo "Restaurando MinIO..."
if ! "${compose[@]}" run --rm -T --no-deps \
  -v "$BACKUP_DIR/objects:/backup:ro" \
  --entrypoint /bin/sh minio-init \
  -c 'set -eu; until mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null 2>&1; do sleep 2; done; mc mb --ignore-existing pmgm/pmgm-documents >/dev/null; mc anonymous set none pmgm/pmgm-documents >/dev/null; mc mirror --overwrite --remove /backup/pmgm-documents pmgm/pmgm-documents'; then
  echo "Falló restore MinIO; servicios públicos permanecen detenidos." >&2
  exit 1
fi

local_count="$(find "$BACKUP_DIR/objects/pmgm-documents" -type f | wc -l | tr -d ' ')"
remote_count="$("${compose[@]}" run --rm -T --no-deps --entrypoint /bin/sh minio-init \
  -c 'set -eu; mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null; mc ls --recursive pmgm/pmgm-documents | wc -l' | tr -d '[:space:]')"
[ "$local_count" = "$remote_count" ] || {
  echo "Conteo MinIO inconsistente: local=$local_count remoto=$remote_count; servicios públicos permanecen detenidos." >&2
  exit 1
}

if [ "${#resume[@]}" -gt 0 ]; then
  echo "Reanudando servicios..."
  "${compose[@]}" up -d "${resume[@]}" >/dev/null
fi

echo "RESTAURACIÓN PILOTO OK"
echo "Objetos MinIO restaurados: $remote_count"
echo "Ejecute: bash scripts/smoke-pilot.sh"
