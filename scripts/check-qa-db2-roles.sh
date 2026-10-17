#!/usr/bin/env bash
# Verificador no destructivo de G4: inspecciona solo catálogo/privilegios y
# ejecuta negativas dentro de transacciones que se revierten al fallar.
set -euo pipefail
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_QA_ENV_FILE:-/etc/pmgm/srv01.env}"
[ -f "$ENV_FILE" ] || { echo "Falta entorno QA privado" >&2; exit 1; }
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a
[ "${PMGM_QA_DB2:-false}" = true ] || { echo "G4 no habilitado en entorno" >&2; exit 1; }
source "$ROOT/scripts/qa/silo-compose.sh"
qa_silo_prepare
qa_silo_require_bundle
compose=(docker compose -p "${PMGM_QA_COMPOSE_PROJECT:-pmgm-srv01}" --env-file "$ENV_FILE" -f "$COMPOSE_FILE")
query(){ "${compose[@]}" exec -T postgres psql -X -U pmgm_app -d pmgm -v ON_ERROR_STOP=1 -tA -c "$1"; }
expected="$(printf '%s\n' pmgm_keycloak pmgm_migrator pmgm_reports pmgm_runtime)"
actual="$(query "SELECT rolname FROM pg_roles WHERE rolname IN ('pmgm_keycloak','pmgm_migrator','pmgm_runtime','pmgm_reports') AND rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication ORDER BY rolname;")"
[ "$actual" = "$expected" ] || { echo "G4: roles restringidos no conformes" >&2; exit 1; }
[ "$(query "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname='pmgm';")" = pmgm_migrator ] || exit 1
[ "$(query "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname='pmgm_keycloak';")" = pmgm_keycloak ] || exit 1
[ "$(query "SELECT has_database_privilege('pmgm_runtime','pmgm_keycloak','CONNECT');")" = f ] || exit 1
[ "$(query "SELECT has_database_privilege('pmgm_keycloak','pmgm','CONNECT');")" = f ] || exit 1
[ "$(query "SELECT has_table_privilege('pmgm_runtime','core.audit_events','INSERT');")" = t ] || exit 1
[ "$(query "SELECT has_table_privilege('pmgm_runtime','core.audit_events','SELECT');")" = t ] || exit 1
for operation in UPDATE DELETE TRUNCATE TRIGGER REFERENCES; do
  [ "$(query "SELECT has_table_privilege('pmgm_runtime','core.audit_events','$operation');")" = f ] || exit 1
done
[ "$(query "SELECT has_table_privilege('pmgm_reports','core.audit_events','SELECT');")" = f ] || exit 1
[ "$(query "SELECT has_database_privilege('pmgm_runtime','pmgm','CREATE');")" = f ] || exit 1
for role in pmgm_runtime pmgm_reports; do
  [ "$(query "SELECT has_table_privilege('$role', 'public.\"__EFMigrationsHistory\"','SELECT');")" = f ] || exit 1
done
expect_denied(){
  if "${compose[@]}" exec -T postgres psql -X -U pmgm_app -d pmgm -v ON_ERROR_STOP=1 \
    -c "BEGIN; SET LOCAL ROLE $1; $2; ROLLBACK;" >/dev/null 2>&1; then
    echo "G4: privilegio indebido para $1" >&2; exit 1
  fi
}
expect_denied pmgm_runtime 'CREATE TABLE core.g4_forbidden (id int)'
expect_denied pmgm_runtime 'ALTER TABLE core.audit_events DISABLE TRIGGER ALL'
expect_denied pmgm_runtime 'DELETE FROM core.audit_events WHERE FALSE'
expect_denied pmgm_reports 'SELECT * FROM core.audit_events LIMIT 0'
echo "G4 BD2 QA PASS: roles, segregación, auditoría y negativas comprobadas."
