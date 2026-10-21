using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #613 C3 — historial privado de correcciones. Sin backfill ni modificación
/// de solicitudes legadas. UPDATE/DELETE denegados por trigger de PostgreSQL.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261021154000_AddCandidateOriginCorrectionHistory")]
public sealed class AddCandidateOriginCorrectionHistory : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(
            name: "candidate_origin_corrections", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CeremonyRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                PreviousKind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                PreviousRecommenderMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                PreviousOtherDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                NewKind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                NewRecommenderMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                NewOtherDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ChangedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_candidate_origin_corrections", x => x.Id);
                table.ForeignKey(
                    name: "FK_candidate_origin_corrections_ceremony_requests_CeremonyRequestId",
                    column: x => x.CeremonyRequestId,
                    principalSchema: "core",
                    principalTable: "ceremony_requests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });
        m.CreateIndex(name: "IX_candidate_origin_corrections_CeremonyRequestId_ChangedAtUtc",
            schema: "core", table: "candidate_origin_corrections",
            columns: new[] { "CeremonyRequestId", "ChangedAtUtc" });
        m.AddCheckConstraint(name: "CK_candidate_origin_corrections_reason",
            schema: "core", table: "candidate_origin_corrections",
            sql: "length(btrim(\"Reason\")) BETWEEN 10 AND 500");
        m.AddCheckConstraint(name: "CK_candidate_origin_corrections_new_kind",
            schema: "core", table: "candidate_origin_corrections",
            sql: "\"NewKind\" IN ('brother', 'internet', 'other')");
        m.Sql("""
            CREATE FUNCTION core.prevent_candidate_origin_correction_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'candidate_origin_corrections is append-only';
            END;
            $$;
            CREATE TRIGGER candidate_origin_corrections_append_only
            BEFORE UPDATE OR DELETE ON core.candidate_origin_corrections
            FOR EACH ROW EXECUTE FUNCTION core.prevent_candidate_origin_correction_mutation();
            """);
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
            DROP TRIGGER IF EXISTS candidate_origin_corrections_append_only ON core.candidate_origin_corrections;
            DROP FUNCTION IF EXISTS core.prevent_candidate_origin_correction_mutation();
            """);
        m.DropTable(name: "candidate_origin_corrections", schema: "core");
    }
}
