using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260913203000_AddMemberWithdrawalRequests")]
public partial class AddMemberWithdrawalRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "member_withdrawal_requests",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                OriginOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                WithdrawalType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                RequestedEffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                RequestedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                DecidedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_member_withdrawal_requests", x => x.Id);
                table.ForeignKey(
                    name: "FK_member_withdrawal_requests_members_MemberId",
                    column: x => x.MemberId,
                    principalSchema: "core",
                    principalTable: "members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_member_withdrawal_requests_organizations_OriginOrganizationId",
                    column: x => x.OriginOrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(
            name: "IX_member_withdrawal_requests_MemberId_Status",
            schema: "core",
            table: "member_withdrawal_requests",
            columns: new[] { "MemberId", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_member_withdrawal_requests_OriginOrganizationId_CreatedAtUtc",
            schema: "core",
            table: "member_withdrawal_requests",
            columns: new[] { "OriginOrganizationId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "member_withdrawal_requests", schema: "core");
}
