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
    public const string GranLogiaAdmin = "grand_lodge_admin";
    public const string RegimenInterior = "internal_affairs";
    public const string GranSecretaria = "grand_secretariat";
    public const string GranTesoreria = "grand_treasury";
    public const string GranHospitalaria = "grand_hospitalaria";
    public const string TallerAdmin = "lodge_admin";
    public const string TallerSecretaria = "lodge_secretariat";
}

public interface IInstitutionalAccessService
{
    bool HasOrderScope(ClaimsPrincipal user);
    bool HasRole(ClaimsPrincipal user, params string[] roles);
    bool CanReadOrganization(ClaimsPrincipal user, Guid organizationId);
    bool CanManageOrganization(ClaimsPrincipal user, Guid organizationId);
    bool CanApproveTransfers(ClaimsPrincipal user);
    bool CanRunRegimenInteriorReports(ClaimsPrincipal user);
}

public sealed class InstitutionalAccessService : IInstitutionalAccessService
{
    public bool HasOrderScope(ClaimsPrincipal user)
        => user.Claims.Any(x =>
            x.Type == InstitutionalClaims.Scope &&
            string.Equals(x.Value, "order", StringComparison.OrdinalIgnoreCase));

    public bool HasRole(ClaimsPrincipal user, params string[] roles)
    {
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return user.Claims.Any(x =>
            x.Type == InstitutionalClaims.Role && roleSet.Contains(x.Value));
    }

    public bool CanReadOrganization(ClaimsPrincipal user, Guid organizationId)
    {
        if (HasOrderScope(user) && HasRole(
                user,
                InstitutionalRoles.GranLogiaAdmin,
                InstitutionalRoles.RegimenInterior,
                InstitutionalRoles.GranSecretaria,
                InstitutionalRoles.GranTesoreria,
                InstitutionalRoles.GranHospitalaria))
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

    public bool CanApproveTransfers(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.RegimenInterior);

    public bool CanRunRegimenInteriorReports(ClaimsPrincipal user)
        => HasOrderScope(user) &&
           HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.RegimenInterior);

    private static bool HasOrganizationClaim(ClaimsPrincipal user, Guid organizationId)
        => user.Claims.Any(x =>
            x.Type == InstitutionalClaims.Organization &&
            Guid.TryParse(x.Value, out var claimOrganizationId) &&
            claimOrganizationId == organizationId);
}
