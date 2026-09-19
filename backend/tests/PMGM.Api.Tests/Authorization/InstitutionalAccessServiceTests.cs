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
        Assert.True(_service.CanProvideGrandMasterApproval(user));
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
    public void AdmissionCommission_IsAppointedByVenerable_NotSecretariat()
    {
        var organization = Guid.NewGuid();
        var venerable = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerVenerable),
            new Claim(InstitutionalClaims.Organization, organization.ToString()));
        var secretaria = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSecretaria),
            new Claim(InstitutionalClaims.Organization, organization.ToString()));

        Assert.True(_service.CanAppointAdmissionCommission(venerable, organization));
        Assert.False(_service.CanManageLodgeSecretariat(venerable, organization));
        Assert.False(_service.CanAppointAdmissionCommission(secretaria, organization));
        Assert.True(_service.CanManageLodgeSecretariat(secretaria, organization));
    }

    [Fact]
    public void LodgeTreasurer_CanPrepareMonthlyStatementOnlyForOwnWorkshop()
    {
        var ownOrganization = Guid.NewGuid();
        var otherOrganization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerTesoreria),
            new Claim(InstitutionalClaims.Organization, ownOrganization.ToString()));

        Assert.True(_service.CanPrepareTreasuryStatement(user, ownOrganization));
        Assert.False(_service.CanPrepareTreasuryStatement(user, otherOrganization));
        Assert.False(_service.CanManageTreasuryRegularity(user));
    }

    [Fact]
    public void GrandTreasurer_ReviewsButDoesNotPrepareWorkshopStatement()
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranTesoreria));

        Assert.True(_service.CanManageTreasuryRegularity(user));
        Assert.False(_service.CanPrepareTreasuryStatement(user, organization));
    }

    [Fact]
    public void LodgeHospitalaria_CanManageAndReadOwnHospitalariaOnly()
    {
        var ownOrganization = Guid.NewGuid();
        var otherOrganization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerHospitalaria),
            new Claim(InstitutionalClaims.Organization, ownOrganization.ToString()));

        Assert.True(_service.CanManageLodgeHospitalaria(user, ownOrganization));
        Assert.True(_service.CanReadLodgeHospitalaria(user, ownOrganization));
        Assert.False(_service.CanManageLodgeHospitalaria(user, otherOrganization));
        Assert.False(_service.CanReadLodgeHospitalaria(user, otherOrganization));
        Assert.False(_service.CanApproveLodgeExpenses(user, ownOrganization));
    }

    [Fact]
    public void LodgeVenerable_ReadsAndApprovesHospitalariaWithoutEditingIt()
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerVenerable),
            new Claim(InstitutionalClaims.Organization, organization.ToString()));

        Assert.True(_service.CanReadLodgeHospitalaria(user, organization));
        Assert.True(_service.CanApproveLodgeExpenses(user, organization));
        Assert.False(_service.CanManageLodgeHospitalaria(user, organization));
    }

    [Fact]
    public void LodgeSecretary_DoesNotManageOrReadHospitalaria()
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSecretaria),
            new Claim(InstitutionalClaims.Organization, organization.ToString()));

        Assert.False(_service.CanManageLodgeHospitalaria(user, organization));
        Assert.False(_service.CanReadLodgeHospitalaria(user, organization));
        Assert.False(_service.CanApproveLodgeExpenses(user, organization));
    }

    [Fact]
    public void GrandHospitalaria_ManagesRegularityButCannotReadPrivateWorkshopAidLedger()
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranHospitalaria));

        Assert.True(_service.CanManageHospitalariaRegularity(user));
        Assert.False(_service.CanManageLodgeHospitalaria(user, organization));
        Assert.False(_service.CanReadLodgeHospitalaria(user, organization));
    }

    [Theory]
    [InlineData(InstitutionalRoles.TallerSegundoVigilante, 1, true)]
    [InlineData(InstitutionalRoles.TallerPrimerVigilante, 2, true)]
    [InlineData(InstitutionalRoles.TallerInmediatoExVenerable, 3, true)]
    [InlineData(InstitutionalRoles.TallerPrimerVigilante, 1, false)]
    [InlineData(InstitutionalRoles.TallerSegundoVigilante, 2, false)]
    [InlineData(InstitutionalRoles.TallerSecretaria, 3, false)]
    public void LodgeInstruction_UsesNormativeOfficerByDegree(string role, int degree, bool expected)
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, role),
            new Claim(InstitutionalClaims.Organization, organization.ToString()));

        Assert.Equal(expected, _service.CanManageLodgeInstruction(user, organization, degree));
    }

    [Fact]
    public void LodgeInstruction_CannotCrossOrganizationBoundary()
    {
        var ownOrganization = Guid.NewGuid();
        var otherOrganization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSegundoVigilante),
            new Claim(InstitutionalClaims.Organization, ownOrganization.ToString()));

        Assert.False(_service.CanManageLodgeInstruction(user, otherOrganization, 1));
    }

    [Theory]
    [InlineData(InstitutionalRoles.TallerVenerable)]
    [InlineData(InstitutionalRoles.TallerInmediatoExVenerable)]
    [InlineData(InstitutionalRoles.TallerPrimerVigilante)]
    [InlineData(InstitutionalRoles.TallerSegundoVigilante)]
    [InlineData(InstitutionalRoles.TallerOrador)]
    [InlineData(InstitutionalRoles.TallerSecretaria)]
    [InlineData(InstitutionalRoles.TallerTesoreria)]
    [InlineData(InstitutionalRoles.TallerHospitalaria)]
    public void LodgeCouncil_RecognizesEightInstitutionalOffices(string role)
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, role),
            new Claim(InstitutionalClaims.Organization, organization.ToString()));

        Assert.True(_service.CanParticipateInLodgeCouncil(user, organization));
    }

    [Fact]
    public void LodgeAdministratorSecurityProfile_IsNotAnInstitutionalCouncilOffice()
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin),
            new Claim(InstitutionalClaims.Organization, organization.ToString()));

        Assert.False(_service.CanParticipateInLodgeCouncil(user, organization));
    }

    [Fact]
    public void LodgeSecretariat_CannotApproveCandidatePublication()
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSecretaria),
            new Claim(InstitutionalClaims.Organization, organization.ToString()));

        Assert.False(_service.CanManageCandidatePublications(user, organization));
    }

    [Fact]
    public void RegimenInterior_CannotApproveCandidatePublication()
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior));

        Assert.False(_service.CanManageCandidatePublications(user, organization));
    }

    [Fact]
    public void GrandSecretariat_WithOrderScope_CanApproveCandidatePublication()
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranSecretaria));

        Assert.True(_service.CanManageCandidatePublications(user, organization));
    }

    [Fact]
    public void GrandSecretariat_WithoutOrderScope_CannotApproveCandidatePublication()
    {
        var organization = Guid.NewGuid();
        var user = CreateUser(new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranSecretaria));

        Assert.False(_service.CanManageCandidatePublications(user, organization));
    }

    [Fact]
    public void GrandMaster_WithOrderScope_CanProvideGrandMasterApproval()
    {
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranMaestria));

        Assert.True(_service.CanProvideGrandMasterApproval(user));
        Assert.True(_service.CanEvaluateCeremonies(user));
    }

    [Fact]
    public void GrandSecretariat_CannotProvideGrandMasterApproval()
    {
        var user = CreateUser(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranSecretaria));

        Assert.False(_service.CanProvideGrandMasterApproval(user));
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
