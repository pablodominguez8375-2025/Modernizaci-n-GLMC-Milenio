using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(RegimenInteriorDbContext))]
[Migration("20260909183500_EnsureRegimenInteriorCoreSchema")]
public partial class EnsureRegimenInteriorCoreSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.EnsureSchema(name: "core");

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // El esquema core es compartido por otros módulos y nunca se elimina desde Régimen Interior.
    }
}
