using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260924160000_AddCeremonyRightPayments")]
public partial class AddCeremonyRightPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ceremony_right_payments",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CeremonyRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                PaymentMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                ReceiptNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                RecordedBySubject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ceremony_right_payments", x => x.Id);
                table.ForeignKey(name: "FK_ceremony_right_payments_ceremony_requests_CeremonyRequestId", column: x => x.CeremonyRequestId,
                    principalSchema: "core", principalTable: "ceremony_requests", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_ceremony_right_payments_CeremonyRequestId_PaymentDate", schema: "core",
            table: "ceremony_right_payments", columns: new[] { "CeremonyRequestId", "PaymentDate" });
        migrationBuilder.CreateIndex(name: "IX_ceremony_right_payments_CeremonyRequestId_IdempotencyKey", schema: "core",
            table: "ceremony_right_payments", columns: new[] { "CeremonyRequestId", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_ceremony_right_payments_ReceiptNumber", schema: "core",
            table: "ceremony_right_payments", column: "ReceiptNumber", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ceremony_right_payments", schema: "core");
    }
}
