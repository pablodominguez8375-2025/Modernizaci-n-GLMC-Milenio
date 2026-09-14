using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260909005000_AddLibraryDegreeSecurity")]
public partial class AddLibraryDegreeSecurity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "MinimumDegreeRequired",
            schema: "core",
            table: "institutional_documents",
            type: "integer",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_institutional_documents_MinimumDegreeRequired",
            schema: "core",
            table: "institutional_documents",
            sql: "\"MinimumDegreeRequired\" IS NULL OR (\"MinimumDegreeRequired\" >= 1 AND \"MinimumDegreeRequired\" <= 99)");

        migrationBuilder.CreateIndex(
            name: "IX_institutional_documents_Status_MinimumDegreeRequired",
            schema: "core",
            table: "institutional_documents",
            columns: new[] { "Status", "MinimumDegreeRequired" });

        migrationBuilder.CreateTable(
            name: "member_identity_links",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                Issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Subject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_member_identity_links", x => x.Id);
                table.ForeignKey(
                    name: "FK_member_identity_links_members_MemberId",
                    column: x => x.MemberId,
                    principalSchema: "core",
                    principalTable: "members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_member_identity_links_MemberId_RevokedAtUtc",
            schema: "core",
            table: "member_identity_links",
            columns: new[] { "MemberId", "RevokedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "UX_member_identity_links_Issuer_Subject_active",
            schema: "core",
            table: "member_identity_links",
            columns: new[] { "Issuer", "Subject" },
            unique: true,
            filter: "\"RevokedAtUtc\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "member_identity_links",
            schema: "core");

        migrationBuilder.DropIndex(
            name: "IX_institutional_documents_Status_MinimumDegreeRequired",
            schema: "core",
            table: "institutional_documents");

        migrationBuilder.DropCheckConstraint(
            name: "CK_institutional_documents_MinimumDegreeRequired",
            schema: "core",
            table: "institutional_documents");

        migrationBuilder.DropColumn(
            name: "MinimumDegreeRequired",
            schema: "core",
            table: "institutional_documents");
    }
}
