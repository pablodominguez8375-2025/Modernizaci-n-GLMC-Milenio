using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260907133500_AddTreasuryHospitalariaRegularity")]
public partial class AddTreasuryHospitalariaRegularity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "financial_regularity_snapshots",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                Scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_financial_regularity_snapshots", x => x.Id);
                table.ForeignKey(
                    name: "FK_financial_regularity_snapshots_members_MemberId",
                    column: x => x.MemberId,
                    principalSchema: "core",
                    principalTable: "members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_financial_regularity_snapshots_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "hospitalaria_regularity_snapshots",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hospitalaria_regularity_snapshots", x => x.Id);
                table.ForeignKey(
                    name: "FK_hospitalaria_regularity_snapshots_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_financial_regularity_snapshots_OrganizationId_MemberId_AsOfDate",
            schema: "core",
            table: "financial_regularity_snapshots",
            columns: new[] { "OrganizationId", "MemberId", "AsOfDate" });

        migrationBuilder.CreateIndex(
            name: "IX_financial_regularity_snapshots_MemberId",
            schema: "core",
            table: "financial_regularity_snapshots",
            column: "MemberId");

        migrationBuilder.CreateIndex(
            name: "IX_hospitalaria_regularity_snapshots_OrganizationId_AsOfDate",
            schema: "core",
            table: "hospitalaria_regularity_snapshots",
            columns: new[] { "OrganizationId", "AsOfDate" });

        migrationBuilder.InsertData(
            schema: "core",
            table: "institutional_rule_settings",
            columns: new[]
            {
                "Id", "Code", "Value", "EffectiveFrom", "EffectiveTo", "Status", "SourceReference", "CreatedAtUtc"
            },
            values: new object[]
            {
                Guid.Parse("25000000-0000-0000-0000-000000000020"),
                "initiation.publication.minimum_days",
                "20",
                new DateOnly(2026, 9, 7),
                null!,
                "active",
                "PMGM-REQ-025",
                new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero)
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "core",
            table: "institutional_rule_settings",
            keyColumn: "Id",
            keyValue: Guid.Parse("25000000-0000-0000-0000-000000000020"));

        migrationBuilder.DropTable(name: "financial_regularity_snapshots", schema: "core");
        migrationBuilder.DropTable(name: "hospitalaria_regularity_snapshots", schema: "core");
    }
}
