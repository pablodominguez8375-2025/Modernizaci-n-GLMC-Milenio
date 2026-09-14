using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260911181000_AddAdmissionCaseReferenceToCeremony")]
public partial class AddAdmissionCaseReferenceToCeremony : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AdmissionCaseId",
            schema: "core",
            table: "ceremony_requests",
            type: "uuid",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AdmissionCaseId",
            schema: "core",
            table: "ceremony_requests");
    }
}
