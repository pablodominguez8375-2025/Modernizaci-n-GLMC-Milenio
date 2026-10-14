#!/usr/bin/env bash
# Solo para el primer initdb de un volumen PostgreSQL NUEVO del piloto.
# No se ejecuta en volúmenes ya inicializados: migrar esos con runbook y respaldo.
set -euo pipefail

: "${PMGM_PILOT_KC_DB_PASSWORD:?Clave de la base Keycloak requerida}"

# -v y :'variable' permiten que psql cite la contraseña como literal SQL.
# El heredoc entre comillas evita interpolación del shell en el texto SQL.
psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --set=ON_ERROR_STOP=1 --set="kc_password=$PMGM_PILOT_KC_DB_PASSWORD" <<'SQL'
CREATE ROLE pmgm_keycloak LOGIN
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION
  PASSWORD :'kc_password';
SQL

createdb --username "$POSTGRES_USER" --owner=pmgm_keycloak pmgm_keycloak

# Ningún otro usuario ordinario puede conectar a las bases ajenas por PUBLIC.
# pmgm_app es el superusuario de bootstrap de esta fase y mantiene acceso
# administrativo: separar app runtime/migraciones sigue en otra entrega.
psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --set=ON_ERROR_STOP=1 <<'SQL'
REVOKE CONNECT, TEMPORARY ON DATABASE pmgm FROM PUBLIC;
REVOKE CONNECT, TEMPORARY ON DATABASE pmgm_keycloak FROM PUBLIC;
SQL
