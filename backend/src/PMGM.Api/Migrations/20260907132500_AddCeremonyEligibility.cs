using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260907132500_AddCeremonyEligibility")]
public partial class AddCeremonyEligibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "institutional_rule_settings",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_institutional_rule_settings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ceremony_requests",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                CeremonyType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                CandidatePersonId = table.Column<Guid>(type: "uuid", nullable: true),
                ProposedDate = table.Column<DateOnly>(type: "date", nullable: true),
                Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ceremony_requests", x => x.Id);
                table.ForeignKey("FK_ceremony_requests_members_MemberId", x => x.MemberId, "core", "members", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ceremony_requests_organizations_OrganizationId", x => x.OrganizationId, "core", "organizations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ceremony_requests_people_CandidatePersonId", x => x.CandidatePersonId, "core", "people", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ceremony_validations",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CeremonyRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                ValidationType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ceremony_validations", x => x.Id);
                table.ForeignKey("FK_ceremony_validations_ceremony_requests_CeremonyRequestId", x => x.CeremonyRequestId, "core", "ceremony_requests", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "candidate_publications",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CeremonyRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                PublishedFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PublishedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RequiredDays = table.Column<int>(type: "integer", nullable: false),
                RuleCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_candidate_publications", x => x.Id);
                table.ForeignKey("FK_candidate_publications_ceremony_requests_CeremonyRequestId", x => x.CeremonyRequestId, "core", "ceremony_requests", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_candidate_publications_organizations_OrganizationId", x => x.OrganizationId, "core", "organizations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_candidate_publications_people_PersonId", x => x.PersonId, "core", "people", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_institutional_rule_settings_Code_EffectiveFrom", "core", "institutional_rule_settings", new[] { "Code", "EffectiveFrom" });
        migrationBuilder.CreateIndex("IX_ceremony_requests_MemberId", "core", "ceremony_requests", "MemberId");
        migrationBuilder.CreateIndex("IX_ceremony_requests_CandidatePersonId", "core", "ceremony_requests", "CandidatePersonId");
        migrationBuilder.CreateIndex("IX_ceremony_requests_OrganizationId_CeremonyType_Status", "core", "ceremony_requests", new[] { "OrganizationId", "CeremonyType", "Status" });
        migrationBuilder.CreateIndex("IX_ceremony_validations_CeremonyRequestId_ValidationType_RecordedAtUtc", "core", "ceremony_validations", new[] { "CeremonyRequestId", "ValidationType", "RecordedAtUtc" });
        migrationBuilder.CreateIndex("IX_candidate_publications_CeremonyRequestId", "core", "candidate_publications", "CeremonyRequestId");
        migrationBuilder.CreateIndex("IX_candidate_publications_PersonId_PublishedFromUtc", "core", "candidate_publications", new[] { "PersonId", "PublishedFromUtc" });
        migrationBuilder.CreateIndex("IX_candidate_publications_OrganizationId_Status", "core", "candidate_publications", new[] { "OrganizationId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "candidate_publications", schema: "core");
        migrationBuilder.DropTable(name: "ceremony_validations", schema: "core");
        migrationBuilder.DropTable(name: "institutional_rule_settings", schema: "core");
        migrationBuilder.DropTable(name: "ceremony_requests", schema: "core");
    }
}
