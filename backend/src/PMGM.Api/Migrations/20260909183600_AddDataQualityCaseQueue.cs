using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(RegimenInteriorDbContext))]
[Migration("20260909183600_AddDataQualityCaseQueue")]
public partial class AddDataQualityCaseQueue : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "data_quality_cases",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                IssueFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                RuleCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Severity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                DetectionAsOf = table.Column<DateOnly>(type: "date", nullable: false),
                PrimaryDate = table.Column<DateOnly>(type: "date", nullable: true),
                RelatedDate = table.Column<DateOnly>(type: "date", nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AssignedToSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                AssignedToDisplayName = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedByDisplayName = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ResolutionSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ResolvedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_data_quality_cases", x => x.Id);
                table.ForeignKey(
                    name: "FK_data_quality_cases_members_MemberId",
                    column: x => x.MemberId,
                    principalSchema: "core",
                    principalTable: "members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_data_quality_cases_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "data_quality_case_events",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DataQualityCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                FromStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                ToStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ActorSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                ActorDisplayName = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_data_quality_case_events", x => x.Id);
                table.ForeignKey(
                    name: "FK_data_quality_case_events_data_quality_cases_DataQualityCaseId",
                    column: x => x.DataQualityCaseId,
                    principalSchema: "core",
                    principalTable: "data_quality_cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_data_quality_cases_IssueFingerprint_active",
            schema: "core",
            table: "data_quality_cases",
            column: "IssueFingerprint",
            unique: true,
            filter: "\"Status\" IN ('open','under_review')");
        migrationBuilder.CreateIndex(
            name: "IX_data_quality_cases_MemberId_Status",
            schema: "core",
            table: "data_quality_cases",
            columns: new[] { "MemberId", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_data_quality_cases_OrganizationId_Status",
            schema: "core",
            table: "data_quality_cases",
            columns: new[] { "OrganizationId", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_data_quality_cases_Status_UpdatedAtUtc",
            schema: "core",
            table: "data_quality_cases",
            columns: new[] { "Status", "UpdatedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_data_quality_case_events_DataQualityCaseId_OccurredAtUtc",
            schema: "core",
            table: "data_quality_case_events",
            columns: new[] { "DataQualityCaseId", "OccurredAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "data_quality_case_events", schema: "core");
        migrationBuilder.DropTable(name: "data_quality_cases", schema: "core");
    }
}
