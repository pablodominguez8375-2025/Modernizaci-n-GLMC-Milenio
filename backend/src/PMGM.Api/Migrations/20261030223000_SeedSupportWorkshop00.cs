using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #812: el Taller 00 es una organización técnica de soporte permanente,
/// nunca una logia institucional ni origen de obligaciones de Taller.
/// La migración no crea miembros, cargos, cuentas, cuotas ni actividades.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261030223000_SeedSupportWorkshop00")]
public sealed class SeedSupportWorkshop00 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $support00$
            DECLARE
                support_count integer;
            BEGIN
                -- Nunca reinterpretar un Taller real número 00 como organización técnica.
                IF EXISTS (
                    SELECT 1 FROM core.organizations
                    WHERE "Number" = '00' AND lower("Type") <> 'support'
                ) THEN
                    RAISE EXCEPTION 'SUPPORT_WORKSHOP_00_CONFLICT_WITH_REAL_ORGANIZATION';
                END IF;

                SELECT count(*) INTO support_count
                FROM core.organizations WHERE lower("Type") = 'support';

                IF support_count = 0 THEN
                    INSERT INTO core.organizations
                        ("Id", "Name", "Number", "Type", "CreatedAtUtc")
                    VALUES
                        ('b12c0000-0000-4000-8000-000000000000',
                         'Taller 00 - Soporte', '00', 'support', now());
                ELSIF support_count <> 1 OR EXISTS (
                    SELECT 1 FROM core.organizations
                    WHERE lower("Type") = 'support'
                      AND "Number" IS DISTINCT FROM '00'
                ) THEN
                    RAISE EXCEPTION 'SUPPORT_WORKSHOP_00_INVALID_OR_DUPLICATED';
                END IF;
            END
            $support00$;

            -- Una sola organización técnica y número 00 reservado a soporte.
            CREATE UNIQUE INDEX "UX_organizations_single_support"
                ON core.organizations ((lower("Type")))
                WHERE lower("Type") = 'support';

            ALTER TABLE core.organizations
                ADD CONSTRAINT "CK_organizations_support_number_00"
                CHECK (
                    (lower("Type") = 'support' AND "Number" = '00')
                    OR (lower("Type") <> 'support' AND "Number" IS DISTINCT FROM '00')
                );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No eliminar el Taller 00: su Id puede figurar en evidencias de soporte.
        // En rollback conservador se retiran solo las restricciones agregadas.
        migrationBuilder.Sql("""
            ALTER TABLE core.organizations
                DROP CONSTRAINT IF EXISTS "CK_organizations_support_number_00";
            DROP INDEX IF EXISTS core."UX_organizations_single_support";
            """);
    }
}
