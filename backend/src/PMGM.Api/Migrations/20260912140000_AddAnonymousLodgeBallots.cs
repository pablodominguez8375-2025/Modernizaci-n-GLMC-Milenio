using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(LodgeManagementDbContext))]
[Migration("20260912140000_AddAnonymousLodgeBallots")]
public partial class AddAnonymousLodgeBallots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lodge_anonymous_ballots", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                BallotType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                AttendeeCount = table.Column<int>(type: "integer", nullable: false),
                EligibleCount = table.Column<int>(type: "integer", nullable: false),
                PositiveCount = table.Column<int>(type: "integer", nullable: false),
                NegativeCount = table.Column<int>(type: "integer", nullable: false),
                RecountObservation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_anonymous_ballots", x => x.Id);
                table.ForeignKey(name: "FK_lodge_anonymous_ballots_lodge_meetings_MeetingId", column: x => x.MeetingId,
                    principalSchema: "core", principalTable: "lodge_meetings", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_lodge_anonymous_ballots_MeetingId_Status", schema: "core", table: "lodge_anonymous_ballots", columns: new[] { "MeetingId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_lodge_anonymous_ballots_MeetingId_Subject_Version", schema: "core", table: "lodge_anonymous_ballots", columns: new[] { "MeetingId", "Subject", "Version" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable("lodge_anonymous_ballots", "core");
}
