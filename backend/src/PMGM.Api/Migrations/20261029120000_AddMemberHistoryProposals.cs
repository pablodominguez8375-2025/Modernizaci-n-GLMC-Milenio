using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>Historia del hermano: propuestas de la Secretaría del Taller aprobadas por Régimen Interior (tablas nuevas; sin cambios destructivos).</summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261029120000_AddMemberHistoryProposals")]
public sealed class AddMemberHistoryProposals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "member_history_proposals", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = t.Column<Guid>(type: "uuid", nullable: false),
                MemberId = t.Column<Guid>(type: "uuid", nullable: false),
                Status = t.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                EvidenceReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Reason = t.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                CreatedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReviewedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ReviewedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReviewNotes = t.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: t => t.PrimaryKey("PK_member_history_proposals", x => x.Id));
        migrationBuilder.CreateIndex("IX_member_history_proposals_MemberId_Status", schema: "core", table: "member_history_proposals", columns: new[] { "MemberId", "Status" });
        migrationBuilder.CreateIndex("IX_member_history_proposals_OrganizationId_Status_CreatedAtUtc", schema: "core", table: "member_history_proposals", columns: new[] { "OrganizationId", "Status", "CreatedAtUtc" });

        migrationBuilder.CreateTable(
            name: "member_history_proposal_items", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                ProposalId = t.Column<Guid>(type: "uuid", nullable: false),
                Kind = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Action = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                TargetRecordId = t.Column<Guid>(type: "uuid", nullable: true),
                OfficeType = t.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                Period = t.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                StartDate = t.Column<DateOnly>(type: "date", nullable: true),
                EndDate = t.Column<DateOnly>(type: "date", nullable: true),
                EventType = t.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                EventDate = t.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_member_history_proposal_items", x => x.Id);
                t.ForeignKey("FK_member_history_proposal_items_member_history_proposals_ProposalId", x => x.ProposalId,
                    principalSchema: "core", principalTable: "member_history_proposals", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_member_history_proposal_items_ProposalId", schema: "core", table: "member_history_proposal_items", column: "ProposalId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "member_history_proposal_items", schema: "core");
        migrationBuilder.DropTable(name: "member_history_proposals", schema: "core");
    }
}
