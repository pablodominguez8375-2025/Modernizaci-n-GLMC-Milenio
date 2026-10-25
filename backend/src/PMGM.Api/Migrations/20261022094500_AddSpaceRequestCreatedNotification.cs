using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>Plantilla interna sin nombres ni información personal de solicitantes.</summary>
[DbContext(typeof(NotificationDbContext))]
[Migration("20261022094500_AddSpaceRequestCreatedNotification")]
public sealed class AddSpaceRequestCreatedNotification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO core.notification_templates
                ("Id", "Code", "Version", "Name", "SubjectTemplate", "BodyTemplate",
                 "AllowedVariablesJson", "Sensitivity", "Status", "EffectiveFromUtc", "CreatedAtUtc")
            VALUES ('c0bb87e6-51e1-4ae8-938a-3e5044e9c531'::uuid,
                    'grand_secretariat.space_request.created', 1,
                    'Nueva solicitud de templo o sala para Gran Secretaría',
                    'Nueva solicitud de espacio del Taller',
                    'Gran Secretaría tiene una nueva solicitud de templo o sala. Revise la bandeja institucional.',
                    '[]'::jsonb, 'internal', 'active',
                    '2026-10-22T00:00:00+00'::timestamptz,
                    '2026-10-22T00:00:00+00'::timestamptz)
            ON CONFLICT ("Code", "Version") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM core.notification_templates
            WHERE "Id" = 'c0bb87e6-51e1-4ae8-938a-3e5044e9c531'::uuid;
            """);
    }
}
