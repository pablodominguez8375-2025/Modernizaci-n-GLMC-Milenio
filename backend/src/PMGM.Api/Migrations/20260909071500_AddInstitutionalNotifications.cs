using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260909071500_AddInstitutionalNotifications")]
public partial class AddInstitutionalNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "notification_templates",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                SubjectTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                BodyTemplate = table.Column<string>(type: "text", nullable: false),
                AllowedVariablesJson = table.Column<string>(type: "jsonb", nullable: false),
                Sensitivity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                EffectiveFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_notification_templates", x => x.Id));

        migrationBuilder.CreateTable(
            name: "notification_messages",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                NotificationTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                TypeCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                RecipientSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Body = table.Column<string>(type: "text", nullable: false),
                ActionUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Classification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Mandatory = table.Column<bool>(type: "boolean", nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                SourceEventId = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                CorrelationId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_messages", x => x.Id);
                table.ForeignKey(
                    name: "FK_notification_messages_notification_templates_NotificationTemplateId",
                    column: x => x.NotificationTemplateId,
                    principalSchema: "core",
                    principalTable: "notification_templates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "notification_deliveries",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                NotificationMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Provider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                ScheduledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                LastErrorCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_deliveries", x => x.Id);
                table.ForeignKey(
                    name: "FK_notification_deliveries_notification_messages_NotificationMessageId",
                    column: x => x.NotificationMessageId,
                    principalSchema: "core",
                    principalTable: "notification_messages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "notification_delivery_attempts",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                NotificationDeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Provider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                ErrorCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                AttemptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_delivery_attempts", x => x.Id);
                table.ForeignKey(
                    name: "FK_notification_delivery_attempts_notification_deliveries_NotificationDeliveryId",
                    column: x => x.NotificationDeliveryId,
                    principalSchema: "core",
                    principalTable: "notification_deliveries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_notification_templates_Code_Version",
            schema: "core",
            table: "notification_templates",
            columns: new[] { "Code", "Version" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_notification_messages_IdempotencyKey",
            schema: "core",
            table: "notification_messages",
            column: "IdempotencyKey",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_notification_messages_RecipientSubject_CreatedAtUtc",
            schema: "core",
            table: "notification_messages",
            columns: new[] { "RecipientSubject", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_notification_messages_NotificationTemplateId",
            schema: "core",
            table: "notification_messages",
            column: "NotificationTemplateId");
        migrationBuilder.CreateIndex(
            name: "IX_notification_deliveries_Status_ScheduledAtUtc",
            schema: "core",
            table: "notification_deliveries",
            columns: new[] { "Status", "ScheduledAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_notification_deliveries_NotificationMessageId_Channel",
            schema: "core",
            table: "notification_deliveries",
            columns: new[] { "NotificationMessageId", "Channel" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_notification_delivery_attempts_NotificationDeliveryId_AttemptNumber",
            schema: "core",
            table: "notification_delivery_attempts",
            columns: new[] { "NotificationDeliveryId", "AttemptNumber" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "notification_delivery_attempts", schema: "core");
        migrationBuilder.DropTable(name: "notification_deliveries", schema: "core");
        migrationBuilder.DropTable(name: "notification_messages", schema: "core");
        migrationBuilder.DropTable(name: "notification_templates", schema: "core");
    }
}
