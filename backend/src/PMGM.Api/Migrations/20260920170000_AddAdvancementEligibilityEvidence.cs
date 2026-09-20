using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260920170000_AddAdvancementEligibilityEvidence")]
public partial class AddAdvancementEligibilityEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "DispensationRequested", schema: "core", table: "ceremony_requests", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "CouncilApprovedDispensation", schema: "core", table: "ceremony_requests", type: "boolean", nullable: true);
        migrationBuilder.AddColumn<string>(name: "CouncilRecordReference", schema: "core", table: "ceremony_requests", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "DispensationRequirement", schema: "core", table: "ceremony_requests", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.Sql("""
            INSERT INTO core.institutional_rule_settings ("Id", "Code", "Value", "EffectiveFrom", "EffectiveTo", "Status", "SourceReference", "CreatedAtUtc") VALUES
            ('26000000-0000-0000-0000-000000000001'::uuid, 'advancement.wage_increase.requirements', '{"minimumMonths":24,"minimumMeetings":30,"minimumInstructions":10,"minimumWorkPapers":2}', DATE '2026-01-01', NULL, 'active', 'Reglamento General y formulario 2026', TIMESTAMPTZ '2026-09-20 17:00:00+00'),
            ('26000000-0000-0000-0000-000000000002'::uuid, 'advancement.exaltation.requirements', '{"minimumMonths":24,"minimumMeetings":10,"minimumInstructions":10,"minimumWorkPapers":2}', DATE '2026-01-01', NULL, 'active', 'Reglamento General y formulario 2026', TIMESTAMPTZ '2026-09-20 17:00:00+00');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM core.institutional_rule_settings WHERE \"Id\" IN ('26000000-0000-0000-0000-000000000001'::uuid, '26000000-0000-0000-0000-000000000002'::uuid);");
        migrationBuilder.DropColumn(name: "DispensationRequested", schema: "core", table: "ceremony_requests");
        migrationBuilder.DropColumn(name: "CouncilApprovedDispensation", schema: "core", table: "ceremony_requests");
        migrationBuilder.DropColumn(name: "CouncilRecordReference", schema: "core", table: "ceremony_requests");
        migrationBuilder.DropColumn(name: "DispensationRequirement", schema: "core", table: "ceremony_requests");
    }
}
