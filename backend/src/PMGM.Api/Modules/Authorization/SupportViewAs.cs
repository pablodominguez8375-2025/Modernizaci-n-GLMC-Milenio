using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.UserAccounts;

namespace PMGM.Api.Modules.Authorization;

/// <summary>
/// «Ver como» (#574): sesión de soporte de SOLO LECTURA. El Máster ve la plataforma como la vería otra
/// persona, sin tocar su clave ni asumir su cargo. Cada solicitud con la cabecera de sesión se evalúa en
/// el servidor: sesión vigente y propia, solo GET/HEAD, solo rutas expresamente permitidas y con la
/// identidad (roles, Taller) del objetivo. La auditoría siempre registra al operador real.
/// </summary>
public sealed class SupportViewSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string OperatorSubject { get; set; } = string.Empty;
    public string TargetSubject { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
}

public static class SupportViewAsClaims
{
    public const string Header = "X-Support-View-As";
    public const string Operator = "pmgm_support_operator";
    public const string SessionId = "pmgm_support_session";
    public const string RoutePrefix = "/api/system/support/view-as";
    /// <summary>Roles que convierten a una cuenta en «privilegiada»: nunca pueden ser objetivo.</summary>
    public static readonly string[] PrivilegedRoles =
        [InstitutionalRoles.PlatformSuperAdmin, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.PrivacyOfficer];
    public static bool IsPrivileged(AccountClaims claims)
        => claims.Kind != "member" || claims.Roles.Any(r => PrivilegedRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// Lista cerrada: solo rutas sin datos personales restringidos (RUT completo, contacto reservado, pagos,
/// documentos sensibles). Todo lo demás se deniega por omisión mientras dure «Ver como».
/// </summary>
public static class SupportViewAsAllowlist
{
    private static readonly HashSet<string> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/session/me", "/api/session/view-access", "/api/session/treasury-access",
        "/api/biblioteca", "/api/biblioteca/facetas", "/api/biblioteca/buscar",
        "/api/calendar", "/api/publicaciones-oficiales",
    };
    public static bool Allows(string? routePattern)
        => !string.IsNullOrEmpty(routePattern) && Routes.Contains(routePattern.TrimEnd('/'));
}

public sealed class SupportViewAsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        IAccountIdentityProvider identity, IAuditService audit, IMemoryCache cache)
    {
        if (!ctx.Request.Headers.TryGetValue(SupportViewAsClaims.Header, out var raw) || ctx.User.Identity?.IsAuthenticated != true)
        { await next(ctx); return; }
        var route = (ctx.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? ctx.Request.Path.Value ?? "";
        // Los endpoints de la propia función (iniciar, terminar, consultar) actúan siempre como el operador.
        if (route.StartsWith(SupportViewAsClaims.RoutePrefix, StringComparison.OrdinalIgnoreCase)) { await next(ctx); return; }

        ctx.Response.Headers.CacheControl = "private, no-store";
        var operatorSubject = ctx.User.FindFirstValue("sub") ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        async Task Deny(int status, string code, string? sessionId = null)
        {
            if (sessionId is not null)
            {
                audit.Add(ctx, "support.view_as.denied", "SupportViewSession", sessionId, null, AuditResults.Rejected, new { code, route });
                await db.SaveChangesAsync(CancellationToken.None);
            }
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new
            {
                code,
                message = code == "SUPPORT_VIEW_READ_ONLY" ? "«Ver como» es solo lectura: no se puede modificar nada."
                    : code == "SUPPORT_VIEW_RESOURCE_DENIED" ? "Este dato no se muestra en «Ver como» para proteger los datos personales."
                    : "La sesión de «Ver como» no es válida o ya terminó."
            });
        }
        if (!Guid.TryParse(raw.ToString(), out var sessionGuid) || string.IsNullOrWhiteSpace(operatorSubject))
        { await Deny(StatusCodes.Status403Forbidden, "SUPPORT_VIEW_SESSION_INVALID"); return; }
        var session = await db.SupportViewSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sessionGuid, ctx.RequestAborted);
        var now = DateTimeOffset.UtcNow;
        if (session is null || session.OperatorSubject != operatorSubject || session.EndedAtUtc is not null || session.ExpiresAtUtc <= now
            || !access.IsPlatformSuperAdmin(ctx.User))
        { await Deny(StatusCodes.Status403Forbidden, "SUPPORT_VIEW_SESSION_INVALID"); return; }
        var sid = session.Id.ToString("D");
        if (!HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method))
        { await Deny(StatusCodes.Status403Forbidden, "SUPPORT_VIEW_READ_ONLY", sid); return; }
        var allowed = SupportViewAsAllowlist.Allows(route);
        if (!allowed) { await Deny(StatusCodes.Status403Forbidden, "SUPPORT_VIEW_RESOURCE_DENIED", sid); return; }

        AccountClaims? target;
        try
        {
            target = await cache.GetOrCreateAsync("support-view-target:" + sid, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                return await identity.GetClaimsAsync(session.TargetSubject, ctx.RequestAborted);
            });
        }
        catch (Exception e) when (e is AccountDeliveryException or HttpRequestException or TaskCanceledException)
        { await Deny(StatusCodes.Status403Forbidden, "SUPPORT_VIEW_SESSION_INVALID"); return; }
        var decision = SupportViewAsPolicy.EvaluateRead(
            new SupportViewAsSession(session.OperatorSubject, session.TargetSubject, session.Reason, session.StartedAtUtc, session.ExpiresAtUtc),
            operatorSubject, ctx.Request.Method, true,
            target is null || !target.Enabled || SupportViewAsClaims.IsPrivileged(target), true, true, false, now);
        if (!decision.Allowed || target is null) { await Deny(StatusCodes.Status403Forbidden, "SUPPORT_VIEW_SESSION_INVALID", sid); return; }

        // Auditoría ANTES de intercambiar la identidad: el actor queda como el operador real.
        audit.Add(ctx, "support.view_as.read", "SupportViewSession", sid, null, AuditResults.Observed, new { route });
        await db.SaveChangesAsync(ctx.RequestAborted);
        ctx.User = BuildTargetPrincipal(ctx.User, target, operatorSubject, sid);
        ctx.Response.Headers[SupportViewAsClaims.Header] = "active";
        await next(ctx);
    }

    internal static ClaimsPrincipal BuildTargetPrincipal(ClaimsPrincipal original, AccountClaims target, string operatorSubject, string sessionId)
    {
        var claims = new List<Claim>
        {
            new("sub", target.Subject), new("name", target.Name),
            new(SupportViewAsClaims.Operator, operatorSubject), new(SupportViewAsClaims.SessionId, sessionId),
        };
        var issuer = original.FindFirstValue("iss");
        if (!string.IsNullOrWhiteSpace(issuer)) claims.Add(new Claim("iss", issuer));
        claims.AddRange(target.Roles.Where(r => !SupportViewAsClaims.PrivilegedRoles.Contains(r, StringComparer.OrdinalIgnoreCase)).Select(r => new Claim(InstitutionalClaims.Role, r)));
        if (!string.IsNullOrWhiteSpace(target.Scope)) claims.Add(new Claim(InstitutionalClaims.Scope, target.Scope));
        claims.AddRange(target.Organizations.Select(o => new Claim(InstitutionalClaims.Organization, o)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, original.Identity?.AuthenticationType ?? "SupportViewAs", "name", InstitutionalClaims.Role));
    }
}

public static class SupportViewAsEndpoints
{
    public sealed record StartRequest(string? TargetSubject, string? Reason);

    public static IEndpointRouteBuilder MapSupportViewAsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(SupportViewAsClaims.RoutePrefix).RequireAuthorization().WithTags("Sistema — Ver como");
        group.MapGet("", GetCurrent);
        group.MapPost("", StartViewAs);
        group.MapPost("/{sessionId:guid}/end", EndViewAs);
        return endpoints;
    }

    private static bool Authorized(HttpContext ctx, IInstitutionalAccessService access)
        => access.CanConfigureSystem(ctx.User) && access.IsPlatformSuperAdmin(ctx.User) &&
           string.IsNullOrEmpty(ctx.User.FindFirstValue(SupportViewAsClaims.Operator));
    private static string Operator(HttpContext ctx) => ctx.User.FindFirstValue("sub") ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    private static async Task<IResult> GetCurrent(HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        if (!Authorized(ctx, access)) return Results.Forbid();
        var op = Operator(ctx); var now = DateTimeOffset.UtcNow;
        var s = await db.SupportViewSessions.AsNoTracking().Where(x => x.OperatorSubject == op && x.EndedAtUtc == null && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.StartedAtUtc).FirstOrDefaultAsync(ct);
        return s is null ? Results.Ok(new { active = false })
            : Results.Ok(new { active = true, sessionId = s.Id, targetSubject = s.TargetSubject, startedAtUtc = s.StartedAtUtc, expiresAtUtc = s.ExpiresAtUtc, readOnly = true });
    }

    private static async Task<IResult> StartViewAs(StartRequest request, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        IAccountIdentityProvider identity, IAuditService audit, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        if (!Authorized(ctx, access)) return Results.Forbid();
        var op = Operator(ctx); var target = request.TargetSubject?.Trim() ?? ""; var reason = request.Reason?.Trim() ?? "";
        async Task<IResult> Reject(int status, string code, string message)
        {
            audit.Add(ctx, "support.view_as.start_denied", "SupportViewSession", "none", null, AuditResults.Rejected, new { code });
            await db.SaveChangesAsync(CancellationToken.None);
            return Results.Json(new { code, message }, statusCode: status);
        }
        var now = DateTimeOffset.UtcNow;
        var proposed = new SupportViewAsSession(op, target, reason, now, now + SupportViewAsPolicy.MaxLifetime);
        if (string.Equals(op, target, StringComparison.Ordinal))
            return await Reject(StatusCodes.Status403Forbidden, "SUPPORT_VIEW_SELF_DENIED", "No puede verse a sí mismo.");
        if (!Guid.TryParse(target, out _) || target.Length > 64)
            return Results.BadRequest(new { code = "SUPPORT_VIEW_IDENTITY_REQUIRED", message = "Seleccione a la persona." });
        if (!SupportViewAsPolicy.EvaluateStart(proposed, true, false, now).Allowed && (reason.Length is < 10 or > 500))
            return Results.BadRequest(new { code = "SUPPORT_VIEW_REASON_REQUIRED", message = "Indique el motivo (10 a 500 caracteres)." });
        if (!identity.Configured)
            return Results.Json(new { code = "IDENTITY_UNAVAILABLE", message = "La identidad no está disponible; reintente más tarde." }, statusCode: 503);
        AccountClaims? claims;
        try { claims = await identity.GetClaimsAsync(target, ct); }
        catch (Exception e) when (e is AccountDeliveryException or HttpRequestException or TaskCanceledException)
        { return Results.Json(new { code = "IDENTITY_UNAVAILABLE", message = "La identidad no está disponible; reintente más tarde." }, statusCode: 503); }
        if (claims is null || !claims.Enabled)
            return await Reject(StatusCodes.Status404NotFound, "SUPPORT_VIEW_TARGET_NOT_FOUND", "La cuenta no existe, no está habilitada o no es administrada por la plataforma.");
        if (!SupportViewAsPolicy.EvaluateStart(proposed, true, SupportViewAsClaims.IsPrivileged(claims), now).Allowed)
            return await Reject(StatusCodes.Status403Forbidden, "SUPPORT_VIEW_PRIVILEGED_TARGET_DENIED", "No se puede ver como una cuenta con cargos de administración.");

        var open = await db.SupportViewSessions.Where(x => x.OperatorSubject == op && x.EndedAtUtc == null).ToListAsync(ct);
        foreach (var previous in open) previous.EndedAtUtc = now;
        var session = new SupportViewSession { OperatorSubject = op, TargetSubject = target, Reason = reason, StartedAtUtc = now, ExpiresAtUtc = now + SupportViewAsPolicy.MaxLifetime };
        db.SupportViewSessions.Add(session);
        audit.Add(ctx, "support.view_as.started", "SupportViewSession", session.Id.ToString("D"), null, AuditResults.Success,
            new { targetSubject = target, reason, expiresAtUtc = session.ExpiresAtUtc });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { sessionId = session.Id, targetSubject = target, targetName = claims.Name,
            startedAtUtc = session.StartedAtUtc, expiresAtUtc = session.ExpiresAtUtc, readOnly = true });
    }

    private static async Task<IResult> EndViewAs(Guid sessionId, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        IAuditService audit, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        if (!Authorized(ctx, access)) return Results.Forbid();
        var op = Operator(ctx);
        var session = await db.SupportViewSessions.FirstOrDefaultAsync(x => x.Id == sessionId && x.OperatorSubject == op, ct);
        if (session is null) return Results.NotFound();
        if (session.EndedAtUtc is null)
        {
            session.EndedAtUtc = DateTimeOffset.UtcNow;
            audit.Add(ctx, "support.view_as.ended", "SupportViewSession", session.Id.ToString("D"), null, AuditResults.Success, new { targetSubject = session.TargetSubject });
            await db.SaveChangesAsync(ct);
        }
        return Results.Ok(new { ended = true });
    }
}
