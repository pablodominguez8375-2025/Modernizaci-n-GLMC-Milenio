using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Treasury;

namespace PMGM.Api.Modules.RegimenInterior;

public static class RegimenInteriorMemberControlEndpoints
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        MembershipCodes.InstitutionalStatus.Active,
        MembershipCodes.InstitutionalStatus.Inactive,
        MembershipCodes.InstitutionalStatus.PastActive,
        MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal,
        MembershipCodes.InstitutionalStatus.ForcedWithdrawal,
        MembershipCodes.InstitutionalStatus.Reinstated,
        MembershipCodes.InstitutionalStatus.Deceased
    };

    private static readonly HashSet<string> ValidFinancialStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        TreasuryCodes.RegularityStatus.UpToDate,
        TreasuryCodes.RegularityStatus.Delinquent,
        TreasuryCodes.RegularityStatus.Pending,
        TreasuryCodes.RegularityStatus.Exempt,
        "no_status"
    };

    public static IEndpointRouteBuilder MapRegimenInteriorMemberControlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/regimen-interior/members", GetMembersAsync)
            .WithTags("Régimen Interior")
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetMembersAsync(
        DateOnly? asOf,
        Guid? organizationId,
        string? status,
        string? degree,
        string? financialStatus,
        string? search,
        bool pastActiveOnly,
        bool pendingTransferOnly,
        int? limit,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IRegimenInteriorMemberControlService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User))
        {
            return Results.Forbid();
        }

        if (organizationId is not null)
        {
            var exists = await db.Organizations
                .AsNoTracking()
                .AnyAsync(x => x.Id == organizationId.Value && x.Type == "workshop", cancellationToken);
            if (!exists)
            {
                return Results.NotFound(new { message = "El Taller indicado no existe." });
            }
        }

        if (!string.IsNullOrWhiteSpace(status) && !ValidStatuses.Contains(status))
        {
            return Results.BadRequest(new { message = "El estado institucional solicitado no es válido." });
        }

        if (!string.IsNullOrWhiteSpace(financialStatus) && !ValidFinancialStatuses.Contains(financialStatus))
        {
            return Results.BadRequest(new { message = "El estado financiero solicitado no es válido." });
        }

        var effectiveLimit = Math.Clamp(limit ?? 250, 1, 1000);
        var query = new MemberControlQuery(
            asOf ?? DateOnly.FromDateTime(DateTime.UtcNow),
            organizationId,
            NormalizeOptional(status),
            NormalizeOptional(degree),
            NormalizeOptional(financialStatus),
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            pastActiveOnly,
            pendingTransferOnly,
            effectiveLimit);

        var response = await service.QueryAsync(query, cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(response);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
