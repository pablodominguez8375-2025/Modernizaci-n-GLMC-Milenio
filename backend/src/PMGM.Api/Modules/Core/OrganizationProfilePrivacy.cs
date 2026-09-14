using System.Security.Claims;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Core;

public static class OrganizationProfilePrivacy
{
    public static bool CanReadRegularity(
        IInstitutionalAccessService access,
        ClaimsPrincipal user,
        Guid organizationId)
        => access.CanReadInstitutionalRegularity(user) ||
           access.CanManageOrganization(user, organizationId);
}
