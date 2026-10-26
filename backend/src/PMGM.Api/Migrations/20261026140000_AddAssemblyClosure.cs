using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #533 entrega 3 — saldos informados por Gran Tesorería (tabla nueva, solo-anexar) y cierre auditado del padrón.
/// Reemplaza la guardia «solo borrador» por una que admite únicamente la transición draft→closed hecha por el
/// servicio de cierre (bandera de transacción + resumen SHA-256 + actor + fecha) y congela la Asamblea cerrada.
/// </summary>
[DbContext(typeof(RegimenInteriorDbContext))]
[Migration("20261026140000_AddAssemblyClosure")]
public sealed class AddAssemblyClosure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assembly_workshop_balance_events", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                AssemblyId = t.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = t.Column<Guid>(type: "uuid", nullable: false),
                Source = t.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AmountPending = t.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Currency = t.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                SourceReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                RecordedBySubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CutoffDate = t.Column<DateOnly>(type: "date", nullable: false),
                OccurredAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SupersedesEventId = t.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_assembly_workshop_balance_events", x => x.Id);
                t.ForeignKey("FK_assembly_balance_events_assemblies", x => x.AssemblyId,
                    principalSchema: "core", principalTable: "assemblies",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("CK_assembly_balance_source", "\"Source\" IN ('gran_tesoreria','gran_hospitalaria')");
                t.CheckConstraint("CK_assembly_balance_amount", "\"AmountPending\" >= 0");
                t.CheckConstraint("CK_assembly_balance_currency", "\"Currency\" ~ '^[A-Z]{3}$'");
            });
        migrationBuilder.CreateIndex("IX_assembly_balance_events_AssemblyId_OrganizationId_OccurredAtUtc",
            schema: "core", table: "assembly_workshop_balance_events",
            columns: new[] { "AssemblyId", "OrganizationId", "Source", "OccurredAtUtc" });
        migrationBuilder.CreateIndex("IX_assembly_balance_events_SupersedesEventId",
            schema: "core", table: "assembly_workshop_balance_events",
            column: "SupersedesEventId", unique: true, filter: "\"SupersedesEventId\" IS NOT NULL");

        migrationBuilder.Sql("""
            CREATE FUNCTION core.reject_assembly_balance_event_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $assembly_balance_guard$
            BEGIN
                RAISE EXCEPTION 'assembly_workshop_balance_events is append-only'
                    USING ERRCODE = '55000';
            END $assembly_balance_guard$;
            CREATE TRIGGER trg_assembly_balance_event_immutable
            BEFORE UPDATE OR DELETE OR TRUNCATE ON core.assembly_workshop_balance_events
            FOR EACH STATEMENT
            EXECUTE FUNCTION core.reject_assembly_balance_event_mutation();
            """);

        // Guardia de cierre: solo el servicio auditado puede pasar draft→closed; una Asamblea cerrada es inmutable.
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION core.reject_unverified_assembly_closure()
            RETURNS trigger LANGUAGE plpgsql AS $assembly_close_guard$
            BEGIN
                IF TG_OP = 'UPDATE' AND OLD."Status" = 'closed' THEN
                    RAISE EXCEPTION 'Closed assembly is immutable' USING ERRCODE = '55000';
                END IF;
                IF NEW."Status" <> 'draft' THEN
                    IF TG_OP = 'UPDATE' AND NEW."Status" = 'closed' AND OLD."Status" = 'draft'
                       AND current_setting('pmgm.assembly_closure_service', true) = 'on'
                       AND NEW."ClosedAtUtc" IS NOT NULL
                       AND NEW."ClosedBySubject" IS NOT NULL AND length(trim(NEW."ClosedBySubject")) > 0
                       AND NEW."ClosureEvidenceDigest" ~ '^[0-9a-f]{64}$' THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'Assembly closure requires audited service'
                        USING ERRCODE = '55000';
                END IF;
                RETURN NEW;
            END $assembly_close_guard$;
            """);

        // Nada cuelga de una Asamblea cerrada: ni acreditados, ni evaluaciones, ni constancias, ni saldos, ni decisiones.
        migrationBuilder.Sql("""
            CREATE FUNCTION core.reject_closed_assembly_child_change()
            RETURNS trigger LANGUAGE plpgsql AS $assembly_child_guard$
            DECLARE assembly_id uuid;
            BEGIN
                IF TG_OP = 'DELETE' THEN assembly_id := OLD."AssemblyId"; ELSE assembly_id := NEW."AssemblyId"; END IF;
                IF EXISTS (SELECT 1 FROM core.assemblies WHERE "Id" = assembly_id AND "Status" = 'closed') THEN
                    RAISE EXCEPTION 'Closed assembly is immutable' USING ERRCODE = '55000';
                END IF;
                IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                RETURN NEW;
            END $assembly_child_guard$;
            CREATE TRIGGER trg_closed_assembly_members BEFORE INSERT OR UPDATE OR DELETE ON core.assembly_member_statuses
                FOR EACH ROW EXECUTE FUNCTION core.reject_closed_assembly_child_change();
            CREATE TRIGGER trg_closed_assembly_eligibilities BEFORE INSERT OR UPDATE OR DELETE ON core.assembly_eligibilities
                FOR EACH ROW EXECUTE FUNCTION core.reject_closed_assembly_child_change();
            CREATE TRIGGER trg_closed_assembly_attestations BEFORE INSERT ON core.assembly_attestation_events
                FOR EACH ROW EXECUTE FUNCTION core.reject_closed_assembly_child_change();
            CREATE TRIGGER trg_closed_assembly_balances BEFORE INSERT ON core.assembly_workshop_balance_events
                FOR EACH ROW EXECUTE FUNCTION core.reject_closed_assembly_child_change();
            CREATE TRIGGER trg_closed_assembly_debt_events BEFORE INSERT ON core.assembly_debt_decision_events
                FOR EACH ROW EXECUTE FUNCTION core.reject_closed_assembly_child_change();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_closed_assembly_members ON core.assembly_member_statuses;
            DROP TRIGGER IF EXISTS trg_closed_assembly_eligibilities ON core.assembly_eligibilities;
            DROP TRIGGER IF EXISTS trg_closed_assembly_attestations ON core.assembly_attestation_events;
            DROP TRIGGER IF EXISTS trg_closed_assembly_balances ON core.assembly_workshop_balance_events;
            DROP TRIGGER IF EXISTS trg_closed_assembly_debt_events ON core.assembly_debt_decision_events;
            DROP FUNCTION IF EXISTS core.reject_closed_assembly_child_change();
            DROP TRIGGER IF EXISTS trg_assembly_balance_event_immutable ON core.assembly_workshop_balance_events;
            DROP FUNCTION IF EXISTS core.reject_assembly_balance_event_mutation();
            CREATE OR REPLACE FUNCTION core.reject_unverified_assembly_closure()
            RETURNS trigger LANGUAGE plpgsql AS $assembly_close_guard$
            BEGIN
                IF NEW."Status" <> 'draft' THEN
                    RAISE EXCEPTION 'Assembly closure requires audited service'
                        USING ERRCODE = '55000';
                END IF;
                RETURN NEW;
            END $assembly_close_guard$;
            """);
        migrationBuilder.DropTable(name: "assembly_workshop_balance_events", schema: "core");
    }
}
