using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260911190000_LinkAdmissionCaseToCeremony")]
public partial class LinkAdmissionCaseToCeremony : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AdmissionCaseId",
            schema: "core",
            table: "ceremony_requests",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ceremony_requests_AdmissionCaseId",
            schema: "core",
            table: "ceremony_requests",
            column: "AdmissionCaseId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ceremony_requests_AdmissionCaseId",
            schema: "core",
            table: "ceremony_requests");

        migrationBuilder.DropColumn(
            name: "AdmissionCaseId",
            schema: "core",
            table: "ceremony_requests");
    }
}
