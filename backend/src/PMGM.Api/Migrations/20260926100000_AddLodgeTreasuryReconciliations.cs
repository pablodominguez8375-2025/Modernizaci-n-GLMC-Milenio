using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260926100000_AddLodgeTreasuryReconciliations")]
public partial class AddLodgeTreasuryReconciliations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lodge_treasury_reconciliations",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                From = table.Column<DateOnly>(type: "date", nullable: false),
                To = table.Column<DateOnly>(type: "date", nullable: false),
                OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Income = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                AuthorizedExpenses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                PendingExpenses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                ClosingBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                ObservedBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Difference = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                MovementCount = table.Column<int>(type: "integer", nullable: false),
                EvidenceReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_treasury_reconciliations", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_treasury_reconciliations_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_treasury_reconciliations_OrganizationId_From_To_RecordedAtUtc",
            schema: "core",
            table: "lodge_treasury_reconciliations",
            columns: new[] { "OrganizationId", "From", "To", "RecordedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "lodge_treasury_reconciliations", schema: "core");
}
