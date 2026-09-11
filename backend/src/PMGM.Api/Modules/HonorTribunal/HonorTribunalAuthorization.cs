using System.Security.Claims;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.HonorTribunal;

public static class HonorTribunalAuthorization
{
    public const string Role = "honor_tribunal";

    public static bool CanManage(ClaimsPrincipal user, IInstitutionalAccessService access)
        => access.HasOrderScope(user) &&
           access.HasRole(user, InstitutionalRoles.GranLogiaAdmin, Role);

    public static bool CanReadInstitutionalEffect(ClaimsPrincipal user, IInstitutionalAccessService access)
        => access.HasOrderScope(user) &&
           access.HasRole(user,
               InstitutionalRoles.GranLogiaAdmin,
               InstitutionalRoles.RegimenInterior,
               Role);
}
