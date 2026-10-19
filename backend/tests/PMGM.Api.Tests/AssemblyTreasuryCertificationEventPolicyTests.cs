using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyTreasuryCertificationEventPolicyTests
{
    private static readonly Guid AssemblyId = Guid.Parse("74f847b4-027e-4309-9e93-26310485296d");
    private static readonly Guid Workshop = Guid.Parse("fee24a03-c327-4a84-b41d-0256eb52ee04");
    private static readonly DateOnly From = new(2026, 8, 1);
    private static readonly DateOnly Cutoff = new(2026, 10, 19);

    private static ClaimsPrincipal Actor(string subject, string role = InstitutionalRoles.GranTesoreria)
    {
        var claims = new[]
        {
            new Claim("sub", subject),
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestIdentity"));
    }

    private static AssemblyTreasuryInspectionResult Inspection() =>
        new(Workshop, From, Cutoff, 3, 3, true, 0m, ["HISTORICAL_CERTIFICATION_REQUIRED"]);

    private static AssemblyGrandTreasuryEvidenceProposal Evidence() =>
        new(Workshop, From, Cutoff, "CLP", From.AddDays(-1), 1000m,
            "GT-APERTURA-EJEMPLO", "GT-PL-2026-EJEMPLO",
            "GT-RECTIF-EJEMPLO", new string('b', 64), true, true, true);

    [Fact]
    public void Grand_treasurer_can_create_document_proposal_but_not_a_certificate()
    {
        var record = AssemblyTreasuryCertificationEventPolicy.BuildProposal(
            Actor("gt-proponente"), AssemblyId, Inspection(), Evidence());
        Assert.NotNull(record);
        Assert.Equal(AssemblyTreasuryCertificationEventPolicy.Proposed, record.Action);
        Assert.Null(record.ProposalEventId);
        Assert.Equal("gt-proponente", record.ActorSubject);
        Assert.Equal(new string('b', 64), record.EvidenceDigestSha256);
    }

    [Fact]
    public void Different_grand_treasurer_may_review_without_enabling_suffrage()
    {
        var proposed = AssemblyTreasuryCertificationEventPolicy.BuildProposal(
            Actor("gt-primero"), AssemblyId, Inspection(), Evidence())!;
        var review = AssemblyTreasuryCertificationEventPolicy.BuildIndependentReview(
            Actor("gt-segundo"), AssemblyId, Workshop, proposed,
            AssemblyTreasuryCertificationEventPolicy.Reviewed,
            "GT-REV-2026", "Revisión documental sin emisión final");
        Assert.NotNull(review);
        Assert.Equal(proposed.Id, review.ProposalEventId);
        Assert.Equal("gt-segundo", review.ActorSubject);
        Assert.Null(review.OpeningPendingAmount);
        Assert.Null(review.EvidenceDigestSha256);
    }

    [Theory]
    [InlineData(InstitutionalRoles.PlatformSuperAdmin)]
    [InlineData(InstitutionalRoles.GranLogiaAdmin)]
    [InlineData(InstitutionalRoles.RegimenInterior)]
    [InlineData(InstitutionalRoles.TallerTesoreria)]
    public void Neither_proposals_nor_reviews_allow_other_roles(string role)
    {
        var proposer = AssemblyTreasuryCertificationEventPolicy.BuildProposal(
            Actor("incorrecto", role), AssemblyId, Inspection(), Evidence());
        Assert.Null(proposer);
        var original = AssemblyTreasuryCertificationEventPolicy.BuildProposal(
            Actor("gt-original"), AssemblyId, Inspection(), Evidence())!;
        var review = AssemblyTreasuryCertificationEventPolicy.BuildIndependentReview(
            Actor("incorrecto", role), AssemblyId, Workshop, original,
            "reviewed", "EVIDENCIA", "Observación independiente");
        Assert.Null(review);
    }

    [Fact]
    public void Author_cannot_review_own_proposal_or_cross_workshop_or_assembly()
    {
        var original = AssemblyTreasuryCertificationEventPolicy.BuildProposal(
            Actor("gt-uno"), AssemblyId, Inspection(), Evidence())!;
        var self = AssemblyTreasuryCertificationEventPolicy.BuildIndependentReview(
            Actor("gt-uno"), AssemblyId, Workshop, original,
            "reviewed", "EVIDENCIA", "Mismo autor");
        Assert.Null(self);
        Assert.Null(AssemblyTreasuryCertificationEventPolicy.BuildIndependentReview(
            Actor("gt-dos"), Guid.NewGuid(), Workshop, original,
            "reviewed", "EVIDENCIA", "Asamblea ajena"));
        Assert.Null(AssemblyTreasuryCertificationEventPolicy.BuildIndependentReview(
            Actor("gt-dos"), AssemblyId, Guid.NewGuid(), original,
            "reviewed", "EVIDENCIA", "Taller ajeno"));
    }

    [Fact]
    public void Proposal_with_incomplete_documents_cannot_be_staged()
    {
        var altered = Evidence() with { EvidenceDigestSha256 = "" };
        Assert.Null(AssemblyTreasuryCertificationEventPolicy.BuildProposal(
            Actor("gt-uno"), AssemblyId, Inspection(), altered));
        Assert.Null(AssemblyTreasuryCertificationEventPolicy.BuildProposal(
            Actor("gt-uno"), Guid.Empty, Inspection(), Evidence()));
    }

    [Fact]
    public void Final_certificate_or_unsupported_review_action_does_not_exist()
    {
        var proposed = AssemblyTreasuryCertificationEventPolicy.BuildProposal(
            Actor("gt-uno"), AssemblyId, Inspection(), Evidence())!;
        Assert.Null(AssemblyTreasuryCertificationEventPolicy.BuildIndependentReview(
            Actor("gt-dos"), AssemblyId, Workshop, proposed,
            "approved_for_voting", "GT-EVID", "No habilitar"));
        var second = AssemblyTreasuryCertificationEventPolicy.BuildIndependentReview(
            Actor("gt-dos"), AssemblyId, Workshop, proposed,
            "rejected", "GT-EVID", "Observaciones de revisión");
        Assert.NotNull(second);
        Assert.Equal("rejected", second.Action);
    }
}
