using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260919210000_AddOperationalSecretariat")]
public partial class AddOperationalSecretariat : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "lodge_correspondence", schema: "core", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false), OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
            Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false), Folio = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
            CorrespondenceDate = table.Column<DateOnly>(type: "date", nullable: false), Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            Counterparty = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false), Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
            Reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
            CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false), CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            ClosedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true), ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_lodge_correspondence", x => x.Id));

        migrationBuilder.CreateTable(name: "lodge_secretariat_tasks", schema: "core", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false), OrganizationId = table.Column<Guid>(type: "uuid", nullable: false), Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
            Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true), DueDate = table.Column<DateOnly>(type: "date", nullable: true), Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Responsible = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true), Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
            CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false), CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            CompletedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true), CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_lodge_secretariat_tasks", x => x.Id));

        migrationBuilder.CreateTable(name: "lodge_agenda_items", schema: "core", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false), OrganizationId = table.Column<Guid>(type: "uuid", nullable: false), MeetingId = table.Column<Guid>(type: "uuid", nullable: true), Order = table.Column<int>(type: "integer", nullable: false),
            Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false), Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true), Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
            CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false), CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true), UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_lodge_agenda_items", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_lodge_correspondence_OrganizationId_Folio", schema: "core", table: "lodge_correspondence", columns: new[] { "OrganizationId", "Folio" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_lodge_correspondence_OrganizationId_CorrespondenceDate", schema: "core", table: "lodge_correspondence", columns: new[] { "OrganizationId", "CorrespondenceDate" });
        migrationBuilder.CreateIndex(name: "IX_lodge_secretariat_tasks_OrganizationId_Status_DueDate", schema: "core", table: "lodge_secretariat_tasks", columns: new[] { "OrganizationId", "Status", "DueDate" });
        migrationBuilder.CreateIndex(name: "IX_lodge_agenda_items_OrganizationId_MeetingId_Order", schema: "core", table: "lodge_agenda_items", columns: new[] { "OrganizationId", "MeetingId", "Order" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "lodge_agenda_items", schema: "core");
        migrationBuilder.DropTable(name: "lodge_secretariat_tasks", schema: "core");
        migrationBuilder.DropTable(name: "lodge_correspondence", schema: "core");
    }
}
