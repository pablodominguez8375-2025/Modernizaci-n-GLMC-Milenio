using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20261017130000_AddInstitutionalSpaceRequests")]
public sealed class AddInstitutionalSpaceRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "institutional_space_requests", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                PreferredSpaceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                PreferredSpaceId = table.Column<Guid>(type: "uuid", nullable: true),
                CeremonyRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                Purpose = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ExpectedAttendees = table.Column<int>(type: "integer", nullable: true),
                EquipmentRequest = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                EquipmentResponsible = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                VmApprovalReference = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ResponseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SpaceReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                DecidedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_institutional_space_requests", x => x.Id);
                table.ForeignKey(name: "FK_space_requests_organizations", column: x => x.OrganizationId, principalSchema: "core", principalTable: "organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_space_requests_spaces", column: x => x.PreferredSpaceId, principalSchema: "core", principalTable: "institutional_spaces", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_space_requests_ceremonies", column: x => x.CeremonyRequestId, principalSchema: "core", principalTable: "ceremony_requests", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_space_requests_reservations", column: x => x.SpaceReservationId, principalSchema: "core", principalTable: "institutional_space_reservations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_space_requests_OrganizationId_CreatedAtUtc",
            schema: "core", table: "institutional_space_requests", columns: new[] { "OrganizationId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_space_requests_Status_StartsAtUtc",
            schema: "core", table: "institutional_space_requests", columns: new[] { "Status", "StartsAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_space_requests_PreferredSpaceId",
            schema: "core", table: "institutional_space_requests", column: "PreferredSpaceId");
        migrationBuilder.CreateIndex(name: "IX_space_requests_CeremonyRequestId",
            schema: "core", table: "institutional_space_requests", column: "CeremonyRequestId");
        migrationBuilder.CreateIndex(name: "IX_space_requests_SpaceReservationId",
            schema: "core", table: "institutional_space_requests", column: "SpaceReservationId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "institutional_space_requests", schema: "core");
    }
}
