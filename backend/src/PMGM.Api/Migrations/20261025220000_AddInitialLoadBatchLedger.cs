using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Modules.Bootstrap;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #694 phase Q. Only keyed batch fingerprints and audit references, no PII.
/// Creating this ledger does not register an importer or enable apply.
/// </summary>
[DbContext(typeof(BootstrapDbContext))]
[Migration("20261025220000_AddInitialLoadBatchLedger")]
public sealed class AddInitialLoadBatchLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "initial_load_batch_ledger",
            schema: "core",
            columns: table => new
            {
                Fingerprint = table.Column<string>(
                    type: "character varying(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(
                    type: "character varying(16)", maxLength: 16, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                ReservedAuditEventId = table.Column<Guid>(type: "uuid", nullable: false),
                AppliedAuditEventId = table.Column<Guid>(type: "uuid", nullable: true),
                ReversedAuditEventId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone", nullable: false),
                AppliedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone", nullable: true),
                ReversedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                // PostgreSQL PK is the only source of truth for uniqueness,
                // including two concurrent reservation attempts.
                table.PrimaryKey("PK_initial_load_batch_ledger",
                    x => x.Fingerprint);
                table.ForeignKey(
                    name: "FK_initial_load_batch_ledger_audit_events_ReservedAuditEventId",
                    column: x => x.ReservedAuditEventId,
                    principalSchema: "core", principalTable: "audit_events",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_initial_load_batch_ledger_audit_events_AppliedAuditEventId",
                    column: x => x.AppliedAuditEventId,
                    principalSchema: "core", principalTable: "audit_events",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_initial_load_batch_ledger_audit_events_ReversedAuditEventId",
                    column: x => x.ReversedAuditEventId,
                    principalSchema: "core", principalTable: "audit_events",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_initial_load_batch_ledger_ReservedAuditEventId",
            schema: "core", table: "initial_load_batch_ledger",
            column: "ReservedAuditEventId");
        migrationBuilder.CreateIndex(
            name: "IX_initial_load_batch_ledger_AppliedAuditEventId",
            schema: "core", table: "initial_load_batch_ledger",
            column: "AppliedAuditEventId");
        migrationBuilder.CreateIndex(
            name: "IX_initial_load_batch_ledger_ReversedAuditEventId",
            schema: "core", table: "initial_load_batch_ledger",
            column: "ReversedAuditEventId");

        migrationBuilder.Sql("""
            ALTER TABLE core.initial_load_batch_ledger
            ADD CONSTRAINT ck_initial_load_batch_ledger_state CHECK (
                "Fingerprint" ~ '^[a-f0-9]{64}$'
                AND "UpdatedAtUtc" >= "CreatedAtUtc"
                AND (
                    ("Status" = 'pending' AND "Version" = 0
                        AND "AppliedAuditEventId" IS NULL
                        AND "ReversedAuditEventId" IS NULL
                        AND "AppliedAtUtc" IS NULL
                        AND "ReversedAtUtc" IS NULL)
                    OR
                    ("Status" = 'applied' AND "Version" = 1
                        AND "AppliedAuditEventId" IS NOT NULL
                        AND "AppliedAuditEventId" <> "ReservedAuditEventId"
                        AND "AppliedAtUtc" IS NOT NULL
                        AND "AppliedAtUtc" >= "CreatedAtUtc"
                        AND "UpdatedAtUtc" >= "AppliedAtUtc"
                        AND "ReversedAuditEventId" IS NULL
                        AND "ReversedAtUtc" IS NULL)
                    OR
                    ("Status" = 'reversed' AND "Version" = 2
                        AND "AppliedAuditEventId" IS NOT NULL
                        AND "ReversedAuditEventId" IS NOT NULL
                        AND "AppliedAuditEventId" <> "ReservedAuditEventId"
                        AND "ReversedAuditEventId" <> "ReservedAuditEventId"
                        AND "ReversedAuditEventId" <> "AppliedAuditEventId"
                        AND "AppliedAtUtc" IS NOT NULL
                        AND "ReversedAtUtc" IS NOT NULL
                        AND "AppliedAtUtc" >= "CreatedAtUtc"
                        AND "ReversedAtUtc" >= "AppliedAtUtc"
                        AND "UpdatedAtUtc" >= "ReversedAtUtc")
                )
            );

            CREATE FUNCTION core.guard_initial_load_batch_ledger()
            RETURNS trigger LANGUAGE plpgsql AS $initial_load_batch_guard$
            BEGIN
                IF TG_OP = 'DELETE' OR TG_OP = 'TRUNCATE' THEN
                    RAISE EXCEPTION 'initial load batch history is protected'
                        USING ERRCODE = '55000';
                END IF;

                IF TG_OP = 'INSERT' THEN
                    IF NEW."Status" <> 'pending' OR NEW."Version" <> 0 THEN
                        RAISE EXCEPTION 'initial load batch must start pending'
                            USING ERRCODE = '55000';
                    END IF;
                    RETURN NEW;
                END IF;

                IF TG_OP = 'UPDATE' THEN
                    IF NOT (
                        (OLD."Status" = 'pending' AND NEW."Status" = 'applied')
                        OR (OLD."Status" = 'applied' AND NEW."Status" = 'reversed')
                    ) THEN
                        RAISE EXCEPTION 'initial load batch transition invalid'
                            USING ERRCODE = '55000';
                    END IF;
                    IF NEW."Version" <> OLD."Version" + 1
                       OR NEW."Fingerprint" IS DISTINCT FROM OLD."Fingerprint"
                       OR NEW."ReservedAuditEventId"
                            IS DISTINCT FROM OLD."ReservedAuditEventId"
                       OR NEW."CreatedAtUtc" IS DISTINCT FROM OLD."CreatedAtUtc"
                       OR NEW."UpdatedAtUtc" < OLD."UpdatedAtUtc" THEN
                        RAISE EXCEPTION 'initial load batch proof immutable'
                            USING ERRCODE = '55000';
                    END IF;
                    IF OLD."Status" = 'applied' AND (
                        NEW."AppliedAuditEventId" IS DISTINCT
                            FROM OLD."AppliedAuditEventId"
                        OR NEW."AppliedAtUtc" IS DISTINCT FROM OLD."AppliedAtUtc"
                    ) THEN
                        RAISE EXCEPTION 'initial load apply proof immutable'
                            USING ERRCODE = '55000';
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $initial_load_batch_guard$;

            CREATE TRIGGER trg_initial_load_batch_protect_rows
            BEFORE INSERT OR UPDATE OR DELETE ON core.initial_load_batch_ledger
            FOR EACH ROW EXECUTE FUNCTION core.guard_initial_load_batch_ledger();

            CREATE TRIGGER trg_initial_load_batch_no_truncate
            BEFORE TRUNCATE ON core.initial_load_batch_ledger
            FOR EACH STATEMENT EXECUTE FUNCTION core.guard_initial_load_batch_ledger();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_initial_load_batch_no_truncate
                ON core.initial_load_batch_ledger;
            DROP TRIGGER IF EXISTS trg_initial_load_batch_protect_rows
                ON core.initial_load_batch_ledger;
            DROP FUNCTION IF EXISTS core.guard_initial_load_batch_ledger();
            ALTER TABLE core.initial_load_batch_ledger
                DROP CONSTRAINT IF EXISTS ck_initial_load_batch_ledger_state;
            """);
        migrationBuilder.DropTable(
            name: "initial_load_batch_ledger", schema: "core");
    }
}
