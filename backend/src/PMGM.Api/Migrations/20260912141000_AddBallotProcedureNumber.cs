using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(LodgeManagementDbContext))]
[Migration("20260912141000_AddBallotProcedureNumber")]
public partial class AddBallotProcedureNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_lodge_anonymous_ballots_MeetingId_Subject_Version", schema: "core", table: "lodge_anonymous_ballots");
        migrationBuilder.AddColumn<int>(name: "ProcedureNumber", schema: "core", table: "lodge_anonymous_ballots", type: "integer", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_lodge_anonymous_ballots_MeetingId_Subject_ProcedureNumber_Version", schema: "core", table: "lodge_anonymous_ballots", columns: new[] { "MeetingId", "Subject", "ProcedureNumber", "Version" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_lodge_anonymous_ballots_MeetingId_Subject_ProcedureNumber_Version", schema: "core", table: "lodge_anonymous_ballots");
        migrationBuilder.DropColumn(name: "ProcedureNumber", schema: "core", table: "lodge_anonymous_ballots");
        migrationBuilder.CreateIndex(name: "IX_lodge_anonymous_ballots_MeetingId_Subject_Version", schema: "core", table: "lodge_anonymous_ballots", columns: new[] { "MeetingId", "Subject", "Version" }, unique: true);
    }
}
