using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.CandidateIntake;

public sealed class CandidatePublishedLockMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PmgmDbContext db)
    {
        if (!TryGetCandidateMutationRequestId(context.Request, out var requestId))
        {
            await next(context);
            return;
        }

        var published = await db.CandidatePublications
            .AsNoTracking()
            .AnyAsync(x =>
                x.CeremonyRequestId == requestId &&
                x.Status == CeremonyCodes.PublicationStatus.Published,
                context.RequestAborted);

        if (!published)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status409Conflict;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers.CacheControl = "private, no-store";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.com/409",
            title = "Ficha de insinuado bloqueada",
            status = StatusCodes.Status409Conflict,
            detail = "La ficha ya fue aprobada y publicada. Cualquier corrección requiere un flujo institucional de reapertura con trazabilidad."
        }, context.RequestAborted);
    }

    public static bool TryGetCandidateMutationRequestId(HttpRequest request, out Guid requestId)
    {
        requestId = Guid.Empty;
        if (request.Method is not "PUT" and not "POST") return false;

        var path = request.Path.Value;
        if (string.IsNullOrWhiteSpace(path)) return false;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 5) return false;
        if (!string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[1], "insinuados", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[2], "solicitudes", StringComparison.OrdinalIgnoreCase))
            return false;

        var mutation = segments[4];
        if (!string.Equals(mutation, "ficha", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mutation, "foto", StringComparison.OrdinalIgnoreCase))
            return false;

        return Guid.TryParse(segments[3], out requestId);
    }
}
