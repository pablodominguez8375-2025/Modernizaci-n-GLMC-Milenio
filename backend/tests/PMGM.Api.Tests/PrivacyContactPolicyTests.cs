using PMGM.Api.Modules.Privacy;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class PrivacyContactPolicyTests
{
    [Theory]
    [InlineData("juridico@granlogiamixta.cl", "juridico@granlogiamixta.cl")]
    [InlineData(" contacto@ejemplo.cl ", "contacto@ejemplo.cl")]
    public void Accepts_one_address(string input, string expected) =>
        Assert.Equal(expected, PrivacyContactPolicy.Normalize(input));

    [Theory]
    [InlineData("")]
    [InlineData("sin-arroba")]
    [InlineData("dos@ejemplo.cl, otro@ejemplo.cl")]
    [InlineData("dos@ejemplo.cl;otro@ejemplo.cl")]
    [InlineData("Persona <persona@ejemplo.cl>")]
    [InlineData("usuario@dominio")]
    [InlineData("usuario @ejemplo.cl")]
    public void Rejects_invalid_and_multiple_recipients(string input) =>
        Assert.Null(PrivacyContactPolicy.Normalize(input));

    [Fact]
    public void Rejects_address_longer_than_254_characters()
    {
        var value = new string('x', 246) + "@ejemplo.cl";
        Assert.True(value.Length > 254);
        Assert.Null(PrivacyContactPolicy.Normalize(value));
    }

    [Fact]
    public void Has_default_for_all_authenticated_members()
    {
        Assert.Equal("system.privacy.contact_email", PrivacyContactPolicy.Code);
        Assert.Equal("juridico@granlogiamixta.cl", PrivacyContactPolicy.DefaultEmail);
    }
}
