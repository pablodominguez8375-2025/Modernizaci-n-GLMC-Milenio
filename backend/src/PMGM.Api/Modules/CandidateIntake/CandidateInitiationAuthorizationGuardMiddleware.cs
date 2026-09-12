using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.CandidateIntake;

public sealed class CandidateInitiationAuthorizationGuardMiddleware(RequestDelegate next)
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

        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .Where(x => x.Id == requestId)
            .Select(x => new { x.Id, x.CeremonyType })
            .SingleOrDefaultAsync(context.RequestAborted);

        if (ceremony is null || ceremony.CeremonyType != CeremonyCodes.Type.Initiation)
        {
            await next(context);
            return;
        }

        var latestBallotStatus = await db.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId &&
                        x.ValidationType == CeremonyCodes.ValidationType.CandidateFinalBallot)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => x.Status)
            .FirstOrDefaultAsync(context.RequestAborted);

        if (latestBallotStatus == CeremonyCodes.ValidationStatus.Approved)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "La iniciación no puede ser autorizada sin balotaje aprobado y registrado.",
            requirement = new
            {
                code = "candidate_final_ballot",
                name = "Balotaje de primer grado",
                status = latestBallotStatus ?? CeremonyCodes.ValidationStatus.Pending
            }
        }, context.RequestAborted);
    }
}
