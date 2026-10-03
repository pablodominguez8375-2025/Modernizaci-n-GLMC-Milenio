using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Modules.Admissions;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(AdmissionsDbContext))]
[Migration("20261003113000_AddWithdrawalLetterGrantedDate")]
public partial class AddWithdrawalLetterGrantedDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<DateOnly>(
            name: "WithdrawalLetterGrantedDate",
            schema: "core",
            table: "admission_cases",
            type: "date",
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(
            name: "WithdrawalLetterGrantedDate",
            schema: "core",
            table: "admission_cases");
}
