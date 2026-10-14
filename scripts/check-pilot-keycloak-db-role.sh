#!/usr/bin/env bash
# Comprobación sin datos personales: rol propio de Keycloak y propietario tras restore.
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-$ROOT/infrastructure/.env.pilot.local}"
[ -f "$ENV_FILE" ] || { echo "Falta archivo de entorno del piloto." >&2; exit 1; }
compose=(docker compose --env-file "$ENV_FILE" -f "$ROOT/infrastructure/docker-compose.pilot.yml")
if [ -n "${PMGM_CI_COMPOSE_OVERRIDE:-}" ]; then
  compose+=(-f "$ROOT/$PMGM_CI_COMPOSE_OVERRIDE")
fi

query() {
  "${compose[@]}" exec -T postgres psql \
    --username pmgm_app --dbname "${2:-pmgm}" --set ON_ERROR_STOP=1 \
    --tuples-only --no-align --command "$1"
}

owner="$(query "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname='pmgm_keycloak';")"
[ "$owner" = "pmgm_keycloak" ] || {
  echo "La BD Keycloak no pertenece a su rol restringido." >&2; exit 1;
}

attributes="$(query "SELECT rolsuper, rolcreatedb, rolcreaterole, rolreplication FROM pg_roles WHERE rolname='pmgm_keycloak';")"
[ "$attributes" = "f|f|f|f" ] || {
  echo "El rol Keycloak tiene privilegios globales imprevistos." >&2; exit 1;
}

external_access="$(query "SELECT has_database_privilege('pmgm_keycloak','pmgm','CONNECT');")"
[ "$external_access" = "f" ] || {
  echo "Keycloak puede conectar a la BD de la aplicación." >&2; exit 1;
}

# Verifica propietario de objetos después de pg_restore --role=pmgm_keycloak.
stray_tables="$(query "SELECT count(*) FROM pg_class c
  JOIN pg_namespace n ON c.relnamespace=n.oid
  WHERE n.nspname='public' AND c.relkind IN ('r','p')
    AND pg_get_userbyid(c.relowner) <> 'pmgm_keycloak';" pmgm_keycloak | tr -d '[:space:]')"
[ "$stray_tables" = "0" ] || {
  echo "Hay $stray_tables tablas Keycloak con propietario ajeno al rol restringido." >&2
  # Solo metadatos de tablas/roles: jamás extraer filas, credenciales ni datos de usuarios.
  query "SELECT c.relname || ':' || pg_get_userbyid(c.relowner) FROM pg_class c
    JOIN pg_namespace n ON c.relnamespace=n.oid
    WHERE n.nspname='public' AND c.relkind IN ('r','p')
      AND pg_get_userbyid(c.relowner) <> 'pmgm_keycloak'
    ORDER BY c.relname LIMIT 12;" pmgm_keycloak | sed 's/^/Tabla con propietario ajeno: /' >&2
  exit 1
}

echo "Keycloak DB aislada: rol limitado, sin acceso a pmgm y propiedad de tablas verificada."
