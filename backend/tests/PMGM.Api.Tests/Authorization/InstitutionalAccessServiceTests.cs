using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class InstitutionalAccessServiceTests
{
    private readonly InstitutionalAccessService _service = new();

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
