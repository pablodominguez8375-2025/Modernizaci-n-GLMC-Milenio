using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260907163200_AddGrandSecretariat")]
public partial class AddGrandSecretariat : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "institutional_spaces",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                SpaceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Capacity = table.Column<int>(type: "integer", nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_institutional_spaces", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "institutional_space_reservations",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                CeremonyRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_institutional_space_reservations", x => x.Id);
                table.ForeignKey(
                    name: "FK_institutional_space_reservations_institutional_spaces_SpaceId",
                    column: x => x.SpaceId,
                    principalSchema: "core",
                    principalTable: "institutional_spaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_institutional_space_reservations_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_institutional_space_reservations_ceremony_requests_CeremonyRequestId",
                    column: x => x.CeremonyRequestId,
                    principalSchema: "core",
                    principalTable: "ceremony_requests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "secretariat_documents",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                DocumentCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                RelatedCeremonyRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                SpaceReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IssuedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_secretariat_documents", x => x.Id);
                table.ForeignKey(
                    name: "FK_secretariat_documents_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_secretariat_documents_ceremony_requests_RelatedCeremonyRequestId",
                    column: x => x.RelatedCeremonyRequestId,
                    principalSchema: "core",
                    principalTable: "ceremony_requests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_secretariat_documents_institutional_space_reservations_SpaceReservationId",
                    column: x => x.SpaceReservationId,
                    principalSchema: "core",
                    principalTable: "institutional_space_reservations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_institutional_spaces_Code",
            schema: "core",
            table: "institutional_spaces",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_institutional_spaces_SpaceType_Status",
            schema: "core",
            table: "institutional_spaces",
            columns: new[] { "SpaceType", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_institutional_space_reservations_SpaceId_StartsAtUtc_EndsAtUtc",
            schema: "core",
            table: "institutional_space_reservations",
            columns: new[] { "SpaceId", "StartsAtUtc", "EndsAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_institutional_space_reservations_OrganizationId_StartsAtUtc",
            schema: "core",
            table: "institutional_space_reservations",
            columns: new[] { "OrganizationId", "StartsAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_institutional_space_reservations_CeremonyRequestId",
            schema: "core",
            table: "institutional_space_reservations",
            column: "CeremonyRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_secretariat_documents_DocumentCode",
            schema: "core",
            table: "secretariat_documents",
            column: "DocumentCode",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_secretariat_documents_DocumentType_Status_IssuedAtUtc",
            schema: "core",
            table: "secretariat_documents",
            columns: new[] { "DocumentType", "Status", "IssuedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_secretariat_documents_RelatedCeremonyRequestId_DocumentType_Status",
            schema: "core",
            table: "secretariat_documents",
            columns: new[] { "RelatedCeremonyRequestId", "DocumentType", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_secretariat_documents_OrganizationId",
            schema: "core",
            table: "secretariat_documents",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_secretariat_documents_SpaceReservationId",
            schema: "core",
            table: "secretariat_documents",
            column: "SpaceReservationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "secretariat_documents", schema: "core");
        migrationBuilder.DropTable(name: "institutional_space_reservations", schema: "core");
        migrationBuilder.DropTable(name: "institutional_spaces", schema: "core");
    }
}
