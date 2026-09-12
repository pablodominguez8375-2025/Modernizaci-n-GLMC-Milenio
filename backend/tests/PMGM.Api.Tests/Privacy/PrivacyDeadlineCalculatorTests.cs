using PMGM.Api.Modules.Privacy;
using Xunit;

namespace PMGM.Api.Tests.Privacy;

public sealed class PrivacyDeadlineCalculatorTests
{
    [Theory]
    [InlineData(PrivacyCodes.DataSubjectRight.Access)]
    [InlineData(PrivacyCodes.DataSubjectRight.Rectification)]
    [InlineData(PrivacyCodes.DataSubjectRight.Erasure)]
    [InlineData(PrivacyCodes.DataSubjectRight.Objection)]
    [InlineData(PrivacyCodes.DataSubjectRight.Portability)]
    [InlineData(PrivacyCodes.DataSubjectRight.Blocking)]
    public void ResponseDaysCode_IsVersionableForEverySupportedRight(string right)
    {
        var code = PrivacyLegalRuleCodes.ResponseDays(right);

        Assert.True(PrivacyLegalRuleCodes.IsSupportedDeadlineCode(code));
        Assert.Equal(right, PrivacyLegalRuleCodes.ExtractRight(code));
    }

    [Fact]
    public void Calculate_AddsConfiguredCalendarDays()
    {
        var received = new DateOnly(2026, 9, 7);

        var due = PrivacyDeadlineCalculator.Calculate(received, 20);

        Assert.Equal(new DateOnly(2026, 9, 27), due);
    }

    [Fact]
    public void Calculate_RejectsNegativeDays()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrivacyDeadlineCalculator.Calculate(new DateOnly(2026, 9, 7), -1));

    [Fact]
    public void UnknownRuleCode_IsRejected()
        => Assert.False(PrivacyLegalRuleCodes.IsSupportedDeadlineCode("privacy.unknown.rule"));
}
