using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(NotificationDbContext))]
[Migration("20260910151500_AddCandidatePublicationNotificationTemplate")]
public partial class AddCandidatePublicationNotificationTemplate : Migration
{
    private static readonly Guid TemplateId = Guid.Parse("53a81b10-3ba9-4d5e-a8fd-9b28ad31c081");

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            schema: "core",
            table: "notification_templates",
            columns: new[]
            {
                "Id",
                "Code",
                "Version",
                "Name",
                "SubjectTemplate",
                "BodyTemplate",
                "AllowedVariablesJson",
                "Sensitivity",
                "Status",
                "EffectiveFromUtc",
                "CreatedAtUtc"
            },
            values: new object[]
            {
                TemplateId,
                "candidate.publication.approved",
                1,
                "Nueva insinuación publicada",
                "Nueva insinuación publicada",
                "Gran Secretaría aprobó la ficha de {{candidateName}}, presentada por {{workshopName}}. Revise la publicación institucional.",
                "[\"candidateName\",\"workshopName\"]",
                "internal",
                "active",
                new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero)
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "core",
            table: "notification_templates",
            keyColumn: "Id",
            keyValue: TemplateId);
    }
}
