using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>Constancias técnicas GT. No certificado financiero ni padrón.</summary>
[DbContext(typeof(RegimenInteriorDbContext))]
[Migration("20261019210000_AddAssemblyTreasurySignedReviews")]
public sealed class AddAssemblyTreasurySignedReviews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assembly_treasury_signed_reviews",
            schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                AssemblyId = t.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = t.Column<Guid>(type: "uuid", nullable: false),
                ProposalEventId = t.Column<Guid>(type: "uuid", nullable: false),
                ReviewEventId = t.Column<Guid>(type: "uuid", nullable: false),
                ReviewerSubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                TrustedKeyId = t.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                DocumentSha256Hex = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SignatureBase64 = t.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                SignedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RecordedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_assembly_treasury_signed_reviews", x => x.Id);
                t.ForeignKey("FK_gt_signed_review_proposal",
                    x => new { x.AssemblyId, x.OrganizationId, x.ProposalEventId },
                    principalSchema: "core", principalTable: "assembly_treasury_certification_events",
                    principalColumns: new[] { "AssemblyId", "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                t.ForeignKey("FK_gt_signed_review_event",
                    x => new { x.AssemblyId, x.OrganizationId, x.ReviewEventId },
                    principalSchema: "core", principalTable: "assembly_treasury_certification_events",
                    principalColumns: new[] { "AssemblyId", "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("CK_gt_signed_review_document_sha",
                    "\"DocumentSha256Hex\" ~ '^[0-9a-f]{64}$'");
                t.CheckConstraint("CK_gt_signed_review_subject_key",
                    """length(trim("ReviewerSubject")) > 0 AND length(trim("TrustedKeyId")) > 0""");
                t.CheckConstraint("CK_gt_signed_review_signature",
                    """length(trim("SignatureBase64")) >= 64 AND "SignedAtUtc" <= "RecordedAtUtc" """);
            });
        migrationBuilder.CreateIndex("UX_gt_signed_review_event",
            schema: "core", table: "assembly_treasury_signed_reviews",
            column: "ReviewEventId", unique: true);
        migrationBuilder.CreateIndex("IX_gt_signed_review_assembly_organization",
            schema: "core", table: "assembly_treasury_signed_reviews",
            columns: new[] { "AssemblyId", "OrganizationId", "RecordedAtUtc" });

        migrationBuilder.Sql("""
            CREATE FUNCTION core.validate_gt_signed_review_insert()
            RETURNS trigger LANGUAGE plpgsql AS $gt_signed_review$
            DECLARE
                proposer_action text;
                proposer_subject text;
                proposer_hash text;
                reviewer_action text;
                reviewer_subject text;
                reviewer_proposal uuid;
                review_occurred timestamptz;
            BEGIN
                SELECT p."Action", p."ActorSubject", p."EvidenceDigestSha256"
                  INTO proposer_action, proposer_subject, proposer_hash
                  FROM core.assembly_treasury_certification_events p
                  WHERE p."Id" = NEW."ProposalEventId"
                    AND p."AssemblyId" = NEW."AssemblyId"
                    AND p."OrganizationId" = NEW."OrganizationId";
                SELECT r."Action", r."ActorSubject", r."ProposalEventId", r."OccurredAtUtc"
                  INTO reviewer_action, reviewer_subject, reviewer_proposal, review_occurred
                  FROM core.assembly_treasury_certification_events r
                  WHERE r."Id" = NEW."ReviewEventId"
                    AND r."AssemblyId" = NEW."AssemblyId"
                    AND r."OrganizationId" = NEW."OrganizationId";
                IF proposer_action IS DISTINCT FROM 'proposed'
                   OR reviewer_action IS DISTINCT FROM 'reviewed'
                   OR reviewer_proposal IS DISTINCT FROM NEW."ProposalEventId"
                   OR proposer_subject IS NULL OR reviewer_subject IS NULL
                   OR proposer_subject = reviewer_subject
                   OR reviewer_subject IS DISTINCT FROM NEW."ReviewerSubject"
                   OR proposer_hash IS DISTINCT FROM NEW."DocumentSha256Hex"
                   OR review_occurred IS NULL
                   OR NEW."SignedAtUtc" < review_occurred THEN
                    RAISE EXCEPTION 'invalid independent signed GT review relation'
                        USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $gt_signed_review$;
            CREATE TRIGGER trg_gt_signed_review_validate
            BEFORE INSERT ON core.assembly_treasury_signed_reviews
            FOR EACH ROW EXECUTE FUNCTION core.validate_gt_signed_review_insert();

            CREATE FUNCTION core.block_gt_signed_review_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $gt_signed_immutable$
            BEGIN
                RAISE EXCEPTION 'GT signed review history is append-only'
                    USING ERRCODE = '55000';
            END $gt_signed_immutable$;
            CREATE TRIGGER trg_gt_signed_review_immutable
            BEFORE UPDATE OR DELETE OR TRUNCATE ON core.assembly_treasury_signed_reviews
            FOR EACH STATEMENT EXECUTE FUNCTION core.block_gt_signed_review_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Solo rollback aislado antes de uso real. No borrar auditoría en producción.
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_gt_signed_review_immutable
                ON core.assembly_treasury_signed_reviews;
            DROP FUNCTION IF EXISTS core.block_gt_signed_review_mutation();
            DROP TRIGGER IF EXISTS trg_gt_signed_review_validate
                ON core.assembly_treasury_signed_reviews;
            DROP FUNCTION IF EXISTS core.validate_gt_signed_review_insert();
            """);
        migrationBuilder.DropTable("assembly_treasury_signed_reviews", schema: "core");
    }
}
