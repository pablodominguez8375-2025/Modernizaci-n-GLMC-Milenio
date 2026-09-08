using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class SessionProfileTests
{
    [Fact]
    public void GranSecretaria_OrderScope_ReceivesOnlyEffectiveCapabilities()
    {
        var principal = Principal(
            new Claim(ClaimTypes.Name, "Hermana Institucional"),
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranSecretaria));

        var profile = SessionProfileBuilder.Build(principal, new InstitutionalAccessService());

        Assert.Equal("Hermana Institucional", profile.DisplayName);
        Assert.Equal("order", profile.AccessScope);
        Assert.True(profile.Capabilities.CanManageGrandSecretariat);
        Assert.True(profile.Capabilities.CanEvaluateCeremonies);
        Assert.False(profile.Capabilities.CanRunRegimenInteriorReports);
        Assert.False(profile.Capabilities.CanManagePrivacy);
    }

    [Fact]
    public void ProfileSurface_DoesNotExposeRawRolesSubjectOrOrganizationIds()
    {
        var properties = typeof(SessionProfileDto).GetProperties().Select(x => x.Name).Order().ToArray();

        Assert.Equal(
            new[] { "AccessScope", "Capabilities", "DisplayName" },
            properties);
    }

    [Fact]
    public void WorkshopScopedUser_IsReportedAsOrganizationScope()
    {
        var principal = Principal(
            new Claim("name", "Secretaría de Taller"),
            new Claim(InstitutionalClaims.Organization, Guid.NewGuid().ToString()),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSecretaria));

        var profile = SessionProfileBuilder.Build(principal, new InstitutionalAccessService());

        Assert.Equal("organization", profile.AccessScope);
        Assert.False(profile.Capabilities.CanManageGrandSecretariat);
        Assert.False(profile.Capabilities.CanRunRegimenInteriorReports);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));
}
