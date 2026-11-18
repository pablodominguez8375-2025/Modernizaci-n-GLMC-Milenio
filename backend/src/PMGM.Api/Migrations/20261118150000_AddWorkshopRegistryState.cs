using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #1035: estado y origen de cada Taller para Sistema › Talleres.
/// Solo agrega columnas con valor por defecto: no borra, no reescribe datos
/// existentes ni cambia restricciones previas. Es compatible con la API anterior
/// (que ignora las columnas nuevas).
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261118150000_AddWorkshopRegistryState")]
public sealed class AddWorkshopRegistryState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE core.organizations
                ADD COLUMN IF NOT EXISTS "Status" character varying(20) NOT NULL DEFAULT 'active',
                ADD COLUMN IF NOT EXISTS "RetiredAtUtc" timestamp with time zone NULL,
                ADD COLUMN IF NOT EXISTS "RetiredReason" character varying(500) NULL,
                ADD COLUMN IF NOT EXISTS "Origin" character varying(40) NOT NULL DEFAULT 'other';

            ALTER TABLE core.organizations
                DROP CONSTRAINT IF EXISTS "CK_organizations_Status";
            ALTER TABLE core.organizations
                ADD CONSTRAINT "CK_organizations_Status" CHECK ("Status" IN ('active', 'retired'));

            -- Las organizaciones técnicas existentes se marcan como configuración inicial;
            -- el resto queda como «other» hasta que la carga inicial o el QA las identifiquen.
            UPDATE core.organizations SET "Origin" = 'bootstrap'
            WHERE lower("Type") IN ('grand_lodge', 'support') AND "Origin" = 'other';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollback conservador: no se eliminan columnas con estado de baja ni su motivo.
        // Solo se retira la restricción agregada.
        migrationBuilder.Sql("""
            ALTER TABLE core.organizations DROP CONSTRAINT IF EXISTS "CK_organizations_Status";
            """);
    }
}
