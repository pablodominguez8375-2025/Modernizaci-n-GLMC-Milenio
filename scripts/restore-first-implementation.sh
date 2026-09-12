#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/infrastructure/docker-compose.first.yml"
ENV_FILE="${PMGM_FIRST_ENV_FILE:-$ROOT/infrastructure/.env.first.local}"
BACKUP_DIR=""
CONFIRMED="false"

usage() {
  cat <<'EOF'
Uso: bash scripts/restore-first-implementation.sh --backup RUTA --yes

ADVERTENCIA: reemplaza el estado PostgreSQL y el bucket pmgm-documents del piloto.
El respaldo se verifica por SHA-256 antes de modificar el entorno.
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --backup)
      [ "$#" -ge 2 ] || { echo "Falta valor para --backup" >&2; exit 2; }
      BACKUP_DIR="$2"
      shift 2
      ;;
    --yes)
      CONFIRMED="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Argumento no reconocido: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

[ -n "$BACKUP_DIR" ] || { echo "Debe indicar --backup RUTA." >&2; exit 2; }
[ "$CONFIRMED" = "true" ] || { echo "Restauración cancelada: agregue --yes para confirmar la operación destructiva." >&2; exit 2; }

command -v docker >/dev/null 2>&1 || { echo "Docker es obligatorio." >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "Python 3 es obligatorio." >&2; exit 1; }
docker compose version >/dev/null

BACKUP_DIR="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$BACKUP_DIR")"
bash "$ROOT/scripts/verify-first-backup.sh" "$BACKUP_DIR"

compose=(docker compose -f "$COMPOSE_FILE")
if [ -f "$ENV_FILE" ]; then
  set -a
  # Archivo generado por el launcher oficial.
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

running_before="$("${compose[@]}" ps --status running --services 2>/dev/null || true)"
resume_services=()
for service in api web; do
  if printf '%s\n' "$running_before" | grep -qx "$service"; then
    resume_services+=("$service")
  fi
done

if [ "${#resume_services[@]}" -gt 0 ]; then
  echo "Deteniendo acceso a la aplicación..."
  "${compose[@]}" stop "${resume_services[@]}" >/dev/null
fi

echo "Preparando PostgreSQL y MinIO..."
"${compose[@]}" up -d postgres minio >/dev/null

postgres_ready="false"
for _ in $(seq 1 30); do
  if "${compose[@]}" exec -T postgres pg_isready -U pmgm_app -d pmgm >/dev/null 2>&1; then
    postgres_ready="true"
    break
  fi
  sleep 2
done
[ "$postgres_ready" = "true" ] || { echo "PostgreSQL no quedó disponible. API/web permanecen detenidos." >&2; exit 1; }

echo "Restaurando PostgreSQL..."
tmp_dump="/tmp/pmgm-first-restore.dump"
"${compose[@]}" exec -T postgres rm -f "$tmp_dump"
"${compose[@]}" cp "$BACKUP_DIR/database/pmgm.dump" "postgres:$tmp_dump" >/dev/null
if ! "${compose[@]}" exec -T postgres pg_restore \
  --clean \
  --if-exists \
  --no-owner \
  --no-privileges \
  --exit-on-error \
  -U pmgm_app \
  -d pmgm \
  "$tmp_dump"; then
  "${compose[@]}" exec -T postgres rm -f "$tmp_dump" >/dev/null 2>&1 || true
  echo "Falló la restauración PostgreSQL. API/web permanecen detenidos." >&2
  exit 1
fi
"${compose[@]}" exec -T postgres rm -f "$tmp_dump"

echo "Restaurando MinIO por API S3..."
if ! "${compose[@]}" run --rm -T --no-deps \
  -v "$BACKUP_DIR/objects:/backup:ro" \
  --entrypoint /bin/sh \
  minio-init \
  -c 'set -eu; until mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null 2>&1; do sleep 2; done; mc mb --ignore-existing pmgm/pmgm-documents >/dev/null; mc anonymous set none pmgm/pmgm-documents >/dev/null; mc mirror --overwrite --remove /backup/pmgm-documents pmgm/pmgm-documents'; then
  echo "Falló la restauración MinIO. API/web permanecen detenidos." >&2
  exit 1
fi

local_object_count="$(find "$BACKUP_DIR/objects/pmgm-documents" -type f | wc -l | tr -d ' ')"
remote_object_count="$("${compose[@]}" run --rm -T --no-deps --entrypoint /bin/sh minio-init -c 'set -eu; mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null; mc ls --recursive pmgm/pmgm-documents | wc -l' | tr -d '[:space:]')"
if [ "$local_object_count" != "$remote_object_count" ]; then
  echo "Conteo MinIO inconsistente tras restaurar: local=$local_object_count remoto=$remote_object_count. API/web permanecen detenidos." >&2
  exit 1
fi

if [ "${#resume_services[@]}" -gt 0 ]; then
  echo "Reanudando servicios previamente activos..."
  "${compose[@]}" up -d "${resume_services[@]}" >/dev/null
fi

echo "RESTAURACIÓN OK"
echo "Objetos MinIO restaurados: $remote_object_count"
echo "Ejecute el smoke autenticado para la validación funcional final:"
echo "  bash scripts/smoke-first-implementation.sh"
