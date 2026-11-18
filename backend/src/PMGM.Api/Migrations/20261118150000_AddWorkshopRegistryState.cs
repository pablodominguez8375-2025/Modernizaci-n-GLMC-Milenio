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
        // Expand puro y de costo acotado:
        //  - ADD COLUMN con DEFAULT constante es solo de metadatos en PostgreSQL 11+ (sin reescribir la tabla).
        //  - El CHECK se agrega NOT VALID y se valida aparte (VALIDATE toma SHARE UPDATE EXCLUSIVE, no bloquea lecturas ni escrituras).
        //  - lock_timeout evita esperar indefinidamente el ACCESS EXCLUSIVE breve del ALTER si hay transacciones largas.
        //  - Sin UPDATE de filas existentes: el origen de Gran Logia/soporte se deriva al leer.
        // Compatibilidad con API N: las columnas nuevas tienen DEFAULT, así que los INSERT de la API anterior
        // (que no las conocen) siguen funcionando, y sus SELECT nombran solo columnas antiguas.
        migrationBuilder.Sql("""
            SET LOCAL lock_timeout = '5s';

            ALTER TABLE core.organizations
                ADD COLUMN IF NOT EXISTS "Status" character varying(20) NOT NULL DEFAULT 'active',
                ADD COLUMN IF NOT EXISTS "RetiredAtUtc" timestamp with time zone NULL,
                ADD COLUMN IF NOT EXISTS "RetiredReason" character varying(500) NULL,
                ADD COLUMN IF NOT EXISTS "Origin" character varying(40) NOT NULL DEFAULT 'other';

            DO $workshop_registry$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'CK_organizations_Status'
                      AND conrelid = 'core.organizations'::regclass
                ) THEN
                    ALTER TABLE core.organizations
                        ADD CONSTRAINT "CK_organizations_Status"
                        CHECK ("Status" IN ('active', 'retired')) NOT VALID;
                END IF;
            END
            $workshop_registry$;

            ALTER TABLE core.organizations VALIDATE CONSTRAINT "CK_organizations_Status";
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
