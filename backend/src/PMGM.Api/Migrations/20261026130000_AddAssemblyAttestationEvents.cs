using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>#533 entrega 2 — constancias de Régimen Interior (tabla nueva, append-only; sin cambios destructivos).</summary>
[DbContext(typeof(RegimenInteriorDbContext))]
[Migration("20261026130000_AddAssemblyAttestationEvents")]
public sealed class AddAssemblyAttestationEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assembly_attestation_events", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                AssemblyId = t.Column<Guid>(type: "uuid", nullable: false),
                Kind = t.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Action = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ActaReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Statement = t.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                RecordedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CutoffDate = t.Column<DateOnly>(type: "date", nullable: false),
                OccurredAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReversesEventId = t.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_assembly_attestation_events", x => x.Id);
                t.ForeignKey("FK_assembly_attestation_events_assemblies", x => x.AssemblyId,
                    principalSchema: "core", principalTable: "assemblies",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("CK_assembly_attestation_action", "\"Action\" IN ('attest','revoke')");
                t.CheckConstraint("CK_assembly_attestation_kind",
                    "\"Kind\" IN ('membership','master_periods','honor_coverage','regimen_coverage')");
            });
        migrationBuilder.CreateIndex("IX_assembly_attestation_events_AssemblyId_Kind_OccurredAtUtc",
            schema: "core", table: "assembly_attestation_events",
            columns: new[] { "AssemblyId", "Kind", "OccurredAtUtc" });
        migrationBuilder.CreateIndex("IX_assembly_attestation_events_ReversesEventId",
            schema: "core", table: "assembly_attestation_events",
            column: "ReversesEventId", unique: true, filter: "\"ReversesEventId\" IS NOT NULL");

        // Una constancia no puede modificarse ni borrarse retroactivamente.
        migrationBuilder.Sql("""
            CREATE FUNCTION core.reject_assembly_attestation_event_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $assembly_attestation_guard$
            BEGIN
                RAISE EXCEPTION 'assembly_attestation_events is append-only'
                    USING ERRCODE = '55000';
            END $assembly_attestation_guard$;
            CREATE TRIGGER trg_assembly_attestation_event_immutable
            BEFORE UPDATE OR DELETE OR TRUNCATE ON core.assembly_attestation_events
            FOR EACH STATEMENT
            EXECUTE FUNCTION core.reject_assembly_attestation_event_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_assembly_attestation_event_immutable
                ON core.assembly_attestation_events;
            DROP FUNCTION IF EXISTS core.reject_assembly_attestation_event_mutation();
            """);
        migrationBuilder.DropTable(name: "assembly_attestation_events", schema: "core");
    }
}
