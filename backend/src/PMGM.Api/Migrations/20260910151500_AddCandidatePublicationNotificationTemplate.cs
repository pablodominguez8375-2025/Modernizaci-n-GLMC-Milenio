using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(NotificationDbContext))]
[Migration("20260910151500_AddCandidatePublicationNotificationTemplate")]
public partial class AddCandidatePublicationNotificationTemplate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Raw SQL is intentional here: this migration is discovered through the
        // migrations assembly before the model snapshot contains the seeded row.
        // ON CONFLICT keeps local/QA upgrades idempotent.
        migrationBuilder.Sql("""
            INSERT INTO core.notification_templates
                ("Id", "Code", "Version", "Name", "SubjectTemplate", "BodyTemplate",
                 "AllowedVariablesJson", "Sensitivity", "Status", "EffectiveFromUtc", "CreatedAtUtc")
            VALUES
                ('53a81b10-3ba9-4d5e-a8fd-9b28ad31c081'::uuid,
                 'candidate.publication.approved',
                 1,
                 'Nueva insinuación publicada',
                 'Nueva insinuación publicada',
                 'Gran Secretaría aprobó la ficha de {{candidateName}}, presentada por {{workshopName}}. Revise la publicación institucional.',
                 '["candidateName","workshopName"]'::jsonb,
                 'internal',
                 'active',
                 '2026-09-10T00:00:00+00'::timestamptz,
                 '2026-09-10T00:00:00+00'::timestamptz)
            ON CONFLICT ("Code", "Version") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM core.notification_templates
            WHERE "Id" = '53a81b10-3ba9-4d5e-a8fd-9b28ad31c081'::uuid;
            """);
    }
}
