using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;

namespace PMGM.Api.Modules.Authorization;

/// <summary>
/// Versioned, data-driven technical access catalogue. Domain endpoints remain the
/// final authority for institutional rules; these grants only reduce visibility
/// and available operations (deny by default).
/// </summary>
public static class DynamicAccessEndpoints
{
    private const string SettingCode = "system.access.dynamic_catalog";
    private static readonly string[] AllowedActions = ["view", "create", "write", "edit", "delete", "print"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapDynamicAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/system/access").WithTags("Sistema — Acceso dinámico").RequireAuthorization();
        group.MapGet("/catalog", GetCatalogAsync);
        group.MapPost("/profiles", CreateProfileAsync);
        group.MapPut("/profiles/{profileCode}", UpdateProfileAsync);
        group.MapDelete("/profiles/{profileCode}", DeleteProfileAsync);
        group.MapPut("/profiles/{profileCode}/grants", ReplaceGrantsAsync);
        group.MapPost("/assignments", AssignProfileAsync);
        group.MapDelete("/assignments/{assignmentId:guid}", RevokeAssignmentAsync);
        group.MapGet("/evaluate", EvaluateAsync);
        return endpoints;
    }

    private static async Task<IResult> GetCatalogAsync(HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        var catalog = await LoadAsync(db, ct);
        return Results.Ok(new { catalog.Version, actions = AllowedActions, catalog.Menus, catalog.Profiles, catalog.Assignments });
    }

    private static async Task<IResult> CreateProfileAsync(CreateDynamicProfileRequest request, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        var error = ValidateProfile(request.Code, request.Name, request.Scope);
        if (error is not null) return Results.BadRequest(new { message = error });
        var catalog = await LoadAsync(db, ct);
        if (catalog.Profiles.Any(x => x.Code.Equals(request.Code.Trim(), StringComparison.OrdinalIgnoreCase))) return Results.Conflict(new { message = "El código del perfil ya existe." });
        var profile = new DynamicProfile(Guid.NewGuid(), request.Code.Trim(), request.Name.Trim(), request.Scope.Trim(), request.Description?.Trim(), false, true, request.MenuCodes?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [], []);
        catalog.Profiles.Add(profile);
        await SaveAsync(catalog, context, db, audit, "system.access.profile_created", profile.Code, ct);
        return Results.Created($"/api/system/access/profiles/{Uri.EscapeDataString(profile.Code)}", profile);
    }

    private static async Task<IResult> UpdateProfileAsync(string profileCode, UpdateDynamicProfileRequest request, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        var catalog = await LoadAsync(db, ct);
        var index = catalog.Profiles.FindIndex(x => x.Code.Equals(profileCode, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return Results.NotFound();
        var current = catalog.Profiles[index];
        if (current.IsSystem) return Results.Conflict(new { message = "Los perfiles de sistema son protegidos." });
        var error = ValidateProfile(current.Code, request.Name, request.Scope);
        if (error is not null) return Results.BadRequest(new { message = error });
        var updated = current with { Name = request.Name.Trim(), Scope = request.Scope.Trim(), Description = request.Description?.Trim(), MenuCodes = request.MenuCodes?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? current.MenuCodes, IsActive = request.IsActive };
        catalog.Profiles[index] = updated;
        await SaveAsync(catalog, context, db, audit, "system.access.profile_updated", current.Code, ct);
        return Results.Ok(updated);
    }

    private static async Task<IResult> DeleteProfileAsync(string profileCode, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        var catalog = await LoadAsync(db, ct);
        var index = catalog.Profiles.FindIndex(x => x.Code.Equals(profileCode, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return Results.NotFound();
        var current = catalog.Profiles[index];
        if (current.IsSystem) return Results.Conflict(new { message = "Los perfiles de sistema no se pueden borrar." });
        catalog.Profiles[index] = current with { IsActive = false };
        for (var assignmentIndex = 0; assignmentIndex < catalog.Assignments.Count; assignmentIndex++)
        {
            if (catalog.Assignments[assignmentIndex].ProfileCode.Equals(current.Code, StringComparison.OrdinalIgnoreCase))
                catalog.Assignments[assignmentIndex] = catalog.Assignments[assignmentIndex] with { IsActive = false };
        }
        await SaveAsync(catalog, context, db, audit, "system.access.profile_deleted_logically", current.Code, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ReplaceGrantsAsync(string profileCode, ReplaceDynamicGrantsRequest request, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        var catalog = await LoadAsync(db, ct);
        var index = catalog.Profiles.FindIndex(x => x.Code.Equals(profileCode, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return Results.NotFound();
        if (catalog.Profiles[index].IsSystem) return Results.Conflict(new { message = "Los grants de perfiles de sistema son protegidos." });
        var grants = new List<DynamicGrant>();
        foreach (var grant in request.Grants ?? [])
        {
            if (string.IsNullOrWhiteSpace(grant.ViewCode)) return Results.BadRequest(new { message = "Cada grant requiere viewCode." });
            var actions = grant.Actions?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
            if (actions.Any(x => !AllowedActions.Contains(x, StringComparer.OrdinalIgnoreCase))) return Results.BadRequest(new { message = "Acción no permitida.", allowedActions = AllowedActions });
            if (!catalog.Menus.SelectMany(x => x.Views).Any(x => x.Code.Equals(grant.ViewCode, StringComparison.OrdinalIgnoreCase))) return Results.BadRequest(new { message = $"La vista '{grant.ViewCode}' no existe en el catálogo." });
            grants.Add(new DynamicGrant(grant.ViewCode.Trim(), actions));
        }
        catalog.Profiles[index] = catalog.Profiles[index] with { Grants = grants };
        await SaveAsync(catalog, context, db, audit, "system.access.profile_grants_replaced", profileCode, ct);
        return Results.Ok(catalog.Profiles[index]);
    }

    private static async Task<IResult> AssignProfileAsync(AssignDynamicProfileRequest request, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.ProfileCode)) return Results.BadRequest(new { message = "Subject y profileCode son obligatorios." });
        var catalog = await LoadAsync(db, ct);
        var profile = catalog.Profiles.FirstOrDefault(x => x.Code.Equals(request.ProfileCode, StringComparison.OrdinalIgnoreCase) && x.IsActive);
        if (profile is null) return Results.BadRequest(new { message = "El perfil no existe o está inactivo." });
        var existing = catalog.Assignments.FirstOrDefault(x => x.Subject.Equals(request.Subject.Trim(), StringComparison.OrdinalIgnoreCase) && x.ProfileCode.Equals(profile.Code, StringComparison.OrdinalIgnoreCase) && x.IsActive);
        if (existing is not null) return Results.Conflict(new { message = "La asignación ya existe.", assignmentId = existing.Id });
        var assignment = new DynamicAssignment(Guid.NewGuid(), request.Subject.Trim(), profile.Code, request.OrganizationId, request.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow), request.EffectiveTo, true);
        catalog.Assignments.Add(assignment);
        await SaveAsync(catalog, context, db, audit, "system.access.profile_assigned", assignment.Id.ToString(), ct);
        return Results.Created($"/api/system/access/assignments/{assignment.Id}", assignment);
    }

    private static async Task<IResult> RevokeAssignmentAsync(Guid assignmentId, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        var catalog = await LoadAsync(db, ct);
        var assignment = catalog.Assignments.FirstOrDefault(x => x.Id == assignmentId);
        if (assignment is null) return Results.NotFound();
        var assignmentIndex = catalog.Assignments.FindIndex(x => x.Id == assignmentId);
        catalog.Assignments[assignmentIndex] = assignment with { IsActive = false };
        await SaveAsync(catalog, context, db, audit, "system.access.profile_assignment_revoked", assignmentId.ToString(), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> EvaluateAsync(string viewCode, string action, HttpContext context, PmgmDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(viewCode) || !AllowedActions.Contains(action, StringComparer.OrdinalIgnoreCase)) return Results.BadRequest(new { message = "viewCode y una acción válida son obligatorios.", allowedActions = AllowedActions });
        var subject = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject)) return Results.Ok(new { allowed = false, reason = "missing_subject" });
        var catalog = await LoadAsync(db, ct);
        var allowed = catalog.Assignments.Where(x => x.IsActive && x.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase) && x.EffectiveFrom <= DateOnly.FromDateTime(DateTime.UtcNow) && (x.EffectiveTo is null || x.EffectiveTo >= DateOnly.FromDateTime(DateTime.UtcNow))).Join(catalog.Profiles.Where(x => x.IsActive), x => x.ProfileCode, x => x.Code, (_, profile) => profile).SelectMany(x => x.Grants).Any(x => x.ViewCode.Equals(viewCode, StringComparison.OrdinalIgnoreCase) && x.Actions.Contains(action, StringComparer.OrdinalIgnoreCase));
        return Results.Ok(new { allowed, subject, viewCode, action = action.ToLowerInvariant() });
    }

    private static string? ValidateProfile(string code, string name, string scope)
        => string.IsNullOrWhiteSpace(code) || code.Length > 80 ? "El código del perfil es obligatorio y admite hasta 80 caracteres." : string.IsNullOrWhiteSpace(name) || name.Length > 240 ? "El nombre del perfil es obligatorio y admite hasta 240 caracteres." : scope is not ("order" or "lodge") ? "El scope debe ser order o lodge." : null;

    private static async Task<DynamicAccessCatalog> LoadAsync(PmgmDbContext db, CancellationToken ct)
    {
        var setting = await db.InstitutionalRuleSettings.AsNoTracking().Where(x => x.Code == SettingCode && x.Status == "active" && x.EffectiveFrom <= DateOnly.FromDateTime(DateTime.UtcNow)).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        if (setting is null) return new DynamicAccessCatalog(1, [], [], []);
        try { return JsonSerializer.Deserialize<DynamicAccessCatalog>(setting.Value, JsonOptions) ?? new DynamicAccessCatalog(1, [], [], []); }
        catch (JsonException) { return new DynamicAccessCatalog(1, [], [], []); }
    }

    private static async Task SaveAsync(DynamicAccessCatalog catalog, HttpContext context, PmgmDbContext db, IAuditService audit, string action, string entityId, CancellationToken ct)
    {
        var value = JsonSerializer.Serialize(catalog, JsonOptions);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var setting = await db.InstitutionalRuleSettings.Where(x => x.Code == SettingCode && x.EffectiveFrom == today).FirstOrDefaultAsync(ct);
        if (setting is null) db.InstitutionalRuleSettings.Add(new InstitutionalRuleSetting { Code = SettingCode, Value = value, EffectiveFrom = today, Status = "active", SourceReference = "PMGM-SEC-004 / GOV-004" });
        else { setting.Value = value; setting.Status = "active"; }
        audit.Add(context, action, "DynamicAccessCatalog", entityId, null, AuditResults.Success, new { settingCode = SettingCode, catalog.Version });
        await db.SaveChangesAsync(ct);
    }
}

public sealed record DynamicAccessCatalog(int Version, List<DynamicMenu> Menus, List<DynamicProfile> Profiles, List<DynamicAssignment> Assignments);
public sealed record DynamicMenu(string Code, string Name, bool IsActive, List<DynamicView> Views);
public sealed record DynamicView(string Code, string Name, string Route, bool IsActive);
public sealed record DynamicProfile(Guid Id, string Code, string Name, string Scope, string? Description, bool IsSystem, bool IsActive, string[] MenuCodes, List<DynamicGrant> Grants);
public sealed record DynamicGrant(string ViewCode, string[] Actions);
public sealed record DynamicAssignment(Guid Id, string Subject, string ProfileCode, Guid? OrganizationId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive);
public sealed record CreateDynamicProfileRequest(string Code, string Name, string Scope, string? Description, string[]? MenuCodes);
public sealed record UpdateDynamicProfileRequest(string Name, string Scope, string? Description, bool IsActive, string[]? MenuCodes);
public sealed record ReplaceDynamicGrantsRequest(List<DynamicGrantRequest>? Grants);
public sealed record DynamicGrantRequest(string ViewCode, string[]? Actions);
public sealed record AssignDynamicProfileRequest(string Subject, string ProfileCode, Guid? OrganizationId, DateOnly? EffectiveFrom, DateOnly? EffectiveTo);
