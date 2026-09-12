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
                table.ForeignKey(
                    name: "FK_ceremony_requests_members_MemberId",
                    column: x => x.MemberId,
                    principalSchema: "core",
                    principalTable: "members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ceremony_requests_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ceremony_requests_people_CandidatePersonId",
                    column: x => x.CandidatePersonId,
                    principalSchema: "core",
                    principalTable: "people",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
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
                table.ForeignKey(
                    name: "FK_ceremony_validations_ceremony_requests_CeremonyRequestId",
                    column: x => x.CeremonyRequestId,
                    principalSchema: "core",
                    principalTable: "ceremony_requests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
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
                table.ForeignKey(
                    name: "FK_candidate_publications_ceremony_requests_CeremonyRequestId",
                    column: x => x.CeremonyRequestId,
                    principalSchema: "core",
                    principalTable: "ceremony_requests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_candidate_publications_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_candidate_publications_people_PersonId",
                    column: x => x.PersonId,
                    principalSchema: "core",
                    principalTable: "people",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_institutional_rule_settings_Code_EffectiveFrom",
            schema: "core",
            table: "institutional_rule_settings",
            columns: new[] { "Code", "EffectiveFrom" });

        migrationBuilder.CreateIndex(
            name: "IX_ceremony_requests_MemberId",
            schema: "core",
            table: "ceremony_requests",
            column: "MemberId");

        migrationBuilder.CreateIndex(
            name: "IX_ceremony_requests_CandidatePersonId",
            schema: "core",
            table: "ceremony_requests",
            column: "CandidatePersonId");

        migrationBuilder.CreateIndex(
            name: "IX_ceremony_requests_OrganizationId_CeremonyType_Status",
            schema: "core",
            table: "ceremony_requests",
            columns: new[] { "OrganizationId", "CeremonyType", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_ceremony_validations_CeremonyRequestId_ValidationType_RecordedAtUtc",
            schema: "core",
            table: "ceremony_validations",
            columns: new[] { "CeremonyRequestId", "ValidationType", "RecordedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_candidate_publications_CeremonyRequestId",
            schema: "core",
            table: "candidate_publications",
            column: "CeremonyRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_candidate_publications_PersonId_PublishedFromUtc",
            schema: "core",
            table: "candidate_publications",
            columns: new[] { "PersonId", "PublishedFromUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_candidate_publications_OrganizationId_Status",
            schema: "core",
            table: "candidate_publications",
            columns: new[] { "OrganizationId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "candidate_publications", schema: "core");
        migrationBuilder.DropTable(name: "ceremony_validations", schema: "core");
        migrationBuilder.DropTable(name: "institutional_rule_settings", schema: "core");
        migrationBuilder.DropTable(name: "ceremony_requests", schema: "core");
    }
}
