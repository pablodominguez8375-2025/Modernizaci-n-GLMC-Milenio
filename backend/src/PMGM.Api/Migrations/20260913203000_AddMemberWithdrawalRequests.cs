using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMGM.Api.Migrations;

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
        migrationBuilder.CreateIndex("IX_member_withdrawal_requests_MemberId_Status", "core", "member_withdrawal_requests", new[] { "MemberId", "Status" });
        migrationBuilder.CreateIndex("IX_member_withdrawal_requests_OriginOrganizationId_CreatedAtUtc", "core", "member_withdrawal_requests", new[] { "OriginOrganizationId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "member_withdrawal_requests", schema: "core");
}
