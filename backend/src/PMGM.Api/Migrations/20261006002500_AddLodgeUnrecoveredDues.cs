using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20261006002500_AddLodgeUnrecoveredDues")]
public sealed class AddLodgeUnrecoveredDues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE core.lodge_unrecovered_dues (
            "Id" uuid PRIMARY KEY,
            "OrganizationId" uuid NOT NULL REFERENCES core.organizations("Id") ON DELETE RESTRICT,
            "WithdrawalRequestId" uuid NOT NULL REFERENCES core.member_withdrawal_requests("Id") ON DELETE RESTRICT,
            "ChargeId" uuid NOT NULL REFERENCES core.lodge_member_charges("Id") ON DELETE RESTRICT,
            "RecognitionDate" date NOT NULL,
            "ChargedAmount" numeric(18,2) NOT NULL,
            "PaidAmount" numeric(18,2) NOT NULL,
            "Amount" numeric(18,2) NOT NULL CHECK ("Amount" > 0 AND "Amount" = "ChargedAmount" - "PaidAmount"),
            "Currency" varchar(3) NOT NULL CHECK ("Currency" IN ('CLP','USD')),
            "EvidenceReference" varchar(500) NOT NULL,
            "RecordedBySubject" varchar(320) NOT NULL,
            "RecordedAtUtc" timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX "IX_lodge_unrecovered_dues_ChargeId" ON core.lodge_unrecovered_dues("ChargeId");
        CREATE INDEX "IX_lodge_unrecovered_dues_OrganizationId_RecognitionDate_Currency"
            ON core.lodge_unrecovered_dues("OrganizationId", "RecognitionDate", "Currency");
        CREATE TRIGGER unrecovered_due_append_only BEFORE UPDATE OR DELETE ON core.lodge_unrecovered_dues
            FOR EACH ROW EXECUTE FUNCTION core.guard_receipt_append_only();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("La pérdida conserva evidencia inmutable; rollback mediante respaldo.");
}
