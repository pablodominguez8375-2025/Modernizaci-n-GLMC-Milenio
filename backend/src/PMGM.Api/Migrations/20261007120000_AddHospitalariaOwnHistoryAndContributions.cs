using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;
#nullable disable
namespace PMGM.Api.Migrations;
[DbContext(typeof(PmgmDbContext))]
[Migration("20261007120000_AddHospitalariaOwnHistoryAndContributions")]
public sealed class AddHospitalariaOwnHistoryAndContributions : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>(name: "DecreeNumber", schema: "core", table: "hospitalaria_replenishment_rates", type: "character varying(80)", maxLength: 80, nullable: true);
        m.AddColumn<DateOnly>(name: "DecreeDate", schema: "core", table: "hospitalaria_replenishment_rates", type: "date", nullable: true);
        m.AddColumn<Guid>(name: "RateId", schema: "core", table: "hospitalaria_death_replenishment_cases", type: "uuid", nullable: true);
        m.CreateIndex(name: "IX_hospitalaria_death_replenishment_cases_RateId", schema: "core", table: "hospitalaria_death_replenishment_cases", column: "RateId");
        m.AddForeignKey(name: "FK_death_cases_rate", schema: "core", table: "hospitalaria_death_replenishment_cases", column: "RateId", principalSchema: "core", principalTable: "hospitalaria_replenishment_rates", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        // Legado se conserva con RateId/decreto null: nunca inventar un decreto ni alterar montos ya cobrados.
        m.CreateTable(name: "hospitalaria_contribution_rates", schema: "core", columns: t => new {
            Id = t.Column<Guid>(type: "uuid", nullable: false), Amount = t.Column<decimal>(type: "numeric(18,2)", nullable: false),
            EffectiveFrom = t.Column<DateOnly>(type: "date", nullable: false), EffectiveUntil = t.Column<DateOnly>(type: "date", nullable: true),
            CreatedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false), CreatedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: t => { t.PrimaryKey("PK_hospitalaria_contribution_rates", x => x.Id); t.CheckConstraint("CK_contribution_rate_amount", "\"Amount\" > 0 AND \"Amount\" = trunc(\"Amount\")"); });
        m.CreateIndex(name: "IX_hospitalaria_contribution_rates_EffectiveFrom", schema: "core", table: "hospitalaria_contribution_rates", column: "EffectiveFrom", unique: true);
        m.CreateTable(name: "hospitalaria_contribution_obligations", schema: "core", columns: t => new {
            Id = t.Column<Guid>(type: "uuid", nullable: false), OrganizationId = t.Column<Guid>(type: "uuid", nullable: false), RateId = t.Column<Guid>(type: "uuid", nullable: false),
            PeriodYear = t.Column<int>(type: "integer", nullable: false), PeriodMonth = t.Column<int>(type: "integer", nullable: false), AmountDue = t.Column<decimal>(type: "numeric(18,2)", nullable: false),
            Status = t.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false), PaymentDate = t.Column<DateOnly>(type: "date", nullable: true),
            PaymentReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), RecordedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
            ReviewedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true), ReviewedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            ReviewNotes = t.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true), CreatedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: t => {
            t.PrimaryKey("PK_hospitalaria_contribution_obligations", x => x.Id);
            t.ForeignKey(name: "FK_contribution_obligation_organization", column: x => x.OrganizationId, principalSchema: "core", principalTable: "organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey(name: "FK_contribution_obligation_rate", column: x => x.RateId, principalSchema: "core", principalTable: "hospitalaria_contribution_rates", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("CK_contribution_obligation_period", "\"PeriodMonth\" BETWEEN 1 AND 12 AND \"PeriodYear\" BETWEEN 1 AND 9999");
            t.CheckConstraint("CK_contribution_obligation_amount", "\"AmountDue\" > 0 AND \"AmountDue\" = trunc(\"AmountDue\")");
        });
        m.CreateIndex(name: "IX_contribution_obligation_period", schema: "core", table: "hospitalaria_contribution_obligations", columns: new[] { "OrganizationId", "PeriodYear", "PeriodMonth" }, unique: true);
        m.CreateIndex(name: "IX_contribution_obligation_rate", schema: "core", table: "hospitalaria_contribution_obligations", column: "RateId");
        // Monto aprobado por el PO; empieza al instalar, sin deuda histórica previa ni decreto ficticio.
        m.Sql("""
            INSERT INTO core.hospitalaria_contribution_rates ("Id", "Amount", "EffectiveFrom", "CreatedBySubject", "CreatedAtUtc")
            VALUES ('35460000-0000-0000-0000-000000000001', 6000, (CURRENT_TIMESTAMP AT TIME ZONE 'America/Santiago')::date, 'system:po-instructions-v2', CURRENT_TIMESTAMP);
            """);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable(name: "hospitalaria_contribution_obligations", schema: "core"); m.DropTable(name: "hospitalaria_contribution_rates", schema: "core");
        m.DropForeignKey(name: "FK_death_cases_rate", schema: "core", table: "hospitalaria_death_replenishment_cases");
        m.DropIndex(name: "IX_hospitalaria_death_replenishment_cases_RateId", schema: "core", table: "hospitalaria_death_replenishment_cases");
        m.DropColumn(name: "RateId", schema: "core", table: "hospitalaria_death_replenishment_cases");
        m.DropColumn(name: "DecreeNumber", schema: "core", table: "hospitalaria_replenishment_rates"); m.DropColumn(name: "DecreeDate", schema: "core", table: "hospitalaria_replenishment_rates");
    }
}
