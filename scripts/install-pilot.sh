#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-/etc/pmgm/pmgm.env}"
BACKUP_ROOT="${PMGM_INITIAL_BACKUP_ROOT:-$ROOT/backups}"
PREPARE_ENV=false
SKIP_INITIAL_BACKUP=false

usage() {
  cat <<'EOF'
Proyecto Milenio — instalador guiado del piloto institucional

Uso:
  bash scripts/install-pilot.sh [opciones]

Opciones:
  --env RUTA               Archivo de entorno del piloto. Por defecto /etc/pmgm/pmgm.env.
  --backup-root RUTA       Carpeta donde crear el respaldo inicial verificado.
  --prepare-env            Crea una copia protegida de pilot.env.example y termina.
  --skip-initial-backup    Omite el respaldo inicial. Sólo para laboratorio/UAT controlado.
  -h, --help               Muestra esta ayuda.

Flujo normal:
  integridad del paquete -> preflight VM -> preflight piloto -> arranque -> smoke -> respaldo inicial
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --env)
      ENV_FILE="${2:?Falta valor para --env}"
      shift 2
      ;;
    --backup-root)
      BACKUP_ROOT="${2:?Falta valor para --backup-root}"
      shift 2
      ;;
    --prepare-env)
      PREPARE_ENV=true
      shift
      ;;
    --skip-initial-backup)
      SKIP_INITIAL_BACKUP=true
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

ENV_FILE="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$ENV_FILE" 2>/dev/null || printf '%s' "$ENV_FILE")"
BACKUP_ROOT="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$BACKUP_ROOT" 2>/dev/null || printf '%s' "$BACKUP_ROOT")"

prepare_env() {
  local template="$ROOT/infrastructure/pilot.env.example"
  [ -f "$template" ] || { echo "No existe la plantilla $template" >&2; exit 1; }
  if [ -e "$ENV_FILE" ]; then
    echo "No se reemplazará el archivo existente: $ENV_FILE" >&2
    exit 1
  fi

  local parent
  parent="$(dirname "$ENV_FILE")"
  if ! mkdir -p "$parent" 2>/dev/null; then
    echo "No se puede crear $parent con el usuario actual." >&2
    echo "Cree la carpeta con permisos administrativos y vuelva a ejecutar --prepare-env." >&2
    exit 1
  fi

  if command -v install >/dev/null 2>&1; then
    install -m 600 "$template" "$ENV_FILE"
  else
    cp "$template" "$ENV_FILE"
    chmod 600 "$ENV_FILE" 2>/dev/null || true
  fi

  echo "Plantilla creada: $ENV_FILE"
  echo "Edítela y reemplace todos los valores de ejemplo por datos institucionales válidos."
  echo "Luego ejecute nuevamente este instalador sin --prepare-env."
}

if "$PREPARE_ENV"; then
  prepare_env
  exit 0
fi

printf 'Proyecto Milenio — instalación guiada del piloto\n'
printf 'Raíz de aplicación: %s\n' "$ROOT"
printf 'Entorno: %s\n\n' "$ENV_FILE"

for required_file in \
  "$ROOT/infrastructure/docker-compose.pilot.yml" \
  "$ROOT/infrastructure/pilot.env.example" \
  "$ROOT/scripts/preflight-definitive-host.sh" \
  "$ROOT/scripts/preflight-pilot.sh" \
  "$ROOT/scripts/start-pilot.sh" \
  "$ROOT/scripts/smoke-pilot.sh" \
  "$ROOT/scripts/backup-pilot.sh" \
  "$ROOT/scripts/verify-pilot-backup.sh"; do
  [ -f "$required_file" ] || { echo "Falta archivo obligatorio: $required_file" >&2; exit 1; }
done

[ -f "$ENV_FILE" ] || {
  echo "No existe el archivo de entorno: $ENV_FILE" >&2
  echo "Puede crear una plantilla con:" >&2
  echo "  bash scripts/install-pilot.sh --env '$ENV_FILE' --prepare-env" >&2
  exit 1
}

if [ -f "$ROOT/MANIFEST.sha256" ]; then
  echo "Verificando integridad del paquete..."
  (
    cd "$ROOT"
    sha256sum -c MANIFEST.sha256
  )
fi

if [ -f "$ROOT/BUILD-INFO.txt" ]; then
  echo
  echo "Identidad del paquete:"
  grep -E '^(PRODUCT|CHANNEL|SOURCE_SHA|SOURCE_REF|BUILD_RUN_NUMBER|BUILD_UTC)=' "$ROOT/BUILD-INFO.txt" || true
fi

export PMGM_PILOT_ENV_FILE="$ENV_FILE"

echo
echo "[1/4] Preflight de VM definitiva"
bash "$ROOT/scripts/preflight-definitive-host.sh"

echo
echo "[2/4] Preflight de configuración institucional"
bash "$ROOT/scripts/preflight-pilot.sh"

echo
echo "[3/4] Arranque HTTPS/OIDC y smoke autenticado"
bash "$ROOT/scripts/start-pilot.sh"

if "$SKIP_INITIAL_BACKUP"; then
  echo
echo "[4/4] Respaldo inicial omitido por opción explícita --skip-initial-backup."
else
  timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
  backup_path="$BACKUP_ROOT/initial-$timestamp"
  echo
echo "[4/4] Creando respaldo inicial verificado"
  bash "$ROOT/scripts/backup-pilot.sh" --output "$backup_path"
  echo "Respaldo inicial: $backup_path"
fi

cat <<EOF

INSTALACIÓN TÉCNICA COMPLETADA

Siguiente control obligatorio:
- confirmar acceso HTTPS y login institucional;
- copiar el respaldo inicial verificado fuera de la VM;
- ejecutar UAT institucional;
- documentar aprobación del Sponsor/Product Owner antes de promoción estable;
- no cargar datos personales reales hasta completar los gates jurídicos y organizacionales aplicables.

La VM queda preparada para actualizaciones controladas; nunca debe seguir la rama dev automáticamente.
EOF
