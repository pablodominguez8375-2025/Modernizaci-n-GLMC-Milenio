using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260927120000_AddWorkshopOriginMetadata")]
public partial class AddWorkshopOriginMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(name: "EstablishedOn", schema: "core", table: "organizations", type: "date", nullable: true);
        migrationBuilder.AddColumn<string>(name: "City", schema: "core", table: "organizations", type: "character varying(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Country", schema: "core", table: "organizations", type: "character varying(120)", maxLength: 120, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EstablishedOn", schema: "core", table: "organizations");
        migrationBuilder.DropColumn(name: "City", schema: "core", table: "organizations");
        migrationBuilder.DropColumn(name: "Country", schema: "core", table: "organizations");
    }
}
