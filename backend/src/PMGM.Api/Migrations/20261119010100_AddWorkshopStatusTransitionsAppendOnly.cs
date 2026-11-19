using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;
using PMGM.Api.Modules.SystemConfiguration;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #1035/#1044: SQL MANUAL (arbitrary_sql=true ⇒ ventana/supervisada, NO se aplica automáticamente) que hace append-only el
/// historial de transiciones. El SQL exacto y su digest viven en <see cref="WorkshopStatusTransitionsSql"/>.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261119010100_AddWorkshopStatusTransitionsAppendOnly")]
public sealed class AddWorkshopStatusTransitionsAppendOnly : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(WorkshopStatusTransitionsSql.Up);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(WorkshopStatusTransitionsSql.Down);
}
