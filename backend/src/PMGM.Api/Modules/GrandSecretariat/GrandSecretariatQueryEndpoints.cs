using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.GrandSecretariat;

public static class GrandSecretariatQueryEndpoints
{
    public static IEndpointRouteBuilder MapGrandSecretariatQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/gran-secretaria/reservas", GetReservationsAsync)
            .WithTags("Gran Secretaría")
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetReservationsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? ceremonyRequestId,
        HttpContext httpContext,
        GrandSecretariatDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandSecretariat(httpContext.User)) return Results.Forbid();
        if (toUtc <= fromUtc) return Results.BadRequest(new { message = "El término del período debe ser posterior al inicio." });
        if (toUtc - fromUtc > TimeSpan.FromDays(93)) return Results.BadRequest(new { message = "La consulta de reservas no puede exceder 93 días." });

        var query = db.SpaceReservations
            .AsNoTracking()
            .Where(x => x.StartsAtUtc < toUtc && x.EndsAtUtc > fromUtc);

        if (ceremonyRequestId is not null) query = query.Where(x => x.CeremonyRequestId == ceremonyRequestId.Value);

        var items = await query
            .OrderBy(x => x.StartsAtUtc)
            .ThenBy(x => x.Space.Name)
            .Select(x => new GrandSecretariatReservationDto(
                x.Id,
                x.SpaceId,
                x.Space.Name,
                x.OrganizationId,
                x.CeremonyRequestId,
                x.Purpose,
                x.StartsAtUtc,
                x.EndsAtUtc,
                x.Status))
            .Take(500)
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new GrandSecretariatReservationsResponse(items.Count, items));
    }
}

public sealed record GrandSecretariatReservationDto(
    Guid Id,
    Guid SpaceId,
    string SpaceName,
    Guid OrganizationId,
    Guid? CeremonyRequestId,
    string Purpose,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string Status);

public sealed record GrandSecretariatReservationsResponse(
    int Total,
    IReadOnlyList<GrandSecretariatReservationDto> Items);
