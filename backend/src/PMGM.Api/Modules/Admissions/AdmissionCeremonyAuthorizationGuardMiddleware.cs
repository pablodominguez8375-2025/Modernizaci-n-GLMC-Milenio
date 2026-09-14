using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;

namespace PMGM.Api.Modules.Admissions;

public sealed class AdmissionCeremonyAuthorizationGuardMiddleware(RequestDelegate next)
{
    private const string Prefix = "/api/ceremonias/solicitudes/";
    private const string AuthorizationSuffix = "/autorizar";

    public async Task InvokeAsync(
        HttpContext context,
        PmgmDbContext coreDb,
        AdmissionsDbContext admissionsDb)
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

        var admissionCaseId = await coreDb.CeremonyRequests.AsNoTracking()
            .Where(x => x.Id == requestId)
            .Select(x => x.AdmissionCaseId)
            .SingleOrDefaultAsync(context.RequestAborted);

        if (admissionCaseId is null)
        {
            await next(context);
            return;
        }

        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking()
            .Include(x => x.Evidence)
            .Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.Id == admissionCaseId.Value, context.RequestAborted);

        if (admissionCase is null)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "La solicitud de ceremonia referencia un expediente de admisión que no está disponible.",
                admissionCaseId
            }, context.RequestAborted);
            return;
        }

        var projection = AdmissionCaseEligibilityProjector.Evaluate(admissionCase);
        if (projection.Decision.CanProceed)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "La ceremonia no puede ser autorizada porque el expediente de afiliación/incorporación ya no está plenamente habilitado.",
            admissionCaseId,
            eligibility = projection.Decision
        }, context.RequestAborted);
    }
}
