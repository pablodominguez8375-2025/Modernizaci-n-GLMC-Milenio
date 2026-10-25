using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>#574 — sesiones de soporte «Ver como» (solo lectura, 30 min máx.).</summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261023230000_AddSupportViewSessions")]
public sealed class AddSupportViewSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "support_view_sessions",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OperatorSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                TargetSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_support_view_sessions", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_support_view_sessions_OperatorSubject_EndedAtUtc", schema: "core",
            table: "support_view_sessions", columns: new[] { "OperatorSubject", "EndedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "support_view_sessions", schema: "core");
}
