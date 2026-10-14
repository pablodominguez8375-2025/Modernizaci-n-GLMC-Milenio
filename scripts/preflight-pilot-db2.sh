#!/usr/bin/env bash
# Rechaza reutilización de volúmenes; no realiza cambios de base de datos.
set -euo pipefail
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-$ROOT/infrastructure/.env.pilot.local}"
export PMGM_PILOT_ENV_FILE="$ENV_FILE"
bash "$ROOT/scripts/preflight-pilot.sh"
[ "${PMGM_DB2_NEW_VOLUME_ACK:-}" = "CONFIRMO_VOLUMEN_NUEVO" ] || {
  echo "Para activar BD2 declare PMGM_DB2_NEW_VOLUME_ACK=CONFIRMO_VOLUMEN_NUEVO." >&2; exit 1;
}
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a
python3 - <<'PY'
import os
secrets = [os.environ.get(n, "") for n in (
    "PMGM_PILOT_RUNTIME_DB_PASSWORD",
    "PMGM_PILOT_MIGRATOR_DB_PASSWORD",
    "PMGM_PILOT_REPORT_DB_PASSWORD",
    "PMGM_PILOT_DB_PASSWORD",
    "PMGM_PILOT_KC_DB_PASSWORD",
)]
if any(len(s) < 20 or "REEMPLAZAR" in s or "CHANGE-ME" in s for s in secrets):
    raise SystemExit("Se requieren cinco claves distintas de al menos 20 caracteres; sin ejemplos.")
if len(set(secrets)) != len(secrets):
    raise SystemExit("Las credenciales del piloto BD2 no pueden reutilizarse.")
PY
compose=(docker compose --env-file "$ENV_FILE" -f "$ROOT/infrastructure/docker-compose.pilot.yml" -f "$ROOT/infrastructure/docker-compose.pilot-db2.yml")
volume_name="$("${compose[@]}" config --format json | python3 -c 'import json,sys; print(json.load(sys.stdin)["volumes"]["pmgm_pilot_postgres_data"]["name"])')"
[ -n "$volume_name" ] || { echo "No se pudo resolver el volumen PostgreSQL." >&2; exit 1; }
if docker volume inspect "$volume_name" >/dev/null 2>&1; then
  echo "BLOQUEADO: el volumen $volume_name ya existe; requiere migración DBA y respaldo aprobado." >&2
  exit 1
fi
if "${compose[@]}" ps -a --services | grep -qx postgres; then
  echo "BLOQUEADO: existe instancia PostgreSQL previa; no reconvertir automáticamente." >&2
  exit 1
fi
echo "PREFLIGHT BD2 OK: credenciales independientes y volumen nuevo."
