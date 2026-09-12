using System.Security.Claims;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Membership;

public static class MemberProfilePrivacy
{
    public static bool CanReadContact(
        IInstitutionalAccessService access,
        ClaimsPrincipal user,
        IEnumerable<Guid> organizationIds)
    {
        if (access.HasOrderScope(user) &&
            access.HasRole(
                user,
                InstitutionalRoles.GranLogiaAdmin,
                InstitutionalRoles.RegimenInterior,
                InstitutionalRoles.PrivacyOfficer))
        {
            return true;
        }

        return organizationIds.Any(organizationId => access.CanManageOrganization(user, organizationId));
    }
}
