using PMGM.Api.Modules.Bootstrap.InitialLoadApply;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadOfficeRowPolicyTests
{
    private static InitialLoadOfficeRowPolicy.Row Ok() => new("P1", "L1", "worshipful_master", "2026-03-01", "2026");

    [Fact]
    public void Valid_row_keeps_the_explicit_period_and_strict_iso_date()
    {
        var r = InitialLoadOfficeRowPolicy.Inspect([Ok()]);
        Assert.True(r.Valid);
        Assert.Equal("2026", r.Rows.Single().Period);
        Assert.Equal(new DateOnly(2026, 3, 1), r.Rows.Single().StartDate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Period_is_never_derived_from_start_date(string period)
    {
        var r = InitialLoadOfficeRowPolicy.Inspect([Ok() with { Period = period }]);
        Assert.False(r.Valid);
        Assert.Contains(r.Findings, f => f.Code == "PERIOD_NOT_EXPLICIT");
    }

    [Theory]
    [InlineData("01-03-2026")]
    [InlineData("2026/03/01")]
    [InlineData("2026-02-30")]
    [InlineData("")]
    public void Start_date_must_be_strict_iso(string date)
    {
        var r = InitialLoadOfficeRowPolicy.Inspect([Ok() with { StartDate = date }]);
        Assert.False(r.Valid);
        Assert.Contains(r.Findings, f => f.Code == "INVALID_DATE");
    }

    [Fact]
    public void Duplicate_rows_block_and_empty_sheet_is_allowed()
    {
        Assert.False(InitialLoadOfficeRowPolicy.Inspect([Ok(), Ok()]).Valid);
        Assert.True(InitialLoadOfficeRowPolicy.Inspect([]).Valid);
    }
}
