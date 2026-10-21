using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #613 — agrega canal social_media sin reescribir migraciones previas
/// ni los datos históricos de candidaturas.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261021173000_AddSocialMediaCandidateOrigin")]
public sealed class AddSocialMediaCandidateOrigin : Migration
{
    private const string CeremonyCheck = """
        ("CandidateOriginKind" IS NULL AND "CandidateRecommenderMemberId" IS NULL AND "CandidateOriginOtherDescription" IS NULL)
        OR ("CandidateOriginKind" = 'brother' AND "CandidateRecommenderMemberId" IS NOT NULL AND "CandidateOriginOtherDescription" IS NULL)
        OR ("CandidateOriginKind" IN ('internet', 'social_media') AND "CandidateRecommenderMemberId" IS NULL AND "CandidateOriginOtherDescription" IS NULL)
        OR ("CandidateOriginKind" = 'other' AND "CandidateRecommenderMemberId" IS NULL AND "CandidateOriginOtherDescription" IS NOT NULL AND length(btrim("CandidateOriginOtherDescription")) > 0)
        """;

    private const string PreviousCeremonyCheck = """
        ("CandidateOriginKind" IS NULL AND "CandidateRecommenderMemberId" IS NULL AND "CandidateOriginOtherDescription" IS NULL)
        OR ("CandidateOriginKind" = 'brother' AND "CandidateRecommenderMemberId" IS NOT NULL AND "CandidateOriginOtherDescription" IS NULL)
        OR ("CandidateOriginKind" = 'internet' AND "CandidateRecommenderMemberId" IS NULL AND "CandidateOriginOtherDescription" IS NULL)
        OR ("CandidateOriginKind" = 'other' AND "CandidateRecommenderMemberId" IS NULL AND "CandidateOriginOtherDescription" IS NOT NULL AND length(btrim("CandidateOriginOtherDescription")) > 0)
        """;

    protected override void Up(MigrationBuilder m)
    {
        m.DropCheckConstraint(
            name: "CK_ceremony_candidate_origin_consistency",
            table: "ceremony_requests", schema: "core");
        m.AddCheckConstraint(
            name: "CK_ceremony_candidate_origin_consistency",
            table: "ceremony_requests", schema: "core",
            sql: CeremonyCheck);
        m.DropCheckConstraint(
            name: "CK_candidate_origin_corrections_new_kind",
            table: "candidate_origin_corrections", schema: "core");
        m.AddCheckConstraint(
            name: "CK_candidate_origin_corrections_new_kind",
            table: "candidate_origin_corrections", schema: "core",
            sql: "\"NewKind\" IN ('brother', 'internet', 'social_media', 'other')");
    }

    protected override void Down(MigrationBuilder m)
    {
        // Es intencional que PostgreSQL impida regresar si ya existen filas
        // social_media: no destruir ni reclasificar evidencia institucional.
        m.DropCheckConstraint(
            name: "CK_candidate_origin_corrections_new_kind",
            table: "candidate_origin_corrections", schema: "core");
        m.AddCheckConstraint(
            name: "CK_candidate_origin_corrections_new_kind",
            table: "candidate_origin_corrections", schema: "core",
            sql: "\"NewKind\" IN ('brother', 'internet', 'other')");
        m.DropCheckConstraint(
            name: "CK_ceremony_candidate_origin_consistency",
            table: "ceremony_requests", schema: "core");
        m.AddCheckConstraint(
            name: "CK_ceremony_candidate_origin_consistency",
            table: "ceremony_requests", schema: "core",
            sql: PreviousCeremonyCheck);
    }
}
