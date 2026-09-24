using PMGM.Api.Modules.Treasury;
using Xunit;

namespace PMGM.Api.Tests.Treasury;

public sealed class GrandTreasuryFeeScheduleTests
{
    [Theory]
    [InlineData("initiation", 41000)]
    [InlineData("wage_increase", 31000)]
    [InlineData("exaltation", 41000)]
    [InlineData("affiliation", 26000)]
    [InlineData("incorporation", 31000)]
    public void Resolves_decree_1759_ceremony_rights(string ceremonyType, decimal expected)
    {
        var right = GrandTreasuryFeeSchedule.ResolveCeremonyRight(ceremonyType, new DateOnly(2026, 9, 24));
        Assert.Equal((expected, "CLP"), right);
    }

    [Fact]
    public void Does_not_apply_ceremony_right_before_decree_effective_date()
    {
        Assert.Null(GrandTreasuryFeeSchedule.ResolveCeremonyRight("initiation", new DateOnly(2025, 12, 31)));
    }

    [Theory]
    [InlineData(TreasuryCodes.LodgeFeeType.Normal, GrandTreasuryFeeSchedule.Santiago, 21000)]
    [InlineData(TreasuryCodes.LodgeFeeType.Spouse, GrandTreasuryFeeSchedule.Santiago, 13000)]
    [InlineData(TreasuryCodes.LodgeFeeType.Senior, GrandTreasuryFeeSchedule.Santiago, 10000)]
    [InlineData(TreasuryCodes.LodgeFeeType.Student, GrandTreasuryFeeSchedule.Santiago, 8000)]
    [InlineData(TreasuryCodes.LodgeFeeType.Normal, GrandTreasuryFeeSchedule.OtherOriente, 15000)]
    [InlineData(TreasuryCodes.LodgeFeeType.Spouse, GrandTreasuryFeeSchedule.OtherOriente, 10000)]
    [InlineData(TreasuryCodes.LodgeFeeType.Senior, GrandTreasuryFeeSchedule.OtherOriente, 8000)]
    [InlineData(TreasuryCodes.LodgeFeeType.Student, GrandTreasuryFeeSchedule.OtherOriente, 8000)]
    public void Resolves_decree_1759_rates_by_fee_type_and_territory(string feeType, string territory, decimal expected)
    {
        var rate = GrandTreasuryFeeSchedule.Resolve(feeType, territory, new DateOnly(2026, 1, 1));

        Assert.Equal(expected, rate?.Amount);
        Assert.Equal("CLP", rate?.Currency);
    }

    [Fact]
    public void Past_active_has_no_grand_treasury_assessment()
    {
        var rate = GrandTreasuryFeeSchedule.Resolve(
            TreasuryCodes.LodgeFeeType.PastActive, GrandTreasuryFeeSchedule.Santiago, new DateOnly(2026, 9, 1));
        Assert.Equal(0m, rate?.Amount);
        Assert.Equal("CLP", rate?.Currency);
    }

    [Fact]
    public void Does_not_convert_peru_usd_tariff_to_clp()
    {
        Assert.Null(GrandTreasuryFeeSchedule.Resolve(
            TreasuryCodes.LodgeFeeType.Normal, GrandTreasuryFeeSchedule.Peru, new DateOnly(2026, 9, 1)));
        Assert.Null(GrandTreasuryFeeSchedule.Resolve(
            TreasuryCodes.LodgeFeeType.Spouse, GrandTreasuryFeeSchedule.Peru, new DateOnly(2026, 9, 1)));
    }
}
