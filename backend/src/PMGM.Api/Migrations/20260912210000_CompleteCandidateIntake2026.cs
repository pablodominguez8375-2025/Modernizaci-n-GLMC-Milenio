using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Modules.CandidateIntake;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(CandidateIntakeDbContext))]
[Migration("20260912210000_CompleteCandidateIntake2026")]
public partial class CompleteCandidateIntake2026 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "EmployerName", schema: "core", table: "candidate_intake_profiles", type: "character varying(240)", maxLength: 240, nullable: true);
        migrationBuilder.AddColumn<string>(name: "WorkAddress", schema: "core", table: "candidate_intake_profiles", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "WorkPosition", schema: "core", table: "candidate_intake_profiles", type: "character varying(240)", maxLength: 240, nullable: true);
        migrationBuilder.AddColumn<string>(name: "WorkPhone", schema: "core", table: "candidate_intake_profiles", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<DateOnly>(name: "FirstDegreePresentationDate", schema: "core", table: "candidate_intake_profiles", type: "date", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ResponsibleSecretaryName", schema: "core", table: "candidate_intake_profiles", type: "character varying(240)", maxLength: 240, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EmployerName", schema: "core", table: "candidate_intake_profiles");
        migrationBuilder.DropColumn(name: "WorkAddress", schema: "core", table: "candidate_intake_profiles");
        migrationBuilder.DropColumn(name: "WorkPosition", schema: "core", table: "candidate_intake_profiles");
        migrationBuilder.DropColumn(name: "WorkPhone", schema: "core", table: "candidate_intake_profiles");
        migrationBuilder.DropColumn(name: "FirstDegreePresentationDate", schema: "core", table: "candidate_intake_profiles");
        migrationBuilder.DropColumn(name: "ResponsibleSecretaryName", schema: "core", table: "candidate_intake_profiles");
    }
}
