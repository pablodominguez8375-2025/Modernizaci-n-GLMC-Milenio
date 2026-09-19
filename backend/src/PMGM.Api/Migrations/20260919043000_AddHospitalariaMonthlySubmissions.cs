using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260919043000_AddHospitalariaMonthlySubmissions")]
public partial class AddHospitalariaMonthlySubmissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ApprovalSource",
            schema: "core",
            table: "lodge_hospitalaria_movements",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CouncilDecisionId",
            schema: "core",
            table: "lodge_hospitalaria_movements",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "hospitalaria_monthly_submissions",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                PeriodYear = table.Column<int>(type: "integer", nullable: false),
                PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                IncomeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                ApprovedExpenseAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                PeriodNetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                MovementCount = table.Column<int>(type: "integer", nullable: false),
                PendingExpenseCount = table.Column<int>(type: "integer", nullable: false),
                ReplenishmentDueAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                ReplenishmentPaidAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                DifferenceAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                PaymentReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CouncilFinancialReviewId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReviewedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hospitalaria_monthly_submissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_hospitalaria_monthly_submissions_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_hospitalaria_monthly_submissions_OrganizationId_PeriodYear_PeriodMonth",
            schema: "core",
            table: "hospitalaria_monthly_submissions",
            columns: new[] { "OrganizationId", "PeriodYear", "PeriodMonth" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_hospitalaria_monthly_submissions_Status_SubmittedAtUtc",
            schema: "core",
            table: "hospitalaria_monthly_submissions",
            columns: new[] { "Status", "SubmittedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "hospitalaria_monthly_submissions", schema: "core");
        migrationBuilder.DropColumn(name: "ApprovalSource", schema: "core", table: "lodge_hospitalaria_movements");
        migrationBuilder.DropColumn(name: "CouncilDecisionId", schema: "core", table: "lodge_hospitalaria_movements");
    }
}
