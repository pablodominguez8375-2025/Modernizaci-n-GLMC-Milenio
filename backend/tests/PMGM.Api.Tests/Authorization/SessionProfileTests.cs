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
        Assert.True(profile.Capabilities.CanManageAnyWorkshopProfile);
        Assert.True(profile.Capabilities.CanEvaluateCeremonies);
        Assert.True(profile.Capabilities.CanReviewCeremonies);
        Assert.False(profile.Capabilities.CanValidateCeremonyInternalAffairs);
        Assert.True(profile.Capabilities.CanAuthorizeCeremonies);
        Assert.False(profile.Capabilities.CanRunRegimenInteriorReports);
        Assert.False(profile.Capabilities.CanManagePrivacy);
    }

    [Fact]
    public void RegimenInterior_CanReviewAndValidateButCannotAuthorizeCeremonies()
    {
        var principal = Principal(
            new Claim(ClaimTypes.Name, "Régimen Interior"),
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior));

        var profile = SessionProfileBuilder.Build(principal, new InstitutionalAccessService());

        Assert.True(profile.Capabilities.CanEvaluateCeremonies);
        Assert.True(profile.Capabilities.CanReviewCeremonies);
        Assert.True(profile.Capabilities.CanValidateCeremonyInternalAffairs);
        Assert.True(profile.Capabilities.CanManageAnyWorkshopProfile);
        Assert.False(profile.Capabilities.CanAuthorizeCeremonies);
    }

    [Fact]
    public void WorkshopScopedUser_CanReviewOnlyItsCeremonyScope()
    {
        var organizationId = Guid.NewGuid();
        var principal = Principal(
            new Claim("name", "Secretaría de Taller"),
            new Claim(InstitutionalClaims.Organization, organizationId.ToString()),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSecretaria));
        var access = new InstitutionalAccessService();
        var profile = SessionProfileBuilder.Build(principal, access);

        Assert.Equal("organization", profile.AccessScope);
        Assert.True(profile.Capabilities.CanReviewCeremonies);
        Assert.False(profile.Capabilities.CanEvaluateCeremonies);
        Assert.False(profile.Capabilities.CanValidateCeremonyInternalAffairs);
        Assert.False(profile.Capabilities.CanAuthorizeCeremonies);
        Assert.True(access.CanReviewCeremonies(principal, organizationId));
        Assert.False(access.CanReviewCeremonies(principal, Guid.NewGuid()));
        Assert.False(profile.Capabilities.CanManageGrandSecretariat);
        Assert.False(profile.Capabilities.CanRunRegimenInteriorReports);
        Assert.True(profile.Capabilities.CanManageAnyWorkshopProfile);
    }

    [Fact]
    public void InstructionCapabilities_AreLimitedToTheResponsibleGrade()
    {
        var organizationId = Guid.NewGuid();
        var principal = Principal(
            new Claim(ClaimTypes.Name, "Segundo Vigilante"),
            new Claim(InstitutionalClaims.Organization, organizationId.ToString()),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSegundoVigilante));

        var capabilities = SessionProfileBuilder.Build(principal, new InstitutionalAccessService()).Capabilities;

        Assert.True(capabilities.CanManageApprenticeInstruction);
        Assert.False(capabilities.CanManageFellowcraftInstruction);
        Assert.False(capabilities.CanManageMasterInstruction);
    }

    [Theory]
    [InlineData(InstitutionalRoles.GranSegundoVigilante, 1, true)]
    [InlineData(InstitutionalRoles.GranSegundoVigilante, 2, false)]
    [InlineData(InstitutionalRoles.GranPrimerVigilante, 2, true)]
    [InlineData(InstitutionalRoles.GranPrimerVigilante, 1, false)]
    [InlineData(InstitutionalRoles.InmediatoExGranMaestro, 3, true)]
    [InlineData(InstitutionalRoles.InmediatoExGranMaestro, 1, false)]
    [InlineData(InstitutionalRoles.JefaturaDepartamentoDocencia, 1, true)]
    [InlineData(InstitutionalRoles.JefaturaDepartamentoDocencia, 2, true)]
    [InlineData(InstitutionalRoles.JefaturaDepartamentoDocencia, 3, true)]
    public void OrderInstructionReadAccess_IsReadOnlyAndGradeScoped(string role, int degree, bool expected)
    {
        var principal = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, role));
        var access = new InstitutionalAccessService();

        Assert.Equal(expected, access.CanReadOrderLodgeInstructions(principal, degree));
        Assert.False(access.CanManageLodgeInstruction(principal, Guid.NewGuid(), degree));
    }

    [Fact]
    public void InstructionDepartmentHead_CanReadAllGradesButCannotManageThem()
    {
        var principal = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.JefaturaDepartamentoDocencia));
        var capabilities = SessionProfileBuilder.Build(principal, new InstitutionalAccessService()).Capabilities;

        Assert.True(capabilities.CanReadAllOrderInstructions);
        Assert.True(capabilities.CanReadOrderApprenticeInstructions);
        Assert.True(capabilities.CanReadOrderFellowcraftInstructions);
        Assert.True(capabilities.CanReadOrderMasterInstructions);
        Assert.False(capabilities.CanManageApprenticeInstruction);
        Assert.False(capabilities.CanManageFellowcraftInstruction);
        Assert.False(capabilities.CanManageMasterInstruction);
    }

    [Fact]
    public void GranTesoreria_DoesNotGainCeremonyReviewCapabilityFromOrderScope()
    {
        var principal = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranTesoreria));

        var profile = SessionProfileBuilder.Build(principal, new InstitutionalAccessService());

        Assert.True(profile.Capabilities.CanManageTreasuryRegularity);
        Assert.False(profile.Capabilities.CanReviewCeremonies);
        Assert.False(profile.Capabilities.CanEvaluateCeremonies);
        Assert.False(profile.Capabilities.CanAuthorizeCeremonies);
    }

    [Fact]
    public void ProfileSurface_DoesNotExposeRawRolesSubjectOrOrganizationIds()
    {
        var properties = typeof(SessionProfileDto).GetProperties().Select(x => x.Name).Order().ToArray();

        Assert.Equal(
            new[] { "AccessScope", "Capabilities", "DisplayName" },
            properties);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));
}
