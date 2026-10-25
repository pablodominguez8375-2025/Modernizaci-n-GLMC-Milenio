using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #588 — revisión del Bibliotecario sobre planchas de trabajo: estado, versión en revisión, motivo de
/// devolución y quién/cuándo resolvió. Todo nullable: lo histórico conserva su comportamiento.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261025000000_AddWorkPaperReview")]
public sealed class AddWorkPaperReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ReviewStatus", schema: "core", table: "institutional_documents",
            type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ReviewVersionId", schema: "core", table: "institutional_documents",
            type: "uuid", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "ReviewSubmittedAtUtc", schema: "core", table: "institutional_documents",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ReviewReason", schema: "core", table: "institutional_documents",
            type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "ReviewedAtUtc", schema: "core", table: "institutional_documents",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ReviewedBySubject", schema: "core", table: "institutional_documents",
            type: "character varying(320)", maxLength: 320, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_institutional_documents_ReviewStatus", schema: "core",
            table: "institutional_documents", column: "ReviewStatus");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_institutional_documents_ReviewStatus", schema: "core", table: "institutional_documents");
        foreach (var column in new[] { "ReviewStatus", "ReviewVersionId", "ReviewSubmittedAtUtc", "ReviewReason", "ReviewedAtUtc", "ReviewedBySubject" })
            migrationBuilder.DropColumn(name: column, schema: "core", table: "institutional_documents");
    }
}
