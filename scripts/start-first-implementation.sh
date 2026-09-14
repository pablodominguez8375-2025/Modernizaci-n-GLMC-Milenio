#!/usr/bin/env sh
set -eu

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
COMPOSE="$ROOT/infrastructure/docker-compose.first.yml"
ENV_FILE="$ROOT/infrastructure/.env.first.local"

command -v docker >/dev/null 2>&1 || { echo "Docker no está disponible." >&2; exit 1; }

new_secret() {
  od -An -N32 -tx1 /dev/urandom | tr -d ' \n'
}

if [ ! -f "$ENV_FILE" ]; then
  old_umask=$(umask)
  umask 077
  {
    printf 'PMGM_FIRST_DB_PASSWORD=%s\n' "$(new_secret)"
    printf 'PMGM_FIRST_MINIO_USER=pmgmqa\n'
    printf 'PMGM_FIRST_MINIO_PASSWORD=%s\n' "$(new_secret)"
    printf 'PMGM_FIRST_KEYCLOAK_ADMIN_PASSWORD=%s\n' "$(new_secret)"
    printf 'PMGM_QA_USER_PASSWORD=%s\n' "$(new_secret)"
  } > "$ENV_FILE"
  umask "$old_umask"
  echo "Secretos QA generados en $ENV_FILE"
fi

cd "$ROOT"
if [ "${1:-}" = "--reset" ]; then
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE" down -v --remove-orphans
fi

echo "Construyendo y levantando Proyecto Milenio v0.30..."
docker compose --env-file "$ENV_FILE" -f "$COMPOSE" up -d --build

wait_url() {
  url="$1"; name="$2"; i=0
  while [ "$i" -lt 120 ]; do
    if curl -fsS --max-time 4 "$url" >/dev/null 2>&1; then
      echo "✓ $name disponible"
      return 0
    fi
    i=$((i + 1)); sleep 2
  done
  echo "$name no quedó disponible. Revise docker compose logs." >&2
  return 1
}

wait_url "http://127.0.0.1:8180/realms/pmgm/.well-known/openid-configuration" "Identidad Keycloak"
wait_url "http://127.0.0.1:8081/health/live" "API PMGM"
wait_url "http://127.0.0.1:8081/health/ready" "Base de datos PMGM"
wait_url "http://127.0.0.1:8081/health/web" "Interfaz web"

echo ""
echo "Proyecto Milenio — primera implementación lista."
echo "Aplicación: http://127.0.0.1:8081"
echo "Usuarios QA: qa.admin y qa.taller23"
echo "Contraseña QA: almacenada localmente en $ENV_FILE"
echo "Entorno: sólo local/QA; no exponer a Internet."
