using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260926210000_AddLodgeSummaryAccessGrants")]
public partial class AddLodgeSummaryAccessGrants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lodge_summary_access_grants",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                GrantedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                GrantedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                GrantReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                RevokedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_summary_access_grants", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_summary_access_grants_members_MemberId",
                    column: x => x.MemberId,
                    principalSchema: "core",
                    principalTable: "members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_lodge_summary_access_grants_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_summary_access_grants_OrganizationId_MemberId",
            schema: "core",
            table: "lodge_summary_access_grants",
            columns: new[] { "OrganizationId", "MemberId" },
            unique: true,
            filter: "\"RevokedAtUtc\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "lodge_summary_access_grants", schema: "core");
    }
}
