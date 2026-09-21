using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260920153000_AddLodgeWorkPapers")]
public partial class AddLodgeWorkPapers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lodge_work_papers", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false), OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                AuthorMemberId = table.Column<Guid>(type: "uuid", nullable: false), DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false), MeetingId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Topic = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                Degree = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false), PresentedOn = table.Column<DateOnly>(type: "date", nullable: false),
                ShortDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false), CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LibraryRequestedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true), LibraryRequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            }, constraints: table => table.PrimaryKey("PK_lodge_work_papers", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_lodge_work_papers_AuthorMemberId_PresentedOn", schema: "core", table: "lodge_work_papers", columns: new[] { "AuthorMemberId", "PresentedOn" });
        migrationBuilder.CreateIndex(name: "IX_lodge_work_papers_DocumentId", schema: "core", table: "lodge_work_papers", column: "DocumentId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_lodge_work_papers_MeetingId", schema: "core", table: "lodge_work_papers", column: "MeetingId");
        migrationBuilder.CreateIndex(name: "IX_lodge_work_papers_OrganizationId_PresentedOn", schema: "core", table: "lodge_work_papers", columns: new[] { "OrganizationId", "PresentedOn" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "lodge_work_papers", schema: "core");
}
