using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class PrivacyAccessTests
{
    private readonly InstitutionalAccessService _service = new();

    [Fact]
    public void PrivacyOfficer_WithOrderScope_CanManagePrivacy()
    {
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.PrivacyOfficer));

        Assert.True(_service.CanManagePrivacy(user));
    }

    [Fact]
    public void PrivacyOfficer_WithoutOrderScope_CannotManagePrivacy()
    {
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.PrivacyOfficer));

        Assert.False(_service.CanManagePrivacy(user));
    }

    [Fact]
    public void LodgeAdministrator_CannotManagePrivacyRegister()
    {
        var user = CreateUser(
            new Claim(InstitutionalClaims.Organization, Guid.NewGuid().ToString()),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin));

        Assert.False(_service.CanManagePrivacy(user));
    }

    [Fact]
    public void GrandLodgeAdministrator_WithOrderScope_CanManagePrivacy()
    {
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin));

        Assert.True(_service.CanManagePrivacy(user));
    }

    private static ClaimsPrincipal CreateUser(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "test"));
}
