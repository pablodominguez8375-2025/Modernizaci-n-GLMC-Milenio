using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260912103000_AddTreasuryMonthlyStatements")]
public partial class AddTreasuryMonthlyStatements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "treasury_adjustments", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                AdjustmentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                AuthorizationReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_treasury_adjustments", x => x.Id);
                table.ForeignKey(name: "FK_treasury_adjustments_members_MemberId", column: x => x.MemberId, principalSchema: "core", principalTable: "members", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_treasury_adjustments_organizations_OrganizationId", column: x => x.OrganizationId, principalSchema: "core", principalTable: "organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "treasury_monthly_statements", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                PeriodYear = table.Column<int>(type: "integer", nullable: false),
                PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                RectifiesStatementId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReconciledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_treasury_monthly_statements", x => x.Id);
                table.ForeignKey(name: "FK_treasury_monthly_statements_organizations_OrganizationId", column: x => x.OrganizationId, principalSchema: "core", principalTable: "organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_treasury_monthly_statements_treasury_monthly_statements_RectifiesStatementId", column: x => x.RectifiesStatementId, principalSchema: "core", principalTable: "treasury_monthly_statements", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "treasury_monthly_statement_lines", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StatementId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                MembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                DegreeCodeAtCutoff = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                OfficeCodeAtCutoff = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                AdjustmentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                AdjustmentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                AuthorizationReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Observation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                IdentityMatchStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_treasury_monthly_statement_lines", x => x.Id);
                table.ForeignKey(name: "FK_treasury_monthly_statement_lines_members_MemberId", column: x => x.MemberId, principalSchema: "core", principalTable: "members", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_treasury_monthly_statement_lines_memberships_MembershipId", column: x => x.MembershipId, principalSchema: "core", principalTable: "memberships", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_treasury_monthly_statement_lines_treasury_monthly_statements_StatementId", column: x => x.StatementId, principalSchema: "core", principalTable: "treasury_monthly_statements", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "treasury_payments", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StatementId = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                PayerDisplayName = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                PayerRut = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                Reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_treasury_payments", x => x.Id);
                table.ForeignKey(name: "FK_treasury_payments_treasury_monthly_statements_StatementId", column: x => x.StatementId, principalSchema: "core", principalTable: "treasury_monthly_statements", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_treasury_adjustments_MemberId_OrganizationId_EffectiveFrom", "core", "treasury_adjustments", new[] { "MemberId", "OrganizationId", "EffectiveFrom" });
        migrationBuilder.CreateIndex("IX_treasury_adjustments_OrganizationId", "core", "treasury_adjustments", "OrganizationId");
        migrationBuilder.CreateIndex("IX_treasury_monthly_statement_lines_MemberId", "core", "treasury_monthly_statement_lines", "MemberId");
        migrationBuilder.CreateIndex("IX_treasury_monthly_statement_lines_MembershipId", "core", "treasury_monthly_statement_lines", "MembershipId");
        migrationBuilder.CreateIndex("IX_treasury_monthly_statement_lines_StatementId_MemberId", "core", "treasury_monthly_statement_lines", new[] { "StatementId", "MemberId" });
        migrationBuilder.CreateIndex("IX_treasury_monthly_statements_OrganizationId_PeriodYear_PeriodMonth", "core", "treasury_monthly_statements", new[] { "OrganizationId", "PeriodYear", "PeriodMonth" }, unique: true, filter: "\"RectifiesStatementId\" IS NULL");
        migrationBuilder.CreateIndex("IX_treasury_monthly_statements_RectifiesStatementId", "core", "treasury_monthly_statements", "RectifiesStatementId");
        migrationBuilder.CreateIndex("IX_treasury_payments_StatementId", "core", "treasury_payments", "StatementId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("treasury_adjustments", "core");
        migrationBuilder.DropTable("treasury_monthly_statement_lines", "core");
        migrationBuilder.DropTable("treasury_payments", "core");
        migrationBuilder.DropTable("treasury_monthly_statements", "core");
    }
}
