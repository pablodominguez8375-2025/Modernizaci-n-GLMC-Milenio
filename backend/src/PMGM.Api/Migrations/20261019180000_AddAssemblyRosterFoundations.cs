using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// Solo tablas nuevas de Régimen Interior. Ninguna migración destructiva ni
/// modificación de tablas de Tesorería, Membresía o Tribunal de Honor.
/// </summary>
[DbContext(typeof(RegimenInteriorDbContext))]
[Migration("20261019180000_AddAssemblyRosterFoundations")]
public sealed class AddAssemblyRosterFoundations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assemblies", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                Name = t.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                MeetingDate = t.Column<DateOnly>(type: "date", nullable: false),
                CutoffDate = t.Column<DateOnly>(type: "date", nullable: false),
                Status = t.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                CreatedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ClosedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ClosedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ClosureEvidenceDigest = t.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_assemblies", x => x.Id);
                t.CheckConstraint("CK_assembly_status", "\"Status\" IN ('draft','closed')");
                t.CheckConstraint("CK_assembly_cutoff", "\"CutoffDate\" <= \"MeetingDate\"");
                t.CheckConstraint("CK_assembly_close_provenance",
                    "(\"Status\" = 'draft' AND \"ClosedAtUtc\" IS NULL AND \"ClosedBySubject\" IS NULL AND \"ClosureEvidenceDigest\" IS NULL) OR " +
                    "(\"Status\" = 'closed' AND \"ClosedAtUtc\" IS NOT NULL AND \"ClosedBySubject\" IS NOT NULL AND \"ClosureEvidenceDigest\" IS NOT NULL)");
            });

        migrationBuilder.CreateTable(
            name: "assembly_member_statuses", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                AssemblyId = t.Column<Guid>(type: "uuid", nullable: false),
                PersonId = t.Column<Guid>(type: "uuid", nullable: false),
                MemberId = t.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = t.Column<Guid>(type: "uuid", nullable: false),
                Category = t.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                SourceOfficeAssignmentId = t.Column<Guid>(type: "uuid", nullable: false),
                SourceEvidenceReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                FullTermVerified = t.Column<bool>(type: "boolean", nullable: false),
                LastNamesAtCutoff = t.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                FirstNamesAtCutoff = t.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                RutAtCutoff = t.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                WorkshopAtCutoff = t.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                VerifiedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_assembly_member_statuses", x => x.Id);
                t.UniqueConstraint("AK_assembly_member_statuses_AssemblyId_Id",
                    x => new { x.AssemblyId, x.Id });
                t.ForeignKey("FK_assembly_member_statuses_assemblies", x => x.AssemblyId,
                    principalSchema: "core", principalTable: "assemblies", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("CK_assembly_member_category",
                    "\"Category\" IN ('representative','permanent_assembly_member')");
            });

        migrationBuilder.CreateTable(
            name: "assembly_eligibilities", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                AssemblyId = t.Column<Guid>(type: "uuid", nullable: false),
                MemberStatusId = t.Column<Guid>(type: "uuid", nullable: false),
                Status = t.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                MayAttend = t.Column<bool>(type: "boolean", nullable: false),
                MayVote = t.Column<bool>(type: "boolean", nullable: false),
                ReasonCode = t.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                GrandTreasuryPendingAmount = t.Column<decimal>(type: "numeric(18,2)", nullable: true),
                GrandTreasuryCurrency = t.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                GrandTreasurySourceReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                GrandTreasuryAsOfDate = t.Column<DateOnly>(type: "date", nullable: true),
                InstitutionalRightsSourceReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                HonorRightsSourceReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                EvaluatedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                EvaluatedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SupersedesEligibilityId = t.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_assembly_eligibilities", x => x.Id);
                t.ForeignKey("FK_assembly_eligibilities_members",
                    x => new { x.AssemblyId, x.MemberStatusId },
                    principalSchema: "core", principalTable: "assembly_member_statuses",
                    principalColumns: new[] { "AssemblyId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("CK_assembly_eligibility_status",
                    "\"Status\" IN ('habilitado','inhabilitado','pendiente','solo_asistencia')");
                t.CheckConstraint("CK_assembly_vote_requires_eligibility",
                    "NOT \"MayVote\" OR (\"MayAttend\" AND \"Status\" = 'habilitado')");
                t.CheckConstraint("CK_assembly_money_nonnegative",
                    "\"GrandTreasuryPendingAmount\" IS NULL OR \"GrandTreasuryPendingAmount\" >= 0");
            });

        migrationBuilder.CreateTable(
            name: "assembly_debt_decision_events", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                AssemblyId = t.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = t.Column<Guid>(type: "uuid", nullable: false),
                Action = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Authority = t.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                DecidedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Reason = t.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                EvidenceReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                OccurredAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReversesEventId = t.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_assembly_debt_decision_events", x => x.Id);
                t.ForeignKey("FK_assembly_debt_events_assemblies", x => x.AssemblyId,
                    principalSchema: "core", principalTable: "assemblies",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("CK_assembly_debt_action", "\"Action\" IN ('lift','revoke')");
                t.CheckConstraint("CK_assembly_debt_authority", "\"Authority\" = 'regimen_interior'");
            });

        migrationBuilder.CreateTable(
            name: "institutional_restrictions", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                PersonId = t.Column<Guid>(type: "uuid", nullable: false),
                SourceAuthority = t.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                RestrictionType = t.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                AffectsAttendance = t.Column<bool>(type: "boolean", nullable: false),
                AffectsSuffrage = t.Column<bool>(type: "boolean", nullable: false),
                AffectsInstitutionalRights = t.Column<bool>(type: "boolean", nullable: false),
                StartsOn = t.Column<DateOnly>(type: "date", nullable: false),
                EndsOn = t.Column<DateOnly>(type: "date", nullable: true),
                EvidenceReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                RecordedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_institutional_restrictions", x => x.Id);
                t.CheckConstraint("CK_institutional_restrictions_dates",
                    "\"EndsOn\" IS NULL OR \"EndsOn\" >= \"StartsOn\"");
            });

        migrationBuilder.CreateIndex("IX_assemblies_Status_CutoffDate", schema: "core",
            table: "assemblies", columns: new[] { "Status", "CutoffDate" });
        migrationBuilder.CreateIndex("IX_assembly_member_statuses_AssemblyId_PersonId",
            schema: "core", table: "assembly_member_statuses",
            columns: new[] { "AssemblyId", "PersonId" }, unique: true);
        migrationBuilder.CreateIndex("IX_assembly_member_statuses_AssemblyId_OrganizationId",
            schema: "core", table: "assembly_member_statuses",
            columns: new[] { "AssemblyId", "OrganizationId" });
        migrationBuilder.CreateIndex("IX_assembly_eligibilities_AssemblyId_MemberStatusId_EvaluatedAtUtc",
            schema: "core", table: "assembly_eligibilities",
            columns: new[] { "AssemblyId", "MemberStatusId", "EvaluatedAtUtc" });
        migrationBuilder.CreateIndex("IX_assembly_eligibilities_SupersedesEligibilityId",
            schema: "core", table: "assembly_eligibilities",
            column: "SupersedesEligibilityId", unique: true,
            filter: "\"SupersedesEligibilityId\" IS NOT NULL");
        migrationBuilder.CreateIndex("IX_assembly_debt_decision_events_AssemblyId_OrganizationId_OccurredAtUtc",
            schema: "core", table: "assembly_debt_decision_events",
            columns: new[] { "AssemblyId", "OrganizationId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex("IX_assembly_debt_decision_events_ReversesEventId",
            schema: "core", table: "assembly_debt_decision_events",
            column: "ReversesEventId", unique: true,
            filter: "\"ReversesEventId\" IS NOT NULL");
        migrationBuilder.CreateIndex("IX_institutional_restrictions_PersonId_StartsOn_EndsOn",
            schema: "core", table: "institutional_restrictions",
            columns: new[] { "PersonId", "StartsOn", "EndsOn" });

        // La etapa B no tiene todavía el cierre transaccional de padrón.
        // Una clave privilegiada tampoco debe poder crear un falso cierre.
        migrationBuilder.Sql("""
            CREATE FUNCTION core.reject_unverified_assembly_closure()
            RETURNS trigger LANGUAGE plpgsql AS $assembly_close_guard$
            BEGIN
                IF NEW."Status" <> 'draft' THEN
                    RAISE EXCEPTION 'Assembly closure requires audited service'
                        USING ERRCODE = '55000';
                END IF;
                RETURN NEW;
            END $assembly_close_guard$;
            CREATE TRIGGER trg_assembly_draft_only
            BEFORE INSERT OR UPDATE ON core.assemblies
            FOR EACH ROW
            EXECUTE FUNCTION core.reject_unverified_assembly_closure();
            """);

        // Una decisión del órgano no puede modificarse retroactivamente por
        // SQL ni por futuras rutas administrativas.
        migrationBuilder.Sql("""
            CREATE FUNCTION core.reject_assembly_debt_event_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $assembly_debt_guard$
            BEGIN
                RAISE EXCEPTION 'assembly_debt_decision_events is append-only'
                    USING ERRCODE = '55000';
            END $assembly_debt_guard$;
            CREATE TRIGGER trg_assembly_debt_event_immutable
            BEFORE UPDATE OR DELETE OR TRUNCATE ON core.assembly_debt_decision_events
            FOR EACH STATEMENT
            EXECUTE FUNCTION core.reject_assembly_debt_event_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_assembly_draft_only ON core.assemblies;
            DROP FUNCTION IF EXISTS core.reject_unverified_assembly_closure();
            """);
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_assembly_debt_event_immutable
                ON core.assembly_debt_decision_events;
            DROP FUNCTION IF EXISTS core.reject_assembly_debt_event_mutation();
            """);
        migrationBuilder.DropTable("assembly_eligibilities", schema: "core");
        migrationBuilder.DropTable("assembly_debt_decision_events", schema: "core");
        migrationBuilder.DropTable("institutional_restrictions", schema: "core");
        migrationBuilder.DropTable("assembly_member_statuses", schema: "core");
        migrationBuilder.DropTable("assemblies", schema: "core");
    }
}
