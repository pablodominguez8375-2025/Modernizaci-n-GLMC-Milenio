using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260917203000_AddLodgeAdministrationCouncil")]
public partial class AddLodgeAdministrationCouncil : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lodge_council_sessions",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                QualifiedQuorumConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                QuorumConfirmedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                QuorumConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_lodge_council_sessions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "lodge_council_attendance_records",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                DisplayName = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                InstitutionalRole = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                ParticipationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                HasVoice = table.Column<bool>(type: "boolean", nullable: false),
                HasVote = table.Column<bool>(type: "boolean", nullable: false),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_council_attendance_records", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_council_attendance_records_lodge_council_sessions_SessionId",
                    column: x => x.SessionId,
                    principalSchema: "core",
                    principalTable: "lodge_council_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "lodge_council_decisions",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Resolution = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                RequiresChamberReview = table.Column<bool>(type: "boolean", nullable: false),
                ChamberReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SupportingDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_council_decisions", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_council_decisions_lodge_council_sessions_SessionId",
                    column: x => x.SessionId,
                    principalSchema: "core",
                    principalTable: "lodge_council_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "lodge_council_financial_reviews",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                ControlArea = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                PeriodLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Conclusion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Observations = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                SupportingDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_council_financial_reviews", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_council_financial_reviews_lodge_council_sessions_SessionId",
                    column: x => x.SessionId,
                    principalSchema: "core",
                    principalTable: "lodge_council_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_council_sessions_OrganizationId_SessionDate",
            schema: "core",
            table: "lodge_council_sessions",
            columns: new[] { "OrganizationId", "SessionDate" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_council_sessions_OrganizationId_Status",
            schema: "core",
            table: "lodge_council_sessions",
            columns: new[] { "OrganizationId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_council_attendance_records_SessionId_MemberId_RecordedAtUtc",
            schema: "core",
            table: "lodge_council_attendance_records",
            columns: new[] { "SessionId", "MemberId", "RecordedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_council_decisions_SessionId_RecordedAtUtc",
            schema: "core",
            table: "lodge_council_decisions",
            columns: new[] { "SessionId", "RecordedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_council_decisions_SessionId_Category",
            schema: "core",
            table: "lodge_council_decisions",
            columns: new[] { "SessionId", "Category" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_council_financial_reviews_SessionId_ControlArea",
            schema: "core",
            table: "lodge_council_financial_reviews",
            columns: new[] { "SessionId", "ControlArea" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "lodge_council_attendance_records", schema: "core");
        migrationBuilder.DropTable(name: "lodge_council_decisions", schema: "core");
        migrationBuilder.DropTable(name: "lodge_council_financial_reviews", schema: "core");
        migrationBuilder.DropTable(name: "lodge_council_sessions", schema: "core");
    }
}
