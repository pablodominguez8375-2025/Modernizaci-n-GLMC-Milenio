using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class InstitutionalAccessServiceTests
{
    private readonly InstitutionalAccessService _service = new();

    [Fact]
    public void PlatformSuperAdmin_WithOrderScope_HasAdministrativeBypass()
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin));

        Assert.True(_service.IsPlatformSuperAdmin(user));
        Assert.True(_service.HasRole(user, InstitutionalRoles.GranLogiaAdmin));
        Assert.True(_service.CanManageOrganization(user, organization));
        Assert.True(_service.CanApproveTransfers(user));
        Assert.True(_service.CanManageGrandSecretariat(user));
        Assert.True(_service.CanManageGrandArchive(user));
        Assert.True(_service.CanManagePrivacy(user));
    }

    [Fact]
    public void PlatformSuperAdmin_WithoutOrderScope_DoesNotBypassRoles()
    {
        var user = CreateUser(new Claim(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin));

        Assert.False(_service.IsPlatformSuperAdmin(user));
        Assert.False(_service.HasRole(user, InstitutionalRoles.GranLogiaAdmin));
    }

    [Fact]
    public void RegimenInterior_WithOrderScope_CanRunReports()
    {
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior));

        Assert.True(_service.CanRunRegimenInteriorReports(user));
    }

    [Fact]
    public void RegimenInterior_WithoutOrderScope_CannotRunReports()
    {
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior));

        Assert.False(_service.CanRunRegimenInteriorReports(user));
    }

    [Fact]
    public void LodgeSecretariat_CanManageOwnOrganizationOnly()
    {
        var ownOrganization = Guid.NewGuid();
        var otherOrganization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSecretaria),
            new Claim(InstitutionalClaims.Organization, ownOrganization.ToString()));

        Assert.True(_service.CanManageOrganization(user, ownOrganization));
        Assert.False(_service.CanManageOrganization(user, otherOrganization));
    }

    [Fact]
    public void GrandSecretariat_CannotApproveTransfers()
    {
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranSecretaria));

        Assert.False(_service.CanApproveTransfers(user));
    }

    [Fact]
    public void GrandLodgeAdministrator_WithOrderScope_CanApproveTransfers()
    {
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin));

        Assert.True(_service.CanApproveTransfers(user));
    }

    private static ClaimsPrincipal CreateUser(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "test"));
}
