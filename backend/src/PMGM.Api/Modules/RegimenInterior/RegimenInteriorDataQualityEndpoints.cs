using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.RegimenInterior;

public static class RegimenInteriorDataQualityEndpoints
{
    private static readonly HashSet<string> ValidSeverities = new(StringComparer.OrdinalIgnoreCase)
    {
        DataQualitySeverity.Error,
        DataQualitySeverity.Warning
    };

    public static IEndpointRouteBuilder MapRegimenInteriorDataQualityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/regimen-interior/data-quality", GetDataQualityAsync)
            .WithTags("Régimen Interior")
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetDataQualityAsync(
        DateOnly? asOf,
        Guid? organizationId,
        string? severity,
        string? code,
        string? search,
        int? limit,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IRegimenInteriorDataQualityService service,
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

        var normalizedSeverity = NormalizeOptional(severity);
        if (normalizedSeverity is not null && !ValidSeverities.Contains(normalizedSeverity))
        {
            return Results.BadRequest(new { message = "La severidad debe ser error o warning." });
        }

        var effectiveLimit = Math.Clamp(limit ?? 250, 1, 1000);
        var query = new DataQualityQuery(
            asOf ?? DateOnly.FromDateTime(DateTime.UtcNow),
            organizationId,
            normalizedSeverity,
            NormalizeOptional(code),
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            effectiveLimit);

        var response = await service.QueryAsync(query, cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(response);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
