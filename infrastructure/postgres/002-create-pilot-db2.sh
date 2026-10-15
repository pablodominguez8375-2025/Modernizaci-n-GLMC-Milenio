#!/usr/bin/env bash
# Ejecutado exclusivamente por postgres:17 al crear un VOLUMEN PILOTO NUEVO.
# El bootstrap superusuario pmgm_app jamás se entrega a la API.
set -euo pipefail
: "${PMGM_PILOT_RUNTIME_DB_PASSWORD:?Falta clave runtime}"
: "${PMGM_PILOT_MIGRATOR_DB_PASSWORD:?Falta clave migrador}"
: "${PMGM_PILOT_REPORT_DB_PASSWORD:?Falta clave reportes}"

psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set=ON_ERROR_STOP=1 \
  --set="runtime_password=$PMGM_PILOT_RUNTIME_DB_PASSWORD" \
  --set="migrator_password=$PMGM_PILOT_MIGRATOR_DB_PASSWORD" \
  --set="report_password=$PMGM_PILOT_REPORT_DB_PASSWORD" <<'SQL'
CREATE ROLE pmgm_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION
  PASSWORD :'migrator_password';
CREATE ROLE pmgm_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION
  PASSWORD :'runtime_password';
CREATE ROLE pmgm_reports LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION
  PASSWORD :'report_password';
-- Migrador propietario SOLO de pmgm, sin superusuario ni acceso al secreto de Keycloak.
ALTER DATABASE pmgm OWNER TO pmgm_migrator;
REVOKE CONNECT, TEMPORARY ON DATABASE pmgm FROM PUBLIC;
GRANT CONNECT, TEMPORARY, CREATE ON DATABASE pmgm TO pmgm_migrator;
GRANT CONNECT ON DATABASE pmgm TO pmgm_runtime, pmgm_reports;
-- El esquema public hereda la propiedad de pg_database_owner.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO pmgm_migrator;
-- Las vistas de reportes se registrarán una por una, nunca acceso directo a tablas.
CREATE SCHEMA IF NOT EXISTS reporting AUTHORIZATION pmgm_migrator;
GRANT USAGE ON SCHEMA reporting TO pmgm_reports;
SQL
