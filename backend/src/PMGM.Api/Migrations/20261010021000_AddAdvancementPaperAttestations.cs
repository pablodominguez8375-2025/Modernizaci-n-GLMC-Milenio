using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable
namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20261010021000_AddAdvancementPaperAttestations")]
public sealed class AddAdvancementPaperAttestations : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "advancement_paper_attestations", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                CeremonyRequestId = t.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = t.Column<Guid>(type: "uuid", nullable: false),
                MemberId = t.Column<Guid>(type: "uuid", nullable: false),
                WorkPaperDocumentId = t.Column<Guid>(type: "uuid", nullable: false),
                WorkPaperVersionId = t.Column<Guid>(type: "uuid", nullable: false),
                MeetingId = t.Column<Guid>(type: "uuid", nullable: false),
                ExtractVersionId = t.Column<Guid>(type: "uuid", nullable: false),
                FullMinuteVersionId = t.Column<Guid>(type: "uuid", nullable: false),
                WorkKind = t.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                PresentationDate = t.Column<DateOnly>(type: "date", nullable: false),
                Status = t.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                CouncilApprovalReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                PresentedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReviewedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ReviewedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReviewNotes = t.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_advancement_paper_attestations", x => x.Id);
                t.ForeignKey(name: "FK_advancement_paper_attestations_ceremony_requests_CeremonyRequestId",
                    column: x => x.CeremonyRequestId, principalSchema: "core",
                    principalTable: "ceremony_requests", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });
        m.CreateIndex(name: "IX_advancement_paper_attestations_CeremonyRequestId_WorkPaperDocumentId_RecordedAtUtc",
            schema: "core", table: "advancement_paper_attestations",
            columns: new[] { "CeremonyRequestId", "WorkPaperDocumentId", "RecordedAtUtc" });
        m.CreateIndex(name: "IX_advancement_paper_attestations_OrganizationId_MemberId",
            schema: "core", table: "advancement_paper_attestations",
            columns: new[] { "OrganizationId", "MemberId" });
    }
    protected override void Down(MigrationBuilder m)
        => m.DropTable(name: "advancement_paper_attestations", schema: "core");
}
