using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260910123000_AddLodgeSecretariat")]
public partial class AddLodgeSecretariat : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lodge_correspondence_records",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Folio = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CorrespondenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Counterparty = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ExternalReference = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                UpdatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_correspondence_records", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_correspondence_records_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "lodge_secretariat_tasks",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ResponsibleLabel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CompletedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_secretariat_tasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_secretariat_tasks_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "lodge_meeting_agenda_items",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Detail = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                UpdatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_meeting_agenda_items", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_meeting_agenda_items_lodge_meetings_MeetingId",
                    column: x => x.MeetingId,
                    principalSchema: "core",
                    principalTable: "lodge_meetings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_lodge_meeting_agenda_items_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_correspondence_records_OrganizationId_Folio",
            schema: "core",
            table: "lodge_correspondence_records",
            columns: new[] { "OrganizationId", "Folio" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_lodge_correspondence_records_OrganizationId_CorrespondenceDate",
            schema: "core",
            table: "lodge_correspondence_records",
            columns: new[] { "OrganizationId", "CorrespondenceDate" });
        migrationBuilder.CreateIndex(
            name: "IX_lodge_correspondence_records_OrganizationId_Status",
            schema: "core",
            table: "lodge_correspondence_records",
            columns: new[] { "OrganizationId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_secretariat_tasks_OrganizationId_Status_DueDate",
            schema: "core",
            table: "lodge_secretariat_tasks",
            columns: new[] { "OrganizationId", "Status", "DueDate" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_meeting_agenda_items_MeetingId_Position",
            schema: "core",
            table: "lodge_meeting_agenda_items",
            columns: new[] { "MeetingId", "Position" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_lodge_meeting_agenda_items_OrganizationId_Status",
            schema: "core",
            table: "lodge_meeting_agenda_items",
            columns: new[] { "OrganizationId", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_lodge_meeting_agenda_items_OrganizationId",
            schema: "core",
            table: "lodge_meeting_agenda_items",
            column: "OrganizationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "lodge_meeting_agenda_items", schema: "core");
        migrationBuilder.DropTable(name: "lodge_secretariat_tasks", schema: "core");
        migrationBuilder.DropTable(name: "lodge_correspondence_records", schema: "core");
    }
}
