using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class MemberProfilePrivacyTests
{
    private readonly InstitutionalAccessService access = new();
    private static readonly Guid OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Ordinary_organization_reader_does_not_receive_contact_data()
    {
        var user = Principal(
            new Claim(InstitutionalClaims.Organization, OrganizationId.ToString()));

        Assert.False(MemberProfilePrivacy.CanReadContact(access, user, [OrganizationId]));
    }

    [Fact]
    public void Lodge_secretariat_can_receive_contact_data_for_its_organization()
    {
        var user = Principal(
            new Claim(InstitutionalClaims.Organization, OrganizationId.ToString()),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSecretaria));

        Assert.True(MemberProfilePrivacy.CanReadContact(access, user, [OrganizationId]));
    }

    [Fact]
    public void Regimen_interior_with_order_scope_can_receive_contact_data()
    {
        var user = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior));

        Assert.True(MemberProfilePrivacy.CanReadContact(access, user, [OrganizationId]));
    }

    [Fact]
    public void Grand_secretariat_order_reader_does_not_receive_contact_data_by_default()
    {
        var user = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranSecretaria));

        Assert.False(MemberProfilePrivacy.CanReadContact(access, user, [OrganizationId]));
    }

    [Theory]
    [InlineData("10000013-k", "10.000.***-K")]
    [InlineData("10.000.013-K", "10.000.***-K")]
    [InlineData("12.345.678-5", "12.345.***-5")]
    [InlineData("98765433", "9.876.***-3")]
    public void Third_party_mask_preserves_only_prefix_and_verifier(string input, string expected)
    {
        var masked = MemberProfilePrivacy.MaskRutForThirdParty(input);
        Assert.Equal(expected, masked);
        Assert.DoesNotContain("10.000.013", masked);
        Assert.DoesNotContain("12.345.678", masked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12.345.678-K")]
    [InlineData("rut-institucional-no-chileno")]
    [InlineData("0-0")]
    public void Third_party_mask_does_not_publish_invalid_or_foreign_legacy_identity(string? input)
    {
        Assert.Null(MemberProfilePrivacy.MaskRutForThirdParty(input));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));
}
