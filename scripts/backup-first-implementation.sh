#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/infrastructure/docker-compose.first.yml"
ENV_FILE="${PMGM_FIRST_ENV_FILE:-$ROOT/infrastructure/.env.first.local}"
OUTPUT=""

usage() {
  cat <<'EOF'
Uso: bash scripts/backup-first-implementation.sh [--output RUTA]

Crea un respaldo consistente del piloto local:
- PostgreSQL: dump lógico custom de la base pmgm.
- MinIO: espejo por API S3 del bucket pmgm-documents.
- manifest.json: SHA-256, tamaños, fecha UTC y commit fuente.

Por consistencia, detiene temporalmente api/web y los vuelve a iniciar si estaban activos.
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --output)
      [ "$#" -ge 2 ] || { echo "Falta valor para --output" >&2; exit 2; }
      OUTPUT="$2"
      shift 2
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

command -v docker >/dev/null 2>&1 || { echo "Docker es obligatorio." >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "Python 3 es obligatorio para generar el manifiesto." >&2; exit 1; }

docker compose version >/dev/null

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

if [ -z "$OUTPUT" ]; then
  timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
  OUTPUT="$ROOT/backups/pmgm-first-$timestamp"
fi
OUTPUT="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$OUTPUT")"

if [ -e "$OUTPUT" ]; then
  echo "La ruta de respaldo ya existe: $OUTPUT" >&2
  exit 1
fi

running_services="$(${compose[@]} ps --status running --services 2>/dev/null || true)"
for required in postgres minio; do
  if ! printf '%s\n' "$running_services" | grep -qx "$required"; then
    echo "El servicio $required no está en ejecución. Inicie primero la primera implementación." >&2
    exit 1
  fi
done

restart_services=()
for service in api web; do
  if printf '%s\n' "$running_services" | grep -qx "$service"; then
    restart_services+=("$service")
  fi
done

restore_runtime() {
  if [ "${#restart_services[@]}" -gt 0 ]; then
    "${compose[@]}" up -d "${restart_services[@]}" >/dev/null || true
  fi
}
trap restore_runtime EXIT

mkdir -p "$OUTPUT/database" "$OUTPUT/objects/pmgm-documents"
chmod 700 "$OUTPUT" 2>/dev/null || true

if [ "${#restart_services[@]}" -gt 0 ]; then
  echo "Pausando escrituras (api/web)..."
  "${compose[@]}" stop "${restart_services[@]}" >/dev/null
fi

echo "Respaldando PostgreSQL..."
tmp_dump="/tmp/pmgm-first-backup.dump"
"${compose[@]}" exec -T postgres rm -f "$tmp_dump"
"${compose[@]}" exec -T postgres pg_dump \
  -U pmgm_app \
  -d pmgm \
  --format=custom \
  --no-owner \
  --no-privileges \
  --file="$tmp_dump"
"${compose[@]}" cp "postgres:$tmp_dump" "$OUTPUT/database/pmgm.dump" >/dev/null
"${compose[@]}" exec -T postgres rm -f "$tmp_dump"

echo "Respaldando documentos MinIO por API S3..."
"${compose[@]}" run --rm -T --no-deps \
  -v "$OUTPUT/objects:/backup" \
  --entrypoint /bin/sh \
  minio-init \
  -c 'set -eu; mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null; mkdir -p /backup/pmgm-documents; mc mirror --overwrite pmgm/pmgm-documents /backup/pmgm-documents'

source_commit="unknown"
if command -v git >/dev/null 2>&1; then
  source_commit="$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || printf unknown)"
fi

BACKUP_DIR="$OUTPUT" SOURCE_COMMIT="$source_commit" python3 - <<'PY'
import datetime as dt
import hashlib
import json
import os
from pathlib import Path

root = Path(os.environ["BACKUP_DIR"])
files = []
for path in sorted(p for p in root.rglob("*") if p.is_file() and p.name != "manifest.json"):
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
            size += len(chunk)
    files.append({
        "path": path.relative_to(root).as_posix(),
        "sha256": digest.hexdigest(),
        "sizeBytes": size,
    })

if not any(item["path"] == "database/pmgm.dump" for item in files):
    raise SystemExit("El dump PostgreSQL no fue generado.")

manifest = {
    "formatVersion": 1,
    "project": "PMGM",
    "stack": "first-implementation",
    "createdAtUtc": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
    "sourceCommit": os.environ.get("SOURCE_COMMIT", "unknown"),
    "components": {
        "postgres": {"database": "pmgm", "format": "pg_dump-custom"},
        "minio": {"bucket": "pmgm-documents", "format": "s3-object-mirror"},
    },
    "fileCount": len(files),
    "totalBytes": sum(item["sizeBytes"] for item in files),
    "files": files,
}
(root / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
PY

echo "Reanudando servicios..."
restore_runtime
restart_services=()
trap - EXIT

echo "Respaldo creado: $OUTPUT"
echo "Verifique antes de restaurar con: bash scripts/verify-first-backup.sh \"$OUTPUT\""
