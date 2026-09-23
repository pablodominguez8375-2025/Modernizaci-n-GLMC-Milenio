using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260923000000_AddHospitalariaDeathReplenishments")]
public partial class AddHospitalariaDeathReplenishments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "hospitalaria_replenishment_rates", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AmountPerActiveMember = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_hospitalaria_replenishment_rates", x => x.Id));

        migrationBuilder.CreateTable(
            name: "hospitalaria_death_replenishment_cases", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DeathStatusEventId = table.Column<Guid>(type: "uuid", nullable: false),
                DeceasedMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                DeathDate = table.Column<DateOnly>(type: "date", nullable: false),
                AmountPerActiveMember = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_hospitalaria_death_replenishment_cases", x => x.Id);
                table.ForeignKey("FK_hospitalaria_death_replenishment_cases_institutional_status_events_DeathStatusEventId", x => x.DeathStatusEventId, "core", "institutional_status_events", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_hospitalaria_death_replenishment_cases_members_DeceasedMemberId", x => x.DeceasedMemberId, "core", "members", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "hospitalaria_death_replenishment_obligations", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                AmountDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_hospitalaria_death_replenishment_obligations", x => x.Id);
                table.ForeignKey("FK_hospitalaria_death_replenishment_obligations_hospitalaria_death_replenishment_cases_CaseId", x => x.CaseId, "core", "hospitalaria_death_replenishment_cases", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_hospitalaria_death_replenishment_obligations_organizations_OrganizationId", x => x.OrganizationId, "core", "organizations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_hospitalaria_death_replenishment_obligations_memberships_MembershipId", x => x.MembershipId, "core", "memberships", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_hospitalaria_death_replenishment_obligations_members_MemberId", x => x.MemberId, "core", "members", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "hospitalaria_death_replenishment_transfers", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                SubmissionNumber = table.Column<int>(type: "integer", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                Reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                ReviewedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_hospitalaria_death_replenishment_transfers", x => x.Id);
                table.ForeignKey("FK_hospitalaria_death_replenishment_transfers_hospitalaria_death_replenishment_cases_CaseId", x => x.CaseId, "core", "hospitalaria_death_replenishment_cases", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_hospitalaria_death_replenishment_transfers_organizations_OrganizationId", x => x.OrganizationId, "core", "organizations", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "hospitalaria_death_replenishment_payments", schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ObligationId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                PaymentMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                ReceiptNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_hospitalaria_death_replenishment_payments", x => x.Id);
                table.ForeignKey("FK_hospitalaria_death_replenishment_payments_hospitalaria_death_replenishment_obligations_ObligationId", x => x.ObligationId, "core", "hospitalaria_death_replenishment_obligations", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_hospitalaria_replenishment_rates_EffectiveFrom", schema: "core", table: "hospitalaria_replenishment_rates", column: "EffectiveFrom", unique: true);
        migrationBuilder.CreateIndex(name: "IX_hospitalaria_death_replenishment_cases_DeathStatusEventId", schema: "core", table: "hospitalaria_death_replenishment_cases", column: "DeathStatusEventId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_hospitalaria_death_replenishment_cases_DeceasedMemberId", schema: "core", table: "hospitalaria_death_replenishment_cases", column: "DeceasedMemberId");
        migrationBuilder.CreateIndex(name: "IX_hospitalaria_death_replenishment_cases_DeathDate", schema: "core", table: "hospitalaria_death_replenishment_cases", column: "DeathDate");
        migrationBuilder.CreateIndex(name: "IX_hospitalaria_death_replenishment_obligations_CaseId_MembershipId", schema: "core", table: "hospitalaria_death_replenishment_obligations", columns: new[] { "CaseId", "MembershipId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_hospitalaria_death_replenishment_obligations_OrganizationId_Status", schema: "core", table: "hospitalaria_death_replenishment_obligations", columns: new[] { "OrganizationId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_hospitalaria_death_replenishment_obligations_MembershipId", schema: "core", table: "hospitalaria_death_replenishment_obligations", column: "MembershipId");
        migrationBuilder.CreateIndex(name: "IX_hospitalaria_death_replenishment_obligations_MemberId", schema: "core", table: "hospitalaria_death_replenishment_obligations", column: "MemberId");
        migrationBuilder.CreateIndex(name: "IX_hospitalaria_death_replenishment_payments_ObligationId", schema: "core", table: "hospitalaria_death_replenishment_payments", column: "ObligationId");
        migrationBuilder.CreateIndex(name: "IX_hospitalaria_death_replenishment_payments_ReceiptNumber", schema: "core", table: "hospitalaria_death_replenishment_payments", column: "ReceiptNumber", unique: true);
        migrationBuilder.CreateIndex(name: "IX_hospitalaria_death_replenishment_transfers_CaseId_OrganizationId_SubmissionNumber", schema: "core", table: "hospitalaria_death_replenishment_transfers", columns: new[] { "CaseId", "OrganizationId", "SubmissionNumber" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_hospitalaria_death_replenishment_transfers_OrganizationId_Status", schema: "core", table: "hospitalaria_death_replenishment_transfers", columns: new[] { "OrganizationId", "Status" });

        migrationBuilder.InsertData(
            table: "hospitalaria_replenishment_rates", schema: "core",
            columns: new[] { "Id", "AmountPerActiveMember", "EffectiveFrom", "EffectiveUntil", "SourceReference", "CreatedBySubject", "CreatedAtUtc" },
            values: new object[] { new Guid("b274fb20-8bba-4c0b-a88d-3b42d042ef01"), 1500m, new DateOnly(2026, 1, 1), null, "Acuerdo de reposición por fallecimiento · tarifa inicial vigente", "system:migration", new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero) });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(table: "hospitalaria_replenishment_rates", schema: "core", keyColumn: "Id", keyValue: new Guid("b274fb20-8bba-4c0b-a88d-3b42d042ef01"));
        migrationBuilder.DropTable("hospitalaria_death_replenishment_payments", "core");
        migrationBuilder.DropTable("hospitalaria_death_replenishment_transfers", "core");
        migrationBuilder.DropTable("hospitalaria_death_replenishment_obligations", "core");
        migrationBuilder.DropTable("hospitalaria_death_replenishment_cases", "core");
        migrationBuilder.DropTable("hospitalaria_replenishment_rates", "core");
    }
}
