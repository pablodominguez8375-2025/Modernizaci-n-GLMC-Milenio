using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PMGM.Api.Data;
using PMGM.Api.Modules.SystemConfiguration;

namespace PMGM.Api.Modules.Authorization;

/// <summary>
/// #1035 T2c (propuesta B de ChatGPT, pendiente de ratificación del PO): un Taller dado de baja es de solo lectura y no
/// otorga ámbito operativo. La baja NO deshabilita la cuenta ni toca Keycloak: la autenticación sigue permitida.
/// - Si TODOS los ámbitos de Taller del usuario son de baja y no tiene ámbito de Orden: sesión sin ámbito operativo
///   (se descartan pmgm_role, pmgm_org y pmgm_scope; nunca se hereda el rol de un Taller de baja) y marca
///   <see cref="Marker"/> para mostrar «Taller de baja» y dar solo consulta histórica propia.
/// - Si tiene otros Talleres activos o ámbito de Orden: solo se quitan los Talleres de baja; el resto de permisos se conserva.
/// - Default-deny: un pmgm_org ilegible junto con Talleres de baja no cuenta como ámbito activo.
/// La lógica pura es <see cref="Apply"/>; el middleware solo aporta el conjunto de Talleres de baja (caché corta).
/// </summary>
public static class RetiredWorkshopScope
{
    public const string Marker = "pmgm_retired_workshop";
    private static readonly string[] OperationalTypes = [InstitutionalClaims.Role, InstitutionalClaims.Organization, InstitutionalClaims.Scope];

    public static ClaimsPrincipal Apply(ClaimsPrincipal user, IReadOnlySet<Guid> retired)
    {
        if (user.Identity?.IsAuthenticated != true || retired.Count == 0) return user;
        var orgClaims = user.Claims.Where(x => x.Type == InstitutionalClaims.Organization).ToList();
        if (orgClaims.Count == 0) return user;
        var hasRetired = orgClaims.Any(x => Guid.TryParse(x.Value, out var id) && retired.Contains(id));
        if (!hasRetired) return user;

        var activeOrgs = orgClaims.Count(x => Guid.TryParse(x.Value, out var id) && !retired.Contains(id));
        var orderScope = user.Claims.Any(x => x.Type == InstitutionalClaims.Scope && string.Equals(x.Value, "order", StringComparison.OrdinalIgnoreCase));
        var retiredOnly = activeOrgs == 0 && !orderScope;

        var identities = user.Identities.Select(identity =>
        {
            var kept = identity.Claims.Where(claim =>
            {
                if (retiredOnly) return !OperationalTypes.Contains(claim.Type);
                if (claim.Type != InstitutionalClaims.Organization) return true;
                return Guid.TryParse(claim.Value, out var id) && !retired.Contains(id);
            }).ToList();
            return new ClaimsIdentity(kept, identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType);
        }).ToList();
        if (retiredOnly) identities[0].AddClaim(new Claim(Marker, "true"));
        return new ClaimsPrincipal(identities);
    }
}

public sealed class RetiredWorkshopScopeMiddleware(RequestDelegate next, IMemoryCache cache)
{
    private const string CacheKey = "pmgm:retired-workshop-ids";

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true && user.HasClaim(x => x.Type == InstitutionalClaims.Organization))
        {
            var db = context.RequestServices.GetRequiredService<PmgmDbContext>();
            // TTL corto: una baja nueva rige en segundos; ante error de BD la petición falla (fail-closed), no se concede acceso.
            var retired = await cache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
                var ids = await db.Organizations.AsNoTracking()
                    .Where(o => o.Status == WorkshopRegistryCodes.Retired).Select(o => o.Id).ToListAsync(context.RequestAborted);
                return (IReadOnlySet<Guid>)ids.ToHashSet();
            }) ?? new HashSet<Guid>();
            context.User = RetiredWorkshopScope.Apply(user, retired);
        }
        await next(context);
    }
}
