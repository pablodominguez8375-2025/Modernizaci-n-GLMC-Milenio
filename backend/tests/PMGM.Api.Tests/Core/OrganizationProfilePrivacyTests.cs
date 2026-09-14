using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class OrganizationProfilePrivacyTests
{
    private readonly InstitutionalAccessService access = new();
    private static readonly Guid OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Ordinary_reader_does_not_receive_regularity_projection()
    {
        var user = Principal(new Claim(InstitutionalClaims.Organization, OrganizationId.ToString()));
        Assert.False(OrganizationProfilePrivacy.CanReadRegularity(access, user, OrganizationId));
    }

    [Fact]
    public void Lodge_secretariat_receives_regularity_for_its_lodge()
    {
        var user = Principal(
            new Claim(InstitutionalClaims.Organization, OrganizationId.ToString()),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSecretaria));
        Assert.True(OrganizationProfilePrivacy.CanReadRegularity(access, user, OrganizationId));
    }

    [Fact]
    public void Treasury_order_role_receives_regularity_projection()
    {
        var user = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranTesoreria));
        Assert.True(OrganizationProfilePrivacy.CanReadRegularity(access, user, OrganizationId));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));
}
