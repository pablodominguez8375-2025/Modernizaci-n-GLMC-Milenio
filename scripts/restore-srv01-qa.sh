#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_QA_ENV_FILE:-/etc/pmgm/srv01.env}"
COMPOSE_FILE="$ROOT/infrastructure/docker-compose.srv01.yml"
BACKUP_DIR=""
CONFIRMED=false
DATA_ONLY=false

while [ "$#" -gt 0 ]; do
  case "$1" in
    --env) ENV_FILE="${2:?Falta ruta para --env}"; shift 2 ;;
    --backup) BACKUP_DIR="${2:?Falta ruta para --backup}"; shift 2 ;;
    --yes) CONFIRMED=true; shift ;;
    --data-only) DATA_ONLY=true; shift ;;
    -h|--help) echo "Uso: bash scripts/restore-srv01-qa.sh --backup RUTA --yes [--env RUTA] [--data-only: no reinicia aplicación ni hace smoke]"; exit 0 ;;
    *) echo "Argumento no reconocido: $1" >&2; exit 2 ;;
  esac
done

[ -n "$BACKUP_DIR" ] || { echo "Debe indicar --backup RUTA." >&2; exit 2; }
[ "$CONFIRMED" = true ] || { echo "Agregue --yes para confirmar restore destructivo." >&2; exit 2; }
[ -f "$ENV_FILE" ] || { echo "No existe $ENV_FILE" >&2; exit 1; }

BACKUP_DIR="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$BACKUP_DIR")"
bash "$ROOT/scripts/verify-srv01-backup.sh" "$BACKUP_DIR"

set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

# Validate storage before stopping services, restoring databases or starting containers.
python3 "$ROOT/scripts/check-qa-storage-config.py" --compose-file "$COMPOSE_FILE" --env-file "$ENV_FILE"

compose=(docker compose -p "${PMGM_QA_COMPOSE_PROJECT:-pmgm-srv01}" --env-file "$ENV_FILE" -f "$COMPOSE_FILE")

running="$("${compose[@]}" ps --status running --services 2>/dev/null || true)"
resume=()
for service in web api keycloak; do
  if printf '%s\n' "$running" | grep -qx "$service"; then resume+=("$service"); fi
done
if [ "${#resume[@]}" -gt 0 ]; then
  "${compose[@]}" stop "${resume[@]}" >/dev/null
fi

"${compose[@]}" up -d postgres minio >/dev/null
ready=false
for _ in $(seq 1 40); do
  if "${compose[@]}" exec -T postgres pg_isready -U pmgm_app -d pmgm >/dev/null 2>&1; then ready=true; break; fi
  sleep 2
done
[ "$ready" = true ] || { echo "PostgreSQL no quedó disponible." >&2; exit 1; }

# Reject non-empty/unavailable destination bucket before any database restore.
# Operator must provision Silo accounts on the new, isolated QA volume first.
"${compose[@]}" run --rm -T --no-deps --entrypoint /bin/sh storage-backup-client -ec \
  'set -eu; mcli alias set qa http://minio:9000 "$BACKUP_ACCESS_KEY" "$BACKUP_SECRET_KEY" >/dev/null; listing="$(mcli ls qa/pmgm-documents 2>/dev/null)" || exit 1; test -z "$listing"' \
  || { echo "Restauración bloqueada: bucket inexistente, inaccesible o no vacío." >&2; exit 1; }
for database in pmgm pmgm_keycloak; do
  echo "Restaurando PostgreSQL: $database"
  tmp="/tmp/${database}-srv01-restore.dump"
  "${compose[@]}" exec -T postgres rm -f "$tmp"
  "${compose[@]}" cp "$BACKUP_DIR/database/${database}.dump" "postgres:$tmp" >/dev/null
  "${compose[@]}" exec -T postgres pg_restore     --clean --if-exists --no-owner --no-privileges --exit-on-error     -U pmgm_app -d "$database" "$tmp"
  "${compose[@]}" exec -T postgres rm -f "$tmp"
done

echo "Restaurando objetos Silo sintéticos en bucket vacío..."
"${compose[@]}" run --rm -T --no-deps \
  -v "$BACKUP_DIR/objects:/backup:ro" --entrypoint /bin/sh storage-backup-client -ec \
  'set -eu; mcli alias set qa http://minio:9000 "$BACKUP_ACCESS_KEY" "$BACKUP_SECRET_KEY" >/dev/null; mcli mirror --overwrite /backup/pmgm-documents qa/pmgm-documents'

if [ "$DATA_ONLY" = true ]; then
  echo "RESTORE DATOS QA COMPLETADO; aplicación detenida, smoke PENDIENTE"
  exit 0
fi
if [ "${#resume[@]}" -gt 0 ]; then
  "${compose[@]}" up -d "${resume[@]}" >/dev/null
fi

echo "RESTORE QA SRV01 OK"
PMGM_QA_ENV_FILE="$ENV_FILE" bash "$ROOT/scripts/smoke-srv01-qa.sh"
