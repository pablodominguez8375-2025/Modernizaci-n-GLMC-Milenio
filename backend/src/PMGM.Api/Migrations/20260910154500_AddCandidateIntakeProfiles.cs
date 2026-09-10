using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Modules.CandidateIntake;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(CandidateIntakeDbContext))]
[Migration("20260910154500_AddCandidateIntakeProfiles")]
public partial class AddCandidateIntakeProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "candidate_intake_profiles",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CeremonyRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                PaternalSurname = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                MaternalSurname = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                RutOrInstitutionalId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                Nationality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                CivilStatus = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                Occupation = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                City = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                Orient = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                PresentersJson = table.Column<string>(type: "jsonb", nullable: false),
                InsinuationDate = table.Column<DateOnly>(type: "date", nullable: false),
                PhotoVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                InterviewSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                InternalObservations = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                SubmittedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_candidate_intake_profiles", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_candidate_intake_profiles_CeremonyRequestId",
            schema: "core",
            table: "candidate_intake_profiles",
            column: "CeremonyRequestId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_candidate_intake_profiles_OrganizationId_SubmittedAtUtc",
            schema: "core",
            table: "candidate_intake_profiles",
            columns: new[] { "OrganizationId", "SubmittedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_candidate_intake_profiles_PersonId",
            schema: "core",
            table: "candidate_intake_profiles",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_candidate_intake_profiles_PhotoVersionId",
            schema: "core",
            table: "candidate_intake_profiles",
            column: "PhotoVersionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "candidate_intake_profiles", schema: "core");
    }
}
