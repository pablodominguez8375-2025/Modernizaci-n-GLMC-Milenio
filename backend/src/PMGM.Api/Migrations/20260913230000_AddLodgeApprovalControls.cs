using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260913230000_AddLodgeApprovalControls")]
public partial class AddLodgeApprovalControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ApprovalStatus", schema: "core", table: "lodge_hospitalaria_movements", type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "not_required");
        migrationBuilder.AddColumn<string>(name: "ApprovedBySubject", schema: "core", table: "lodge_hospitalaria_movements", type: "character varying(320)", maxLength: 320, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "ApprovedAtUtc", schema: "core", table: "lodge_hospitalaria_movements", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "OratorSignatureSubject", schema: "core", table: "member_withdrawal_requests", type: "character varying(320)", maxLength: 320, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "OratorSignedAtUtc", schema: "core", table: "member_withdrawal_requests", type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateTable(name: "lodge_treasury_expenses", schema: "core", columns: table => new { Id = table.Column<Guid>(type: "uuid", nullable: false), OrganizationId = table.Column<Guid>(type: "uuid", nullable: false), Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false), Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false), ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false), Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false), EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), ApprovalStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false), RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false), ApprovedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true), ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true), RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false) }, constraints: table => { table.PrimaryKey("PK_lodge_treasury_expenses", x => x.Id); table.ForeignKey(name: "FK_lodge_treasury_expenses_organizations_OrganizationId", column: x => x.OrganizationId, principalSchema: "core", principalTable: "organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict); });
        migrationBuilder.CreateIndex(name: "IX_lodge_treasury_expenses_OrganizationId_ExpenseDate", schema: "core", table: "lodge_treasury_expenses", columns: new[] { "OrganizationId", "ExpenseDate" });
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ApprovalStatus", schema: "core", table: "lodge_hospitalaria_movements");
        migrationBuilder.DropColumn(name: "ApprovedBySubject", schema: "core", table: "lodge_hospitalaria_movements");
        migrationBuilder.DropColumn(name: "ApprovedAtUtc", schema: "core", table: "lodge_hospitalaria_movements");
        migrationBuilder.DropColumn(name: "OratorSignatureSubject", schema: "core", table: "member_withdrawal_requests");
        migrationBuilder.DropColumn(name: "OratorSignedAtUtc", schema: "core", table: "member_withdrawal_requests");
        migrationBuilder.DropTable(name: "lodge_treasury_expenses", schema: "core");
    }
}
