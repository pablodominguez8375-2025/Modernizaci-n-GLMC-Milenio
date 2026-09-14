using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.ExecutiveReporting;

public static class ExecutiveReportingEndpoints
{
    public static IEndpointRouteBuilder MapExecutiveReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reporting")
            .WithTags("Reportería ejecutiva")
            .RequireAuthorization();

        group.MapGet("/executive", GetExecutiveReportAsync);
        return endpoints;
    }

    private static async Task<IResult> GetExecutiveReportAsync(
        DateOnly? asOf,
        DateOnly? from,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IExecutiveReportingService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User))
        {
            return Results.Forbid();
        }

        var cutoff = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = from ?? new DateOnly(cutoff.Year, 1, 1);
        if (periodStart > cutoff)
        {
            return Results.BadRequest(new
            {
                message = "La fecha inicial del período no puede ser posterior a la fecha de corte."
            });
        }

        var report = await service.BuildAsync(cutoff, periodStart, cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(report);
    }
}
