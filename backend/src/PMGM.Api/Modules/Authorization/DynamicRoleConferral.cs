using System.Security.Claims;
using PMGM.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace PMGM.Api.Modules.Authorization;

/// <summary>
/// Perfiles institucionales sin desarrollo: un perfil dinámico puede CONFERIR cargos base (de una lista cerrada
/// y no sensible) dentro de su alcance. Los permisos concretos (vistas y acciones) siguen siendo los del perfil.
/// Solo el Máster crea o edita perfiles. Nunca se confieren cargos de Tesorería, Hospitalaria, Privacidad,
/// administración, Gran Maestría ni Régimen Interior (esas áreas van en una etapa posterior con aprobación del PO y Jurídico).
/// </summary>
public static class DynamicRoleConferral
{
    public static readonly string[] LodgeRoles =
    [
        InstitutionalRoles.TallerSecretaria, InstitutionalRoles.TallerOrador, InstitutionalRoles.TallerPrimerVigilante,
        InstitutionalRoles.TallerSegundoVigilante, InstitutionalRoles.TallerInmediatoExVenerable
    ];
    public static readonly string[] OrderRoles =
    [
        InstitutionalRoles.Librarian, InstitutionalRoles.Communications, InstitutionalRoles.GrandArchivist,
        InstitutionalRoles.DocumentManager, InstitutionalRoles.JefaturaDepartamentoDocencia,
        InstitutionalRoles.GranPrimerVigilante, InstitutionalRoles.GranSegundoVigilante, InstitutionalRoles.InmediatoExGranMaestro
    ];

    public static string? Validate(string[] roles, string scope)
    {
        if (roles.Length == 0) return null;
        var lodge = DynamicAccessEndpoints.IsLodgeScoped(scope);
        foreach (var role in roles)
        {
            if (lodge && !LodgeRoles.Contains(role)) return "Un perfil de Taller solo puede conferir cargos de Taller permitidos.";
            if (!lodge && !OrderRoles.Contains(role)) return "Un perfil de la Orden solo puede conferir cargos de Gran Logia permitidos.";
        }
        return null;
    }

    /// <summary>Agrega al usuario de la solicitud los cargos (y Talleres) que le confieren sus perfiles vigentes.</summary>
    public static async Task ApplyAsync(HttpContext context)
    {
        var user = context.User;
        var subject = DynamicTreasuryAccess.Subject(user);
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(subject)) return;
        var access = context.RequestServices.GetRequiredService<IInstitutionalAccessService>();
        if (access.IsPlatformSuperAdmin(user)) return; // el Máster nunca hereda cargos
        var db = context.RequestServices.GetRequiredService<PmgmDbContext>();
        var catalog = await DynamicAccessEndpoints.LoadRawAsync(db, context.RequestAborted);
        var today = DynamicAccessEndpoints.Today();
        var conferring = catalog.Profiles.Where(p => p.IsActive && !p.IsSystem && p.BaseRoles is { Length: > 0 }).ToDictionary(p => p.Code);
        if (conferring.Count == 0) return;
        var mine = catalog.Assignments.Where(a => a.IsActive && a.Subject == subject && conferring.ContainsKey(a.ProfileCode)
            && a.EffectiveFrom <= today && (a.EffectiveTo is null || a.EffectiveTo >= today)).ToList();
        if (mine.Count == 0) return;

        var roles = new HashSet<string>(); var orgs = new HashSet<Guid>(); var order = false;
        HashSet<Guid>? own = null; List<Guid>? all = null;
        foreach (var a in mine)
        {
            var profile = conferring[a.ProfileCode];
            foreach (var role in profile.BaseRoles!) roles.Add(role);
            switch (profile.Scope)
            {
                case "lodge": if (a.OrganizationId is Guid org) orgs.Add(org); break;
                case "own-lodge": own ??= await DynamicScopeExpansion.OwnLodgesAsync(db, user, context.RequestAborted); orgs.UnionWith(own); break;
                case "all-lodges": all ??= await db.Organizations.AsNoTracking().Where(o => o.Type == "workshop").Select(o => o.Id).ToListAsync(context.RequestAborted); orgs.UnionWith(all); break;
                default: order = true; break;
            }
        }
        var claims = new List<Claim>();
        foreach (var role in roles) if (!user.HasClaim(InstitutionalClaims.Role, role)) claims.Add(new Claim(InstitutionalClaims.Role, role));
        foreach (var org in orgs) if (!user.HasClaim(InstitutionalClaims.Organization, org.ToString())) claims.Add(new Claim(InstitutionalClaims.Organization, org.ToString()));
        if (order && !access.HasOrderScope(user)) claims.Add(new Claim(InstitutionalClaims.Scope, "order"));
        if (claims.Count > 0) user.AddIdentity(new ClaimsIdentity(claims, "dynamic-profile"));
    }
}

public sealed class DynamicRoleConferralMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await DynamicRoleConferral.ApplyAsync(context);
        await next(context);
    }
}
