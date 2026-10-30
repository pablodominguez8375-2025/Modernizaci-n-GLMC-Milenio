using PMGM.Api.Modules.Core;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>#812: Taller 00 es una identidad técnica, no una logia.</summary>
public sealed class SupportWorkshopPolicyTests
{
    [Theory]
    [InlineData("support", "00", true)]
    [InlineData("SUPPORT", "00", true)]
    [InlineData("support", "0", false)]
    [InlineData("support", "23", false)]
    [InlineData("workshop", "00", false)]
    [InlineData("grand_lodge", "00", false)]
    [InlineData(null, "00", false)]
    [InlineData("support", null, false)]
    public void Support_workshop_is_identified_by_type_and_reserved_number(
        string? type, string? number, bool expected)
        => Assert.Equal(expected, SupportWorkshopPolicy.IsConfiguredSupportWorkshop(type, number));

    [Fact]
    public void Support_organization_is_always_excluded_from_institutional_directory()
    {
        Assert.False(SupportWorkshopPolicy.IncludeInInstitutionalDirectory("support"));
        Assert.False(SupportWorkshopPolicy.IncludeInInstitutionalDirectory("SUPPORT"));
        Assert.True(SupportWorkshopPolicy.IncludeInInstitutionalDirectory("workshop"));
    }
}
