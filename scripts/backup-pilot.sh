#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/infrastructure/docker-compose.pilot.yml"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-$ROOT/infrastructure/.env.pilot.local}"
OUTPUT=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    --output) OUTPUT="${2:?Falta valor para --output}"; shift 2 ;;
    -h|--help)
      echo "Uso: bash scripts/backup-pilot.sh [--output RUTA]"
      exit 0 ;;
    *) echo "Argumento no reconocido: $1" >&2; exit 2 ;;
  esac
done

command -v docker >/dev/null 2>&1 || { echo "Docker es obligatorio." >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "Python 3 es obligatorio." >&2; exit 1; }
[ -f "$ENV_FILE" ] || { echo "No existe $ENV_FILE" >&2; exit 1; }

set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a
compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")

if [ -z "$OUTPUT" ]; then
  OUTPUT="$ROOT/backups/pmgm-pilot-$(date -u +%Y%m%dT%H%M%SZ)"
fi
OUTPUT="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$OUTPUT")"
[ ! -e "$OUTPUT" ] || { echo "La ruta ya existe: $OUTPUT" >&2; exit 1; }

running="$("${compose[@]}" ps --status running --services 2>/dev/null || true)"
for required in postgres minio; do
  printf '%s\n' "$running" | grep -qx "$required" || { echo "$required no está en ejecución." >&2; exit 1; }
done

resume=()
for service in gateway web api keycloak; do
  if printf '%s\n' "$running" | grep -qx "$service"; then resume+=("$service"); fi
done

restart_runtime() {
  if [ "${#resume[@]}" -gt 0 ]; then "${compose[@]}" up -d "${resume[@]}" >/dev/null || true; fi
}
trap restart_runtime EXIT

mkdir -p "$OUTPUT/database" "$OUTPUT/objects/pmgm-documents"
chmod 700 "$OUTPUT" 2>/dev/null || true

if [ "${#resume[@]}" -gt 0 ]; then
  echo "Pausando acceso y escrituras (gateway/web/api/keycloak)..."
  "${compose[@]}" stop "${resume[@]}" >/dev/null
fi

for database in pmgm pmgm_keycloak; do
  echo "Respaldando PostgreSQL: $database..."
  tmp="/tmp/${database}-pilot-backup.dump"
  "${compose[@]}" exec -T postgres rm -f "$tmp"
  "${compose[@]}" exec -T postgres pg_dump -U pmgm_app -d "$database" --format=custom --no-owner --no-privileges --file="$tmp"
  "${compose[@]}" cp "postgres:$tmp" "$OUTPUT/database/${database}.dump" >/dev/null
  "${compose[@]}" exec -T postgres rm -f "$tmp"
done

echo "Respaldando MinIO pmgm-documents..."
"${compose[@]}" run --rm -T --no-deps \
  -v "$OUTPUT/objects:/backup" \
  --entrypoint /bin/sh minio-init \
  -c 'set -eu; mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null; mkdir -p /backup/pmgm-documents; mc mirror --overwrite pmgm/pmgm-documents /backup/pmgm-documents'

source_commit="unknown"
if command -v git >/dev/null 2>&1; then source_commit="$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || printf unknown)"; fi

BACKUP_DIR="$OUTPUT" SOURCE_COMMIT="$source_commit" python3 - <<'PY'
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
root = Path(os.environ['BACKUP_DIR'])
files=[]
for path in sorted(p for p in root.rglob('*') if p.is_file() and p.name != 'manifest.json'):
    digest=hashlib.sha256(); size=0
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024*1024), b''):
            digest.update(chunk); size += len(chunk)
    files.append({'path': path.relative_to(root).as_posix(), 'sha256': digest.hexdigest(), 'sizeBytes': size})
for required in ('database/pmgm.dump','database/pmgm_keycloak.dump'):
    if not any(item['path']==required for item in files): raise SystemExit(f'Falta {required}')
manifest={
 'formatVersion':1,
 'project':'PMGM',
 'stack':'pilot-operational',
 'createdAtUtc':dt.datetime.now(dt.timezone.utc).isoformat().replace('+00:00','Z'),
 'sourceCommit':os.environ.get('SOURCE_COMMIT','unknown'),
 'components':{
   'postgres':{'databases':['pmgm','pmgm_keycloak'],'format':'pg_dump-custom'},
   'minio':{'bucket':'pmgm-documents','format':'s3-object-mirror'}
 },
 'fileCount':len(files),
 'totalBytes':sum(x['sizeBytes'] for x in files),
 'files':files
}
(root/'manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
PY

bash "$ROOT/scripts/verify-pilot-backup.sh" "$OUTPUT"
restart_runtime
resume=()
trap - EXIT

echo "RESPALDO PILOTO CREADO: $OUTPUT"
