using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>#588 — categorías persistentes de la Biblioteca Virtual (4 base sembradas).</summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261024030000_AddLibraryCategories")]
public sealed class AddLibraryCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "library_categories",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                IsBase = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                UpdatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_library_categories", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_library_categories_Code", schema: "core", table: "library_categories", column: "Code", unique: true);
        migrationBuilder.Sql("""
            INSERT INTO core.library_categories ("Id","Code","Label","Position","Active","IsBase","CreatedAtUtc") VALUES
              ('5b0c1e10-0000-4000-8000-000000000001','work_paper','Planchas de Trabajo',1,true,true,'2026-10-24T00:00:00Z'),
              ('5b0c1e10-0000-4000-8000-000000000002','book','Libros',2,true,true,'2026-10-24T00:00:00Z'),
              ('5b0c1e10-0000-4000-8000-000000000003','magazine','Revistas',3,true,true,'2026-10-24T00:00:00Z'),
              ('5b0c1e10-0000-4000-8000-000000000004','bulletin','Boletines',4,true,true,'2026-10-24T00:00:00Z')
            ON CONFLICT DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "library_categories", schema: "core");
}
