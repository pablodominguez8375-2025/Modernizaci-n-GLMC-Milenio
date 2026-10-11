using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable
namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20261010214500_AddCandidateInterviewAcceptance")]
public sealed class AddCandidateInterviewAcceptance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Aditiva y compatible con los índices únicos de designaciones activas.
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "AcceptedAtUtc", schema: "core",
            table: "candidate_interview_assignments",
            type: "timestamp with time zone", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AcceptedAtUtc", schema: "core",
            table: "candidate_interview_assignments");
    }
}
