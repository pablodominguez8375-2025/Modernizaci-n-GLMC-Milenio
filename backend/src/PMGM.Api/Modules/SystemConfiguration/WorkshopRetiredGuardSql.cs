namespace PMGM.Api.Modules.SystemConfiguration;

/// <summary>
/// #1039 T1: SQL EXACTO de la guardia de Talleres dados de baja. Es la unica fuente: la migracion lo ejecuta y una prueba
/// fija su SHA-256, de modo que cualquier cambio exige nueva revision y nuevo digest firmado. Es SQL manual
/// (arbitrary_sql=true) => migracion MANUAL/supervisada; NO se aplica automaticamente.
///
/// Regla (default-deny): un Taller con Status='retired' es de SOLO LECTURA. INSERT/UPDATE/DELETE de cualquier fila que lo
/// referencie => SQLSTATE PM409 (mensaje fijo, sin datos). La propia fila de core.organizations (retire/reactivate) queda
/// fuera; en organizations solo se vigila ParentOrganizationId. Sin GUC de sesion ni cabecera: el mantenimiento tecnico se
/// hace con un rol DB propietario separado y auditado (ALTER TABLE ... DISABLE TRIGGER).
/// Serializacion con retire: el trigger toma FOR SHARE sobre la fila de la organizacion, que conflicta con el
/// FOR NO KEY UPDATE del UPDATE de retire. El trigger se llama a00_... para ejecutarse ANTES de otros triggers de la tabla
/// (orden alfabetico): la regla de baja prevalece sobre los bloqueos append-only existentes.
/// Los triggers se instalan por INVENTARIO del esquema real (mismo criterio que la huella de hasData); una prueba falla
/// si aparece una tabla nueva sin cubrir: ejecutar core.install_workshop_retired_guard() tras agregarla.
/// </summary>
public static class WorkshopRetiredGuardSql
{
    public const string Up = """
        CREATE OR REPLACE FUNCTION core.workshop_retired_guard() RETURNS trigger
        LANGUAGE plpgsql
        SET search_path = pg_catalog
        AS $fn$
        DECLARE
          col text;
          rec jsonb;
          val uuid;
          st text;
        BEGIN
          IF TG_TABLE_SCHEMA = 'core' AND TG_TABLE_NAME = 'organizations' THEN
            IF TG_OP = 'DELETE' THEN
              RETURN OLD;
            END IF;
            IF TG_OP = 'UPDATE' AND NEW."ParentOrganizationId" IS NOT DISTINCT FROM OLD."ParentOrganizationId" THEN
              RETURN NEW;
            END IF;
            IF NEW."ParentOrganizationId" IS NULL THEN
              RETURN NEW;
            END IF;
            SELECT o."Status" INTO st FROM core.organizations o WHERE o."Id" = NEW."ParentOrganizationId" FOR SHARE;
            IF st = 'retired' THEN
              RAISE EXCEPTION 'WORKSHOP_RETIRED' USING ERRCODE = 'PM409';
            END IF;
            RETURN NEW;
          END IF;

          FOREACH col IN ARRAY TG_ARGV LOOP
            FOR rec IN
              SELECT unnest(CASE TG_OP
                WHEN 'INSERT' THEN ARRAY[to_jsonb(NEW)]
                WHEN 'DELETE' THEN ARRAY[to_jsonb(OLD)]
                ELSE ARRAY[to_jsonb(OLD), to_jsonb(NEW)] END)
            LOOP
              val := NULLIF(rec ->> col, '')::uuid;
              IF val IS NOT NULL THEN
                SELECT o."Status" INTO st FROM core.organizations o WHERE o."Id" = val FOR SHARE;
                IF st = 'retired' THEN
                  RAISE EXCEPTION 'WORKSHOP_RETIRED' USING ERRCODE = 'PM409';
                END IF;
              END IF;
            END LOOP;
          END LOOP;

          IF TG_OP = 'DELETE' THEN
            RETURN OLD;
          END IF;
          RETURN NEW;
        END
        $fn$;

        CREATE OR REPLACE FUNCTION core.install_workshop_retired_guard() RETURNS integer
        LANGUAGE plpgsql
        SET search_path = pg_catalog
        AS $fn$
        DECLARE
          r record;
          installed integer := 0;
        BEGIN
          FOR r IN
            SELECT n.nspname::text AS s, c.relname::text AS t,
                   string_agg(quote_literal(a.attname::text), ', ' ORDER BY a.attname::text) AS cols
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid AND c.relkind = 'r'
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE a.attnum > 0 AND NOT a.attisdropped
              AND a.atttypid = 'uuid'::regtype
              AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
              AND NOT (n.nspname = 'core' AND c.relname = 'organizations' AND a.attname = 'Id')
              AND c.relname <> 'audit_events'
              AND c.relname NOT LIKE '\_\_EFMigrations%'
              AND (a.attname ~* '(organi[sz]ation|workshop|lodge|taller)id$'
                   OR EXISTS (SELECT 1 FROM pg_constraint k
                              WHERE k.contype = 'f' AND k.conrelid = c.oid
                                AND k.confrelid = 'core.organizations'::regclass AND a.attnum = ANY (k.conkey)))
            GROUP BY n.nspname, c.relname
          LOOP
            EXECUTE format('DROP TRIGGER IF EXISTS a00_workshop_retired_guard ON %I.%I', r.s, r.t);
            EXECUTE format('CREATE TRIGGER a00_workshop_retired_guard BEFORE INSERT OR UPDATE OR DELETE ON %I.%I FOR EACH ROW EXECUTE FUNCTION core.workshop_retired_guard(%s)', r.s, r.t, r.cols);
            installed := installed + 1;
          END LOOP;
          RETURN installed;
        END
        $fn$;

        SELECT core.install_workshop_retired_guard();
        """;

    public const string Down = """
        DO $do$
        DECLARE r record;
        BEGIN
          FOR r IN
            SELECT n.nspname::text AS s, c.relname::text AS t
            FROM pg_trigger g
            JOIN pg_class c ON c.oid = g.tgrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE g.tgname = 'a00_workshop_retired_guard' AND NOT g.tgisinternal
          LOOP
            EXECUTE format('DROP TRIGGER a00_workshop_retired_guard ON %I.%I', r.s, r.t);
          END LOOP;
        END
        $do$;
        DROP FUNCTION IF EXISTS core.install_workshop_retired_guard();
        DROP FUNCTION IF EXISTS core.workshop_retired_guard();
        """;
}
