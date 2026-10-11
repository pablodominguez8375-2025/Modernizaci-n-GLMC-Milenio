using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable
namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20261011023000_AddAdvancementDispensationCases")]
public sealed class AddAdvancementDispensationCases : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "advancement_dispensation_cases", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                CeremonyRequestId = t.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = t.Column<Guid>(type: "uuid", nullable: false),
                MemberId = t.Column<Guid>(type: "uuid", nullable: false),
                InstitutionalRuleId = t.Column<Guid>(type: "uuid", nullable: false),
                AdvancementRuleId = t.Column<Guid>(type: "uuid", nullable: false),
                SeniorityRuleId = t.Column<Guid>(type: "uuid", nullable: false),
                Foundation = t.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                MeetingId = t.Column<Guid>(type: "uuid", nullable: false),
                FullMinuteId = t.Column<Guid>(type: "uuid", nullable: false),
                BallotId = t.Column<Guid>(type: "uuid", nullable: false),
                EligibleMasterIdsJson = t.Column<string>(type: "jsonb", nullable: false),
                ReductionsJson = t.Column<string>(type: "jsonb", nullable: false),
                EligibleCount = t.Column<int>(type: "integer", nullable: false),
                PositiveCount = t.Column<int>(type: "integer", nullable: false),
                NegativeCount = t.Column<int>(type: "integer", nullable: false),
                Status = t.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                SubmittedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                SubmittedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ResolvedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ResolvedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ResolutionReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ResolutionNotes = t.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            }, constraints: t =>
            {
                t.PrimaryKey("PK_advancement_dispensation_cases", x => x.Id);
                t.ForeignKey("FK_advancement_dispensation_cases_ceremony_requests_CeremonyRequestId",
                    x => x.CeremonyRequestId, principalSchema: "core",
                    principalTable: "ceremony_requests", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });
        m.CreateIndex(name: "IX_advancement_dispensation_cases_CeremonyRequestId",
            schema: "core", table: "advancement_dispensation_cases",
            column: "CeremonyRequestId", unique: true);
        m.CreateIndex(name: "IX_advancement_dispensation_cases_OrganizationId_Status",
            schema: "core", table: "advancement_dispensation_cases",
            columns: new[] { "OrganizationId", "Status" });
        m.Sql("""
            INSERT INTO core.institutional_rule_settings
                ("Id", "Code", "Value", "EffectiveFrom", "EffectiveTo", "Status", "SourceReference", "CreatedAtUtc")
            VALUES
                ('48000000-0000-0000-0000-000000000034'::uuid,
                 'ceremony.advancement.dispensation',
                 '{"decision":"unanimous","meetingGrade":"master","maximumReductionPercent":50}',
                 DATE '2026-01-01', NULL, 'active',
                 'Reglamento General GLMCH, artículo 3.4',
                 TIMESTAMPTZ '2026-10-11 02:30:00+00')
            ON CONFLICT ("Id") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""DELETE FROM core.institutional_rule_settings
            WHERE "Id" = '48000000-0000-0000-0000-000000000034'::uuid;""");
        m.DropTable(name: "advancement_dispensation_cases", schema: "core");
    }
}
