using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Modules.Bootstrap;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #694 — estado singleton de la carga inicial, seguro por defecto.
/// Sólo contiene metadatos del procedimiento, no nóminas ni RUT/correos.
/// No agrega servicios, endpoints ni permisos de aplicación.
/// </summary>
[DbContext(typeof(BootstrapDbContext))]
[Migration("20261024183000_AddInitialLoadWindow")]
public sealed class AddInitialLoadWindow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "initial_load_windows",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<short>(type: "smallint", nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LegalApprovalDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                OpenedAuditEventId = table.Column<Guid>(type: "uuid", nullable: true),
                ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ClosedAuditEventId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_initial_load_windows", x => x.Id);
                table.ForeignKey(
                    name: "FK_initial_load_windows_audit_events_OpenedAuditEventId",
                    column: x => x.OpenedAuditEventId,
                    principalSchema: "core", principalTable: "audit_events",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_initial_load_windows_audit_events_ClosedAuditEventId",
                    column: x => x.ClosedAuditEventId,
                    principalSchema: "core", principalTable: "audit_events",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_initial_load_windows_OpenedAuditEventId",
            schema: "core", table: "initial_load_windows",
            column: "OpenedAuditEventId");
        migrationBuilder.CreateIndex(
            name: "IX_initial_load_windows_ClosedAuditEventId",
            schema: "core", table: "initial_load_windows",
            column: "ClosedAuditEventId");

        migrationBuilder.Sql("""
            ALTER TABLE core.initial_load_windows
            ADD CONSTRAINT ck_initial_load_window_state
            CHECK (
                "Id" = 1 AND
                "Version" >= 0 AND
                "UpdatedAtUtc" >= "CreatedAtUtc" AND
                (
                    ("Status" = 'not_opened' AND "Version" = 0 AND
                     "OpenedAtUtc" IS NULL AND "OpenedAuditEventId" IS NULL AND
                     "ClosedAtUtc" IS NULL AND "ClosedAuditEventId" IS NULL AND
                     "LegalApprovalDigest" IS NULL)
                    OR
                    ("Status" = 'open' AND "Version" = 1 AND
                     "OpenedAtUtc" IS NOT NULL AND "OpenedAuditEventId" IS NOT NULL AND
                     "ClosedAtUtc" IS NULL AND "ClosedAuditEventId" IS NULL AND
                     "LegalApprovalDigest" IS NOT NULL AND
                     "LegalApprovalDigest" ~ '^[a-f0-9]{64}$')
                    OR
                    ("Status" = 'closed' AND "Version" = 2 AND
                     "OpenedAtUtc" IS NOT NULL AND "OpenedAuditEventId" IS NOT NULL AND
                     "ClosedAtUtc" IS NOT NULL AND "ClosedAuditEventId" IS NOT NULL AND
                     "ClosedAtUtc" >= "OpenedAtUtc" AND
                     "OpenedAuditEventId" <> "ClosedAuditEventId" AND
                     "LegalApprovalDigest" IS NOT NULL AND
                     "LegalApprovalDigest" ~ '^[a-f0-9]{64}$')
                )
            );

            CREATE FUNCTION core.guard_initial_load_window()
            RETURNS trigger LANGUAGE plpgsql AS $initial_load_guard$
            BEGIN
                IF TG_OP = 'DELETE' OR TG_OP = 'TRUNCATE' THEN
                    RAISE EXCEPTION 'initial load window is append-protected'
                        USING ERRCODE = '55000';
                END IF;

                IF TG_OP = 'UPDATE' THEN
                    IF NOT (
                        (OLD."Status" = 'not_opened' AND NEW."Status" = 'open')
                        OR (OLD."Status" = 'open' AND NEW."Status" = 'closed')
                    ) THEN
                        RAISE EXCEPTION 'invalid or irreversible initial load window transition'
                            USING ERRCODE = '55000';
                    END IF;

                    IF NEW."Version" <> OLD."Version" + 1
                       OR NEW."Id" IS DISTINCT FROM OLD."Id"
                       OR NEW."CreatedAtUtc" IS DISTINCT FROM OLD."CreatedAtUtc"
                       OR NEW."UpdatedAtUtc" < OLD."UpdatedAtUtc" THEN
                        RAISE EXCEPTION 'invalid initial load window revision'
                            USING ERRCODE = '55000';
                    END IF;

                    IF OLD."Status" = 'open' AND (
                        NEW."LegalApprovalDigest" IS DISTINCT FROM OLD."LegalApprovalDigest"
                        OR NEW."OpenedAtUtc" IS DISTINCT FROM OLD."OpenedAtUtc"
                        OR NEW."OpenedAuditEventId" IS DISTINCT FROM OLD."OpenedAuditEventId"
                    ) THEN
                        RAISE EXCEPTION 'initial load legal and opening proof is immutable'
                            USING ERRCODE = '55000';
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $initial_load_guard$;

            CREATE TRIGGER trg_initial_load_window_no_unsafe_change
            BEFORE UPDATE OR DELETE ON core.initial_load_windows
            FOR EACH ROW EXECUTE FUNCTION core.guard_initial_load_window();

            CREATE TRIGGER trg_initial_load_window_no_truncate
            BEFORE TRUNCATE ON core.initial_load_windows
            FOR EACH STATEMENT EXECUTE FUNCTION core.guard_initial_load_window();

            INSERT INTO core.initial_load_windows (
                "Id", "Status", "Version", "CreatedAtUtc", "UpdatedAtUtc"
            ) VALUES (1, 'not_opened', 0, now(), now());
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_initial_load_window_no_truncate
                ON core.initial_load_windows;
            DROP TRIGGER IF EXISTS trg_initial_load_window_no_unsafe_change
                ON core.initial_load_windows;
            DROP FUNCTION IF EXISTS core.guard_initial_load_window();
            ALTER TABLE core.initial_load_windows
                DROP CONSTRAINT IF EXISTS ck_initial_load_window_state;
            """);
        migrationBuilder.DropTable(name: "initial_load_windows", schema: "core");
    }
}
