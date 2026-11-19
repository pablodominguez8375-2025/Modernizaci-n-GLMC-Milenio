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

        -- Integridad de INSERT (sin FK a propósito: no es dato del Taller para T1/«datos asociados»).
        CREATE OR REPLACE FUNCTION core.workshop_status_transitions_validate_insert() RETURNS trigger
        LANGUAGE plpgsql
        SET search_path = pg_catalog
        AS $fn$
        DECLARE
          last_next text;
          last_effective timestamptz;
        BEGIN
          IF NOT EXISTS (SELECT 1 FROM core.organizations o WHERE o."Id" = NEW."SubjectRef" AND o."Type" = 'workshop') THEN
            RAISE EXCEPTION 'WORKSHOP_TRANSITION_SUBJECT_INVALID' USING ERRCODE = 'PM411';
          END IF;
          SELECT t."NextStatus", t."EffectiveAtUtc" INTO last_next, last_effective
            FROM core.workshop_status_transitions t WHERE t."SubjectRef" = NEW."SubjectRef" ORDER BY t."Sequence" DESC LIMIT 1;
          IF last_next IS NOT NULL AND last_next <> NEW."PreviousStatus" THEN
            RAISE EXCEPTION 'WORKSHOP_TRANSITION_CHAIN_BROKEN' USING ERRCODE = 'PM412';
          END IF;
          IF NEW."Origin" = 'runtime' THEN
            -- Reloj de BD: sin fechas retroactivas ni futuras en línea; el backfill es la única vía autorizada.
            IF NEW."EffectiveAtUtc" < now() - interval '5 minutes' OR NEW."EffectiveAtUtc" > now() + interval '1 minute'
               OR (last_effective IS NOT NULL AND NEW."EffectiveAtUtc" < last_effective) THEN
              RAISE EXCEPTION 'WORKSHOP_TRANSITION_DATE_INVALID' USING ERRCODE = 'PM413';
            END IF;
          END IF;
          RETURN NEW;
        END
        $fn$;

        REVOKE ALL ON FUNCTION core.workshop_status_transitions_validate_insert() FROM PUBLIC;

        DROP TRIGGER IF EXISTS a00_workshop_status_transitions_validate_insert ON core.workshop_status_transitions;
        CREATE TRIGGER a00_workshop_status_transitions_validate_insert
          BEFORE INSERT ON core.workshop_status_transitions
          FOR EACH ROW EXECUTE FUNCTION core.workshop_status_transitions_validate_insert();

        REVOKE UPDATE, DELETE, TRUNCATE ON core.workshop_status_transitions FROM PUBLIC;

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
        DROP TRIGGER IF EXISTS a00_workshop_status_transitions_validate_insert ON core.workshop_status_transitions;
        DROP FUNCTION IF EXISTS core.workshop_status_transitions_validate_insert();
        DROP FUNCTION IF EXISTS core.workshop_status_transitions_append_only();
        """;
}
