using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// Solo acciones preliminares RI, inmutables y sin habilitación electoral.
/// No altera la barrera global que impide cerrar Asambleas sin certificación.
/// </summary>
[DbContext(typeof(RegimenInteriorDbContext))]
[Migration("20261020090000_AddAssemblyReviewDecisionEvents")]
public sealed class AddAssemblyReviewDecisionEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assembly_review_decision_events", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                AssemblyId = t.Column<Guid>(type: "uuid", nullable: false),
                MemberStatusId = t.Column<Guid>(type: "uuid", nullable: false),
                Action = t.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Reason = t.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                EvidenceReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                DecidedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                OccurredAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_assembly_review_decision_events", x => x.Id);
                t.ForeignKey("FK_assembly_ri_decisions_member_same_assembly",
                    x => new { x.AssemblyId, x.MemberStatusId },
                    principalSchema: "core", principalTable: "assembly_member_statuses",
                    principalColumns: new[] { "AssemblyId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("CK_assembly_ri_review_actions",
                    "\"Action\" IN ('request_correction','refer_validation','confirm_ineligible','attendance_only')");
                t.CheckConstraint("CK_assembly_ri_review_provenance",
                    "length(btrim(\"Reason\")) > 0 AND length(btrim(\"EvidenceReference\")) > 0 AND length(btrim(\"DecidedBySubject\")) > 0");
            });

        migrationBuilder.CreateIndex("IX_assembly_ri_review_member_time",
            schema: "core", table: "assembly_review_decision_events",
            columns: new[] { "AssemblyId", "MemberStatusId", "OccurredAtUtc" });

        migrationBuilder.Sql("""
            CREATE FUNCTION core.guard_assembly_ri_decision_insert()
            RETURNS trigger LANGUAGE plpgsql AS $ri_decision_check$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM core.assemblies a
                    WHERE a."Id" = NEW."AssemblyId"
                      AND a."Status" = 'draft'
                      AND a."ClosedAtUtc" IS NULL
                ) THEN
                    RAISE EXCEPTION 'RI decision requires a draft assembly'
                        USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $ri_decision_check$;
            CREATE TRIGGER trg_assembly_ri_decision_guard
            BEFORE INSERT ON core.assembly_review_decision_events
            FOR EACH ROW EXECUTE FUNCTION core.guard_assembly_ri_decision_insert();
            """);

        migrationBuilder.Sql("""
            CREATE FUNCTION core.reject_assembly_ri_decision_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $ri_decision_immutable$
            BEGIN
                RAISE EXCEPTION 'RI decision events are append-only'
                    USING ERRCODE = '55000';
            END $ri_decision_immutable$;
            CREATE TRIGGER trg_assembly_ri_decisions_append_only
            BEFORE UPDATE OR DELETE OR TRUNCATE ON core.assembly_review_decision_events
            FOR EACH STATEMENT EXECUTE FUNCTION core.reject_assembly_ri_decision_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Únicamente rollback de una migración todavía no desplegada en producción.
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_assembly_ri_decisions_append_only
                ON core.assembly_review_decision_events;
            DROP FUNCTION IF EXISTS core.reject_assembly_ri_decision_mutation();
            DROP TRIGGER IF EXISTS trg_assembly_ri_decision_guard
                ON core.assembly_review_decision_events;
            DROP FUNCTION IF EXISTS core.guard_assembly_ri_decision_insert();
            """);
        migrationBuilder.DropTable("assembly_review_decision_events", schema: "core");
    }
}
