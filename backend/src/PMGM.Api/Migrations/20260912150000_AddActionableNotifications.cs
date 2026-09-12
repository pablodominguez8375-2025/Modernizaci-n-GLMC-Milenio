using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMGM.Api.Migrations;

public partial class AddActionableNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "ActionRequired", table: "notification_messages", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "ActionStatus", table: "notification_messages", type: "character varying(32)", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<string>(name: "RelatedResourceType", table: "notification_messages", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(name: "RelatedResourceId", table: "notification_messages", type: "character varying(120)", maxLength: 120, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ActionRequired", table: "notification_messages");
        migrationBuilder.DropColumn(name: "ActionStatus", table: "notification_messages");
        migrationBuilder.DropColumn(name: "RelatedResourceType", table: "notification_messages");
        migrationBuilder.DropColumn(name: "RelatedResourceId", table: "notification_messages");
    }
}
