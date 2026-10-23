using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>#653 — vigencia de la publicación: el Gran Archivero fija hasta cuándo es visible (por defecto 12 meses).</summary>
[DbContext(typeof(GrandArchiveDbContext))]
[Migration("20261023170000_AddGrandArchivePublicationExpiry")]
public sealed class AddGrandArchivePublicationExpiry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(name: "PublishedUntilUtc", schema: "core", table: "grand_archive_records", type: "timestamp with time zone", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PublishedUntilUtc", schema: "core", table: "grand_archive_records");
    }
}
