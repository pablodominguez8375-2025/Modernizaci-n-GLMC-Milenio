using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// Los eventos de auditoría son de solo inserción: bloquear su modificación,
/// eliminación y vaciado desde la aplicación, incluso si una ruta se equivoca.
/// No sustituye la futura separación de roles de PostgreSQL (#470).
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261013230000_EnforceAppendOnlyAuditEvents")]
public partial class EnforceAppendOnlyAuditEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE FUNCTION core.reject_audit_event_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $audit_guard$
            BEGIN
                RAISE EXCEPTION 'audit_events is append-only: % not permitted', TG_OP
                    USING ERRCODE = '55000';
            END;
            $audit_guard$;

            CREATE TRIGGER trg_audit_events_no_update_delete
            BEFORE UPDATE OR DELETE ON core.audit_events
            FOR EACH ROW
            EXECUTE FUNCTION core.reject_audit_event_mutation();

            CREATE TRIGGER trg_audit_events_no_truncate
            BEFORE TRUNCATE ON core.audit_events
            FOR EACH STATEMENT
            EXECUTE FUNCTION core.reject_audit_event_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_audit_events_no_truncate ON core.audit_events;
            DROP TRIGGER IF EXISTS trg_audit_events_no_update_delete ON core.audit_events;
            DROP FUNCTION IF EXISTS core.reject_audit_event_mutation();
            """);
    }
}
