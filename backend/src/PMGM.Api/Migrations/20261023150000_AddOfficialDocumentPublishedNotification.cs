using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable
namespace PMGM.Api.Migrations;

[DbContext(typeof(NotificationDbContext))]
[Migration("20261023150000_AddOfficialDocumentPublishedNotification")]
public sealed class AddOfficialDocumentPublishedNotification : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
            INSERT INTO core.notification_templates
                ("Id", "Code", "Version", "Name", "SubjectTemplate", "BodyTemplate",
                 "AllowedVariablesJson", "Sensitivity", "Status", "EffectiveFromUtc", "CreatedAtUtc")
            VALUES ('3f6c1d52-9a41-4c7e-8b2a-5d0e7c914a10'::uuid,
                    'grandarchive.official.published', 1, 'Nuevo documento oficial publicado',
                    'Nuevo documento oficial',
                    '{{type}} publicado en Documentos oficiales: {{title}}.',
                    '["type","title"]'::jsonb, 'internal', 'active',
                    '2026-10-23T00:00:00+00'::timestamptz,
                    '2026-10-23T00:00:00+00'::timestamptz)
            ON CONFLICT ("Code", "Version") DO NOTHING;
            """);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
            DELETE FROM core.notification_templates
            WHERE "Id" = '3f6c1d52-9a41-4c7e-8b2a-5d0e7c914a10'::uuid;
            """);
    }
}
