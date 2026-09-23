using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260923160000_AddLodgeTreasuryConfiguration")]
public partial class AddLodgeTreasuryConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "lodge_treasury_configurations", schema: "core", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false), OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
            OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false), OpeningBalanceDate = table.Column<DateOnly>(type: "date", nullable: false),
            IncomeCategories = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
            ExpenseCategories = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
            UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_lodge_treasury_configurations", x => x.Id);
            table.ForeignKey(name: "FK_lodge_treasury_configurations_organizations_OrganizationId", column: x => x.OrganizationId,
                principalSchema: "core", principalTable: "organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_lodge_treasury_configurations_OrganizationId", schema: "core", table: "lodge_treasury_configurations", column: "OrganizationId", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "lodge_treasury_configurations", schema: "core");
}
