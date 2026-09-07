using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260907122500_InitialCore")]
public partial class InitialCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "core");

        migrationBuilder.CreateTable(
            name: "organizations",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                ParentOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_organizations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "people",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FirstNames = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                LastNames = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_people", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "organizations", schema: "core");
        migrationBuilder.DropTable(name: "people", schema: "core");
    }
}
