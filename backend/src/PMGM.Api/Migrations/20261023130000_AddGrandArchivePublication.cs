using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #653 — publicación de documentos oficiales del Gran Archivero a los Hermanos
/// con grado mínimo (1, 2 o 3). Todas las columnas son nulables: lo ya archivado
/// sigue siendo interno hasta que el Gran Archivero lo publique.
/// </summary>
[DbContext(typeof(GrandArchiveDbContext))]
[Migration("20261023130000_AddGrandArchivePublication")]
public sealed class AddGrandArchivePublication : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "PublishedMinimumDegree", schema: "core", table: "grand_archive_records", type: "integer", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "PublishedAtUtc", schema: "core", table: "grand_archive_records", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PublishedBySubject", schema: "core", table: "grand_archive_records", type: "character varying(320)", maxLength: 320, nullable: true);
        migrationBuilder.AddColumn<string>(name: "PublishedByDisplayName", schema: "core", table: "grand_archive_records", type: "character varying(320)", maxLength: 320, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "UnpublishedAtUtc", schema: "core", table: "grand_archive_records", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "UnpublishReason", schema: "core", table: "grand_archive_records", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddCheckConstraint(
            name: "CK_grand_archive_records_published_degree", schema: "core", table: "grand_archive_records",
            sql: "\"PublishedMinimumDegree\" IS NULL OR \"PublishedMinimumDegree\" BETWEEN 1 AND 3");
        migrationBuilder.CreateIndex(
            name: "IX_grand_archive_records_PublishedMinimumDegree_Status", schema: "core", table: "grand_archive_records",
            columns: new[] { "PublishedMinimumDegree", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_grand_archive_records_PublishedMinimumDegree_Status", schema: "core", table: "grand_archive_records");
        migrationBuilder.DropCheckConstraint(name: "CK_grand_archive_records_published_degree", schema: "core", table: "grand_archive_records");
        foreach (var column in new[] { "PublishedMinimumDegree", "PublishedAtUtc", "PublishedBySubject", "PublishedByDisplayName", "UnpublishedAtUtc", "UnpublishReason" })
            migrationBuilder.DropColumn(name: column, schema: "core", table: "grand_archive_records");
    }
}
