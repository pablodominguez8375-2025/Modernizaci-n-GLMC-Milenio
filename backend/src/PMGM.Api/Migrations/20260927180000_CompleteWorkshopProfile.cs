using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260927180000_CompleteWorkshopProfile")]
public partial class CompleteWorkshopProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "LogoObjectKey", schema: "core", table: "organizations", type: "character varying(240)", maxLength: 240, nullable: true);
        migrationBuilder.AddColumn<string>(name: "LogoContentType", schema: "core", table: "organizations", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(name: "LogoSha256", schema: "core", table: "organizations", type: "character varying(64)", maxLength: 64, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LogoObjectKey", schema: "core", table: "organizations");
        migrationBuilder.DropColumn(name: "LogoContentType", schema: "core", table: "organizations");
        migrationBuilder.DropColumn(name: "LogoSha256", schema: "core", table: "organizations");
    }
}
