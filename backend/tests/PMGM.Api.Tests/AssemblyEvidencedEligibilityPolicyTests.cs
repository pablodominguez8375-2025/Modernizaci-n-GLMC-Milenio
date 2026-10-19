using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyEvidencedEligibilityPolicyTests
{
    private static readonly Guid Lodge = Guid.NewGuid();
    private static readonly DateOnly Cutoff = new(2026, 10, 19);

    private static AssemblyMemberCategoryDecision Category(bool qualifies = true) =>
        new(qualifies ? AssemblyMemberCategoryPolicy.Representative : null,
            qualifies, 0, qualifies, "Test sintético");

    private static AssemblyInstitutionalRightsResult Rights() =>
        new(true, true, true, true, true, true);

    private static AssemblyGrandTreasuryBalance Balance(decimal pending) =>
        new(Lodge, Cutoff, "grand_treasury", "GT-VERIFICADA",
            "CLP", pending, true);

    [Fact]
    public void Verified_rights_and_zero_balance_allow_suffrage()
    {
        var decision = AssemblyEvidencedEligibilityPolicy.Evaluate(
            Lodge, Cutoff, Category(), Rights(), Balance(0m), null);
        Assert.True(decision.MayVote);
        Assert.Equal("habilitado", decision.Status);
    }

    [Fact]
    public void Missing_institutional_coverage_remains_pending_even_if_treasury_zero()
    {
        var unknown = Rights() with { HonorAllowsSuffrage = null };
        var decision = AssemblyEvidencedEligibilityPolicy.Evaluate(
            Lodge, Cutoff, Category(), unknown, Balance(0m), null);
        Assert.False(decision.MayVote);
        Assert.Equal("pendiente", decision.Status);
    }

    [Fact]
    public void No_accredited_assembly_category_never_votes()
    {
        var decision = AssemblyEvidencedEligibilityPolicy.Evaluate(
            Lodge, Cutoff, Category(false), Rights(), Balance(0m), null);
        Assert.False(decision.MayVote);
        Assert.Equal("inhabilitado", decision.Status);
    }

    [Fact]
    public void Gran_treasury_positive_debt_prohibits_vote_even_without_sanctions()
    {
        var decision = AssemblyEvidencedEligibilityPolicy.Evaluate(
            Lodge, Cutoff, Category(), Rights(), Balance(0.01m), null);
        Assert.False(decision.MayVote);
        Assert.Equal("solo_asistencia", decision.Status);
    }
}
