using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Bootstrap;

public static class BootstrapEndpoints
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/platform/bootstrap/plan", PlanAsync)
            .WithTags("Bootstrap institucional")
            .RequireAuthorization();

        endpoints.MapPost("/api/platform/bootstrap/apply", ApplyAsync)
            .WithTags("Bootstrap institucional")
            .RequireAuthorization();

        endpoints.MapGet("/api/platform/bootstrap/catalog", GetCatalogAsync)
            .WithTags("Bootstrap institucional")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> PlanAsync(
        HttpContext httpContext,
        InstitutionalBootstrapRequest request,
        IInstitutionalAccessService access,
        IInstitutionalBootstrapService bootstrap,
        CancellationToken cancellationToken)
    {
        if (!access.IsPlatformSuperAdmin(httpContext.User))
            return Results.Forbid();

        var result = await bootstrap.PlanAsync(request, cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return result.Valid ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> ApplyAsync(
        HttpContext httpContext,
        InstitutionalBootstrapRequest request,
        IInstitutionalAccessService access,
        IInstitutionalBootstrapService bootstrap,
        CancellationToken cancellationToken)
    {
        if (!access.IsPlatformSuperAdmin(httpContext.User))
            return Results.Forbid();

        var result = await bootstrap.ApplyAsync(httpContext, request, cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return result.Applied || result.AlreadyApplied ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> GetCatalogAsync(
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IInstitutionalBootstrapService bootstrap,
        CancellationToken cancellationToken)
    {
        if (!access.HasOrderScope(httpContext.User) ||
            !access.HasRole(httpContext.User, InstitutionalRoles.GranLogiaAdmin))
        {
            return Results.Forbid();
        }

        var result = await bootstrap.GetCatalogAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(result);
    }
}
