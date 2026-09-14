using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(NotificationDbContext))]
[Migration("20260912150000_AddActionableNotifications")]
public partial class AddActionableNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "ActionRequired", schema: "core", table: "notification_messages", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "ActionStatus", schema: "core", table: "notification_messages", type: "character varying(32)", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<string>(name: "RelatedResourceType", schema: "core", table: "notification_messages", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(name: "RelatedResourceId", schema: "core", table: "notification_messages", type: "character varying(120)", maxLength: 120, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ActionRequired", schema: "core", table: "notification_messages");
        migrationBuilder.DropColumn(name: "ActionStatus", schema: "core", table: "notification_messages");
        migrationBuilder.DropColumn(name: "RelatedResourceType", schema: "core", table: "notification_messages");
        migrationBuilder.DropColumn(name: "RelatedResourceId", schema: "core", table: "notification_messages");
    }
}
