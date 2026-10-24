using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Read-only API for the uniquely registered initial-load Master. No ability
/// to open, close, import, apply, or change any institutional records.
/// Server-owned Master registration is intentionally absent by default,
/// therefore the endpoint rejects every request until securely configured.
/// </summary>
public static class InitialLoadWindowStatusEndpoints
{
    public static IEndpointRouteBuilder MapInitialLoadWindowStatusEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/platform/bootstrap/initial-load/status", GetStatusAsync)
            .WithTags("Bootstrap institucional — Carga inicial")
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetStatusAsync(
        HttpContext httpContext,
        IInstitutionalAccessService access,
        BootstrapDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "private, no-store";
        httpContext.Response.Headers.Pragma = "no-cache";

        // Explicitly require validated Bearer auth. A principal from another
        // provider or a raw user-supplied "iss" header is not an auth result.
        var auth = await httpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!auth.Succeeded || auth.Principal is null ||
            !IsRegisteredMaster(auth.Principal, access, configuration))
            return Results.Forbid();

        var snapshot = await InitialLoadWindowStatusReader.ReadAsync(db, cancellationToken);
        return Results.Ok(snapshot);
    }

    /// <summary>
    /// Only call this helper with a ClaimsPrincipal from validated JWT bearer
    /// authentication, never a manually constructed principal from HTTP input.
    /// </summary>
    public static bool IsRegisteredMaster(
        ClaimsPrincipal? validatedJwtPrincipal,
        IInstitutionalAccessService? access,
        IConfiguration? configuration)
    {
        if (configuration is null || validatedJwtPrincipal is null)
            return false;

        // Authority MUST already be configured in the JWT middleware and
        // match the token issuer and the Master registration exactly.
        var authority = configuration["Authentication:Authority"];
        var issuerClaims = validatedJwtPrincipal.FindAll("iss")
            .Select(x => x.Value).ToArray();
        if (string.IsNullOrWhiteSpace(authority) || issuerClaims.Length != 1 ||
            !string.Equals(authority, issuerClaims[0], StringComparison.Ordinal))
            return false;

        var registration = new InitialLoadMasterIdentityPolicy.TrustedMasterRegistration(
            configuration["InitialLoad:MasterSubject"],
            configuration["InitialLoad:MasterIssuer"]);
        return InitialLoadMasterIdentityPolicy.Verify(
            validatedJwtPrincipal, access, issuerClaims[0], registration).Verified;
    }
}
