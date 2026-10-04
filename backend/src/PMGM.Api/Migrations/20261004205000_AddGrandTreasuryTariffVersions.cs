using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;
using PMGM.Api.Modules.Treasury;

#nullable disable
namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20261004205000_AddGrandTreasuryTariffVersions")]
public partial class AddGrandTreasuryTariffVersions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "OrienteCode", schema: "core", table: "organizations",
            type: "character varying(40)", maxLength: 40, nullable: true);
        migrationBuilder.CreateTable(name: "grand_treasury_tariff_versions", schema: "core", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Version = table.Column<int>(type: "integer", nullable: false),
            Number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
            DecreeDate = table.Column<DateOnly>(type: "date", nullable: false),
            EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
            EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
            SourceReference = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
            Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Payload = table.Column<string>(type: "jsonb", nullable: false),
            RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_grand_treasury_tariff_versions", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_grand_treasury_tariff_versions_Version", schema: "core",
            table: "grand_treasury_tariff_versions", column: "Version", unique: true);
        migrationBuilder.CreateIndex(name: "IX_grand_treasury_tariff_versions_EffectiveFrom_Version", schema: "core",
            table: "grand_treasury_tariff_versions", columns: new[] { "EffectiveFrom", "Version" });
        migrationBuilder.AddColumn<Guid>(name: "TariffVersionId", schema: "core", table: "lodge_fee_plans", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "TariffVersionId", schema: "core", table: "lodge_member_charges", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "TariffVersionId", schema: "core", table: "ceremony_right_payments", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "RightAmount", schema: "core", table: "ceremony_right_payments", type: "numeric(18,2)", nullable: true);
        foreach (var name in new[] { "lodge_fee_plans", "lodge_member_charges", "ceremony_right_payments" })
        {
            migrationBuilder.CreateIndex(name: $"IX_{name}_TariffVersionId", schema: "core", table: name, column: "TariffVersionId");
            migrationBuilder.AddForeignKey(name: $"FK_{name}_grand_treasury_tariff_versions_TariffVersionId", schema: "core", table: name,
                column: "TariffVersionId", principalSchema: "core", principalTable: "grand_treasury_tariff_versions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }
        var initial = GrandTreasuryTariffSeed.Load();
        migrationBuilder.InsertData(schema: "core", table: "grand_treasury_tariff_versions",
            columns: new[] { "Id", "Version", "Number", "DecreeDate", "EffectiveFrom", "EffectiveUntil", "SourceReference", "Status", "Payload", "RecordedAtUtc" },
            columnTypes: new[] { "uuid", "integer", "character varying(80)", "date", "date", "date", "character varying(1200)", "character varying(20)", "jsonb", "timestamp with time zone" },
            values: new object[] { initial.Id, initial.Version, initial.Number, initial.DecreeDate, initial.EffectiveFrom,
                initial.EffectiveUntil, initial.SourceReference, initial.Status,
                JsonSerializer.Serialize(new TariffValues(initial.Rates, initial.CeremonyRights, initial.Unemployment), GrandTreasuryTariff.Json),
                new DateTimeOffset(2026, 10, 4, 20, 50, 0, TimeSpan.Zero) });
        // Known legacy classification supplies missing country/Santiago city; unknown cities are not invented.
        migrationBuilder.Sql("""
            UPDATE core.organizations SET "Country" = 'Chile'
            WHERE "Country" IS NULL AND "TreasuryTerritory" IN ('santiago', 'other_oriente');
            UPDATE core.organizations SET "Country" = 'Perú'
            WHERE "Country" IS NULL AND "TreasuryTerritory" = 'peru';
            UPDATE core.organizations SET "City" = 'Santiago'
            WHERE "City" IS NULL AND "TreasuryTerritory" = 'santiago' AND lower(trim("Country")) = 'chile';
            UPDATE core.organizations SET "OrienteCode" = CASE
                WHEN lower(trim("Country")) = 'chile' AND lower(trim("City")) = 'santiago' THEN 'santiago'
                WHEN lower(trim("Country")) = 'chile' AND nullif(trim("City"), '') IS NOT NULL THEN 'other_chile'
                WHEN lower(trim("Country")) IN ('peru', 'perú') AND nullif(trim("City"), '') IS NOT NULL THEN 'peru'
                ELSE NULL END;
            UPDATE core.organizations SET "TreasuryTerritory" = CASE "OrienteCode"
                WHEN 'santiago' THEN 'santiago' WHEN 'other_chile' THEN 'other_oriente' WHEN 'peru' THEN 'peru' ELSE NULL END;
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var name in new[] { "lodge_fee_plans", "lodge_member_charges", "ceremony_right_payments" })
        {
            migrationBuilder.DropForeignKey(name: $"FK_{name}_grand_treasury_tariff_versions_TariffVersionId", schema: "core", table: name);
            migrationBuilder.DropIndex(name: $"IX_{name}_TariffVersionId", schema: "core", table: name);
        }
        migrationBuilder.DropColumn(name: "TariffVersionId", schema: "core", table: "ceremony_right_payments");
        migrationBuilder.DropColumn(name: "RightAmount", schema: "core", table: "ceremony_right_payments");
        migrationBuilder.DropColumn(name: "TariffVersionId", schema: "core", table: "lodge_fee_plans");
        migrationBuilder.DropColumn(name: "TariffVersionId", schema: "core", table: "lodge_member_charges");
        migrationBuilder.DropTable(name: "grand_treasury_tariff_versions", schema: "core");
        migrationBuilder.DropColumn(name: "OrienteCode", schema: "core", table: "organizations");
    }
}
