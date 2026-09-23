using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260923140000_AddTreasuryTerritoryToOrganizations")]
public partial class AddTreasuryTerritoryToOrganizations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TreasuryTerritory",
            schema: "core",
            table: "organizations",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "TreasuryTerritory", schema: "core", table: "organizations");
    }
}
