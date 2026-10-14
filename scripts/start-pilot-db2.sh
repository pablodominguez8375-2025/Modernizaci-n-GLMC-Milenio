#!/usr/bin/env bash
# Arranque explícito, opt-in, exclusivamente primer despliegue de laboratorio/piloto con datos sintéticos.
set -euo pipefail
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-$ROOT/infrastructure/.env.pilot.local}"
export PMGM_PILOT_ENV_FILE="$ENV_FILE"
bash "$ROOT/scripts/preflight-pilot-db2.sh"
compose=(docker compose --env-file "$ENV_FILE" -f "$ROOT/infrastructure/docker-compose.pilot.yml" -f "$ROOT/infrastructure/docker-compose.pilot-db2.yml")
"${compose[@]}" up -d --build
# No confundir servicio one-shot correcto con permisos reales comprobados.
bash "$ROOT/scripts/check-pilot-db2-roles.sh"
bash "$ROOT/scripts/smoke-pilot.sh"
echo "Piloto BD2 iniciado con separación de credenciales; pendiente UAT física y aprobación institucional."
