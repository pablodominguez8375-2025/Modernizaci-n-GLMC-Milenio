#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-$ROOT/infrastructure/.env.pilot.local}"
COMPOSE_FILE="$ROOT/infrastructure/docker-compose.pilot.yml"

bash "$ROOT/scripts/preflight-pilot.sh"

set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

echo "Levantando Proyecto Milenio — piloto operacional..."
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d --build

if ! bash "$ROOT/scripts/smoke-pilot.sh"; then
  echo "El stack arrancó, pero el smoke del piloto falló." >&2
  echo "Revise: docker compose --env-file \"$ENV_FILE\" -f \"$COMPOSE_FILE\" logs --tail=300" >&2
  exit 1
fi

echo
printf 'PILOTO OPERACIONAL DISPONIBLE: %s\n' "$PMGM_PILOT_PUBLIC_URL"
echo "El primer usuario institucional debe cambiar su contraseña temporal al iniciar sesión."
