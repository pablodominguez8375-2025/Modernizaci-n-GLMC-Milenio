using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Membership;

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

    private static async Task<IResult> GetCurrentSession(
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver memberContextResolver,
        PmgmDbContext db,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";
        var profile = SessionProfileBuilder.Build(httpContext.User, access);
        var organizationSet = httpContext.User.Claims
            .Where(x => x.Type == InstitutionalClaims.Organization && Guid.TryParse(x.Value, out _))
            .Select(x => Guid.Parse(x.Value))
            .Distinct()
            .ToHashSet();
        var memberContext = await memberContextResolver.ResolveAsync(httpContext.User, cancellationToken);
        if (memberContext is { EffectiveDegree: 3 })
        {
            var delegatedOrganizations = await (from grant in db.LodgeSummaryAccessGrants.AsNoTracking()
                                                join membership in db.Memberships.AsNoTracking() on grant.MemberId equals membership.MemberId
                                                where grant.MemberId == memberContext.MemberId && grant.RevokedAtUtc == null &&
                                                      membership.OrganizationId == grant.OrganizationId &&
                                                      membership.Status == MembershipCodes.MembershipStatus.Active && membership.EndDate == null
                                                select grant.OrganizationId).Distinct().ToListAsync(cancellationToken);
            organizationSet.UnionWith(delegatedOrganizations);
        }
        var canReadLodgeSummary = false;
        var canManageLodgeSummaryAccess = false;
        foreach (var organizationId in organizationSet)
        {
            canReadLodgeSummary |= await LodgeSummaryAccessPolicy.CanReadAsync(
                httpContext.User, organizationId, db, access, memberContextResolver, cancellationToken);
            canReadLodgeSummary |= access.HasOrderScope(httpContext.User) && access.CanReadOrganization(httpContext.User, organizationId);
            canManageLodgeSummaryAccess |= access.CanManageLodgeCouncilSummaryAccess(httpContext.User, organizationId);
        }
        profile = profile with
        {
            Capabilities = profile.Capabilities with
            {
                CanReadLodgeCouncilSummary = canReadLodgeSummary,
                CanManageLodgeCouncilSummaryAccess = canManageLodgeSummaryAccess
            }
        };
        audit.Add(httpContext, "identity.session.started", "Session", httpContext.TraceIdentifier, null, AuditResults.Success,
            new { profile.AccessScope });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(profile);
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
    bool CanReadLodgeSecretariat,
    bool CanManageLodgeSecretariat,
    bool CanManageLodgeTreasury,
    bool CanReadLodgeHospitalaria,
    bool CanManageLodgeHospitalaria,
    bool CanApproveLodgeExpenses,
    bool CanManageDocuments,
    bool CanReadLibrary,
    bool CanManagePrivacy,
    bool CanConfigureSystem,
    bool CanReadLodgeCouncilSummary,
    bool CanManageLodgeCouncilSummaryAccess,
    bool CanManageAnyWorkshopProfile,
    bool CanManageApprenticeInstruction,
    bool CanManageFellowcraftInstruction,
    bool CanManageMasterInstruction,
    bool CanReadOrderApprenticeInstructions,
    bool CanReadOrderFellowcraftInstructions,
    bool CanReadOrderMasterInstructions,
    bool CanReadAllOrderInstructions);

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
                CanReadLodgeSecretariat: organizationId is not null && access.CanReadLodgeSecretariat(user, organizationId.Value),
                CanManageLodgeSecretariat: organizationId is not null && access.CanManageLodgeSecretariat(user, organizationId.Value),
                CanManageLodgeTreasury: organizationId is not null && access.CanManageLodgeTreasury(user, organizationId.Value),
                CanReadLodgeHospitalaria: organizationId is not null && access.CanReadLodgeHospitalaria(user, organizationId.Value),
                CanManageLodgeHospitalaria: organizationId is not null && access.CanManageLodgeHospitalaria(user, organizationId.Value),
                CanApproveLodgeExpenses: organizationId is not null && access.CanApproveLodgeExpenses(user, organizationId.Value),
                CanManageDocuments: canManageDocuments,
                CanReadLibrary: user.Identity?.IsAuthenticated == true,
                CanManagePrivacy: access.CanManagePrivacy(user),
                CanConfigureSystem: access.CanConfigureSystem(user),
                CanReadLodgeCouncilSummary: false,
                CanManageLodgeCouncilSummaryAccess: false,
                CanManageAnyWorkshopProfile: access.CanEditAnyWorkshopProfile(user),
                CanManageApprenticeInstruction: organizationId is not null && access.CanManageLodgeInstruction(user, organizationId.Value, 1),
                CanManageFellowcraftInstruction: organizationId is not null && access.CanManageLodgeInstruction(user, organizationId.Value, 2),
                CanManageMasterInstruction: organizationId is not null && access.CanManageLodgeInstruction(user, organizationId.Value, 3),
                CanReadOrderApprenticeInstructions: access.CanReadOrderLodgeInstructions(user, 1),
                CanReadOrderFellowcraftInstructions: access.CanReadOrderLodgeInstructions(user, 2),
                CanReadOrderMasterInstructions: access.CanReadOrderLodgeInstructions(user, 3),
                CanReadAllOrderInstructions: access.CanReadOrderLodgeInstructions(user, null)));
    }
}
