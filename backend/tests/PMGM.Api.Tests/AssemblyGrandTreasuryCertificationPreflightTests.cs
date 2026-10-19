using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyGrandTreasuryCertificationPreflightTests
{
    private static readonly Guid Lodge = Guid.Parse("78339133-5a9a-4d56-9da5-8dd3de536560");
    private static readonly DateOnly From = new(2026, 8, 1);
    private static readonly DateOnly Cutoff = new(2026, 10, 19);

    private static ClaimsPrincipal Principal(string role, bool orderScope = true, bool authenticated = true,
        bool hasSubject = true)
    {
        var claims = new List<Claim> { new(InstitutionalClaims.Role, role) };
        if (orderScope) claims.Add(new Claim(InstitutionalClaims.Scope, "order"));
        if (hasSubject) claims.Add(new Claim("sub", "gt-ficticio"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "TestAuthentication" : null));
    }

    private static AssemblyTreasuryInspectionResult Inspection(bool consistent = true) =>
        new(Lodge, From, Cutoff, 3, 3, consistent, 0m,
            ["HISTORICAL_CERTIFICATION_REQUIRED"]);

    private static AssemblyGrandTreasuryEvidenceProposal Proposal() =>
        new(Lodge, From, Cutoff, "CLP", From.AddDays(-1), 100m,
            "GT-APERTURA-FICTICIA", "GT-RESOLUCION-FICTICIA",
            "GT-RECTIFICACIONES-FICTICIAS", new string('a', 64),
            true, true, true);

    [Fact]
    public void Explicit_grand_treasury_role_can_propose_only_for_independent_review()
    {
        var actor = Principal(InstitutionalRoles.GranTesoreria);
        var result = AssemblyGrandTreasuryCertificationPreflight.Assess(actor, Inspection(), Proposal());
        Assert.True(AssemblyGrandTreasuryCertificationAuthority.HasExplicitGrandTreasuryAuthority(actor));
        Assert.True(result.DocumentsReadyForIndependentReview);
        Assert.False(result.MayAuthorizeSuffrage);
        Assert.Empty(result.BlockingReasons);
    }

    [Theory]
    [InlineData(InstitutionalRoles.PlatformSuperAdmin)]
    [InlineData(InstitutionalRoles.GranLogiaAdmin)]
    [InlineData(InstitutionalRoles.RegimenInterior)]
    [InlineData(InstitutionalRoles.TallerTesoreria)]
    [InlineData(InstitutionalRoles.TallerVenerable)]
    public void Only_explicit_grand_treasurer_can_propose_not_platform_bypass(string role)
    {
        var result = AssemblyGrandTreasuryCertificationPreflight.Assess(
            Principal(role), Inspection(), Proposal());
        Assert.False(result.DocumentsReadyForIndependentReview);
        Assert.False(result.MayAuthorizeSuffrage);
        Assert.Contains("NOT_EXPLICIT_GRAND_TREASURY_AUTHORITY", result.BlockingReasons);
    }

    [Fact]
    public void No_order_scope_subject_or_authenticated_identity_blocks()
    {
        var invalid = new[]
        {
            Principal(InstitutionalRoles.GranTesoreria, orderScope: false),
            Principal(InstitutionalRoles.GranTesoreria, authenticated: false),
            Principal(InstitutionalRoles.GranTesoreria, hasSubject: false)
        };
        foreach (var user in invalid)
            Assert.False(AssemblyGrandTreasuryCertificationPreflight.Assess(
                user, Inspection(), Proposal()).DocumentsReadyForIndependentReview);
    }

    [Fact]
    public void Wrong_workshop_cutoff_currency_opening_and_documents_are_rejected()
    {
        var original = Proposal();
        var altered = new[]
        {
            original with { OrganizationId = Guid.NewGuid() },
            original with { Cutoff = Cutoff.AddDays(-1) },
            original with { Currency = "CLP;USD" },
            original with { Currency = "clp" },
            original with { OpeningDebtAsOf = From },
            original with { OpeningPendingAmount = null },
            original with { OpeningPendingAmount = -1m },
            original with { OpeningDebtSourceReference = "" },
            original with { CertificationActReference = "" },
            original with { RectificationRegisterReference = "" },
            original with { EvidenceDigestSha256 = "sin-hash" },
            original with { CompleteDebtHistoryReviewed = false },
            original with { RectificationsReviewed = false },
            original with { PaymentsAndCurrencyReviewed = false },
            original with { CoverageFrom = DateOnly.MinValue }
        };
        foreach (var item in altered)
        {
            var result = AssemblyGrandTreasuryCertificationPreflight.Assess(
                Principal(InstitutionalRoles.GranTesoreria), Inspection(), item);
            Assert.False(result.DocumentsReadyForIndependentReview);
            Assert.False(result.MayAuthorizeSuffrage);
            Assert.NotEmpty(result.BlockingReasons);
        }
    }

    [Fact]
    public void Inspector_inconsistency_or_missing_rows_blocks_even_with_documents()
    {
        var actor = Principal(InstitutionalRoles.GranTesoreria);
        var corrupt = Inspection() with { BlockingReasons =
            ["MISSING_OR_OUT_OF_SCOPE_PERIOD", "HISTORICAL_CERTIFICATION_REQUIRED"] };
        var r = AssemblyGrandTreasuryCertificationPreflight.Assess(actor, corrupt, Proposal());
        Assert.Contains("MONTHLY_STATEMENTS_NOT_READY", r.BlockingReasons);
        Assert.False(r.DocumentsReadyForIndependentReview);

        var inconsistent = AssemblyGrandTreasuryCertificationPreflight.Assess(
            actor, Inspection(false), Proposal());
        Assert.False(inconsistent.DocumentsReadyForIndependentReview);

        var unbalanced = AssemblyGrandTreasuryCertificationPreflight.Assess(
            actor, Inspection() with { ObservedStatementDifference = 1m }, Proposal());
        Assert.False(unbalanced.DocumentsReadyForIndependentReview);
    }
}
