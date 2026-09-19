using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260919013000_AddMeetingDocumentClosure")]
public partial class AddMeetingDocumentClosure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "HeldAtUtc",
            schema: "core",
            table: "lodge_meetings",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CeremonyAuthorizationDocumentId",
            schema: "core",
            table: "lodge_secretariat_records",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE core.lodge_meetings
            SET "HeldAtUtc" = "ClosedAtUtc"
            WHERE "Status" IN ('held', 'closed')
              AND "HeldAtUtc" IS NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE core.lodge_meetings
            SET "Status" = 'held',
                "ClosedAtUtc" = NULL
            WHERE "Status" = 'closed';
            """);

        migrationBuilder.CreateIndex(
            name: "IX_lodge_secretariat_records_CeremonyAuthorizationDocumentId",
            schema: "core",
            table: "lodge_secretariat_records",
            column: "CeremonyAuthorizationDocumentId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_lodge_secretariat_records_CeremonyAuthorizationDocumentId",
            schema: "core",
            table: "lodge_secretariat_records");

        migrationBuilder.DropColumn(
            name: "CeremonyAuthorizationDocumentId",
            schema: "core",
            table: "lodge_secretariat_records");

        migrationBuilder.DropColumn(
            name: "HeldAtUtc",
            schema: "core",
            table: "lodge_meetings");
    }
}
