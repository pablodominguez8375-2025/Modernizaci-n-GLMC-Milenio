using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(CalendarDbContext))]
[Migration("20261015230000_AddCalendarEventGrade")]
public partial class AddCalendarEventGrade : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // La columna nullable deja como generales los eventos históricos;
        // la reconciliación posterior incorpora grado desde fuentes canónicas.
        migrationBuilder.AddColumn<string>(
            name: "Grade",
            schema: "core",
            table: "calendar_events",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Grade", schema: "core", table: "calendar_events");
    }
}
