using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;
using PMGM.Api.Modules.SystemConfiguration;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #1039 T1: guardia en base de datos para Talleres dados de baja (solo lectura). SQL manual con digest fijado
/// (WorkshopRetiredGuardSql): migracion MANUAL/supervisada, NO auto-aplicable. Ver WorkshopRetiredGuardSql.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261118170000_AddWorkshopRetiredGuard")]
public sealed class AddWorkshopRetiredGuard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(WorkshopRetiredGuardSql.Up);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(WorkshopRetiredGuardSql.Down);
}
