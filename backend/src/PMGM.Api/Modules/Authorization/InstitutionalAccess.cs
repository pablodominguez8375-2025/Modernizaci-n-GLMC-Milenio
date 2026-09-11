using System.Security.Claims;

namespace PMGM.Api.Modules.Authorization;

public static class InstitutionalClaims
{
    public const string Role = "pmgm_role";
    public const string Organization = "pmgm_org";
    public const string Scope = "pmgm_scope";
}

public static class InstitutionalRoles
{
    public const string PlatformSuperAdmin = "platform_superadmin";
    public const string GranLogiaAdmin = "grand_lodge_admin";
    public const string GranMaestria = "grand_master";
    public const string RegimenInterior = "internal_affairs";
    public const string GranSecretaria = "grand_secretariat";
    public const string GranTesoreria = "grand_treasury";
    public const string GranHospitalaria = "grand_hospitalaria";
    public const string GrandArchivist = "grand_archivist";
    public const string PrivacyOfficer = "privacy_officer";
    public const string DocumentManager = "document_manager";
    public const string TallerAdmin = "lodge_admin";
    public const string TallerSecretaria = "lodge_secretariat";
}

public interface IInstitutionalAccessService
{
    bool HasOrderScope(ClaimsPrincipal user);
    bool IsPlatformSuperAdmin(ClaimsPrincipal user);
    bool HasRole(ClaimsPrincipal user, params string[] roles);
    bool CanReadOrganization(ClaimsPrincipal user, Guid organizationId);
    bool CanManageOrganization(ClaimsPrincipal user, Guid organizationId);
    bool CanManageLodgeOperations(ClaimsPrincipal user);
    bool CanApproveTransfers(ClaimsPrincipal user);
    bool CanRunRegimenInteriorReports(ClaimsPrincipal user);
    bool CanManageGrandSecretariat(ClaimsPrincipal user);
    bool CanManageTreasuryRegularity(ClaimsPrincipal user);
    bool CanManageHospitalariaRegularity(ClaimsPrincipal user);
    bool CanManageGrandArchive(ClaimsPrincipal user);
    bool CanReadInstitutionalRegularity(ClaimsPrincipal user);
    bool CanEvaluateCeremonies(ClaimsPrincipal user);
    bool CanReviewCeremonies(ClaimsPrincipal user, Guid organizationId);
    bool CanValidateCeremonyInternalAffairs(ClaimsPrincipal user);
    bool CanProvideGrandMasterApproval(ClaimsPrincipal user);
    bool CanAuthorizeCeremonies(ClaimsPrincipal user);
    bool CanManageCandidatePublications(ClaimsPrincipal user, Guid organizationId);
    bool CanManageDocuments(ClaimsPrincipal user, Guid? organizationId);
    bool CanReadOrganizationLibrary(ClaimsPrincipal user, Guid organizationId);
    bool CanManagePrivacy(ClaimsPrincipal user);
}

public sealed class InstitutionalAccessService : IInstitutionalAccessService
{
    public bool HasOrderScope(ClaimsPrincipal user)
        => user.Claims.Any(x =>
            x.Type == InstitutionalClaims.Scope &&
            string.Equals(x.Value, "order", StringComparison.OrdinalIgnoreCase));

    public bool IsPlatformSuperAdmin(ClaimsPrincipal user)
        => HasOrderScope(user) && user.Claims.Any(x =>
            x.Type == InstitutionalClaims.Role &&
            string.Equals(x.Value, InstitutionalRoles.PlatformSuperAdmin, StringComparison.OrdinalIgnoreCase));

    public bool HasRole(ClaimsPrincipal user, params string[] roles)
    {
        if (IsPlatformSuperAdmin(user)) return true;

        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return user.Claims.Any(x =>
            x.Type == InstitutionalClaims.Role && roleSet.Contains(x.Value));
    }

    public bool CanReadOrganization(ClaimsPrincipal user, Guid organizationId)
    {
        if (HasOrderScope(user) && HasRole(
                user,
                InstitutionalRoles.GranLogiaAdmin,
                InstitutionalRoles.GranMaestria,
                InstitutionalRoles.RegimenInterior,
                InstitutionalRoles.GranSecretaria,
                InstitutionalRoles.GranTesoreria,
                InstitutionalRoles.GranHospitalaria,
                InstitutionalRoles.GrandArchivist,
                InstitutionalRoles.PrivacyOfficer))
        {
            return true;
        }

        return HasOrganizationClaim(user, organizationId);
    }

    public bool CanManageOrganization(ClaimsPrincipal user, Guid organizationId)
    {
        if (HasOrderScope(user) && HasRole(user, InstitutionalRoles.GranLogiaAdmin))
        {
            return true;
        }

        return HasOrganizationClaim(user, organizationId) &&
               HasRole(user, InstitutionalRoles.TallerAdmin, InstitutionalRoles.TallerSecretaria);
    }

    public bool CanManageLodgeOperations(ClaimsPrincipal user)
        => (HasOrderScope(user) && HasRole(user, InstitutionalRoles.GranLogiaAdmin)) ||
           (HasRole(user, InstitutionalRoles.TallerAdmin, InstitutionalRoles.TallerSecretaria) &&
            user.Claims.Any(x => x.Type == InstitutionalClaims.Organization && Guid.TryParse(x.Value, out _)));

    public bool CanApproveTransfers(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.RegimenInterior);

    public bool CanRunRegimenInteriorReports(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.RegimenInterior);

    public bool CanManageGrandSecretariat(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.GranSecretaria);

    public bool CanManageTreasuryRegularity(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.GranTesoreria);

    public bool CanManageHospitalariaRegularity(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.GranHospitalaria);

    public bool CanManageGrandArchive(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.GrandArchivist);

    public bool CanReadInstitutionalRegularity(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(
               user,
               InstitutionalRoles.GranLogiaAdmin,
               InstitutionalRoles.GranMaestria,
               InstitutionalRoles.RegimenInterior,
               InstitutionalRoles.GranSecretaria,
               InstitutionalRoles.GranTesoreria,
               InstitutionalRoles.GranHospitalaria);

    public bool CanEvaluateCeremonies(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(
               user,
               InstitutionalRoles.GranLogiaAdmin,
               InstitutionalRoles.GranMaestria,
               InstitutionalRoles.RegimenInterior,
               InstitutionalRoles.GranSecretaria);

    public bool CanReviewCeremonies(ClaimsPrincipal user, Guid organizationId)
    {
        if (CanEvaluateCeremonies(user))
        {
            return true;
        }

        return HasOrganizationClaim(user, organizationId) &&
               HasRole(user, InstitutionalRoles.TallerAdmin, InstitutionalRoles.TallerSecretaria);
    }

    public bool CanValidateCeremonyInternalAffairs(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.RegimenInterior);

    public bool CanProvideGrandMasterApproval(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.GranMaestria);

    public bool CanAuthorizeCeremonies(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.GranSecretaria);

    public bool CanManageCandidatePublications(ClaimsPrincipal user, Guid organizationId)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.GranSecretaria);

    public bool CanManageDocuments(ClaimsPrincipal user, Guid? organizationId)
    {
        if (HasOrderScope(user) &&
            HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.DocumentManager))
        {
            return true;
        }

        return organizationId is not null &&
               HasOrganizationClaim(user, organizationId.Value) &&
               HasRole(user, InstitutionalRoles.TallerAdmin, InstitutionalRoles.TallerSecretaria);
    }

    public bool CanReadOrganizationLibrary(ClaimsPrincipal user, Guid organizationId)
    {
        if (HasOrderScope(user) &&
            HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.DocumentManager))
        {
            return true;
        }

        return HasOrganizationClaim(user, organizationId);
    }

    public bool CanManagePrivacy(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.PrivacyOfficer);

    private static bool HasOrganizationClaim(ClaimsPrincipal user, Guid organizationId)
        => user.Claims.Any(x =>
            x.Type == InstitutionalClaims.Organization &&
            Guid.TryParse(x.Value, out var claimOrganizationId) &&
            claimOrganizationId == organizationId);
}
