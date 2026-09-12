using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(GrandArchiveDbContext))]
[Migration("20260909192000_EnsureGrandArchiveCoreSchema")]
public partial class EnsureGrandArchiveCoreSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.EnsureSchema(name: "core");

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // El esquema core es compartido por los módulos institucionales.
    }
}
