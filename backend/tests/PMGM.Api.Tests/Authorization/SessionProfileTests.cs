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
    }

    [Fact]
    public void Venerable_ReceivesCommissionCapabilityWithoutSecretariatManagement()
    {
        var organizationId = Guid.NewGuid();
        var principal = Principal(
            new Claim(ClaimTypes.Name, "Venerable Maestro"),
            new Claim(InstitutionalClaims.Organization, organizationId.ToString()),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerVenerable));

        var profile = SessionProfileBuilder.Build(principal, new InstitutionalAccessService());

        Assert.True(profile.Capabilities.CanReadLodgeSecretariat);
        Assert.False(profile.Capabilities.CanManageLodgeSecretariat);
        Assert.True(profile.Capabilities.CanAppointAdmissionCommission);
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

    [Theory]
    [InlineData(InstitutionalRoles.TallerSegundoVigilante, true, false, false)]
    [InlineData(InstitutionalRoles.TallerPrimerVigilante, false, true, false)]
    [InlineData(InstitutionalRoles.TallerInmediatoExVenerable, false, false, true)]
    public void DocenciaCapabilities_AreExposedOnlyForTheOfficeDegree(
        string role,
        bool firstDegree,
        bool secondDegree,
        bool thirdDegree)
    {
        var organizationId = Guid.NewGuid();
        var principal = Principal(
            new Claim(InstitutionalClaims.Organization, organizationId.ToString()),
            new Claim(InstitutionalClaims.Role, role));

        var profile = SessionProfileBuilder.Build(principal, new InstitutionalAccessService());

        Assert.Equal(firstDegree, profile.Capabilities.CanManageLodgeInstructionFirstDegree);
        Assert.Equal(secondDegree, profile.Capabilities.CanManageLodgeInstructionSecondDegree);
        Assert.Equal(thirdDegree, profile.Capabilities.CanManageLodgeInstructionThirdDegree);
    }

    [Theory]
    [InlineData(InstitutionalRoles.TallerVenerable, true, false, false, false)]
    [InlineData(InstitutionalRoles.TallerTesoreria, false, true, false, false)]
    [InlineData(InstitutionalRoles.TallerOrador, false, false, true, false)]
    [InlineData(InstitutionalRoles.TallerSecretaria, false, false, false, true)]
    public void WithdrawalSignatureCapabilities_AreExposedOnlyForTheInstitutionalOffice(
        string role,
        bool venerable,
        bool treasurer,
        bool orator,
        bool secretary)
    {
        var organizationId = Guid.NewGuid();
        var principal = Principal(
            new Claim(InstitutionalClaims.Organization, organizationId.ToString()),
            new Claim(InstitutionalClaims.Role, role));

        var profile = SessionProfileBuilder.Build(principal, new InstitutionalAccessService());

        Assert.Equal(venerable, profile.Capabilities.CanSignWithdrawalAsVenerable);
        Assert.Equal(treasurer, profile.Capabilities.CanSignWithdrawalAsTreasurer);
        Assert.Equal(orator, profile.Capabilities.CanSignWithdrawalAsOrator);
        Assert.Equal(secretary, profile.Capabilities.CanSignWithdrawalAsSecretary);
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
