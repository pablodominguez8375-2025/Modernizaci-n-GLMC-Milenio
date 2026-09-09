using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;
using Xunit;

namespace PMGM.Api.Tests.Membership;

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

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));
}
