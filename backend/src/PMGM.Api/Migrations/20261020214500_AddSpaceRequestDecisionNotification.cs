using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>Plantilla sin personas ni motivos reservados. Ver GOV-004 / Issue #531.</summary>
[DbContext(typeof(NotificationDbContext))]
[Migration("20261020214500_AddSpaceRequestDecisionNotification")]
public sealed class AddSpaceRequestDecisionNotification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO core.notification_templates
                ("Id", "Code", "Version", "Name", "SubjectTemplate", "BodyTemplate",
                 "AllowedVariablesJson", "Sensitivity", "Status", "EffectiveFromUtc", "CreatedAtUtc")
            VALUES ('f0ab4672-d3e8-4b1e-8240-f184ec3a205a'::uuid,
                    'grand_secretariat.space_request.resolved', 1,
                    'Resultado de solicitud de espacio del Taller',
                    'Solicitud de espacio: {{status}}',
                    'La Gran Secretaría registró el estado {{status}} para su solicitud de espacio. Revise Secretaría del Taller.',
                    '["status"]'::jsonb, 'internal', 'active',
                    '2026-10-20T00:00:00+00'::timestamptz,
                    '2026-10-20T00:00:00+00'::timestamptz)
            ON CONFLICT ("Code", "Version") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM core.notification_templates
            WHERE "Id" = 'f0ab4672-d3e8-4b1e-8240-f184ec3a205a'::uuid;
            """);
    }
}
