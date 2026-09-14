using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260908101500_AddLodgeManagement")]
public partial class AddLodgeManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lodge_meetings",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                MeetingDate = table.Column<DateOnly>(type: "date", nullable: false),
                MeetingType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Grade = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_meetings", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_meetings_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "lodge_attendance_records",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ExcuseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_attendance_records", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_attendance_records_lodge_meetings_MeetingId",
                    column: x => x.MeetingId,
                    principalSchema: "core",
                    principalTable: "lodge_meetings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_lodge_attendance_records_members_MemberId",
                    column: x => x.MemberId,
                    principalSchema: "core",
                    principalTable: "members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "lodge_minutes",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                Content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ApprovedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_minutes", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_minutes_lodge_meetings_MeetingId",
                    column: x => x.MeetingId,
                    principalSchema: "core",
                    principalTable: "lodge_meetings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_meetings_OrganizationId_MeetingDate",
            schema: "core",
            table: "lodge_meetings",
            columns: new[] { "OrganizationId", "MeetingDate" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_meetings_OrganizationId_Status",
            schema: "core",
            table: "lodge_meetings",
            columns: new[] { "OrganizationId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_attendance_records_MeetingId_MemberId_RecordedAtUtc",
            schema: "core",
            table: "lodge_attendance_records",
            columns: new[] { "MeetingId", "MemberId", "RecordedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_attendance_records_MemberId",
            schema: "core",
            table: "lodge_attendance_records",
            column: "MemberId");

        migrationBuilder.CreateIndex(
            name: "IX_lodge_minutes_MeetingId_Version",
            schema: "core",
            table: "lodge_minutes",
            columns: new[] { "MeetingId", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_lodge_minutes_MeetingId_Status",
            schema: "core",
            table: "lodge_minutes",
            columns: new[] { "MeetingId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "lodge_minutes", schema: "core");
        migrationBuilder.DropTable(name: "lodge_attendance_records", schema: "core");
        migrationBuilder.DropTable(name: "lodge_meetings", schema: "core");
    }
}
