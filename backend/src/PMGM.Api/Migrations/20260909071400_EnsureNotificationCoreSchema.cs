using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(NotificationDbContext))]
[Migration("20260909071400_EnsureNotificationCoreSchema")]
public partial class EnsureNotificationCoreSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.EnsureSchema(name: "core");

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // El esquema core es compartido por otros módulos y nunca debe eliminarse desde Notificaciones.
    }
}
