using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260924120000_AddLodgePaymentIdempotency")]
public partial class AddLodgePaymentIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "IdempotencyKey", schema: "core", table: "lodge_member_payments",
            type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_lodge_member_payments_ChargeId_IdempotencyKey", schema: "core",
            table: "lodge_member_payments", columns: new[] { "ChargeId", "IdempotencyKey" }, unique: true,
            filter: "\"IdempotencyKey\" IS NOT NULL");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_lodge_member_payments_ChargeId_IdempotencyKey", schema: "core", table: "lodge_member_payments");
        migrationBuilder.DropColumn(name: "IdempotencyKey", schema: "core", table: "lodge_member_payments");
    }
}
