using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>#609 — foto de perfil del Hermano (normalizada, visible solo si él lo decide).</summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261023200000_AddMemberPhotos")]
public sealed class AddMemberPhotos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "member_photos",
            schema: "core",
            columns: table => new
            {
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Content = table.Column<byte[]>(type: "bytea", nullable: false),
                Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Width = table.Column<int>(type: "integer", nullable: false),
                Height = table.Column<int>(type: "integer", nullable: false),
                VisibleToMembers = table.Column<bool>(type: "boolean", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_member_photos", x => x.MemberId);
                table.CheckConstraint("CK_member_photos_content_type", "\"ContentType\" IN ('image/jpeg','image/png')");
                table.ForeignKey(name: "FK_member_photos_members_MemberId", column: x => x.MemberId, principalSchema: "core", principalTable: "members", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "member_photos", schema: "core");
}
