using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Core;

public static class LodgeSummaryAccessEndpoints
{
    public static IEndpointRouteBuilder MapLodgeSummaryAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/institutional/organizations/{organizationId:guid}/summary-access")
            .WithTags("Resumen del Taller").RequireAuthorization();

        group.MapGet("", GetSummaryAccessAsync);
        group.MapPost("", GrantSummaryAccessAsync);
        group.MapDelete("/{grantId:guid}", RevokeSummaryAccessAsync);
        return endpoints;
    }

    private static async Task<IResult> GetSummaryAccessAsync(
        Guid organizationId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeCouncilSummaryAccess(httpContext.User, organizationId)) return Results.Forbid();

        var activeMemberIds = await db.Memberships.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Status == MembershipCodes.MembershipStatus.Active && x.EndDate == null)
            .Select(x => x.MemberId).Distinct().ToListAsync(cancellationToken);

        var degreeRows = new List<LodgeSummaryDegreeRow>();
        if (activeMemberIds.Count > 0)
        {
            degreeRows = await db.DegreeEvents.AsNoTracking()
                .Where(x => activeMemberIds.Contains(x.MemberId) && x.EffectiveDate <= DateOnly.FromDateTime(DateTime.UtcNow))
                .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.RecordedAtUtc)
                .Select(x => new LodgeSummaryDegreeRow(x.MemberId, x.Degree))
                .ToListAsync(cancellationToken);
        }

        var masterIds = degreeRows.GroupBy(x => x.MemberId)
            .Where(group => InstitutionalDegree.TryParse(group.First().Degree, out var degree) && degree == 3)
            .Select(group => group.Key).ToArray();

        var activeGrants = await db.LodgeSummaryAccessGrants.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.RevokedAtUtc == null)
            .OrderBy(x => x.GrantedAtUtc)
            .Select(x => new { x.Id, x.MemberId, x.GrantedAtUtc, x.GrantReason })
            .ToListAsync(cancellationToken);
        var lookupIds = masterIds.Union(activeGrants.Select(x => x.MemberId)).Distinct().ToArray();
        var names = lookupIds.Length == 0
            ? new List<LodgeSummaryMemberName>()
            : await db.Members.AsNoTracking().Where(x => lookupIds.Contains(x.Id))
                .Select(x => new LodgeSummaryMemberName(x.Id, x.Person.FirstNames + " " + x.Person.LastNames))
                .OrderBy(x => x.DisplayName).ToListAsync(cancellationToken);
        var nameById = names.ToDictionary(x => x.Id, x => x.DisplayName);
        var eligibleIds = masterIds.ToHashSet();
        var eligible = names.Where(x => eligibleIds.Contains(x.Id)).Select(x => new LodgeSummaryMasterDto(x.Id, x.DisplayName)).ToArray();
        var grants = activeGrants.Where(x => nameById.ContainsKey(x.MemberId))
            .Select(x => new LodgeSummaryGrantDto(x.Id, x.MemberId, nameById[x.MemberId], x.GrantedAtUtc,
                x.GrantReason, eligibleIds.Contains(x.MemberId))).ToArray();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LodgeSummaryAccessResponse(eligible, grants));
    }

    private static async Task<IResult> GrantSummaryAccessAsync(
        Guid organizationId,
        LodgeSummaryGrantRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeCouncilSummaryAccess(httpContext.User, organizationId)) return Results.Forbid();
        var reason = request.Reason?.Trim();
        if (request.MemberId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return Results.BadRequest(new { message = "Seleccione un Maestro y registre un fundamento breve (máximo 500 caracteres)." });

        var activeMaster = await IsActiveMasterAsync(db, organizationId, request.MemberId, cancellationToken);
        if (!activeMaster)
            return Results.UnprocessableEntity(new { message = "La delegación sólo puede otorgarse a un Maestro activo del mismo Taller." });

        var existing = await db.LodgeSummaryAccessGrants.AnyAsync(x =>
            x.OrganizationId == organizationId && x.MemberId == request.MemberId && x.RevokedAtUtc == null,
            cancellationToken);
        if (existing) return Results.Conflict(new { message = "Este Hermano ya tiene acceso vigente al Resumen del Taller." });

        var subject = httpContext.User.FindFirst("sub")?.Value ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();

        var grant = new LodgeSummaryAccessGrant
        {
            OrganizationId = organizationId,
            MemberId = request.MemberId,
            GrantedBySubject = subject,
            GrantReason = reason
        };
        db.LodgeSummaryAccessGrants.Add(grant);
        audit.Add(httpContext, "lodge.summary_access.granted", "LodgeSummaryAccessGrant", grant.Id.ToString(), organizationId,
            AuditResults.Success, new { grant.Id });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Conflict(new { message = "Este Hermano ya tiene acceso vigente al Resumen del Taller." });
        }
        return Results.Created($"/api/institutional/organizations/{organizationId}/summary-access/{grant.Id}", new { grant.Id, grant.GrantedAtUtc });
    }

    private static async Task<IResult> RevokeSummaryAccessAsync(
        Guid organizationId,
        Guid grantId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeCouncilSummaryAccess(httpContext.User, organizationId)) return Results.Forbid();
        var grant = await db.LodgeSummaryAccessGrants.SingleOrDefaultAsync(x => x.Id == grantId && x.OrganizationId == organizationId, cancellationToken);
        if (grant is null) return Results.NotFound();
        if (grant.RevokedAtUtc is not null) return Results.Conflict(new { message = "La delegación ya fue revocada." });

        var subject = httpContext.User.FindFirst("sub")?.Value ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
        grant.RevokedBySubject = subject;
        grant.RevokedAtUtc = DateTimeOffset.UtcNow;
        audit.Add(httpContext, "lodge.summary_access.revoked", "LodgeSummaryAccessGrant", grant.Id.ToString(), organizationId,
            AuditResults.Success, new { grant.Id });
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<bool> IsActiveMasterAsync(PmgmDbContext db, Guid organizationId, Guid memberId, CancellationToken cancellationToken)
    {
        var active = await db.Memberships.AsNoTracking().AnyAsync(x => x.MemberId == memberId && x.OrganizationId == organizationId &&
            x.Status == MembershipCodes.MembershipStatus.Active && x.EndDate == null, cancellationToken);
        if (!active) return false;
        var degree = await db.DegreeEvents.AsNoTracking().Where(x => x.MemberId == memberId && x.EffectiveDate <= DateOnly.FromDateTime(DateTime.UtcNow))
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.RecordedAtUtc).Select(x => x.Degree).FirstOrDefaultAsync(cancellationToken);
        return InstitutionalDegree.TryParse(degree, out var numericDegree) && numericDegree == 3;
    }
}

public sealed record LodgeSummaryGrantRequest(Guid MemberId, string? Reason);
public sealed record LodgeSummaryMasterDto(Guid MemberId, string DisplayName);
public sealed record LodgeSummaryGrantDto(Guid Id, Guid MemberId, string DisplayName, DateTimeOffset GrantedAtUtc, string Reason, bool IsCurrentlyEligible);
public sealed record LodgeSummaryAccessResponse(IReadOnlyList<LodgeSummaryMasterDto> EligibleMasters, IReadOnlyList<LodgeSummaryGrantDto> ActiveGrants);
internal sealed record LodgeSummaryDegreeRow(Guid MemberId, string Degree);
internal sealed record LodgeSummaryMemberName(Guid Id, string DisplayName);
