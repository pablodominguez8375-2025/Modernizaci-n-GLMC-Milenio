using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Audit;

/// <summary>
/// Soporte técnico de solo lectura. Devuelve únicamente metadatos HTTP,
/// nunca archivos de servidor, payloads, PII, claims o excepciones.
/// </summary>
public static class SupportDiagnosticEndpoints
{
    public static IEndpointRouteBuilder MapSupportDiagnosticEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/system/support/diagnostics", GetAsync)
            .RequireAuthorization()
            .WithTags("Sistema — Soporte técnico");
        return endpoints;
    }

    private static IResult GetAsync(
        HttpContext context,
        IInstitutionalAccessService access,
        SupportDiagnosticBuffer buffer,
        string? severity,
        string? route,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit = 100)
    {
        // Compatible with the institutional System/Audit permission.
        // Not a general logging endpoint for workshop users.
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        if (!string.IsNullOrWhiteSpace(severity) && severity is not ("error" or "warning"))
            return Results.BadRequest(new { message = "Gravedad no válida." });
        if (from > to)
            return Results.BadRequest(new { message = "Rango de fechas no válido." });
        if (route?.Length > 80)
            return Results.BadRequest(new { message = "Filtro de ruta demasiado largo." });

        var count = Math.Clamp(limit, 1, 200);
        var entries = buffer.Snapshot().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(severity))
            entries = entries.Where(x => x.Severity == severity);
        if (!string.IsNullOrWhiteSpace(route))
            entries = entries.Where(x => x.Route.Contains(route, StringComparison.OrdinalIgnoreCase));
        if (from is not null)
            entries = entries.Where(x => x.OccurredAtUtc >= from.Value);
        if (to is not null)
            entries = entries.Where(x => x.OccurredAtUtc <= to.Value);
        var matches = entries.ToArray();

        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            total = matches.Length,
            limit = count,
            capacity = SupportDiagnosticBuffer.Capacity,
            volatileStorage = true,
            source = "api-process-memory",
            note = "Sólo errores HTTP observados por este proceso desde su arranque; al reiniciar se pierden.",
            items = matches.Take(count).ToArray()
        });
    }
}
