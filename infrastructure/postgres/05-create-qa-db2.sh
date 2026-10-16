#!/usr/bin/env bash
# G4: ejecutar SOLO por postgres:17 durante initdb en volumen QA nuevo.
# La cuenta bootstrap pmgm_app es administrativa y NUNCA accede a API/Keycloak.
set -euo pipefail
for var in PMGM_QA_KC_DB_PASSWORD PMGM_QA_RUNTIME_DB_PASSWORD PMGM_QA_MIGRATOR_DB_PASSWORD PMGM_QA_REPORT_DB_PASSWORD; do
  [ -n "${!var:-}" ] || { echo "Falta secreto QA BD2: $var" >&2; exit 1; }
done

psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -v ON_ERROR_STOP=1 \
  -v kc_password="$PMGM_QA_KC_DB_PASSWORD" \
  -v runtime_password="$PMGM_QA_RUNTIME_DB_PASSWORD" \
  -v migrator_password="$PMGM_QA_MIGRATOR_DB_PASSWORD" \
  -v report_password="$PMGM_QA_REPORT_DB_PASSWORD" <<'SQL'
CREATE ROLE pmgm_keycloak LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION
  PASSWORD :'kc_password';
CREATE ROLE pmgm_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION
  PASSWORD :'migrator_password';
CREATE ROLE pmgm_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION
  PASSWORD :'runtime_password';
CREATE ROLE pmgm_reports LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION
  PASSWORD :'report_password';
ALTER DATABASE pmgm OWNER TO pmgm_migrator;
REVOKE CONNECT, TEMPORARY ON DATABASE pmgm FROM PUBLIC;
GRANT CONNECT, TEMPORARY, CREATE ON DATABASE pmgm TO pmgm_migrator;
GRANT CONNECT ON DATABASE pmgm TO pmgm_runtime, pmgm_reports;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO pmgm_migrator;
CREATE SCHEMA IF NOT EXISTS reporting AUTHORIZATION pmgm_migrator;
GRANT USAGE ON SCHEMA reporting TO pmgm_reports;
SQL

# La plantilla histórica 10-init-srv01.sql se ejecuta DESPUÉS; omite
# la creación de pmgm_keycloak porque ya existe con propietario propio.
createdb --username "$POSTGRES_USER" --owner=pmgm_keycloak pmgm_keycloak
psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -v ON_ERROR_STOP=1 <<'SQL'
REVOKE CONNECT, TEMPORARY ON DATABASE pmgm_keycloak FROM PUBLIC;
SQL
