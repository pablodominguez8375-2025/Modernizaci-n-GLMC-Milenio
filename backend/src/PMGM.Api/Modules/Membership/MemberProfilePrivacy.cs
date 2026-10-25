using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core;

namespace PMGM.Api.Modules.Membership;

public static class MemberProfilePrivacy
{
    /// <summary>
    /// Identificación mínima para fichas de terceros: no divulga cuerpo completo.
    /// Registros antiguos con DV erróneo no se proyectan a vistas ajenas.
    /// La consulta privada del propio titular usa un contrato distinto.
    /// </summary>
    public static string? MaskRutForThirdParty(string? raw)
    {
        if (!ChileanRut.TryNormalize(raw, out _, out var formatted)) return null;
        var separator = formatted.LastIndexOf('-');
        if (separator < 0) return null;
        var body = formatted[..separator];
        var visiblePrefix = body[..Math.Max(0, body.Length - 3)];
        return visiblePrefix + "***" + formatted[separator..];
    }

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
