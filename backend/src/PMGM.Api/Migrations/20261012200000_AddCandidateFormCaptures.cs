using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// Private candidate supplemental captures are append-only. Historical rows
/// retain the exact published schema version and payload checksum.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261012200000_AddCandidateFormCaptures")]
public sealed class AddCandidateFormCaptures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "candidate_form_captures",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CeremonyRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                SchemaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                ScopeKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                CaptureRevision = table.Column<int>(type: "integer", nullable: false),
                ValuesJson = table.Column<string>(type: "text", nullable: false),
                PayloadSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CapturedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CapturedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_candidate_form_captures", x => x.Id);
                table.ForeignKey("FK_candidate_form_captures_ceremony_requests_CeremonyRequestId",
                    x => x.CeremonyRequestId, "ceremony_requests", "Id",
                    principalSchema: "core", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_candidate_form_captures_form_schema_versions_SchemaVersionId",
                    x => x.SchemaVersionId, "form_schema_versions", "Id",
                    principalSchema: "core", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_candidate_form_captures_positive_revision",
                    "\"CaptureRevision\" >= 1 AND \"SchemaVersion\" >= 1");
                table.CheckConstraint("CK_candidate_form_captures_valid_checksum",
                    "length(\"PayloadSha256\") = 64");
                table.CheckConstraint("CK_candidate_form_captures_valid_scope",
                    "\"ScopeKey\" = 'order' OR \"ScopeKey\" ~ '^lodge:[0-9a-f]{32}$'");
            });

        migrationBuilder.CreateIndex("IX_candidate_form_captures_CeremonyRequestId_CaptureRevision",
            schema: "core", table: "candidate_form_captures",
            columns: new[] { "CeremonyRequestId", "CaptureRevision" }, unique: true);
        migrationBuilder.CreateIndex("IX_candidate_form_captures_OrganizationId_CapturedAtUtc",
            schema: "core", table: "candidate_form_captures",
            columns: new[] { "OrganizationId", "CapturedAtUtc" });
        migrationBuilder.CreateIndex("IX_candidate_form_captures_SchemaVersionId",
            schema: "core", table: "candidate_form_captures", column: "SchemaVersionId");

        migrationBuilder.Sql("""
            CREATE FUNCTION core.prevent_candidate_form_capture_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Candidate form captures are historical and immutable'
                    USING ERRCODE = '55000';
            END;
            $$;
            CREATE TRIGGER trg_candidate_form_captures_immutable
                BEFORE UPDATE OR DELETE ON core.candidate_form_captures
                FOR EACH ROW EXECUTE FUNCTION core.prevent_candidate_form_capture_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "candidate_form_captures", schema: "core");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS core.prevent_candidate_form_capture_mutation();");
    }
}
