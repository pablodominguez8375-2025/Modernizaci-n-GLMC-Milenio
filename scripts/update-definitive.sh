#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-/etc/pmgm/pmgm.env}"
BACKUP_ROOT="${PMGM_BACKUP_ROOT:-/opt/pmgm/backups-local}"
TARGET_REF=""
APPROVED=false
ALLOW_DEV=false

usage() {
  cat <<'EOF'
Uso:
  bash scripts/update-definitive.sh --target <tag|sha|ref> --approved [--backup-root RUTA] [--allow-dev]

Reglas:
- crea y verifica backup antes de cambiar código;
- exige repositorio limpio;
- por defecto rechaza desplegar la rama dev;
- no restaura automáticamente una BD si una migración ya pudo ejecutarse;
- si el smoke falla, deja instrucciones para rollback controlado.
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --target) TARGET_REF="${2:?Falta valor para --target}"; shift 2 ;;
    --backup-root) BACKUP_ROOT="${2:?Falta valor para --backup-root}"; shift 2 ;;
    --approved) APPROVED=true; shift ;;
    --allow-dev) ALLOW_DEV=true; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Argumento no reconocido: $1" >&2; usage; exit 2 ;;
  esac
done

[ -n "$TARGET_REF" ] || { echo "Debe indicar --target." >&2; exit 2; }
[ "$APPROVED" = true ] || { echo "Debe indicar --approved para confirmar que la versión fue autorizada para este entorno." >&2; exit 2; }
[ -d "$ROOT/.git" ] || { echo "La instalación debe conservar el repositorio Git para usar este actualizador." >&2; exit 1; }
[ -f "$ENV_FILE" ] || { echo "No existe el archivo de entorno: $ENV_FILE" >&2; exit 1; }

for cmd in git docker python3; do command -v "$cmd" >/dev/null 2>&1 || { echo "Falta comando obligatorio: $cmd" >&2; exit 1; }; done
docker compose version >/dev/null 2>&1 || { echo "Docker Compose v2 es obligatorio." >&2; exit 1; }

if [ -n "$(git -C "$ROOT" status --porcelain)" ]; then
  echo "El repositorio tiene cambios locales. No se actualiza una VM institucional con working tree sucio." >&2
  exit 1
fi

if [ "$TARGET_REF" = "dev" ] && [ "$ALLOW_DEV" != true ]; then
  echo "Se rechaza desplegar dev por defecto. Use una versión/SHA aprobada. --allow-dev queda reservado para una decisión operacional explícita." >&2
  exit 1
fi

CURRENT_SHA="$(git -C "$ROOT" rev-parse HEAD)"
echo "Versión actual: $CURRENT_SHA"

git -C "$ROOT" fetch origin --tags --prune
TARGET_SHA="$(git -C "$ROOT" rev-parse "${TARGET_REF}^{commit}" 2>/dev/null || true)"
[ -n "$TARGET_SHA" ] || { echo "No se pudo resolver target: $TARGET_REF" >&2; exit 1; }

if [ "$TARGET_SHA" = "$CURRENT_SHA" ]; then
  echo "El target ya está desplegado: $TARGET_SHA"
  exit 0
fi

echo "Target aprobado: $TARGET_REF -> $TARGET_SHA"

mkdir -p "$BACKUP_ROOT"
chmod 700 "$BACKUP_ROOT" 2>/dev/null || true
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
BACKUP_PATH="$BACKUP_ROOT/pre-update-${STAMP}-${CURRENT_SHA:0:12}"

export PMGM_PILOT_ENV_FILE="$ENV_FILE"
echo "Creando backup pre-update: $BACKUP_PATH"
bash "$ROOT/scripts/backup-pilot.sh" --output "$BACKUP_PATH"
bash "$ROOT/scripts/verify-pilot-backup.sh" "$BACKUP_PATH"

echo "Backup pre-update verificado. Cambiando código..."
git -C "$ROOT" checkout --detach "$TARGET_SHA"

rollback_code_only() {
  echo "Restaurando sólo el código anterior: $CURRENT_SHA" >&2
  git -C "$ROOT" checkout --detach "$CURRENT_SHA" >/dev/null 2>&1 || true
}

if ! bash "$ROOT/scripts/preflight-pilot.sh"; then
  echo "El target no supera preflight. No se inició actualización de contenedores." >&2
  rollback_code_only
  exit 1
fi

COMPOSE_FILE="$ROOT/infrastructure/docker-compose.pilot.yml"
compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")

set +e
"${compose[@]}" up -d --build
DEPLOY_RC=$?
set -e

if [ "$DEPLOY_RC" -ne 0 ]; then
  cat >&2 <<EOF
FALLO DURANTE BUILD/UP DEL TARGET.

El backup pre-update está verificado en:
  $BACKUP_PATH

No se ejecuta rollback automático porque una migración de base de datos pudo haberse iniciado.
Procedimiento controlado:
1. revisar logs;
2. detener acceso público si corresponde;
3. evaluar si basta volver al código $CURRENT_SHA o si debe restaurarse el backup;
4. para restauración de datos usar scripts/restore-pilot.sh con confirmación explícita;
5. ejecutar smoke antes de reabrir servicio.
EOF
  exit 1
fi

if ! bash "$ROOT/scripts/smoke-pilot.sh"; then
  cat >&2 <<EOF
EL TARGET ARRANCÓ PERO EL SMOKE FALLÓ.

Target: $TARGET_SHA
Anterior: $CURRENT_SHA
Backup pre-update: $BACKUP_PATH

No se restaura automáticamente la base de datos. Mantenga trazabilidad y ejecute rollback controlado según el impacto de migraciones.
EOF
  exit 1
fi

LOG_DIR="${PMGM_DEPLOY_LOG_DIR:-/var/log/pmgm}"
if mkdir -p "$LOG_DIR" 2>/dev/null && [ -w "$LOG_DIR" ]; then
  python3 - "$LOG_DIR/deployment-${STAMP}.json" "$CURRENT_SHA" "$TARGET_SHA" "$TARGET_REF" "$BACKUP_PATH" <<'PY'
import datetime as dt
import json
import sys
path, previous, target, ref, backup = sys.argv[1:]
payload = {
    "project": "PMGM",
    "event": "deployment",
    "executedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
    "previousSha": previous,
    "targetSha": target,
    "targetRef": ref,
    "preUpdateBackup": backup,
    "smoke": "pass"
}
with open(path, "w", encoding="utf-8") as handle:
    json.dump(payload, handle, indent=2, ensure_ascii=False)
    handle.write("\n")
PY
fi

echo "ACTUALIZACIÓN COMPLETADA"
echo "Anterior: $CURRENT_SHA"
echo "Actual:   $TARGET_SHA"
echo "Backup:   $BACKUP_PATH"
echo "Smoke:    PASS"
