using PMGM.Api.Modules.Treasury;
using Xunit;
namespace PMGM.Api.Tests.Treasury;
public sealed class WorkshopOrienteTests
{
    [Theory]
    [InlineData("santiago", "Santiago", "Chile", "santiago")]
    [InlineData("other_chile", "Concepción", "Chile", "other_oriente")]
    [InlineData("peru", "Lima", "Perú", "peru")]
    [InlineData(null, " Valparaíso ", " Chile ", "other_oriente")]
    [InlineData("peru", "Santiago", "Chile", null)]
    [InlineData("other_chile", "Santiago", "Chile", null)]
    [InlineData("santiago", null, "Chile", null)]
    [InlineData(null, "Buenos Aires", "Argentina", null)]
    public void Derives_only_from_consistent_profile(string? code, string? city, string? country, string? expected)
        => Assert.Equal(expected, WorkshopOriente.Territory(code, city, country));
}
