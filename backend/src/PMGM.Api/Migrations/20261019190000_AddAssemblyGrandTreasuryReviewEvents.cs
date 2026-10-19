using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// Historial documental GT solamente. No habilita certificación final,
/// emisión de saldo electoral ni cierre de Asamblea.
/// </summary>
[DbContext(typeof(RegimenInteriorDbContext))]
[Migration("20261019190000_AddAssemblyGrandTreasuryReviewEvents")]
public sealed class AddAssemblyGrandTreasuryReviewEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assembly_treasury_certification_events",
            schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                AssemblyId = t.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = t.Column<Guid>(type: "uuid", nullable: false),
                Action = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ActorSubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                OccurredAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ProposalEventId = t.Column<Guid>(type: "uuid", nullable: true),
                CoverageFrom = t.Column<DateOnly>(type: "date", nullable: true),
                CutoffDate = t.Column<DateOnly>(type: "date", nullable: true),
                Currency = t.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                OpeningPendingAmount = t.Column<decimal>(type: "numeric(18,2)", nullable: true),
                SourceEvidenceReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                EvidenceDigestSha256 = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ReviewEvidenceReference = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ReviewNote = t.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_assembly_treasury_certification_events", x => x.Id);
                t.UniqueConstraint("AK_assembly_gt_events_Assembly_Organization_Id",
                    x => new { x.AssemblyId, x.OrganizationId, x.Id });
                t.ForeignKey("FK_assembly_gt_events_assemblies", x => x.AssemblyId,
                    principalSchema: "core", principalTable: "assemblies",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                t.ForeignKey("FK_assembly_gt_events_origin",
                    x => new { x.AssemblyId, x.OrganizationId, x.ProposalEventId },
                    principalSchema: "core", principalTable: "assembly_treasury_certification_events",
                    principalColumns: new[] { "AssemblyId", "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("CK_assembly_gt_actions",
                    """"Action" IN ('proposed','reviewed','rejected')""");
                t.CheckConstraint("CK_assembly_gt_actor",
                    """length(trim("ActorSubject")) > 0""");
                t.CheckConstraint("CK_assembly_gt_shape",
                    """(
                        "Action" = 'proposed' AND "ProposalEventId" IS NULL AND
                        "CoverageFrom" IS NOT NULL AND "CutoffDate" IS NOT NULL AND
                        "CoverageFrom" <= "CutoffDate" AND
                        "Currency" IS NOT NULL AND "Currency" ~ '^[A-Z]{3}
                        "ReviewEvidenceReference" IS NULL AND "ReviewNote" IS NULL
                    ) OR (
                        "Action" IN ('reviewed','rejected') AND "ProposalEventId" IS NOT NULL AND
                        "CoverageFrom" IS NULL AND "CutoffDate" IS NULL AND
                        "Currency" IS NULL AND "OpeningPendingAmount" IS NULL AND
                        "SourceEvidenceReference" IS NULL AND "EvidenceDigestSha256" IS NULL AND
                        "ReviewEvidenceReference" IS NOT NULL AND length(trim("ReviewEvidenceReference")) > 0 AND
                        "ReviewNote" IS NOT NULL AND length(trim("ReviewNote")) > 0
                    )""");
            });

        migrationBuilder.CreateIndex("IX_assembly_gt_events_Assembly_Org_Occurred",
            schema: "core", table: "assembly_treasury_certification_events",
            columns: new[] { "AssemblyId", "OrganizationId", "OccurredAtUtc" });
        // Un único resultado de revisión por propuesta. Si se rechaza, solo una
        // NUEVA propuesta documental podrá volver a iniciar el procedimiento.
        migrationBuilder.CreateIndex("UX_assembly_gt_events_Proposal",
            schema: "core", table: "assembly_treasury_certification_events",
            columns: new[] { "AssemblyId", "OrganizationId", "ProposalEventId" },
            unique: true, filter: """"ProposalEventId" IS NOT NULL""");

        migrationBuilder.Sql("""
            CREATE FUNCTION core.check_gt_cert_review_insert()
            RETURNS trigger LANGUAGE plpgsql AS $gt_review_guard$
            DECLARE original_actor text; original_action text;
            BEGIN
                IF NEW."Action" IN ('reviewed','rejected') THEN
                    SELECT e."ActorSubject", e."Action" INTO original_actor, original_action
                    FROM core.assembly_treasury_certification_events e
                    WHERE e."Id" = NEW."ProposalEventId"
                      AND e."AssemblyId" = NEW."AssemblyId"
                      AND e."OrganizationId" = NEW."OrganizationId";
                    IF original_actor IS NULL OR original_action <> 'proposed'
                       OR original_actor = NEW."ActorSubject" THEN
                        RAISE EXCEPTION 'independent original GT review required'
                            USING ERRCODE = '23514';
                    END IF;
                END IF;
                RETURN NEW;
            END $gt_review_guard$;
            CREATE TRIGGER trg_assembly_gt_review_guard
            BEFORE INSERT ON core.assembly_treasury_certification_events
            FOR EACH ROW EXECUTE FUNCTION core.check_gt_cert_review_insert();
            """);

        migrationBuilder.Sql("""
            CREATE FUNCTION core.reject_gt_cert_event_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $gt_cert_guard$
            BEGIN
                RAISE EXCEPTION 'assembly_treasury_certification_events is append-only'
                    USING ERRCODE = '55000';
            END $gt_cert_guard$;
            CREATE TRIGGER trg_assembly_gt_events_immutable
            BEFORE UPDATE OR DELETE OR TRUNCATE ON core.assembly_treasury_certification_events
            FOR EACH STATEMENT EXECUTE FUNCTION core.reject_gt_cert_event_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Solo para desmontaje aislado de una migración nunca puesta en producción.
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_assembly_gt_review_guard
                ON core.assembly_treasury_certification_events;
            DROP FUNCTION IF EXISTS core.check_gt_cert_review_insert();
            """);
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_assembly_gt_events_immutable
                ON core.assembly_treasury_certification_events;
            DROP FUNCTION IF EXISTS core.reject_gt_cert_event_mutation();
            """);
        migrationBuilder.DropTable("assembly_treasury_certification_events", schema: "core");
    }
}
 AND
                        "OpeningPendingAmount" IS NOT NULL AND "OpeningPendingAmount" >= 0 AND
                        "SourceEvidenceReference" IS NOT NULL AND length(trim("SourceEvidenceReference")) > 0 AND
                        "EvidenceDigestSha256" IS NOT NULL AND "EvidenceDigestSha256" ~ '^[0-9a-f]{64}
                        "ReviewEvidenceReference" IS NULL AND "ReviewNote" IS NULL
                    ) OR (
                        "Action" IN ('reviewed','rejected') AND "ProposalEventId" IS NOT NULL AND
                        "CoverageFrom" IS NULL AND "CutoffDate" IS NULL AND
                        "Currency" IS NULL AND "OpeningPendingAmount" IS NULL AND
                        "SourceEvidenceReference" IS NULL AND "EvidenceDigestSha256" IS NULL AND
                        length(trim("ReviewEvidenceReference")) > 0 AND
                        length(trim("ReviewNote")) > 0
                    )""");
            });

        migrationBuilder.CreateIndex("IX_assembly_gt_events_Assembly_Org_Occurred",
            schema: "core", table: "assembly_treasury_certification_events",
            columns: new[] { "AssemblyId", "OrganizationId", "OccurredAtUtc" });
        // Un único resultado de revisión por propuesta. Si se rechaza, solo una
        // NUEVA propuesta documental podrá volver a iniciar el procedimiento.
        migrationBuilder.CreateIndex("UX_assembly_gt_events_Proposal",
            schema: "core", table: "assembly_treasury_certification_events",
            columns: new[] { "AssemblyId", "OrganizationId", "ProposalEventId" },
            unique: true, filter: """"ProposalEventId" IS NOT NULL""");

        migrationBuilder.Sql("""
            CREATE FUNCTION core.reject_gt_cert_event_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $gt_cert_guard$
            BEGIN
                RAISE EXCEPTION 'assembly_treasury_certification_events is append-only'
                    USING ERRCODE = '55000';
            END $gt_cert_guard$;
            CREATE TRIGGER trg_assembly_gt_events_immutable
            BEFORE UPDATE OR DELETE OR TRUNCATE ON core.assembly_treasury_certification_events
            FOR EACH STATEMENT EXECUTE FUNCTION core.reject_gt_cert_event_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Solo para desmontaje aislado de una migración nunca puesta en producción.
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_assembly_gt_events_immutable
                ON core.assembly_treasury_certification_events;
            DROP FUNCTION IF EXISTS core.reject_gt_cert_event_mutation();
            """);
        migrationBuilder.DropTable("assembly_treasury_certification_events", schema: "core");
    }
}
 AND
                        "ReviewEvidenceReference" IS NULL AND "ReviewNote" IS NULL
                    ) OR (
                        "Action" IN ('reviewed','rejected') AND "ProposalEventId" IS NOT NULL AND
                        "CoverageFrom" IS NULL AND "CutoffDate" IS NULL AND
                        "Currency" IS NULL AND "OpeningPendingAmount" IS NULL AND
                        "SourceEvidenceReference" IS NULL AND "EvidenceDigestSha256" IS NULL AND
                        length(trim("ReviewEvidenceReference")) > 0 AND
                        length(trim("ReviewNote")) > 0
                    )""");
            });

        migrationBuilder.CreateIndex("IX_assembly_gt_events_Assembly_Org_Occurred",
            schema: "core", table: "assembly_treasury_certification_events",
            columns: new[] { "AssemblyId", "OrganizationId", "OccurredAtUtc" });
        // Un único resultado de revisión por propuesta. Si se rechaza, solo una
        // NUEVA propuesta documental podrá volver a iniciar el procedimiento.
        migrationBuilder.CreateIndex("UX_assembly_gt_events_Proposal",
            schema: "core", table: "assembly_treasury_certification_events",
            columns: new[] { "AssemblyId", "OrganizationId", "ProposalEventId" },
            unique: true, filter: """"ProposalEventId" IS NOT NULL""");

        migrationBuilder.Sql("""
            CREATE FUNCTION core.reject_gt_cert_event_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $gt_cert_guard$
            BEGIN
                RAISE EXCEPTION 'assembly_treasury_certification_events is append-only'
                    USING ERRCODE = '55000';
            END $gt_cert_guard$;
            CREATE TRIGGER trg_assembly_gt_events_immutable
            BEFORE UPDATE OR DELETE OR TRUNCATE ON core.assembly_treasury_certification_events
            FOR EACH STATEMENT EXECUTE FUNCTION core.reject_gt_cert_event_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Solo para desmontaje aislado de una migración nunca puesta en producción.
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_assembly_gt_events_immutable
                ON core.assembly_treasury_certification_events;
            DROP FUNCTION IF EXISTS core.reject_gt_cert_event_mutation();
            """);
        migrationBuilder.DropTable("assembly_treasury_certification_events", schema: "core");
    }
}
