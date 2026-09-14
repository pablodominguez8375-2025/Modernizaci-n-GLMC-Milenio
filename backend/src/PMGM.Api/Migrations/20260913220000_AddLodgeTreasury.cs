using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260913220000_AddLodgeTreasury")]
public partial class AddLodgeTreasury : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lodge_fee_plans", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                FeeType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                MemberAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                GrandTreasuryAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_fee_plans", x => x.Id);
                table.ForeignKey(name: "FK_lodge_fee_plans_organizations_OrganizationId", column: x => x.OrganizationId,
                    principalSchema: "core", principalTable: "organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "lodge_member_charges", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                FeePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                PeriodYear = table.Column<int>(type: "integer", nullable: false),
                PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                MemberAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                GrandTreasuryAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_member_charges", x => x.Id);
                table.ForeignKey(name: "FK_lodge_member_charges_lodge_fee_plans_FeePlanId", column: x => x.FeePlanId,
                    principalSchema: "core", principalTable: "lodge_fee_plans", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_lodge_member_charges_members_MemberId", column: x => x.MemberId,
                    principalSchema: "core", principalTable: "members", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_lodge_member_charges_organizations_OrganizationId", column: x => x.OrganizationId,
                    principalSchema: "core", principalTable: "organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "lodge_member_payments", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                PaymentMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                ReceiptNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_member_payments", x => x.Id);
                table.ForeignKey(name: "FK_lodge_member_payments_lodge_member_charges_ChargeId", column: x => x.ChargeId,
                    principalSchema: "core", principalTable: "lodge_member_charges", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_lodge_fee_plans_OrganizationId_FeeType_EffectiveFrom", schema: "core",
            table: "lodge_fee_plans", columns: new[] { "OrganizationId", "FeeType", "EffectiveFrom" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_lodge_member_charges_FeePlanId", schema: "core", table: "lodge_member_charges", column: "FeePlanId");
        migrationBuilder.CreateIndex(name: "IX_lodge_member_charges_MemberId", schema: "core", table: "lodge_member_charges", column: "MemberId");
        migrationBuilder.CreateIndex(name: "IX_lodge_member_charges_OrganizationId_MemberId_PeriodYear_PeriodMonth", schema: "core",
            table: "lodge_member_charges", columns: new[] { "OrganizationId", "MemberId", "PeriodYear", "PeriodMonth" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_lodge_member_payments_ChargeId", schema: "core", table: "lodge_member_payments", column: "ChargeId");
        migrationBuilder.CreateIndex(name: "IX_lodge_member_payments_ReceiptNumber", schema: "core", table: "lodge_member_payments", column: "ReceiptNumber", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "lodge_member_payments", schema: "core");
        migrationBuilder.DropTable(name: "lodge_member_charges", schema: "core");
        migrationBuilder.DropTable(name: "lodge_fee_plans", schema: "core");
    }
}
