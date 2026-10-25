using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>#574 etapa 2 — «Corregir como soporte»: permisos del Máster con vigencia y correcciones auditadas.</summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261026000000_AddSupportCorrections")]
public sealed class AddSupportCorrections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "support_correction_grants", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OperatorSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                OperatorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                GrantedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_support_correction_grants", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_support_correction_grants_OperatorSubject_RevokedAtUtc", schema: "core",
            table: "support_correction_grants", columns: new[] { "OperatorSubject", "RevokedAtUtc" });
        migrationBuilder.CreateTable(
            name: "support_corrections", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GrantId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                OperatorSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                OperatorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Field = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_support_corrections", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_support_corrections_MemberId_CreatedAtUtc", schema: "core",
            table: "support_corrections", columns: new[] { "MemberId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "support_corrections", schema: "core");
        migrationBuilder.DropTable(name: "support_correction_grants", schema: "core");
    }
}
