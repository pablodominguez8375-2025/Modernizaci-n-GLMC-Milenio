using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
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
/// - SIN caché: el middleware consulta el estado vigente de los Talleres del propio usuario en CADA petición (una baja rige de inmediato).
/// - Verificación de ámbitos con BD y fail-closed: un pmgm_org inexistente, ilegible o de un Taller que no está activo NO cuenta como ámbito activo.
/// - Los cargos (pmgm_role) de la plataforma son planos para todos los Talleres del usuario (no existe vínculo cargo↔Taller en las claims): la baja
///   solo QUITA ámbitos, nunca concede; un cargo conferido por el Taller de baja no abre datos de ese Taller (su pmgm_org se elimina).
///   La autorización por organización de cada endpoint sigue rigiendo al Taller activo.
/// - Las claims <see cref="Marker"/> y <see cref="RetiredOrg"/> las pone SOLO este middleware (se descartan las entrantes).
/// La lógica pura es <see cref="Apply"/>; el middleware solo aporta el estado vigente de los Talleres del usuario.
/// </summary>
public enum WorkshopScopeState { Active, Retired, Unusable }

public static class RetiredWorkshopScope
{
    public const string Marker = "pmgm_retired_workshop";
    /// <summary>Un Taller de baja del usuario, para consulta histórica propia (una claim por Taller). Solo la emite este middleware.</summary>
    public const string RetiredOrg = "pmgm_retired_org";
    private static readonly string[] OperationalTypes = [InstitutionalClaims.Role, InstitutionalClaims.Organization, InstitutionalClaims.Scope];

    private static WorkshopScopeState StateOf(string value, IReadOnlyDictionary<Guid, WorkshopScopeState> states)
        => Guid.TryParse(value, out var id) && states.TryGetValue(id, out var state) ? state : WorkshopScopeState.Unusable;

    public static ClaimsPrincipal RemoveForgedClaims(ClaimsPrincipal user)
        => new(user.Identities.Select(identity => new ClaimsIdentity(identity.Claims.Where(c => c.Type is not (Marker or RetiredOrg)).ToList(),
            identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType)).ToList());

    public static ClaimsPrincipal Apply(ClaimsPrincipal user, IReadOnlyDictionary<Guid, WorkshopScopeState> states)
    {
        if (user.Identity?.IsAuthenticated != true) return user;
        var orgClaims = user.Claims.Where(x => x.Type == InstitutionalClaims.Organization).ToList();
        var spoofed = user.Claims.Any(x => x.Type is Marker or RetiredOrg);
        var classified = orgClaims.Select(x => (Claim: x, State: StateOf(x.Value, states))).ToList();
        var retiredIds = classified.Where(x => x.State == WorkshopScopeState.Retired).Select(x => Guid.Parse(x.Claim.Value)).Distinct().ToList();
        var unusable = classified.Count(x => x.State == WorkshopScopeState.Unusable);
        if (retiredIds.Count == 0 && unusable == 0 && !spoofed) return user;

        var activeOrgs = classified.Count(x => x.State == WorkshopScopeState.Active);
        var orderScope = user.Claims.Any(x => x.Type == InstitutionalClaims.Scope && string.Equals(x.Value, "order", StringComparison.OrdinalIgnoreCase));
        var privileged = user.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin) || user.HasClaim(InstitutionalClaims.AccountKind, "superuser");
        var retiredOnly = retiredIds.Count > 0 && activeOrgs == 0 && !orderScope;
        // Fail-closed: todos sus pmgm_org son inexistentes/no verificables/no permitidos y no tiene Orden: no conserva cargos ni ámbitos operativos.
        var noValidScope = retiredIds.Count == 0 && orgClaims.Count > 0 && activeOrgs == 0 && !orderScope && !privileged;
        var stripOperational = retiredOnly || noValidScope;

        var identities = user.Identities.Select(identity =>
        {
            var kept = identity.Claims.Where(claim =>
            {
                if (claim.Type is Marker or RetiredOrg) return false;   // nunca se confía en claims entrantes de este middleware
                if (stripOperational) return !OperationalTypes.Contains(claim.Type);
                if (claim.Type != InstitutionalClaims.Organization) return true;
                return StateOf(claim.Value, states) == WorkshopScopeState.Active;   // Retired y Unusable siempre se eliminan
            }).ToList();
            return new ClaimsIdentity(kept, identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType);
        }).ToList();
        if (identities.Count == 0) return user;
        foreach (var id in retiredIds) identities[0].AddClaim(new Claim(RetiredOrg, id.ToString()));
        if (retiredOnly) identities[0].AddClaim(new Claim(Marker, "true"));
        return new ClaimsPrincipal(identities);
    }
}

public sealed class RetiredWorkshopScopeMiddleware(RequestDelegate next, IConfiguration configuration)
{
    /// <summary>Bandera de activación de T2c (default false = inerte, igual que RetireEnabled): se activa junto con la baja, en la fase 2 del corte.</summary>
    public const string EnabledKey = "WorkshopRegistry:RetiredSessionEnabled";

    /// <summary>
    /// Validación POSITIVA del tipo de ámbito: solo un Taller activo, Gran Logia o el Taller 00 de soporte (activos) son ámbitos válidos;
    /// un Taller de baja es Retired; cualquier otro tipo o estado inesperado es Unusable (no se infiere Active por descarte).
    /// </summary>
    public static WorkshopScopeState Classify(string? type, string? status)
    {
        if (status == WorkshopRegistryCodes.Retired) return type == "workshop" ? WorkshopScopeState.Retired : WorkshopScopeState.Unusable;
        if (status != WorkshopRegistryCodes.Active) return WorkshopScopeState.Unusable;
        return type is "workshop" or WorkshopRegistryCodes.GrandLodgeType or WorkshopRegistryCodes.SupportType ? WorkshopScopeState.Active : WorkshopScopeState.Unusable;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true && !configuration.GetValue<bool>(EnabledKey))
        {
            // Inerte: no consulta BD ni cambia ámbitos; solo descarta claims de este middleware que vengan en el token (anti-spoof).
            if (user.Claims.Any(x => x.Type is RetiredWorkshopScope.Marker or RetiredWorkshopScope.RetiredOrg)) context.User = RetiredWorkshopScope.RemoveForgedClaims(user);
        }
        else if (user.Identity?.IsAuthenticated == true)
        {
            var ids = user.Claims.Where(x => x.Type == InstitutionalClaims.Organization)
                .Select(x => Guid.TryParse(x.Value, out var id) ? id : (Guid?)null).Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
            var hasIncoming = user.Claims.Any(x => x.Type is RetiredWorkshopScope.Marker or RetiredWorkshopScope.RetiredOrg);
            if (ids.Count > 0 || hasIncoming)
            {
                var db = context.RequestServices.GetRequiredService<PmgmDbContext>();
                // SIN caché: estado vigente en cada petición; ante error de BD la petición falla (fail-closed), no se concede acceso.
                var rows = ids.Count == 0 ? [] : await db.Organizations.AsNoTracking().Where(o => ids.Contains(o.Id))
                    .Select(o => new { o.Id, o.Type, o.Status }).ToListAsync(context.RequestAborted);
                var states = rows.ToDictionary(o => o.Id, o => Classify(o.Type, o.Status));
                context.User = RetiredWorkshopScope.Apply(user, states);
            }
        }
        await next(context);
    }
}
