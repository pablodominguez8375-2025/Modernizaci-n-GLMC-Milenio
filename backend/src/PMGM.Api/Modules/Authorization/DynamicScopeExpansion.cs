using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Authorization;

/// <summary>
/// Resuelve los alcances «su Taller» (pertenencia vigente en la ficha del hermano) y «todos los Talleres»
/// convirtiéndolos, solo en memoria y para el usuario de la solicitud, en asignaciones por Taller concretas.
/// El motor de evaluación no cambia. Si el usuario no tiene pertenencia vigente queda inscrito sin ningún Taller
/// (cierra el acceso, nunca vuelve al acceso legado). Nada de esto se guarda.
/// </summary>
public static class DynamicScopeExpansion
{
    private static readonly AsyncLocal<ClaimsPrincipal?> Current = new();
    public static void SetCurrentUser(ClaimsPrincipal? user) => Current.Value = user;

    public static async Task<DynamicAccessCatalog> ApplyAsync(PmgmDbContext db, DynamicAccessCatalog catalog, CancellationToken ct)
    {
        var user = Current.Value;
        var subject = user is null ? null : DynamicTreasuryAccess.Subject(user);
        if (user is null || string.IsNullOrWhiteSpace(subject)) return catalog;
        var derivedCodes = catalog.Profiles.Where(p => DynamicAccessEndpoints.IsDerivedScope(p.Scope)).Select(p => p.Code).ToHashSet();
        if (derivedCodes.Count == 0 || !catalog.Assignments.Any(a => a.Subject == subject && derivedCodes.Contains(a.ProfileCode))) return catalog;

        var scopeOf = catalog.Profiles.ToDictionary(p => p.Code, p => p.Scope);
        HashSet<Guid>? own = null;
        List<Guid>? all = null;
        var assignments = new List<DynamicAssignment>();
        var enrolledWithoutLodge = false;
        foreach (var a in catalog.Assignments)
        {
            if (a.Subject != subject || !derivedCodes.Contains(a.ProfileCode)) { assignments.Add(a); continue; }
            var scope = scopeOf[a.ProfileCode];
            IEnumerable<Guid> lodges;
            if (scope == "own-lodge")
            {
                own ??= await OwnLodgesAsync(db, user, ct);
                lodges = own;
            }
            else
            {
                all ??= await db.Organizations.AsNoTracking().Where(o => o.Type == "workshop").Select(o => o.Id).ToListAsync(ct);
                lodges = all;
            }
            var any = false;
            foreach (var lodge in lodges)
            {
                any = true;
                assignments.Add(a with { Id = Synthetic(a.Id, lodge), OrganizationId = lodge });
            }
            if (!any) enrolledWithoutLodge = true;
        }
        // Inscrito pero sin Taller: un marcador inactivo mantiene la restricción (acceso cerrado, no legado).
        if (enrolledWithoutLodge)
            assignments.Add(new DynamicAssignment(Guid.Empty, subject, "none", Guid.Empty, DateOnly.MinValue, null, false));
        return catalog with { Assignments = assignments };
    }

    private static async Task<HashSet<Guid>> OwnLodgesAsync(PmgmDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        // Solo el vínculo identidad↔ficha (sin exigir grado), para no cerrar el acceso por un dato ajeno.
        var subject = DynamicTreasuryAccess.Subject(user);
        var issuer = user.FindFirstValue("iss");
        if (string.IsNullOrWhiteSpace(issuer)) issuer = "urn:pmgm:unspecified-issuer";
        var memberIds = await db.Database.SqlQuery<Guid>($"SELECT \"MemberId\" AS \"Value\" FROM core.member_identity_links WHERE \"Issuer\" = {issuer} AND \"Subject\" = {subject} AND \"RevokedAtUtc\" IS NULL LIMIT 1").ToListAsync(ct);
        if (memberIds.Count == 0) return [];
        var memberId = memberIds[0];
        var ids = await (from m in db.Memberships.AsNoTracking()
                         join o in db.Organizations.AsNoTracking() on m.OrganizationId equals o.Id
                         where m.MemberId == memberId && m.Status == MembershipCodes.MembershipStatus.Active && m.EndDate == null && o.Type == "workshop"
                         select m.OrganizationId).Distinct().ToListAsync(ct);
        return ids.ToHashSet();
    }

    private static Guid Synthetic(Guid assignment, Guid lodge)
    {
        var bytes = assignment.ToByteArray(); var l = lodge.ToByteArray();
        for (var i = 0; i < 16; i++) bytes[i] ^= l[i];
        return new Guid(bytes);
    }
}

public sealed class DynamicScopeExpansionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        DynamicScopeExpansion.SetCurrentUser(context.User.Identity?.IsAuthenticated == true ? context.User : null);
        await next(context);
    }
}
