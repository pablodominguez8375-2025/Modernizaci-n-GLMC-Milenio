using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #1035: estado y origen de cada Taller para Sistema › Talleres.
///
/// Solo operaciones nativas de EF (AddColumn / AddCheckConstraint): SIN <c>migrationBuilder.Sql</c>, de modo que el
/// clasificador del actualizador pueda PROBAR que es aditiva sin allowlist de SQL manual. Una prueba
/// (WorkshopRegistryMigrationShapeTests) fija esa forma: cualquier SQL manual u operación distinta rompe CI.
///
/// Costo: ADD COLUMN con DEFAULT constante es solo de metadatos en PostgreSQL 11+; el CHECK recorre una tabla de
/// decenas de filas. Compatible con API N: las columnas nuevas tienen DEFAULT, los INSERT de la API anterior siguen
/// funcionando y sus SELECT nombran solo columnas antiguas (probado contra PostgreSQL real).
/// Sin UPDATE de filas existentes: el origen de Gran Logia/soporte se deriva al leer.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261118150000_AddWorkshopRegistryState")]
public sealed class AddWorkshopRegistryState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Status", schema: "core", table: "organizations", type: "character varying(20)",
            maxLength: 20, nullable: false, defaultValue: "active");
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RetiredAtUtc", schema: "core", table: "organizations", type: "timestamp with time zone",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RetiredReason", schema: "core", table: "organizations", type: "character varying(500)",
            maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "Origin", schema: "core", table: "organizations", type: "character varying(40)",
            maxLength: 40, nullable: false, defaultValue: "other");
        migrationBuilder.AddCheckConstraint(
            name: "CK_organizations_Status", schema: "core", table: "organizations",
            sql: "\"Status\" IN ('active', 'retired')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollback conservador: no se eliminan columnas con estado de baja ni su motivo.
        // Solo se retira la restricción agregada.
        migrationBuilder.DropCheckConstraint(name: "CK_organizations_Status", schema: "core", table: "organizations");
    }
}
