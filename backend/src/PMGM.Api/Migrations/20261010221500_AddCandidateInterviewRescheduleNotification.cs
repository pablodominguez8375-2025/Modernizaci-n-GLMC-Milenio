using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable
namespace PMGM.Api.Migrations;

[DbContext(typeof(NotificationDbContext))]
[Migration("20261010221500_AddCandidateInterviewRescheduleNotification")]
public sealed class AddCandidateInterviewRescheduleNotification : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
            INSERT INTO core.notification_templates
                ("Id", "Code", "Version", "Name", "SubjectTemplate", "BodyTemplate",
                 "AllowedVariablesJson", "Sensitivity", "Status", "EffectiveFromUtc", "CreatedAtUtc")
            VALUES ('7712a3f2-73fd-4af9-8f06-10519a1f2743'::uuid,
                    'candidate.interview.rescheduled', 1, 'Reprogramación privada de entrevista',
                    'Fecha de entrevista reprogramada',
                    'La entrevista que le fue asignada se reprogramó para {{date}}. Consulte su tarea privada en Centenario.',
                    '["date"]'::jsonb, 'restricted', 'active',
                    '2026-10-10T00:00:00+00'::timestamptz,
                    '2026-10-10T00:00:00+00'::timestamptz)
            ON CONFLICT ("Code", "Version") DO NOTHING;
            """);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
            DELETE FROM core.notification_templates
            WHERE "Id" = '7712a3f2-73fd-4af9-8f06-10519a1f2743'::uuid;
            """);
    }
}
