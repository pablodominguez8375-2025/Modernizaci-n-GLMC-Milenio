using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class LodgeManagementAuthorizationTests
{
    private readonly InstitutionalAccessService access = new();

    [Fact]
    public void Workshop_secretariat_can_manage_only_with_organization_scope()
    {
        var organizationId = Guid.NewGuid();
        var principal = Principal(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSecretaria),
            new Claim(InstitutionalClaims.Organization, organizationId.ToString()));

        Assert.True(access.CanManageLodgeOperations(principal));
        Assert.True(access.CanManageOrganization(principal, organizationId));
        Assert.False(access.CanManageOrganization(principal, Guid.NewGuid()));
    }

    [Fact]
    public void RegimenInterior_does_not_inherit_lodge_minutes_or_attendance_access()
    {
        var principal = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior));

        Assert.False(access.CanManageLodgeOperations(principal));
        Assert.False(access.CanManageOrganization(principal, Guid.NewGuid()));
    }

    [Fact]
    public void Grand_lodge_admin_can_manage_lodge_operations()
    {
        var principal = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin));

        Assert.True(access.CanManageLodgeOperations(principal));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));
}
