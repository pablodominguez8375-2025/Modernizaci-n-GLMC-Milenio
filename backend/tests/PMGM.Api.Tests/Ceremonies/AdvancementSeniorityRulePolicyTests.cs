using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementSeniorityRulePolicyTests
{
    [Theory]
    [InlineData(CeremonyCodes.Type.WageIncrease, AdvancementSeniorityRulePolicy.ApprenticeCode)]
    [InlineData(CeremonyCodes.Type.Exaltation, AdvancementSeniorityRulePolicy.FellowcraftCode)]
    public void CodeIsBoundToSourceDegree(string ceremonyType, string expected)
        => Assert.Equal(expected, AdvancementSeniorityRulePolicy.CodeFor(ceremonyType));

    [Theory]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"minimumCompleteMonths\":0}")]
    [InlineData("{\"minimumCompleteMonths\":-1}")]
    [InlineData("{\"minimumCompleteMonths\":\"24\"}")]
    [InlineData("{\"minimumCompleteMonths\":1.25}")]
    public void InvalidMinimumsAreRejected(string input)
        => Assert.False(AdvancementSeniorityRulePolicy.TryParse(input, out _));

    [Fact]
    public void RoundTripDoesNotHardcodeTwoYears()
    {
        Assert.True(AdvancementSeniorityRulePolicy.TryParse(
            AdvancementSeniorityRulePolicy.Serialize(31), out var minimum));
        Assert.Equal(31, minimum);
    }

    [Fact]
    public void MissingInvalidAndOverlappingRulesFailClosed()
    {
        var asOf = new DateOnly(2026, 10, 9);
        Assert.Null(AdvancementSeniorityRulePolicy.Resolve(
            CeremonyCodes.Type.WageIncrease, asOf, []));
        Assert.Null(AdvancementSeniorityRulePolicy.Resolve(
            CeremonyCodes.Type.WageIncrease, asOf, [Setting(24, " ", new DateOnly(2026, 1, 1))]));
        Assert.Null(AdvancementSeniorityRulePolicy.Resolve(
            CeremonyCodes.Type.WageIncrease, asOf,
            [Setting(24, "RES-A", new DateOnly(2026, 1, 1)),
             Setting(20, "RES-B", new DateOnly(2026, 9, 1))]));
        var inactive = Setting(24, "RES", new DateOnly(2026, 1, 1));
        inactive.Status = "retired";
        Assert.Null(AdvancementSeniorityRulePolicy.Resolve(
            CeremonyCodes.Type.WageIncrease, asOf, [inactive]));
    }

    [Fact]
    public void VersionedRuleHonorsEffectiveDateWithoutRewritingHistory()
    {
        var old = Setting(24, "RES-OLD", new DateOnly(2026, 1, 1));
        old.EffectiveTo = new DateOnly(2026, 9, 30);
        var newer = Setting(28, "RES-NEW", new DateOnly(2026, 10, 1));
        var historical = AdvancementSeniorityRulePolicy.Resolve(
            CeremonyCodes.Type.WageIncrease, new DateOnly(2026, 9, 1), [old, newer]);
        var current = AdvancementSeniorityRulePolicy.Resolve(
            CeremonyCodes.Type.WageIncrease, new DateOnly(2026, 10, 9), [old, newer]);
        Assert.NotNull(historical);
        Assert.NotNull(current);
        Assert.Equal(24, historical.MinimumCompleteMonths);
        Assert.Equal(28, current.MinimumCompleteMonths);
        Assert.NotEqual(historical.RuleId, current.RuleId);
    }

    [Fact]
    public void ChronologicalMinimumReachedStillDoesNotCertifyContinuityOrAuthorize()
    {
        var review = AdvancementSeniorityRulePolicy.Review(
            Snapshot(24), Evidence(26, "membership_dates_covered", true, false));
        Assert.True(review.ChronologicalThresholdReached);
        Assert.Equal("threshold_reached_pending_certification", review.Status);
        Assert.False(review.InstitutionalContinuityCertified);
        Assert.False(review.AuthorizesCeremony);
    }

    [Theory]
    [InlineData("membership_gap", false, false)]
    [InlineData("institutional_interruption", true, true)]
    [InlineData("indeterminate", false, false)]
    public void MembershipGapsOrInterruptionsCannotPassReview(
        string status, bool covered, bool interrupted)
    {
        var review = AdvancementSeniorityRulePolicy.Review(
            Snapshot(24), Evidence(32, status, covered, interrupted));
        Assert.Equal("continuity_unverified", review.Status);
        Assert.False(review.ChronologicalThresholdReached);
        Assert.False(review.AuthorizesCeremony);
    }

    [Fact]
    public void IncompleteMonthsAndMissingRuleNeverAuthorize()
    {
        var under = AdvancementSeniorityRulePolicy.Review(
            Snapshot(24), Evidence(23, "membership_dates_covered", true, false));
        Assert.Equal("minimum_not_reached", under.Status);
        Assert.False(under.ChronologicalThresholdReached);
        var none = AdvancementSeniorityRulePolicy.Review(null, Evidence(30, "membership_dates_covered", true, false));
        Assert.Equal("institutional_rule_missing", none.Status);
        Assert.False(none.AuthorizesCeremony);
    }

    private static AdvancementSeniorityRuleSnapshot Snapshot(int minimum) =>
        new(Guid.NewGuid(), AdvancementSeniorityRulePolicy.ApprenticeCode,
            new DateOnly(2026, 1, 1), null, "RES-TEST", minimum);

    private static AdvancementSenioritySnapshot Evidence(int months, string status, bool covered, bool interrupted) =>
        new(new DateOnly(2024, 8, 9), new DateOnly(2026, 10, 9), months, status,
            "Prueba", 1, covered, interrupted);

    private static InstitutionalRuleSetting Setting(int minimum, string source, DateOnly from) =>
        new()
        {
            Code = AdvancementSeniorityRulePolicy.ApprenticeCode,
            Value = AdvancementSeniorityRulePolicy.Serialize(minimum),
            EffectiveFrom = from,
            Status = "active",
            SourceReference = source
        };
}
