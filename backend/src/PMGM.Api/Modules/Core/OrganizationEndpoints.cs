using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Core;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/institutional/organizations/options", GetOrganizationOptionsAsync)
            .WithTags("Organizaciones institucionales")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetOrganizationOptionsAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var rows = await db.Organizations
            .AsNoTracking()
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Number)
            .ThenBy(x => x.Name)
            .Select(x => new OrganizationOptionDto(x.Id, x.Name, x.Number, x.Type))
            .Take(2000)
            .ToListAsync(cancellationToken);

        var items = rows
            .Where(x => access.CanReadOrganization(httpContext.User, x.Id))
            .ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new OrganizationOptionsResponse(items.Count, items));
    }
}

public sealed record OrganizationOptionDto(
    Guid Id,
    string Name,
    string? Number,
    string Type);

public sealed record OrganizationOptionsResponse(
    int Total,
    IReadOnlyList<OrganizationOptionDto> Items);
