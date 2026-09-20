using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260920123000_CompleteWithdrawalLetterSignatures")]
public partial class CompleteWithdrawalLetterSignatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "VenerableSignatureSubject", schema: "core", table: "member_withdrawal_requests", type: "character varying(320)", maxLength: 320, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "VenerableSignedAtUtc", schema: "core", table: "member_withdrawal_requests", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "TreasurerSignatureSubject", schema: "core", table: "member_withdrawal_requests", type: "character varying(320)", maxLength: 320, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "TreasurerSignedAtUtc", schema: "core", table: "member_withdrawal_requests", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "SecretarySignatureSubject", schema: "core", table: "member_withdrawal_requests", type: "character varying(320)", maxLength: 320, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "SecretarySignedAtUtc", schema: "core", table: "member_withdrawal_requests", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "ExecutedAtUtc", schema: "core", table: "member_withdrawal_requests", type: "timestamp with time zone", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "VenerableSignatureSubject", schema: "core", table: "member_withdrawal_requests");
        migrationBuilder.DropColumn(name: "VenerableSignedAtUtc", schema: "core", table: "member_withdrawal_requests");
        migrationBuilder.DropColumn(name: "TreasurerSignatureSubject", schema: "core", table: "member_withdrawal_requests");
        migrationBuilder.DropColumn(name: "TreasurerSignedAtUtc", schema: "core", table: "member_withdrawal_requests");
        migrationBuilder.DropColumn(name: "SecretarySignatureSubject", schema: "core", table: "member_withdrawal_requests");
        migrationBuilder.DropColumn(name: "SecretarySignedAtUtc", schema: "core", table: "member_withdrawal_requests");
        migrationBuilder.DropColumn(name: "ExecutedAtUtc", schema: "core", table: "member_withdrawal_requests");
    }
}
