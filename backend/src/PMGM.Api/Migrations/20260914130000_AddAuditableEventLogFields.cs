using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PMGM.Api.Migrations;

public partial class AddAuditableEventLogFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name:"IpAddress", schema:"core", table:"audit_events", type:"character varying(64)", maxLength:64, nullable:true);
        migrationBuilder.AddColumn<string>(name:"Menu", schema:"core", table:"audit_events", type:"character varying(100)", maxLength:100, nullable:true);
        migrationBuilder.AddColumn<string>(name:"Submenu", schema:"core", table:"audit_events", type:"character varying(100)", maxLength:100, nullable:true);
        migrationBuilder.AddColumn<string>(name:"Summary", schema:"core", table:"audit_events", type:"character varying(300)", maxLength:300, nullable:true);
        migrationBuilder.CreateIndex(name:"IX_audit_events_Menu_OccurredAtUtc", schema:"core", table:"audit_events", columns:new[] { "Menu", "OccurredAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_audit_events_Menu_OccurredAtUtc", "core", "audit_events");
        migrationBuilder.DropColumn("IpAddress", "core", "audit_events");
        migrationBuilder.DropColumn("Menu", "core", "audit_events");
        migrationBuilder.DropColumn("Submenu", "core", "audit_events");
        migrationBuilder.DropColumn("Summary", "core", "audit_events");
    }
}
