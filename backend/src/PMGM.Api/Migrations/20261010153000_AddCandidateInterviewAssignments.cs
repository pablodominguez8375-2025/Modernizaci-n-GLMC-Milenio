using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable
namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20261010153000_AddCandidateInterviewAssignments")]
public sealed class AddCandidateInterviewAssignments : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "candidate_interview_assignments", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                CeremonyRequestId = t.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = t.Column<Guid>(type: "uuid", nullable: false),
                InterviewerMemberId = t.Column<Guid>(type: "uuid", nullable: false),
                Position = t.Column<int>(type: "integer", nullable: false),
                CouncilBody = t.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CouncilDecisionDate = t.Column<DateOnly>(type: "date", nullable: false),
                CouncilMinuteReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ScheduledDate = t.Column<DateOnly>(type: "date", nullable: true),
                Status = t.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                AssignedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                AssignedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReplacedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ReplacedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReplacementReason = t.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                ReportDocumentVersionId = t.Column<Guid>(type: "uuid", nullable: true),
                CompletedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                NotificationQueuedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            }, constraints: t =>
            {
                t.PrimaryKey("PK_candidate_interview_assignments", x => x.Id);
                t.ForeignKey("FK_candidate_interview_assignments_ceremony_requests_CeremonyRequestId",
                    x => x.CeremonyRequestId, "core", "ceremony_requests", "Id", onDelete: ReferentialAction.Restrict);
                t.ForeignKey("FK_candidate_interview_assignments_members_InterviewerMemberId",
                    x => x.InterviewerMemberId, "core", "members", "Id", onDelete: ReferentialAction.Restrict);
            });
        m.CreateIndex(name: "IX_candidate_interview_assignments_CeremonyRequestId_Status",
            schema: "core", table: "candidate_interview_assignments", columns: new[] { "CeremonyRequestId", "Status" });
        m.CreateIndex(name: "IX_candidate_interview_assignments_InterviewerMemberId_Status",
            schema: "core", table: "candidate_interview_assignments", columns: new[] { "InterviewerMemberId", "Status" });
        m.CreateIndex(name: "IX_candidate_interview_assignments_OrganizationId",
            schema: "core", table: "candidate_interview_assignments", column: "OrganizationId");
        m.Sql("""
            CREATE UNIQUE INDEX "UX_candidate_interview_active_member" ON core.candidate_interview_assignments
                ("CeremonyRequestId", "InterviewerMemberId") WHERE "Status" IN ('assigned','completed');
            """);
        m.Sql("""
            CREATE UNIQUE INDEX "UX_candidate_interview_active_position" ON core.candidate_interview_assignments
                ("CeremonyRequestId", "Position") WHERE "Status" IN ('assigned','completed');
            """);
    }

    protected override void Down(MigrationBuilder m)
        => m.DropTable(name: "candidate_interview_assignments", schema: "core");
}
