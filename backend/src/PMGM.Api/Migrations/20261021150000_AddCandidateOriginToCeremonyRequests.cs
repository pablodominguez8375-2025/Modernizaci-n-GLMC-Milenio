using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #613 — aditivo y reversible. Las solicitudes históricas conservan
/// origen NULL (Sin registrar); no inferir ni migrar el recomendante.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261021150000_AddCandidateOriginToCeremonyRequests")]
public sealed class AddCandidateOriginToCeremonyRequests : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>(
            name: "CandidateOriginKind", schema: "core", table: "ceremony_requests",
            type: "character varying(24)", maxLength: 24, nullable: true);
        m.AddColumn<Guid>(
            name: "CandidateRecommenderMemberId", schema: "core", table: "ceremony_requests",
            type: "uuid", nullable: true);
        m.AddColumn<string>(
            name: "CandidateOriginOtherDescription", schema: "core",
            table: "ceremony_requests", type: "character varying(200)",
            maxLength: 200, nullable: true);
        m.AddCheckConstraint(
            name: "CK_ceremony_candidate_origin_consistency", schema: "core",
            table: "ceremony_requests",
            sql: """
                ("CandidateOriginKind" IS NULL AND "CandidateRecommenderMemberId" IS NULL AND "CandidateOriginOtherDescription" IS NULL)
                OR ("CandidateOriginKind" = 'brother' AND "CandidateRecommenderMemberId" IS NOT NULL AND "CandidateOriginOtherDescription" IS NULL)
                OR ("CandidateOriginKind" = 'internet' AND "CandidateRecommenderMemberId" IS NULL AND "CandidateOriginOtherDescription" IS NULL)
                OR ("CandidateOriginKind" = 'other' AND "CandidateRecommenderMemberId" IS NULL AND "CandidateOriginOtherDescription" IS NOT NULL AND length(btrim("CandidateOriginOtherDescription")) > 0)
                """);
        m.CreateIndex(name: "IX_ceremony_requests_CandidateRecommenderMemberId",
            schema: "core", table: "ceremony_requests",
            column: "CandidateRecommenderMemberId");
        m.AddForeignKey(name: "FK_ceremony_requests_members_CandidateRecommenderMemberId",
            schema: "core", table: "ceremony_requests",
            column: "CandidateRecommenderMemberId",
            principalSchema: "core", principalTable: "members",
            principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropForeignKey(name: "FK_ceremony_requests_members_CandidateRecommenderMemberId",
            schema: "core", table: "ceremony_requests");
        m.DropIndex(name: "IX_ceremony_requests_CandidateRecommenderMemberId",
            schema: "core", table: "ceremony_requests");
        m.DropCheckConstraint(name: "CK_ceremony_candidate_origin_consistency",
            schema: "core", table: "ceremony_requests");
        m.DropColumn(name: "CandidateOriginOtherDescription", schema: "core",
            table: "ceremony_requests");
        m.DropColumn(name: "CandidateRecommenderMemberId", schema: "core",
            table: "ceremony_requests");
        m.DropColumn(name: "CandidateOriginKind", schema: "core",
            table: "ceremony_requests");
    }
}
