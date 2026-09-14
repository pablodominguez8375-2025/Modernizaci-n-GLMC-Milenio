using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Audit;

public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/system/audit-events", GetAsync).RequireAuthorization().WithTags("Sistema — Bitácora auditable");
        return endpoints;
    }

    private static async Task<IResult> GetAsync(HttpContext context, PmgmDbContext db, IInstitutionalAccessService access,
        string? user, string? menu, string? result, DateTimeOffset? from, DateTimeOffset? to, int page = 1, int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(user)) query = query.Where(x => (x.ActorDisplayName ?? x.ActorSubject ?? "").Contains(user));
        if (!string.IsNullOrWhiteSpace(menu)) query = query.Where(x => x.Menu == menu);
        if (!string.IsNullOrWhiteSpace(result)) query = query.Where(x => x.Result == result);
        if (from is not null) query = query.Where(x => x.OccurredAtUtc >= from);
        if (to is not null) query = query.Where(x => x.OccurredAtUtc <= to);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.OccurredAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.OccurredAtUtc, user = x.ActorDisplayName ?? x.ActorSubject ?? "Sistema", ipAddress = x.IpAddress ?? "No disponible", x.Menu, x.Submenu, summary = x.Summary ?? x.Action, x.Action, x.Result, x.CorrelationId })
            .ToListAsync(cancellationToken);
        return Results.Ok(new { total, page, pageSize, items, immutable = true });
    }
}
