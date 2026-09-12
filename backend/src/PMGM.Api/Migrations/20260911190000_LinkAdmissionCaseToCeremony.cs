using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Modules.Admissions;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(AdmissionsDbContext))]
[Migration("20260911190000_LinkAdmissionCaseToCeremony")]
public partial class LinkAdmissionCaseToCeremony : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_ceremony_requests_AdmissionCaseId",
            schema: "core",
            table: "ceremony_requests",
            column: "AdmissionCaseId",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_ceremony_requests_admission_cases_AdmissionCaseId",
            schema: "core",
            table: "ceremony_requests",
            column: "AdmissionCaseId",
            principalSchema: "core",
            principalTable: "admission_cases",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ceremony_requests_admission_cases_AdmissionCaseId",
            schema: "core",
            table: "ceremony_requests");

        migrationBuilder.DropIndex(
            name: "IX_ceremony_requests_AdmissionCaseId",
            schema: "core",
            table: "ceremony_requests");

    }
}
