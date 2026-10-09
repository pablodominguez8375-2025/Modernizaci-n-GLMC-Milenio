using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementRulePolicyTests
{
    [Theory]
    [InlineData(CeremonyCodes.Type.WageIncrease, AdvancementRulePolicy.ApprenticeCode)]
    [InlineData(CeremonyCodes.Type.Exaltation, AdvancementRulePolicy.FellowcraftCode)]
    public void CodeFor_UsesTheSourceDegree(string ceremonyType, string expected)
        => Assert.Equal(expected, AdvancementRulePolicy.CodeFor(ceremonyType));

    [Fact]
    public void Initiation_DoesNotHaveAnAdvancementThresholdRule()
        => Assert.Null(AdvancementRulePolicy.CodeFor(CeremonyCodes.Type.Initiation));

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    public void NegativeMinimums_AreRejected(int meetings, int instructions, int papers)
        => Assert.False(AdvancementRulePolicy.IsValid(new(meetings, instructions, papers)));

    [Theory]
    [InlineData("{}")]
    [InlineData("not JSON")]
    [InlineData("{\"minimumMeetingAttendance\":1,\"minimumInstructionAttendance\":2}")]
    [InlineData("{\"minimumMeetingAttendance\":-1,\"minimumInstructionAttendance\":2,\"minimumWorkPapers\":3}")]
    [InlineData("{\"minimumMeetingAttendance\":\"1\",\"minimumInstructionAttendance\":2,\"minimumWorkPapers\":3}")]
    public void InvalidOrIncompleteRule_IsNotAccepted(string json)
        => Assert.False(AdvancementRulePolicy.TryParse(json, out _));

    [Fact]
    public void ConfigurationRoundTrip_PreservesAllThreeMinimums()
    {
        var original = new AdvancementThresholdConfiguration(8, 4, 2);
        Assert.True(AdvancementRulePolicy.TryParse(AdvancementRulePolicy.Serialize(original), out var parsed));
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void NoEffectiveInstitutionalRule_DoesNotFallbackToDemoThresholds()
    {
        Assert.Null(AdvancementRulePolicy.Resolve(
            CeremonyCodes.Type.WageIncrease,
            new DateOnly(2026, 10, 9),
            Array.Empty<InstitutionalRuleSetting>()));
    }

    [Fact]
    public void EffectiveDates_SelectHistoricRuleWithoutRewritingIt()
    {
        var oldRule = CreateRule(
            CeremonyCodes.Type.WageIncrease,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 9, 30),
            new(4, 2, 1),
            "ACTA-2026-01");
        var newRule = CreateRule(
            CeremonyCodes.Type.WageIncrease,
            new DateOnly(2026, 10, 1),
            null,
            new(6, 3, 2),
            "ACTA-2026-10");
        var rules = new[] { oldRule, newRule };

        var historical = AdvancementRulePolicy.Resolve(CeremonyCodes.Type.WageIncrease, new DateOnly(2026, 9, 9), rules);
        var current = AdvancementRulePolicy.Resolve(CeremonyCodes.Type.WageIncrease, new DateOnly(2026, 10, 9), rules);

        Assert.NotNull(historical);
        Assert.NotNull(current);
        Assert.Equal(oldRule.Id, historical.RuleId);
        Assert.Equal(newRule.Id, current.RuleId);
        Assert.Equal(4, historical.Thresholds.MinimumMeetingAttendance);
        Assert.Equal(6, current.Thresholds.MinimumMeetingAttendance);
        Assert.NotEqual(historical.Thresholds.RuleVersion, current.Thresholds.RuleVersion);
        Assert.Equal("ACTA-2026-10", current.SourceReference);
    }

    [Fact]
    public void OverlappingEffectiveRules_FailClosed()
    {
        var a = CreateRule(CeremonyCodes.Type.Exaltation, new DateOnly(2026, 1, 1), null, new(4, 3, 2), "A");
        var b = CreateRule(CeremonyCodes.Type.Exaltation, new DateOnly(2026, 3, 1), null, new(5, 3, 2), "B");

        Assert.Null(AdvancementRulePolicy.Resolve(
            CeremonyCodes.Type.Exaltation, new DateOnly(2026, 9, 1), new[] { a, b }));
    }

    [Fact]
    public void MissingFormalResolution_AndInactiveRules_AreRejected()
    {
        var missing = CreateRule(CeremonyCodes.Type.Exaltation, new DateOnly(2026, 1, 1), null, new(1, 1, 1), " ");
        Assert.Null(AdvancementRulePolicy.Resolve(CeremonyCodes.Type.Exaltation, new DateOnly(2026, 9, 1), new[] { missing }));

        missing.SourceReference = "RES-001";
        missing.Status = "retired";
        Assert.Null(AdvancementRulePolicy.Resolve(CeremonyCodes.Type.Exaltation, new DateOnly(2026, 9, 1), new[] { missing }));
    }

    private static InstitutionalRuleSetting CreateRule(
        string ceremonyType,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        AdvancementThresholdConfiguration thresholds,
        string source)
        => new()
        {
            Code = AdvancementRulePolicy.CodeFor(ceremonyType)!,
            Value = AdvancementRulePolicy.Serialize(thresholds),
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            Status = "active",
            SourceReference = source
        };
}
