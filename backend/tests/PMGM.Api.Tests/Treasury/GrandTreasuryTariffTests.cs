using PMGM.Api.Modules.Treasury;
using Xunit;
namespace PMGM.Api.Tests.Treasury;
public sealed class GrandTreasuryTariffTests
{
    private static readonly DateOnly Today = new(2026, 10, 4);
    [Fact]
    public void New_decree_changes_future_rates_and_ceremonies_without_repricing_history()
    {
        var original = GrandTreasuryTariffSeed.Load();
        var future = original with { Id = Guid.NewGuid(), Version = 2, EffectiveFrom = new(2026, 11, 1),
            Rates = [new("normal", "santiago", 25000, "CLP")], CeremonyRights = [new("initiation", "peru", 12.50m, "USD")] };
        var catalog = new[] { original, future };
        Assert.Equal(21000m, GrandTreasuryTariff.Resolve(catalog, "normal", "santiago", Today)?.Amount);
        Assert.Equal(25000m, GrandTreasuryTariff.Resolve(catalog, "normal", "santiago", future.EffectiveFrom)?.Amount);
        Assert.Null(GrandTreasuryTariff.Resolve(catalog, "spouse", "santiago", future.EffectiveFrom));
        Assert.Equal("USD", GrandTreasuryTariff.ResolveCeremonyRight(catalog, "initiation", "peru", future.EffectiveFrom)?.Currency);
        Assert.Equal(41000m, GrandTreasuryTariff.ResolveCeremonyRight(catalog, "initiation", "peru", Today)?.Amount);
        Assert.Equal(21000m, original.Rates.Single(x => x.Territory == "santiago" && x.FeeType == "normal").Amount);
    }
    [Fact]
    public void Draft_and_expired_version_do_not_silently_fall_back()
    {
        var original = GrandTreasuryTariffSeed.Load();
        var draft = original with { Version = 2, EffectiveFrom = Today, Status = "draft" };
        Assert.Equal(1, GrandTreasuryTariff.At([original, draft], Today)?.Version);
        var expired = draft with { Status = "published", EffectiveUntil = Today };
        Assert.Null(GrandTreasuryTariff.At([original, expired], Today.AddDays(1)));
        Assert.Null(GrandTreasuryTariff.At([], Today));
    }
    [Fact]
    public void Cesantia_is_percentage_discount_and_peru_has_no_invented_rates()
    {
        var initial = GrandTreasuryTariffSeed.Load();
        Assert.Equal(new[] { 100m, 75m, 50m, 25m }, initial.Unemployment.Where(x => x.Territory == "santiago").Select(x => x.DiscountPercent));
        Assert.Equal(new[] { 0m, 5250m, 10500m, 15750m }, initial.Unemployment.Where(x => x.Territory == "santiago").Select(x => x.Amount));
        Assert.DoesNotContain(initial.Unemployment, x => x.Territory == "peru");
    }
    [Theory]
    [InlineData("Santiago", "Chile", "santiago")]
    [InlineData("Valparaíso", "Chile", "other_chile")]
    [InlineData("Lima", "Perú", "peru")]
    [InlineData(null, "Chile", null)]
    [InlineData("Lima", "Otro", null)]
    public void Ficha_is_the_geographic_source(string? city, string? country, string? expected)
        => Assert.Equal(expected, WorkshopOriente.FromLocation(city, country));
    [Fact]
    public void Validate_blocks_retroactivity_duplicate_cells_and_non_exempt_past_active()
    {
        var seed = GrandTreasuryTariffSeed.Load();
        var request = new RegisterTariffRequest(1, "QA", Today, new(2026, 11, 1), null, "PDF QA", "published", seed.Rates, seed.CeremonyRights, seed.Unemployment);
        Assert.Null(GrandTreasuryTariff.Validate(request, Today));
        Assert.NotNull(GrandTreasuryTariff.Validate(request with { EffectiveFrom = Today }, Today));
        Assert.NotNull(GrandTreasuryTariff.Validate(request with { Rates = [seed.Rates[0], seed.Rates[0]] }, Today));
        Assert.NotNull(GrandTreasuryTariff.Validate(request with { Rates = [new("past_active", "peru", 1, "USD")] }, Today));
    }
}
