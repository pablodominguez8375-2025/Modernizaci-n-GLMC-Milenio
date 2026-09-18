#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_QA_ENV_FILE:-/etc/pmgm/srv01.env}"
COMPOSE_FILE="$ROOT/infrastructure/docker-compose.srv01.yml"
PREPARE=false
SKIP_SMOKE=false

usage(){
  cat <<'EOF'
Uso: bash scripts/install-srv01-qa.sh [--env RUTA] [--prepare-env] [--skip-smoke]

--prepare-env  crea una plantilla de entorno protegida y termina.
--skip-smoke   levanta el stack pero no ejecuta el smoke autenticado.
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --env) ENV_FILE="${2:?Falta ruta para --env}"; shift 2 ;;
    --prepare-env) PREPARE=true; shift ;;
    --skip-smoke) SKIP_SMOKE=true; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Argumento no reconocido: $1" >&2; usage >&2; exit 2 ;;
  esac
done

prepare_env(){
  local source="$ROOT/infrastructure/srv01.env.example"
  local dir
  dir="$(dirname "$ENV_FILE")"
  if [ -e "$ENV_FILE" ]; then
    echo "El archivo ya existe: $ENV_FILE"
    return 0
  fi
  if [ -w "$dir" ] 2>/dev/null || { [ ! -e "$dir" ] && [ -w "$(dirname "$dir")" ]; }; then
    mkdir -p "$dir"
    install -m 600 "$source" "$ENV_FILE"
  else
    command -v sudo >/dev/null 2>&1 || { echo "Se requiere sudo para escribir $ENV_FILE" >&2; exit 1; }
    sudo mkdir -p "$dir"
    sudo install -m 600 "$source" "$ENV_FILE"
    sudo chown "$(id -u):$(id -g)" "$ENV_FILE" || true
  fi
  echo "Plantilla creada: $ENV_FILE"
  echo "Edite los valores REEMPLAZAR_* antes de instalar."
}

if [ "$PREPARE" = true ]; then
  prepare_env
  exit 0
fi

if [ -f "$ROOT/MANIFEST.sha256" ]; then
  echo "Verificando integridad del paquete..."
  (cd "$ROOT" && sha256sum -c MANIFEST.sha256 >/dev/null)
  echo "MANIFEST OK"
fi

PMGM_QA_ENV_FILE="$ENV_FILE" bash "$ROOT/scripts/preflight-srv01-qa.sh"

compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")

echo "Descargando imágenes base..."
"${compose[@]}" pull postgres minio minio-init clamav keycloak

echo "Construyendo API y frontend..."
"${compose[@]}" build api web

echo "Levantando QA srv01..."
"${compose[@]}" up -d

if [ "$SKIP_SMOKE" = false ]; then
  PMGM_QA_ENV_FILE="$ENV_FILE" bash "$ROOT/scripts/smoke-srv01-qa.sh"
else
  echo "Smoke omitido por solicitud explícita."
fi

echo
echo "QA SRV01 INSTALADO"
echo "Web: consulte PMGM_QA_WEB_PUBLIC_URL en $ENV_FILE"
echo "OIDC: consulte PMGM_QA_OIDC_PUBLIC_URL en $ENV_FILE"
echo "Siguiente paso: bash scripts/prepare-srv01-regression.sh"
