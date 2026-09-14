#!/usr/bin/env sh
set -eu

PORT="${1:-8082}"
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
cd "$REPO_ROOT"

export PMGM_SHOWCASE_HTTP_PORT="$PORT"
echo "Construyendo Proyecto Milenio Showcase..."
docker compose -f infrastructure/docker-compose.showcase.yml up -d --build

echo "Showcase disponible en http://127.0.0.1:$PORT"
echo "Salud: http://127.0.0.1:$PORT/health/web"
