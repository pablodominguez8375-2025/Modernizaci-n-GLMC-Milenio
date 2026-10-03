using PMGM.Api.Modules.Admissions;
using Xunit;

namespace PMGM.Api.Tests.Admissions;

public sealed class WithdrawalLetterPolicyTests
{
    [Theory]
    [InlineData("2026-07-03", "2026-10-03", "simple")]
    [InlineData("2026-07-03", "2026-10-04", "activation")]
    [InlineData("2026-01-31", "2026-04-30", "simple")]
    [InlineData("2026-01-31", "2026-05-01", "activation")]
    [InlineData("2025-11-30", "2026-02-28", "simple")]
    [InlineData("2025-11-30", "2026-03-01", "activation")]
    [InlineData("2023-11-30", "2024-02-29", "simple")]
    [InlineData("2023-11-30", "2024-03-01", "activation")]
    [InlineData("2026-10-04", "2026-10-03", null)]
    [InlineData("9999-12-31", "9999-12-31", "simple")]
    public void Calendar_boundary_is_inclusive_and_future_is_invalid(string granted, string today, string? expected)
        => Assert.Equal(expected, WithdrawalLetterPolicy.ModeAtCreation(DateOnly.Parse(granted), DateOnly.Parse(today)));

    [Fact]
    public void Legacy_null_is_not_reclassified()
        => Assert.Null(WithdrawalLetterPolicy.ModeAtCreation(null, new DateOnly(2026, 10, 3)));
}
