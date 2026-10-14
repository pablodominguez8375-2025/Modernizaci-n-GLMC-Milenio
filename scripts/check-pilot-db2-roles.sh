#!/usr/bin/env bash
# Inspección de privilegios y pruebas NEGATIVAS no destructivas en PostgreSQL real.
# Diseñado exclusivamente para el entorno sintético piloto BD2.
set -euo pipefail
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-$ROOT/infrastructure/.env.pilot.local}"
[ -f "$ENV_FILE" ] || { echo "Falta entorno piloto" >&2; exit 1; }
compose=(docker compose --env-file "$ENV_FILE" -f "$ROOT/infrastructure/docker-compose.pilot.yml" -f "$ROOT/infrastructure/docker-compose.pilot-db2.yml")

query() {
  "${compose[@]}" exec -T postgres psql -X -U pmgm_app -d pmgm -v ON_ERROR_STOP=1 -tA -c "$1"
}
roles="$(query "SELECT rolname FROM pg_roles WHERE rolname IN ('pmgm_migrator','pmgm_runtime','pmgm_reports') AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication ORDER BY rolname;")"
expected="$(printf '%s\n' pmgm_migrator pmgm_reports pmgm_runtime)"
[ "$roles" = "$expected" ] || { echo "Los tres roles PostgreSQL limitados no están correctamente configurados." >&2; exit 1; }
[ "$(query "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname='pmgm';")" = "pmgm_migrator" ] || {
  echo "El migrador no es propietario de la base de aplicación." >&2; exit 1;
}
[ "$(query "SELECT pg_get_userbyid(relowner) FROM pg_class WHERE oid='core.audit_events'::regclass;")" = "pmgm_migrator" ] || {
  echo "La auditoría debe pertenecer al migrador, nunca al runtime." >&2; exit 1;
}
[ "$(query "SELECT has_schema_privilege('pmgm_runtime', 'core', 'USAGE');")" = "t" ] || exit 1
[ "$(query "SELECT has_table_privilege('pmgm_runtime', 'core.audit_events', 'INSERT');")" = "t" ] || exit 1
[ "$(query "SELECT has_table_privilege('pmgm_runtime', 'core.audit_events', 'SELECT');")" = "t" ] || exit 1
for denied in UPDATE DELETE TRUNCATE TRIGGER REFERENCES; do
  [ "$(query "SELECT has_table_privilege('pmgm_runtime', 'core.audit_events', '$denied');")" = "f" ] || {
    echo "Privilegio $denied indebidamente disponible sobre auditoría." >&2; exit 1;
  }
done
[ "$(query "SELECT has_database_privilege('pmgm_runtime','pmgm','CREATE');")" = "f" ] || exit 1
[ "$(query "SELECT has_database_privilege('pmgm_runtime','pmgm_keycloak','CONNECT');")" = "f" ] || exit 1
[ "$(query "SELECT has_table_privilege('pmgm_reports', 'core.audit_events', 'SELECT');")" = "f" ] || exit 1
[ "$(query "SELECT pg_has_role('pmgm_runtime','pmgm_migrator','MEMBER');")" = "f" ] || exit 1

# Las sentencias que inesperadamente pasen lo hacen dentro de BEGIN/ROLLBACK.
# Cualquier permiso indebido causa FAIL, sin borrar ni modificar datos.
expect_denied() {
  local label="$1" role="$2" statement="$3"
  if "${compose[@]}" exec -T postgres psql -X -U pmgm_app -d pmgm -v ON_ERROR_STOP=1 \
      -c "BEGIN; SET LOCAL ROLE $role; $statement; ROLLBACK;" >/dev/null 2>&1; then
    echo "Acción prohibida fue autorizada: $label" >&2
    exit 1
  fi
}
expect_denied "DDL runtime" pmgm_runtime "CREATE TABLE core.pmgm_bd2_forbidden (id integer)"
expect_denied "desactivar trigger auditoría" pmgm_runtime "ALTER TABLE core.audit_events DISABLE TRIGGER ALL"
expect_denied "borrar auditoría" pmgm_runtime "DELETE FROM core.audit_events WHERE FALSE"
expect_denied "truncar auditoría" pmgm_runtime "TRUNCATE TABLE core.audit_events"
expect_denied "leer datos ajenos desde reportes" pmgm_reports "SELECT * FROM core.audit_events LIMIT 0"
echo "BD2 OK: roles no-superuser, propiedad protegida y denegaciones reales verificadas."
