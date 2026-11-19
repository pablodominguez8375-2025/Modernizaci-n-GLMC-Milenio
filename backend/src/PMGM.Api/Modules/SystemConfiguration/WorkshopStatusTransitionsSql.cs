namespace PMGM.Api.Modules.SystemConfiguration;

/// <summary>
/// #1035/#1044: SQL EXACTO que vuelve append-only core.workshop_status_transitions (UPDATE, DELETE y TRUNCATE rechazados con
/// SQLSTATE PM410). SQL manual (arbitrary_sql=true) => migración MANUAL/supervisada, igual que la guardia T1; una prueba fija su
/// SHA-256. Funciona también para el rol dueño: el mantenimiento excepcional exige DISABLE TRIGGER explícito y auditado.
/// </summary>
public static class WorkshopStatusTransitionsSql
{
    public const string Up = """
        CREATE OR REPLACE FUNCTION core.workshop_status_transitions_append_only() RETURNS trigger
        LANGUAGE plpgsql
        SET search_path = pg_catalog
        AS $fn$
        BEGIN
          RAISE EXCEPTION 'WORKSHOP_TRANSITIONS_APPEND_ONLY' USING ERRCODE = 'PM410';
        END
        $fn$;

        REVOKE ALL ON FUNCTION core.workshop_status_transitions_append_only() FROM PUBLIC;

        DROP TRIGGER IF EXISTS a00_workshop_status_transitions_append_only ON core.workshop_status_transitions;
        CREATE TRIGGER a00_workshop_status_transitions_append_only
          BEFORE UPDATE OR DELETE ON core.workshop_status_transitions
          FOR EACH ROW EXECUTE FUNCTION core.workshop_status_transitions_append_only();

        DROP TRIGGER IF EXISTS a00_workshop_status_transitions_no_truncate ON core.workshop_status_transitions;
        CREATE TRIGGER a00_workshop_status_transitions_no_truncate
          BEFORE TRUNCATE ON core.workshop_status_transitions
          FOR EACH STATEMENT EXECUTE FUNCTION core.workshop_status_transitions_append_only();
        """;

    public const string Down = """
        DROP TRIGGER IF EXISTS a00_workshop_status_transitions_no_truncate ON core.workshop_status_transitions;
        DROP TRIGGER IF EXISTS a00_workshop_status_transitions_append_only ON core.workshop_status_transitions;
        DROP FUNCTION IF EXISTS core.workshop_status_transitions_append_only();
        """;
}
