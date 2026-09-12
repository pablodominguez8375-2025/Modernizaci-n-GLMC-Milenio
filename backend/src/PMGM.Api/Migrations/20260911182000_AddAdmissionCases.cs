using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Modules.Admissions;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(AdmissionsDbContext))]
[Migration("20260911182000_AddAdmissionCases")]
public partial class AddAdmissionCases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "admission_cases",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                AdmissionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AffiliationMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                OriginOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                OriginLodgeName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                OriginLodgeNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                OriginObedience = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                Degree = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                WageIncreaseEvidenceApplies = table.Column<bool>(type: "boolean", nullable: false),
                ExaltationEvidenceApplies = table.Column<bool>(type: "boolean", nullable: false),
                HasPeaceAndFriendshipPact = table.Column<bool>(type: "boolean", nullable: true),
                PreviousRejectionDate = table.Column<DateOnly>(type: "date", nullable: true),
                RejectionCausesRemedied = table.Column<bool>(type: "boolean", nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_admission_cases", x => x.Id));

        migrationBuilder.CreateTable(
            name: "admission_decisions",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AdmissionCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                DecisionType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admission_decisions", x => x.Id);
                table.ForeignKey(
                    name: "FK_admission_decisions_admission_cases_AdmissionCaseId",
                    column: x => x.AdmissionCaseId,
                    principalSchema: "core",
                    principalTable: "admission_cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "admission_evidence",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AdmissionCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                EvidenceType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                EvidenceDate = table.Column<DateOnly>(type: "date", nullable: true),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ReviewStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ReviewedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admission_evidence", x => x.Id);
                table.ForeignKey(
                    name: "FK_admission_evidence_admission_cases_AdmissionCaseId",
                    column: x => x.AdmissionCaseId,
                    principalSchema: "core",
                    principalTable: "admission_cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_admission_cases_OrganizationId_AdmissionType_Status",
            schema: "core",
            table: "admission_cases",
            columns: new[] { "OrganizationId", "AdmissionType", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_admission_cases_PersonId",
            schema: "core",
            table: "admission_cases",
            column: "PersonId");
        migrationBuilder.CreateIndex(
            name: "IX_admission_cases_MemberId",
            schema: "core",
            table: "admission_cases",
            column: "MemberId");
        migrationBuilder.CreateIndex(
            name: "IX_admission_decisions_AdmissionCaseId_DecisionType_RecordedAtUtc",
            schema: "core",
            table: "admission_decisions",
            columns: new[] { "AdmissionCaseId", "DecisionType", "RecordedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_admission_evidence_AdmissionCaseId_EvidenceType_CreatedAtUtc",
            schema: "core",
            table: "admission_evidence",
            columns: new[] { "AdmissionCaseId", "EvidenceType", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_admission_evidence_DocumentVersionId",
            schema: "core",
            table: "admission_evidence",
            column: "DocumentVersionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "admission_decisions", schema: "core");
        migrationBuilder.DropTable(name: "admission_evidence", schema: "core");
        migrationBuilder.DropTable(name: "admission_cases", schema: "core");
    }
}
