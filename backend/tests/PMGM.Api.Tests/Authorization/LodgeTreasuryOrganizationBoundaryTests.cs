using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class LodgeTreasuryOrganizationBoundaryTests
{
    private readonly InstitutionalAccessService _service = new();

    [Fact]
    public void LodgeTreasurer_CanManageTreasuryOnlyForClaimedWorkshop()
    {
        var ownWorkshop = Guid.NewGuid();
        var otherWorkshop = Guid.NewGuid();
        var treasurer = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerTesoreria),
            new Claim(InstitutionalClaims.Organization, ownWorkshop.ToString()));

        Assert.True(_service.CanManageLodgeTreasury(treasurer, ownWorkshop));
        Assert.False(_service.CanManageLodgeTreasury(treasurer, otherWorkshop));
        Assert.True(_service.CanPrepareTreasuryStatement(treasurer, ownWorkshop));
        Assert.False(_service.CanPrepareTreasuryStatement(treasurer, otherWorkshop));
        Assert.False(_service.CanApproveLodgeExpenses(treasurer, ownWorkshop));
        Assert.False(_service.CanApproveLodgeExpenses(treasurer, otherWorkshop));
    }

    [Fact]
    public void LodgeTreasurer_WithoutOrganizationClaim_CannotManageAnyWorkshop()
    {
        var treasurer = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerTesoreria));

        Assert.False(_service.CanManageLodgeTreasury(treasurer, Guid.NewGuid()));
        Assert.False(_service.CanPrepareTreasuryStatement(treasurer, Guid.NewGuid()));
    }

    private static ClaimsPrincipal CreateUser(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "test"));
}
