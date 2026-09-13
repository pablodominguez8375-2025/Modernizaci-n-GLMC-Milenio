using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMGM.Api.Migrations;

public partial class AddLodgeHospitalariaMovements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "lodge_hospitalaria_movements", schema: "core",
            columns: table => new { Id = table.Column<Guid>(type: "uuid", nullable: false), OrganizationId = table.Column<Guid>(type: "uuid", nullable: false), MovementType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false), Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false), Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false), MovementDate = table.Column<DateOnly>(type: "date", nullable: false), MemberReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true), Destination = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true), EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), Observation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true), RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false), RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false) },
            constraints: table => { table.PrimaryKey("PK_lodge_hospitalaria_movements", x => x.Id); table.ForeignKey(name: "FK_lodge_hospitalaria_movements_organizations_OrganizationId", column: x => x.OrganizationId, principalSchema: "core", principalTable: "organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict); });
        migrationBuilder.CreateIndex(name: "IX_lodge_hospitalaria_movements_OrganizationId_MovementDate", schema: "core", table: "lodge_hospitalaria_movements", columns: new[] { "OrganizationId", "MovementDate" });
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "lodge_hospitalaria_movements", schema: "core");
}
