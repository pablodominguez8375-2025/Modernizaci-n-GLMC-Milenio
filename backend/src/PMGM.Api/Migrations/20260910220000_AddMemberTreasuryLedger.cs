using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(TreasuryLedgerDbContext))]
[Migration("20260910220000_AddMemberTreasuryLedger")]
public partial class AddMemberTreasuryLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "member_charges",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                Concept = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Period = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                ChargeType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                IssuedDate = table.Column<DateOnly>(type: "date", nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_member_charges", x => x.Id);
                table.ForeignKey(
                    name: "FK_member_charges_members_MemberId",
                    column: x => x.MemberId,
                    principalSchema: "core",
                    principalTable: "members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_member_charges_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "member_payments",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ReceiptNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                ReceiptDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_member_payments", x => x.Id);
                table.ForeignKey(
                    name: "FK_member_payments_members_MemberId",
                    column: x => x.MemberId,
                    principalSchema: "core",
                    principalTable: "members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_member_payments_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "member_payment_allocations",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_member_payment_allocations", x => x.Id);
                table.ForeignKey(
                    name: "FK_member_payment_allocations_member_charges_ChargeId",
                    column: x => x.ChargeId,
                    principalSchema: "core",
                    principalTable: "member_charges",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_member_payment_allocations_member_payments_PaymentId",
                    column: x => x.PaymentId,
                    principalSchema: "core",
                    principalTable: "member_payments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_member_charges_OrganizationId_MemberId_DueDate",
            schema: "core",
            table: "member_charges",
            columns: new[] { "OrganizationId", "MemberId", "DueDate" });

        migrationBuilder.CreateIndex(
            name: "IX_member_charges_MemberId_Status",
            schema: "core",
            table: "member_charges",
            columns: new[] { "MemberId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_member_charges_MemberId_ChargeType_IssuedDate",
            schema: "core",
            table: "member_charges",
            columns: new[] { "MemberId", "ChargeType", "IssuedDate" });

        migrationBuilder.CreateIndex(
            name: "IX_member_payments_OrganizationId_MemberId_PaymentDate",
            schema: "core",
            table: "member_payments",
            columns: new[] { "OrganizationId", "MemberId", "PaymentDate" });

        migrationBuilder.CreateIndex(
            name: "IX_member_payments_ReceiptDocumentId",
            schema: "core",
            table: "member_payments",
            column: "ReceiptDocumentId");

        migrationBuilder.CreateIndex(
            name: "IX_member_payment_allocations_PaymentId",
            schema: "core",
            table: "member_payment_allocations",
            column: "PaymentId");

        migrationBuilder.CreateIndex(
            name: "IX_member_payment_allocations_ChargeId",
            schema: "core",
            table: "member_payment_allocations",
            column: "ChargeId");

        migrationBuilder.CreateIndex(
            name: "IX_member_payment_allocations_PaymentId_ChargeId",
            schema: "core",
            table: "member_payment_allocations",
            columns: new[] { "PaymentId", "ChargeId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "member_payment_allocations", schema: "core");
        migrationBuilder.DropTable(name: "member_payments", schema: "core");
        migrationBuilder.DropTable(name: "member_charges", schema: "core");
    }
}
