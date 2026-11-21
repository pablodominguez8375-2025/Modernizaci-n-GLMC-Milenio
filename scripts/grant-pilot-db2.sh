#!/usr/bin/env bash
# Se ejecuta una sola vez en el contenedor efímero db-grants, tras finalizar las migraciones.
set -euo pipefail
: "${PGPASSWORD:?Se exige credencial bootstrap solo para el job grants}"
psql -h postgres -U pmgm_app -d pmgm -X -v ON_ERROR_STOP=1 <<'SQL'
BEGIN;
DO $grant$
DECLARE schema_name text;
BEGIN
  FOR schema_name IN
    SELECT nspname FROM pg_namespace
    WHERE nspname <> 'information_schema' AND nspname NOT LIKE 'pg_%'
  LOOP
    EXECUTE format('GRANT USAGE ON SCHEMA %I TO pmgm_runtime', schema_name);
    EXECUTE format('REVOKE CREATE ON SCHEMA %I FROM pmgm_runtime, pmgm_reports', schema_name);
    EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO pmgm_runtime', schema_name);
    EXECUTE format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO pmgm_runtime', schema_name);
    EXECUTE format('REVOKE TRUNCATE, REFERENCES, TRIGGER ON ALL TABLES IN SCHEMA %I FROM pmgm_runtime', schema_name);
    -- Nuevas tablas/secuencias EF posteriores heredarán DML, pero nunca TRUNCATE ni TRIGGER.
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE pmgm_migrator IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO pmgm_runtime', schema_name);
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE pmgm_migrator IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO pmgm_runtime', schema_name);
    -- Informes: denegar acceso directo a datos; autorizar vistas una por una tras revisión.
    EXECUTE format('REVOKE ALL ON ALL TABLES IN SCHEMA %I FROM pmgm_reports', schema_name);
    EXECUTE format('REVOKE ALL ON ALL SEQUENCES IN SCHEMA %I FROM pmgm_reports', schema_name);
  END LOOP;
END;
$grant$;
-- El runtime NO posee el objeto y jamás modifica ni deshabilita triggers de auditoría.
REVOKE UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON core.audit_events FROM pmgm_runtime;
GRANT SELECT, INSERT ON core.audit_events TO pmgm_runtime;
-- Las transiciones de Taller son append-only. El GRANT global anterior devuelve
-- UPDATE/DELETE al runtime en cada reejecución de db-grants: retirarlos aquí.
-- Si la tabla aún no existe, el job falla y revierte esta transacción.
REVOKE UPDATE, DELETE, TRUNCATE ON core.workshop_status_transitions FROM pmgm_runtime;
DO $verify_workshop$
BEGIN
  IF NOT (
    has_table_privilege('pmgm_runtime', 'core.workshop_status_transitions', 'SELECT')
    AND has_table_privilege('pmgm_runtime', 'core.workshop_status_transitions', 'INSERT')
    AND NOT has_table_privilege('pmgm_runtime', 'core.workshop_status_transitions', 'UPDATE')
    AND NOT has_table_privilege('pmgm_runtime', 'core.workshop_status_transitions', 'DELETE')
    AND NOT has_table_privilege('pmgm_runtime', 'core.workshop_status_transitions', 'TRUNCATE')
  ) THEN
    RAISE EXCEPTION 'QA525_WORKSHOP_TRANSITIONS_RUNTIME_PRIVILEGES_INVALID';
  END IF;
END;
$verify_workshop$;
-- El historial de EF Core controla qué migraciones se aplican. El runtime
-- jamás debe consultarlo o alterarlo, incluso si recibe DML por esquema.
-- El migrador mantiene ownership y permisos para nuevas migraciones.
REVOKE ALL PRIVILEGES ON TABLE public."__EFMigrationsHistory"
  FROM pmgm_runtime, pmgm_reports;
COMMIT;
SQL
echo "Permisos BD2 aplicados: runtime DML sin DDL, auditoría append-only, informes sin tablas."
