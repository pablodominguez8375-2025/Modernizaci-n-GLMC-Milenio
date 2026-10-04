using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable
namespace PMGM.Api.Migrations;
[DbContext(typeof(PmgmDbContext))]
[Migration("20261004031137_AddDynamicAccessSnapshots")]
public partial class AddDynamicAccessSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "dynamic_access_snapshots", schema: "core", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Version = table.Column<int>(type: "integer", nullable: false),
            Payload = table.Column<string>(type: "jsonb", nullable: false),
            RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_dynamic_access_snapshots", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_dynamic_access_snapshots_Version", schema: "core", table: "dynamic_access_snapshots", column: "Version", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "dynamic_access_snapshots", schema: "core");
}
