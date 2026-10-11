using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable
namespace PMGM.Api.Migrations;

[DbContext(typeof(NotificationDbContext))]
[Migration("20261010153100_AddCandidateInterviewAssignmentNotification")]
public sealed class AddCandidateInterviewAssignmentNotification : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
            INSERT INTO core.notification_templates
                ("Id", "Code", "Version", "Name", "SubjectTemplate", "BodyTemplate",
                 "AllowedVariablesJson", "Sensitivity", "Status", "EffectiveFromUtc", "CreatedAtUtc")
            VALUES ('7712a3f2-73fd-4af9-8f06-10519a1f2742'::uuid,
                    'candidate.interview.assigned', 1, 'Designación de entrevista privada',
                    'Nueva entrevista asignada',
                    'Ha sido designado por el Venerable, según acuerdo institucional, para una entrevista de candidato de {{workshop}}. Revise su tarea privada en Centenario.',
                    '["workshop"]'::jsonb, 'restricted', 'active',
                    '2026-10-10T00:00:00+00'::timestamptz,
                    '2026-10-10T00:00:00+00'::timestamptz)
            ON CONFLICT ("Code", "Version") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""DELETE FROM core.notification_templates WHERE "Id" = '7712a3f2-73fd-4af9-8f06-10519a1f2742'::uuid;""");
    }
}
