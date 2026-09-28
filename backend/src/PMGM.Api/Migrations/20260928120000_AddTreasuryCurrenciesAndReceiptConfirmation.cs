using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260928120000_AddTreasuryCurrenciesAndReceiptConfirmation")]
public partial class AddTreasuryCurrenciesAndReceiptConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "lodge_fee_plans", "lodge_member_charges", "lodge_member_payments", "lodge_treasury_expenses", "lodge_treasury_incomes", "lodge_treasury_configurations", "lodge_treasury_year_closures", "lodge_treasury_reconciliations", "treasury_monthly_statements" })
            migrationBuilder.AddColumn<string>(name: "Currency", schema: "core", table: table, type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "CLP");

        migrationBuilder.AddColumn<string>(name: "BankReceiptConfirmedBySubject", schema: "core", table: "treasury_monthly_statements", type: "character varying(320)", maxLength: 320, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "BankReceiptConfirmedAtUtc", schema: "core", table: "treasury_monthly_statements", type: "timestamp with time zone", nullable: true);

        migrationBuilder.DropIndex(name: "IX_lodge_treasury_configurations_OrganizationId", schema: "core", table: "lodge_treasury_configurations");
        migrationBuilder.CreateIndex(name: "IX_lodge_treasury_configurations_OrganizationId_Currency", schema: "core", table: "lodge_treasury_configurations", columns: new[] { "OrganizationId", "Currency" }, unique: true);
        migrationBuilder.DropIndex(name: "IX_lodge_treasury_year_closures_OrganizationId_AccountingYear", schema: "core", table: "lodge_treasury_year_closures");
        migrationBuilder.CreateIndex(name: "IX_lodge_treasury_year_closures_OrganizationId_AccountingYear_Currency", schema: "core", table: "lodge_treasury_year_closures", columns: new[] { "OrganizationId", "AccountingYear", "Currency" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_lodge_treasury_configurations_OrganizationId_Currency", schema: "core", table: "lodge_treasury_configurations");
        migrationBuilder.CreateIndex(name: "IX_lodge_treasury_configurations_OrganizationId", schema: "core", table: "lodge_treasury_configurations", column: "OrganizationId", unique: true);
        migrationBuilder.DropIndex(name: "IX_lodge_treasury_year_closures_OrganizationId_AccountingYear_Currency", schema: "core", table: "lodge_treasury_year_closures");
        migrationBuilder.CreateIndex(name: "IX_lodge_treasury_year_closures_OrganizationId_AccountingYear", schema: "core", table: "lodge_treasury_year_closures", columns: new[] { "OrganizationId", "AccountingYear" }, unique: true);

        migrationBuilder.DropColumn(name: "BankReceiptConfirmedBySubject", schema: "core", table: "treasury_monthly_statements");
        migrationBuilder.DropColumn(name: "BankReceiptConfirmedAtUtc", schema: "core", table: "treasury_monthly_statements");
        foreach (var table in new[] { "lodge_fee_plans", "lodge_member_charges", "lodge_member_payments", "lodge_treasury_expenses", "lodge_treasury_incomes", "lodge_treasury_configurations", "lodge_treasury_year_closures", "lodge_treasury_reconciliations", "treasury_monthly_statements" })
            migrationBuilder.DropColumn(name: "Currency", schema: "core", table: table);
    }
}
