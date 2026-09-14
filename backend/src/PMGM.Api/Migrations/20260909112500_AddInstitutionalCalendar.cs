using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(CalendarDbContext))]
[Migration("20260909112500_AddInstitutionalCalendar")]
public partial class AddInstitutionalCalendar : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "core");

        migrationBuilder.CreateTable(
            name: "calendar_events",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TimeZoneId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                LocationDisplay = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SpaceId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                ScopeType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ScopeReference = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                Visibility = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                SourceModule = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                SourceEntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                SourceEntityId = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                SourceControlled = table.Column<bool>(type: "boolean", nullable: false),
                OccupancyOnlyWhenRestricted = table.Column<bool>(type: "boolean", nullable: false),
                ResponsibleSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_calendar_events", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_calendar_events_StartsAtUtc_EndsAtUtc",
            schema: "core",
            table: "calendar_events",
            columns: new[] { "StartsAtUtc", "EndsAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_calendar_events_OrganizationId_StartsAtUtc",
            schema: "core",
            table: "calendar_events",
            columns: new[] { "OrganizationId", "StartsAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_calendar_events_SourceModule_SourceEntityType_SourceEntityId",
            schema: "core",
            table: "calendar_events",
            columns: new[] { "SourceModule", "SourceEntityType", "SourceEntityId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_calendar_events_SpaceId_StartsAtUtc",
            schema: "core",
            table: "calendar_events",
            columns: new[] { "SpaceId", "StartsAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "calendar_events", schema: "core");
    }
}
