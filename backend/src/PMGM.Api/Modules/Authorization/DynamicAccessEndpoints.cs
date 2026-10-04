using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;

namespace PMGM.Api.Modules.Authorization;

public static class DynamicAccessEndpoints
{
    public static readonly string[] AllowedActions = ["view", "create", "write", "edit", "delete", "print"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const long CatalogLock = 26620261004;

    public static IEndpointRouteBuilder MapDynamicAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/system/access").WithTags("Sistema — Acceso dinámico").RequireAuthorization();
        group.MapGet("/catalog", async (HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct) =>
        {
            if (!access.CanConfigureSystem(ctx.User)) return Results.Forbid();
            ctx.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(await LoadAsync(db, ct));
        });
        group.MapPost("/profiles", (CreateDynamicProfileRequest request, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct) =>
            MutateAsync(request.ExpectedVersion, ctx, db, access, audit, "profile_created", catalog =>
            {
                var error = ValidateProfile(request.Code, request.Name, request.Scope);
                if (error is not null) return Results.BadRequest(new { message = error });
                if (catalog.Profiles.Any(p => Eq(p.Code, request.Code))) return Conflict("El código del perfil ya existe.");
                if (!ValidMenus(catalog, request.MenuCodes) || request.Scope == "lodge" && (request.MenuCodes ?? []).Any(m => Eq(m, "system"))) return Results.BadRequest(new { message = "Menú inexistente o inactivo." });
                var profile = new DynamicProfile(Guid.NewGuid(), request.Code.Trim().ToLowerInvariant(), request.Name.Trim(), request.Scope, false, true, Normalize(request.MenuCodes), []);
                catalog.Profiles.Add(profile);
                return Results.Ok(profile);
            }, ct));
        group.MapPut("/profiles/{profileCode}", (string profileCode, UpdateDynamicProfileRequest request, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct) =>
            MutateAsync(request.ExpectedVersion, ctx, db, access, audit, "profile_updated", catalog =>
            {
                var index = catalog.Profiles.FindIndex(p => Eq(p.Code, profileCode));
                if (index < 0) return Results.NotFound();
                var current = catalog.Profiles[index];
                if (current.IsSystem || !current.IsActive) return Conflict("El perfil está protegido o inactivo.");
                var error = ValidateProfile(current.Code, request.Name, request.Scope);
                if (error is not null) return Results.BadRequest(new { message = error });
                if (!ValidMenus(catalog, request.MenuCodes) || request.Scope == "lodge" && (request.MenuCodes ?? []).Any(m => Eq(m, "system"))) return Results.BadRequest(new { message = "Menú inexistente o inactivo." });
                if (current.Scope != request.Scope && catalog.Assignments.Any(a => a.IsActive && Eq(a.ProfileCode, current.Code))) return Conflict("Revoque las asignaciones antes de cambiar el alcance.");
                var menus = Normalize(request.MenuCodes);
                var views = catalog.Menus.Where(m => menus.Contains(m.Code)).SelectMany(m => m.Views).Select(v => v.Code).ToHashSet();
                var profile = current with { Name = request.Name.Trim(), Scope = request.Scope, MenuCodes = menus, Grants = current.Grants.Where(g => views.Contains(g.ViewCode)).ToList() };
                catalog.Profiles[index] = profile;
                return Results.Ok(profile);
            }, ct));
        group.MapDelete("/profiles/{profileCode}", (string profileCode, int expectedVersion, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct) =>
            MutateAsync(expectedVersion, ctx, db, access, audit, "profile_deleted_logically", catalog =>
            {
                var index = catalog.Profiles.FindIndex(p => Eq(p.Code, profileCode));
                if (index < 0) return Results.NotFound();
                if (catalog.Profiles[index].IsSystem) return Conflict("Los perfiles de sistema son protegidos.");
                catalog.Profiles[index] = catalog.Profiles[index] with { IsActive = false };
                for (var i = 0; i < catalog.Assignments.Count; i++)
                    if (Eq(catalog.Assignments[i].ProfileCode, profileCode)) catalog.Assignments[i] = catalog.Assignments[i] with { IsActive = false };
                return Results.Ok(new { deleted = true });
            }, ct));
        group.MapPut("/profiles/{profileCode}/grants", (string profileCode, ReplaceDynamicGrantsRequest request, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct) =>
            MutateAsync(request.ExpectedVersion, ctx, db, access, audit, "profile_grants_replaced", catalog =>
            {
                var index = catalog.Profiles.FindIndex(p => Eq(p.Code, profileCode));
                if (index < 0) return Results.NotFound();
                var profile = catalog.Profiles[index];
                if (profile.IsSystem || !profile.IsActive) return Conflict("El perfil está protegido o inactivo.");
                var grants = new List<DynamicGrant>();
                foreach (var grant in request.Grants ?? [])
                {
                    var menu = catalog.Menus.FirstOrDefault(m => m.IsActive && m.Views.Any(v => v.IsActive && Eq(v.Code, grant.ViewCode)));
                    var actions = Normalize(grant.Actions);
                    if (menu is null || !profile.MenuCodes.Contains(menu.Code) || actions.Any(a => !AllowedActions.Contains(a)) || grants.Any(g => Eq(g.ViewCode, grant.ViewCode)))
                        return Results.BadRequest(new { message = "Vista duplicada, fuera del menú seleccionado o acción inválida." });
                    if (actions.Any(a => a != "view") && !actions.Contains("view")) return Results.BadRequest(new { message = "Las acciones requieren permiso de ver la misma vista." });
                    if (profile.Scope == "lodge" && menu.Code == "system") return Results.BadRequest(new { message = "Un perfil de Taller no puede configurar el sistema." });
                    grants.Add(new DynamicGrant(grant.ViewCode.Trim().ToLowerInvariant(), actions));
                }
                catalog.Profiles[index] = profile with { Grants = grants };
                return Results.Ok(catalog.Profiles[index]);
            }, ct));
        group.MapPost("/assignments", (AssignDynamicProfileRequest request, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct) =>
            MutateAsync(request.ExpectedVersion, ctx, db, access, audit, "profile_assigned", catalog =>
            {
                if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 320 || request.Subject != request.Subject.Trim()) return Results.BadRequest(new { message = "Indique el identificador exacto del sujeto OIDC (hasta 320 caracteres)." });
                var profile = catalog.Profiles.FirstOrDefault(p => Eq(p.Code, request.ProfileCode) && p.IsActive);
                if (profile is null || profile.IsSystem) return Results.BadRequest(new { message = "Seleccione un perfil técnico activo; los cargos base se gestionan institucionalmente." });
                if (request.EffectiveTo < request.EffectiveFrom || (profile.Scope == "lodge" ? request.OrganizationId is null || request.OrganizationId == Guid.Empty : request.OrganizationId is not null)) return Results.BadRequest(new { message = "Alcance o vigencia no válidos." });
                if (request.OrganizationId is Guid org && !db.Organizations.Any(o => o.Id == org && o.Type == "workshop")) return Results.BadRequest(new { message = "El Taller no existe." });
                if (catalog.Assignments.Any(a => a.IsActive && a.Subject == request.Subject && Eq(a.ProfileCode, profile.Code) && a.OrganizationId == request.OrganizationId && a.EffectiveFrom <= (request.EffectiveTo ?? DateOnly.MaxValue) && (a.EffectiveTo ?? DateOnly.MaxValue) >= request.EffectiveFrom)) return Conflict("La asignación se superpone con una ya vigente o programada.");
                var assignment = new DynamicAssignment(Guid.NewGuid(), request.Subject, profile.Code, request.OrganizationId, request.EffectiveFrom, request.EffectiveTo, true);
                catalog.Assignments.Add(assignment);
                return Results.Ok(assignment);
            }, ct));
        group.MapDelete("/assignments/{assignmentId:guid}", (Guid assignmentId, int expectedVersion, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct) =>
            MutateAsync(expectedVersion, ctx, db, access, audit, "profile_assignment_revoked", catalog =>
            {
                var index = catalog.Assignments.FindIndex(a => a.Id == assignmentId);
                if (index < 0) return Results.NotFound();
                catalog.Assignments[index] = catalog.Assignments[index] with { IsActive = false };
                return Results.Ok(new { revoked = true });
            }, ct));
        group.MapGet("/evaluate", async (string viewCode, string action, Guid? organizationId, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct) =>
        {
            if (!AllowedActions.Contains(action)) return Results.BadRequest(new { message = "Acción inválida." });
            ctx.Response.Headers.CacheControl = "private, no-store";
            var subject = ctx.User.FindFirstValue("sub") ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var boundary = organizationId is Guid org ? access.CanReadOrganization(ctx.User, org) : access.HasOrderScope(ctx.User) || access.CanConfigureSystem(ctx.User);
            var catalog = await LoadAsync(db, ct);
            return Results.Ok(new { allowed = boundary && IsGranted(catalog, subject, viewCode.Trim().ToLowerInvariant(), action, organizationId, Today()), catalog.Version });
        });
        group.MapPost("/print", async (HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct) =>
        {
            if (!access.CanConfigureSystem(ctx.User)) return Results.Forbid();
            audit.Add(ctx, "system.access.review_print_requested", "DynamicAccessCatalog", "review", null, AuditResults.Success);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { allowed = true });
        });
        return endpoints;
    }

    public static bool IsGranted(DynamicAccessCatalog catalog, string? subject, string view, string action, Guid? organizationId, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(subject) || !AllowedActions.Contains(action)) return false;
        var menu = catalog.Menus.FirstOrDefault(m => m.IsActive && m.Views.Any(v => v.IsActive && v.Code == view));
        if (menu is null) return false;
        return catalog.Assignments.Where(a => a.IsActive && a.Subject == subject && a.OrganizationId == organizationId && a.EffectiveFrom <= today && (a.EffectiveTo is null || a.EffectiveTo >= today))
            .Join(catalog.Profiles.Where(p => p.IsActive && !p.IsSystem && p.MenuCodes.Contains(menu.Code) && (p.Scope == "lodge" ? organizationId is not null : organizationId is null)), a => a.ProfileCode, p => p.Code, (_, p) => p)
            .SelectMany(p => p.Grants).Any(g => g.ViewCode == view && g.Actions.Contains("view") && g.Actions.Contains(action));
    }
    private static async Task<IResult> MutateAsync(int expectedVersion, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, string action, Func<DynamicAccessCatalog, IResult> mutate, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(ctx.User)) return Results.Forbid();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({CatalogLock})", ct);
        var catalog = await LoadAsync(db, ct);
        if (expectedVersion != catalog.Version) return Conflict("El catálogo cambió. Recargue antes de guardar.");
        var result = mutate(catalog);
        if (result is IStatusCodeHttpResult status && status.StatusCode is >= 400) return result;
        var next = catalog with { Version = catalog.Version + 1 };
        db.DynamicAccessSnapshots.Add(new DynamicAccessSnapshot { Version = next.Version, Payload = JsonSerializer.Serialize(next, JsonOptions) });
        audit.Add(ctx, "system.access." + action, "DynamicAccessCatalog", next.Version.ToString(), null, AuditResults.Success, new { previousVersion = catalog.Version, version = next.Version });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Ok(next);
    }
    private static async Task<DynamicAccessCatalog> LoadAsync(PmgmDbContext db, CancellationToken ct)
    {
        var snapshot = await db.DynamicAccessSnapshots.AsNoTracking().OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (snapshot is null) return CreateCatalog();
        // A malformed stored snapshot is a fault, never a silent reset of grants.
        return JsonSerializer.Deserialize<DynamicAccessCatalog>(snapshot.Payload, JsonOptions) ?? throw new InvalidOperationException("Catálogo de accesos inválido.");
    }
    public static DynamicAccessCatalog CreateCatalog()
    {
        DynamicMenu Menu(string code, string name, params (string code, string name)[] views) => new(code, name, true, views.Select(v => new DynamicView(v.code.ToLowerInvariant(), v.name, true)).ToList());
        var menus = new List<DynamicMenu> {
            Menu("personal", "Mi espacio", ("member", "Mi ficha"), ("calendar", "Agenda"), ("notifications", "Avisos")),
            Menu("admissions", "Insinuaciones e Iniciación", ("candidates", "Publicados"), ("candidateProfile", "Carga y revisión"), ("initiationCircuit", "Circuito de Iniciación"), ("admissions", "Afiliación e Incorporación")),
            Menu("lodge", "Taller", ("lodge", "Gestión Logial"), ("lodgeProfile", "Ficha del Taller"), ("members", "Fichas de miembros")),
            Menu("treasury", "Tesorería", ("lodgeTreasury", "Tesorería del Taller"), ("treasury", "Gran Tesorería")),
            Menu("hospitalaria", "Hospitalaria", ("hospitalaria", "Hospitalaria")),
            Menu("documents", "Documentos", ("library", "Biblioteca"), ("documentManager", "Gestor documental"), ("grandArchive", "Gran Archivo")),
            Menu("order", "Gran Logia", ("secretariat", "Gran Secretaría"), ("regimen", "Régimen Interior"), ("ceremonies", "Ceremonias")),
            Menu("system", "Sistema", ("system", "Parámetros"), ("access-review", "Revisión de accesos")) };
        var names = new[] { "Venerable Maestro", "Secretaría del Taller", "Tesorería del Taller", "Hospitalaria del Taller", "Orador del Taller", "Primer Vigilante", "Segundo Vigilante", "Inmediato Ex-Venerable Maestro", "Administrador del Sistema" };
        return new DynamicAccessCatalog(0, AllowedActions, menus, names.Select((name, i) => new DynamicProfile(Guid.Parse($"00000000-0000-0000-0000-{i + 1:000000000000}"), "system-" + i, name, i == 8 ? "order" : "lodge", true, true, [], [])).ToList(), []);
    }
    private static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
    private static bool Eq(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string[] Normalize(string[]? values) => (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim().ToLowerInvariant()).Distinct().ToArray();
    private static bool ValidMenus(DynamicAccessCatalog catalog, string[]? menus) => (menus ?? []).All(m => catalog.Menus.Any(x => x.IsActive && Eq(x.Code, m)));
    private static IResult Conflict(string message) => Results.Conflict(new { message });
    private static string? ValidateProfile(string? code, string? name, string scope) => code is null || !Regex.IsMatch(code, "^[a-z][a-z0-9-]{0,79}$") ? "Código: letras minúsculas, números y guiones (máximo 80)." : string.IsNullOrWhiteSpace(name) || name.Length > 240 ? "Nombre obligatorio (máximo 240)." : scope is not ("order" or "lodge") ? "Alcance inválido." : null;
}
public sealed record DynamicAccessCatalog(int Version, string[] Actions, List<DynamicMenu> Menus, List<DynamicProfile> Profiles, List<DynamicAssignment> Assignments);
public sealed record DynamicMenu(string Code, string Name, bool IsActive, List<DynamicView> Views);
public sealed record DynamicView(string Code, string Name, bool IsActive);
public sealed record DynamicProfile(Guid Id, string Code, string Name, string Scope, bool IsSystem, bool IsActive, string[] MenuCodes, List<DynamicGrant> Grants);
public sealed record DynamicGrant(string ViewCode, string[] Actions);
public sealed record DynamicAssignment(Guid Id, string Subject, string ProfileCode, Guid? OrganizationId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive);
public sealed record CreateDynamicProfileRequest(string Code, string Name, string Scope, string[]? MenuCodes, int ExpectedVersion);
public sealed record UpdateDynamicProfileRequest(string Name, string Scope, string[]? MenuCodes, int ExpectedVersion);
public sealed record ReplaceDynamicGrantsRequest(List<DynamicGrant>? Grants, int ExpectedVersion);
public sealed record AssignDynamicProfileRequest(string Subject, string ProfileCode, Guid? OrganizationId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, int ExpectedVersion);
