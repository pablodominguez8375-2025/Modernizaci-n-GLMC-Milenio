using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>#574 — complemento visible para el Hermano en las acciones de soporte (p. ej. aviso reenviado).</summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261026120000_AddSupportCorrectionDetail")]
public sealed class AddSupportCorrectionDetail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(name: "Detail", schema: "core", table: "support_corrections",
            type: "character varying(200)", maxLength: 200, nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "Detail", schema: "core", table: "support_corrections");
}
