using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260923150000_AddLodgeTreasuryIncomes")]
public partial class AddLodgeTreasuryIncomes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "lodge_treasury_incomes", schema: "core", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
            Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
            Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
            IncomeDate = table.Column<DateOnly>(type: "date", nullable: false),
            Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
            EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
            RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_lodge_treasury_incomes", x => x.Id);
            table.ForeignKey(name: "FK_lodge_treasury_incomes_organizations_OrganizationId", column: x => x.OrganizationId,
                principalSchema: "core", principalTable: "organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_lodge_treasury_incomes_OrganizationId_IncomeDate", schema: "core", table: "lodge_treasury_incomes", columns: new[] { "OrganizationId", "IncomeDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "lodge_treasury_incomes", schema: "core");
}
