using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260923210000_AddLodgeTreasuryYearClosures")]
public partial class AddLodgeTreasuryYearClosures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lodge_treasury_year_closures",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                AccountingYear = table.Column<int>(type: "integer", nullable: false),
                OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Income = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                AuthorizedExpenses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                ClosingBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                MovementCount = table.Column<int>(type: "integer", nullable: false),
                ClosedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_treasury_year_closures", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_treasury_year_closures_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_treasury_year_closures_OrganizationId_AccountingYear",
            schema: "core",
            table: "lodge_treasury_year_closures",
            columns: new[] { "OrganizationId", "AccountingYear" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "lodge_treasury_year_closures", schema: "core");
}
