using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260929233000_AddLodgeReceiptAdjustments")]
public partial class AddLodgeReceiptAdjustments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE core.lodge_receipt_adjustments (
                "Id" uuid PRIMARY KEY,
                "ReceiptId" uuid NOT NULL REFERENCES core.lodge_member_receipts("Id") ON DELETE RESTRICT,
                "Kind" varchar(20) NOT NULL CHECK ("Kind" IN ('void', 'correction')),
                "EffectiveDate" date NOT NULL,
                "CashAmount" numeric(18,2) NOT NULL,
                "Reason" varchar(1000) NOT NULL,
                "IdempotencyKey" varchar(100) NOT NULL,
                "RequestPayload" text NOT NULL,
                "RecordedBySubject" varchar(320) NOT NULL,
                "RecordedAtUtc" timestamptz NOT NULL,
                CHECK (("Kind" = 'void' AND "CashAmount" < 0) OR ("Kind" = 'correction' AND "CashAmount" = 0))
            );
            CREATE UNIQUE INDEX "IX_lodge_receipt_adjustments_ReceiptId_IdempotencyKey" ON core.lodge_receipt_adjustments ("ReceiptId", "IdempotencyKey");
            CREATE UNIQUE INDEX "IX_lodge_receipt_adjustments_ReceiptId" ON core.lodge_receipt_adjustments ("ReceiptId") WHERE "Kind" = 'void';
            ALTER TABLE core.lodge_member_payment_allocations
                ADD "EffectiveDate" date NULL,
                ADD "AdjustmentId" uuid NULL REFERENCES core.lodge_receipt_adjustments("Id") ON DELETE RESTRICT,
                ADD "ReversesAllocationId" uuid NULL REFERENCES core.lodge_member_payment_allocations("Id") ON DELETE RESTRICT;
            CREATE INDEX "IX_lodge_member_payment_allocations_AdjustmentId" ON core.lodge_member_payment_allocations ("AdjustmentId");
            CREATE INDEX "IX_lodge_member_payment_allocations_ReversesAllocationId" ON core.lodge_member_payment_allocations ("ReversesAllocationId");
            DROP INDEX core."IX_lodge_member_payment_allocations_ReceiptId_ChargeId";
            CREATE INDEX "IX_lodge_member_payment_allocations_ReceiptId_ChargeId" ON core.lodge_member_payment_allocations ("ReceiptId", "ChargeId");
            CREATE FUNCTION core.guard_receipt_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Receipt history is append-only'; END; $$;
            CREATE TRIGGER receipt_append_only BEFORE UPDATE OR DELETE ON core.lodge_member_receipts
                FOR EACH ROW EXECUTE FUNCTION core.guard_receipt_append_only();
            CREATE TRIGGER allocation_append_only BEFORE UPDATE OR DELETE ON core.lodge_member_payment_allocations
                FOR EACH ROW EXECUTE FUNCTION core.guard_receipt_append_only();
            CREATE TRIGGER adjustment_append_only BEFORE UPDATE OR DELETE ON core.lodge_receipt_adjustments
                FOR EACH ROW EXECUTE FUNCTION core.guard_receipt_append_only();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Los ajustes contables append-only requieren conservar su historial; restaure un respaldo para rollback.");
}
