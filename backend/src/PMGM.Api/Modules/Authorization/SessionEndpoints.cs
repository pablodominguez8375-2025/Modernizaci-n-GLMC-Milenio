using System.Security.Claims;

namespace PMGM.Api.Modules.Authorization;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/session/me", GetCurrentSession)
            .WithTags("Sesión institucional")
            .RequireAuthorization();

        return endpoints;
    }

    private static IResult GetCurrentSession(
        HttpContext httpContext,
        IInstitutionalAccessService access)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";
        return Results.Ok(SessionProfileBuilder.Build(httpContext.User, access));
    }
}

public sealed record SessionProfileDto(
    string DisplayName,
    string AccessScope,
    SessionCapabilitiesDto Capabilities);

public sealed record SessionCapabilitiesDto(
    bool CanApproveTransfers,
    bool CanRunRegimenInteriorReports,
    bool CanManageGrandSecretariat,
    bool CanManageTreasuryRegularity,
    bool CanManageHospitalariaRegularity,
    bool CanEvaluateCeremonies,
    bool CanReviewCeremonies,
    bool CanValidateCeremonyInternalAffairs,
    bool CanAuthorizeCeremonies,
    bool CanManagePrivacy);

public static class SessionProfileBuilder
{
    public static SessionProfileDto Build(
        ClaimsPrincipal user,
        IInstitutionalAccessService access)
    {
        var displayName = user.Identity?.Name
            ?? user.FindFirstValue("name")
            ?? "Usuario institucional";

        var accessScope = access.HasOrderScope(user)
            ? "order"
            : user.Claims.Any(x =>
                x.Type == InstitutionalClaims.Organization &&
                Guid.TryParse(x.Value, out _))
                ? "organization"
                : "authenticated";

        var canReviewCeremonies = access.CanEvaluateCeremonies(user) ||
                                  (access.HasRole(user, InstitutionalRoles.TallerAdmin, InstitutionalRoles.TallerSecretaria) &&
                                   user.Claims.Any(x => x.Type == InstitutionalClaims.Organization && Guid.TryParse(x.Value, out _)));

        return new SessionProfileDto(
            DisplayName: displayName.Trim(),
            AccessScope: accessScope,
            Capabilities: new SessionCapabilitiesDto(
                CanApproveTransfers: access.CanApproveTransfers(user),
                CanRunRegimenInteriorReports: access.CanRunRegimenInteriorReports(user),
                CanManageGrandSecretariat: access.CanManageGrandSecretariat(user),
                CanManageTreasuryRegularity: access.CanManageTreasuryRegularity(user),
                CanManageHospitalariaRegularity: access.CanManageHospitalariaRegularity(user),
                CanEvaluateCeremonies: access.CanEvaluateCeremonies(user),
                CanReviewCeremonies: canReviewCeremonies,
                CanValidateCeremonyInternalAffairs: access.CanValidateCeremonyInternalAffairs(user),
                CanAuthorizeCeremonies: access.CanAuthorizeCeremonies(user),
                CanManagePrivacy: access.CanManagePrivacy(user)));
    }
}
