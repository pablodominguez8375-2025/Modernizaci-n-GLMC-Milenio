using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260929150000_AddLodgeMemberReceiptsAndAllocations")]
public partial class AddLodgeMemberReceiptsAndAllocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lodge_member_receipts", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                PaymentMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                ReceiptNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_member_receipts", x => x.Id);
                table.ForeignKey(name: "FK_lodge_member_receipts_members_MemberId", column: x => x.MemberId,
                    principalSchema: "core", principalTable: "members", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_lodge_member_receipts_organizations_OrganizationId", column: x => x.OrganizationId,
                    principalSchema: "core", principalTable: "organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "lodge_member_payment_allocations", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                AllocatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                AllocatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_member_payment_allocations", x => x.Id);
                table.ForeignKey(name: "FK_lodge_member_payment_allocations_lodge_member_charges_ChargeId", column: x => x.ChargeId,
                    principalSchema: "core", principalTable: "lodge_member_charges", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_lodge_member_payment_allocations_lodge_member_receipts_ReceiptId", column: x => x.ReceiptId,
                    principalSchema: "core", principalTable: "lodge_member_receipts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_lodge_member_receipts_MemberId", schema: "core", table: "lodge_member_receipts", column: "MemberId");
        migrationBuilder.CreateIndex(name: "IX_lodge_member_receipts_OrganizationId_PaymentDate_Currency", schema: "core",
            table: "lodge_member_receipts", columns: new[] { "OrganizationId", "PaymentDate", "Currency" });
        migrationBuilder.CreateIndex(name: "IX_lodge_member_receipts_ReceiptNumber", schema: "core", table: "lodge_member_receipts", column: "ReceiptNumber", unique: true);
        migrationBuilder.CreateIndex(name: "IX_lodge_member_receipts_IdempotencyKey", schema: "core", table: "lodge_member_receipts", column: "IdempotencyKey", unique: true);
        migrationBuilder.CreateIndex(name: "IX_lodge_member_payment_allocations_ChargeId", schema: "core", table: "lodge_member_payment_allocations", column: "ChargeId");
        migrationBuilder.CreateIndex(name: "IX_lodge_member_payment_allocations_ReceiptId_ChargeId", schema: "core",
            table: "lodge_member_payment_allocations", columns: new[] { "ReceiptId", "ChargeId" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "lodge_member_payment_allocations", schema: "core");
        migrationBuilder.DropTable(name: "lodge_member_receipts", schema: "core");
    }
}
