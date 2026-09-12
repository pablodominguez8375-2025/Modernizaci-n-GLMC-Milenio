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
    bool CanBootstrapInstitutional,
    bool CanApproveTransfers,
    bool CanRunRegimenInteriorReports,
    bool CanManageGrandSecretariat,
    bool CanManageTreasuryRegularity,
    bool CanManageHospitalariaRegularity,
    bool CanManageGrandArchive,
    bool CanEvaluateCeremonies,
    bool CanReviewCeremonies,
    bool CanValidateCeremonyInternalAffairs,
    bool CanAuthorizeCeremonies,
    bool CanManageLodgeOperations,
    bool CanManageDocuments,
    bool CanReadLibrary,
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

        var organizationId = user.Claims
            .Where(x => x.Type == InstitutionalClaims.Organization)
            .Select(x => Guid.TryParse(x.Value, out var id) ? id : (Guid?)null)
            .FirstOrDefault(x => x is not null);

        var accessScope = access.HasOrderScope(user)
            ? "order"
            : organizationId is not null
                ? "organization"
                : "authenticated";

        var canReviewCeremonies = access.CanEvaluateCeremonies(user) ||
                                  (access.HasRole(user, InstitutionalRoles.TallerAdmin, InstitutionalRoles.TallerSecretaria) &&
                                   organizationId is not null);

        var canManageDocuments = access.CanManageDocuments(user, null) ||
                                 (organizationId is not null && access.CanManageDocuments(user, organizationId.Value));

        return new SessionProfileDto(
            DisplayName: displayName.Trim(),
            AccessScope: accessScope,
            Capabilities: new SessionCapabilitiesDto(
                CanBootstrapInstitutional: access.IsPlatformSuperAdmin(user),
                CanApproveTransfers: access.CanApproveTransfers(user),
                CanRunRegimenInteriorReports: access.CanRunRegimenInteriorReports(user),
                CanManageGrandSecretariat: access.CanManageGrandSecretariat(user),
                CanManageTreasuryRegularity: access.CanManageTreasuryRegularity(user),
                CanManageHospitalariaRegularity: access.CanManageHospitalariaRegularity(user),
                CanManageGrandArchive: access.CanManageGrandArchive(user),
                CanEvaluateCeremonies: access.CanEvaluateCeremonies(user),
                CanReviewCeremonies: canReviewCeremonies,
                CanValidateCeremonyInternalAffairs: access.CanValidateCeremonyInternalAffairs(user),
                CanAuthorizeCeremonies: access.CanAuthorizeCeremonies(user),
                CanManageLodgeOperations: access.CanManageLodgeOperations(user),
                CanManageDocuments: canManageDocuments,
                CanReadLibrary: user.Identity?.IsAuthenticated == true,
                CanManagePrivacy: access.CanManagePrivacy(user)));
    }
}
