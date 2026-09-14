using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;

namespace PMGM.Api.Modules.Ceremonies;

public sealed class GrandMasterCeremonyAuthorizationGuardMiddleware(RequestDelegate next)
{
    private const string Prefix = "/api/ceremonias/solicitudes/";
    private const string AuthorizationSuffix = "/autorizar";

    public async Task InvokeAsync(HttpContext context, PmgmDbContext db)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !path.EndsWith(AuthorizationSuffix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var idPart = path[Prefix.Length..^AuthorizationSuffix.Length].Trim('/');
        if (!Guid.TryParse(idPart, out var requestId))
        {
            await next(context);
            return;
        }

        var ceremonyExists = await db.CeremonyRequests
            .AsNoTracking()
            .AnyAsync(x => x.Id == requestId, context.RequestAborted);

        if (!ceremonyExists)
        {
            await next(context);
            return;
        }

        var latestStatus = await db.CeremonyValidations
            .AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId &&
                        x.ValidationType == CeremonyCodes.ValidationType.GrandMaster)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => x.Status)
            .FirstOrDefaultAsync(context.RequestAborted);

        if (GrandMasterCeremonyAuthorizationPolicy.CanAuthorize(latestStatus))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "La ceremonia no puede ser autorizada sin visto bueno vigente de Gran Maestría.",
            requirement = new
            {
                code = "gran_maestria",
                name = "Gran Maestría",
                status = latestStatus ?? CeremonyCodes.ValidationStatus.Pending,
                reason = latestStatus is null
                    ? "El visto bueno de Gran Maestría está pendiente."
                    : "El último pronunciamiento de Gran Maestría no es habilitante."
            }
        }, context.RequestAborted);
    }
}
