using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.UserAccounts;

namespace PMGM.Api.Modules.Authorization;

/// <summary>
/// Gestión acotada de colaboradores. Solo responsables institucionales pueden
/// delegar su área: los colaboradores nunca heredan la facultad de delegar.
/// Persistencia en catálogo dinámico versionado, con mismo advisory lock que Sistema.
/// </summary>
public static class DocumentAreaCollaboratorEndpoints
{
    private const long CatalogLock = 26620261004;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] DelegableActions = ["view", "create", "write", "edit", "delete"];

    public static IEndpointRouteBuilder MapDocumentAreaCollaboratorEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var library = endpoints.MapGroup("/api/biblioteca/colaboradores")
            .WithTags("Biblioteca Virtual - Colaboradores").RequireAuthorization();
        library.MapGet("", (HttpContext ctx, PmgmDbContext db,
            IInstitutionalAccessService access, CancellationToken ct) =>
            ListAsync("library", ctx, db, access, ct));
        library.MapPost("", (DocumentAreaCollaboratorRequest request,
            HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
            IAuditService audit, CancellationToken ct) =>
            AssignAsync("library", request, ctx, db, access, audit, ct));
        library.MapDelete("/{assignmentId:guid}", (Guid assignmentId,
            int expectedVersion, HttpContext ctx, PmgmDbContext db,
            IInstitutionalAccessService access, IAuditService audit, CancellationToken ct) =>
            RevokeAsync("library", assignmentId, expectedVersion, ctx, db, access, audit, ct));

        library.MapGet("/candidatos", (HttpContext ctx, PmgmDbContext db,
            IInstitutionalAccessService access, IAccountIdentityProvider identity,
            IAuditService audit, CancellationToken ct) =>
            CandidatesAsync("library", ctx, db, access, identity, audit, ct));

        var documents = endpoints.MapGroup("/api/documentos/colaboradores")
            .WithTags("Gestión Documental - Colaboradores").RequireAuthorization();
        documents.MapGet("", (HttpContext ctx, PmgmDbContext db,
            IInstitutionalAccessService access, CancellationToken ct) =>
            ListAsync("documentmanager", ctx, db, access, ct));
        documents.MapPost("", (DocumentAreaCollaboratorRequest request,
            HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
            IAuditService audit, CancellationToken ct) =>
            AssignAsync("documentmanager", request, ctx, db, access, audit, ct));
        documents.MapDelete("/{assignmentId:guid}", (Guid assignmentId,
            int expectedVersion, HttpContext ctx, PmgmDbContext db,
            IInstitutionalAccessService access, IAuditService audit, CancellationToken ct) =>
            RevokeAsync("documentmanager", assignmentId, expectedVersion,
                ctx, db, access, audit, ct));
        documents.MapGet("/candidatos", (HttpContext ctx, PmgmDbContext db,
            IInstitutionalAccessService access, IAccountIdentityProvider identity,
            IAuditService audit, CancellationToken ct) =>
            CandidatesAsync("documentmanager", ctx, db, access, identity, audit, ct));
        return endpoints;
    }

    /// <summary>
    /// Personas elegibles para recibir acceso: solo nombre e identificador (nunca correo ni RUT),
    /// únicamente cuentas de Hermanos habilitadas; excluye al Máster, al Superusuario y al propio solicitante.
    /// Solo el responsable del área. Si la identidad no está disponible, devuelve lista vacía y la
    /// pantalla permite ingresar el identificador a mano.
    /// </summary>
    private static async Task<IResult> CandidatesAsync(
        string view, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        IAccountIdentityProvider identity, IAuditService audit, CancellationToken ct)
    {
        if (!CanAdminister(view, ctx, access)) return Results.Forbid();
        ctx.Response.Headers.CacheControl = "private, no-store";
        var self = ctx.User.FindFirst("sub")?.Value;
        IReadOnlyList<AccountUser> users = [];
        var available = identity.Configured;
        if (available)
        {
            try { users = await identity.ListAsync(ct); }
            catch (Exception e) when (e is AccountDeliveryException or HttpRequestException or TaskCanceledException)
            { available = false; }
        }
        var items = users.Where(u => u.Enabled && u.Kind == "member" &&
                !string.Equals(u.Subject, self, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(u.Name))
            .OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(u => new { subject = u.Subject, name = u.Name }).ToArray();
        audit.Add(ctx, "library.collaborator.candidates_viewed", "DynamicAccessCatalog", view, null,
            available ? AuditResults.Success : AuditResults.Rejected, new { area = view, count = items.Length });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { available, items });
    }

    private static bool CanAdminister(
        string view, HttpContext ctx, IInstitutionalAccessService access)
        => ctx.User.Identity?.IsAuthenticated == true &&
           !string.IsNullOrWhiteSpace(ctx.User.FindFirst("sub")?.Value) &&
           // Cada área administra sus propios colaboradores (Bibliotecario → Biblioteca, Gran Archivero →
           // Gestión Documental); el Máster y los superadmin pueden editarlos en ambas (PO 25-10).
           (access.IsPlatformSuperAdmin(ctx.User) ||
            (view == "library"
               ? access.CanManageLibrary(ctx.User, null)
               : view == "documentmanager" && access.CanManageGrandArchive(ctx.User)));

    private static string CodePrefix(string view) => "collab-" + view + "-";

    private static DocumentAreaCollaboratorDto[] Project(
        DynamicAccessCatalog catalog, string view)
    {
        var managedProfiles = catalog.Profiles
            .Where(p => p.Code.StartsWith(CodePrefix(view), StringComparison.Ordinal) &&
                        p.Scope == "order" && p.MenuCodes.Contains("documents") &&
                        !p.IsSystem && p.Grants.Count == 1 &&
                        p.Grants[0].ViewCode == view &&
                        p.Grants[0].Categories is { Length: 1 })
            .ToDictionary(p => p.Code, StringComparer.Ordinal);

        return catalog.Assignments
            .Where(a => managedProfiles.ContainsKey(a.ProfileCode))
            .Select(a =>
            {
                var grant = managedProfiles[a.ProfileCode].Grants.Single();
                return new DocumentAreaCollaboratorDto(
                    a.Id, a.Subject, view, grant.Categories![0], grant.Actions,
                    a.EffectiveFrom, a.EffectiveTo,
                    a.IsActive && managedProfiles[a.ProfileCode].IsActive);
            })
            .OrderBy(x => x.Category).ThenBy(x => x.Subject).ToArray();
    }

    private static async Task<IResult> ListAsync(
        string view, HttpContext ctx, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!CanAdminister(view, ctx, access)) return Results.Forbid();
        ctx.Response.Headers.CacheControl = "private, no-store";
        var catalog = await DynamicAccessEndpoints.LoadRawAsync(db, ct);
        return Results.Ok(new { version = catalog.Version, items = Project(catalog, view) });
    }

    private static async Task<IResult> AssignAsync(
        string view, DocumentAreaCollaboratorRequest request,
        HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        IAuditService audit, CancellationToken ct)
    {
        if (!CanAdminister(view, ctx, access)) return Results.Forbid();
        ctx.Response.Headers.CacheControl = "private, no-store";
        var subject = request.Subject?.Trim();
        var category = request.Category?.Trim().ToLowerInvariant();
        var actions = (request.Actions ?? []).Select(a => a?.Trim().ToLowerInvariant() ?? "")
            .Distinct().ToArray();

        if (subject is null || subject.Length is < 1 or > 320 ||
            request.Subject != subject ||
            string.IsNullOrWhiteSpace(category) ||
            !DynamicAccessEndpoints.CatalogCategories.Contains(category) ||
            actions.Length == 0 || actions.Any(a => !DelegableActions.Contains(a)) ||
            // El permiso de contenido se delega sólo en publicaciones editoriales
            // de Biblioteca (Revista y Boletín); otras categorías y Gestión
            // Documental no reciben una vía de carga.
            (actions.Contains("write") && (view != "library" || category is not ("magazine" or "bulletin"))) ||
            !actions.Contains("view") ||
            request.EffectiveTo < request.EffectiveFrom)
            return Results.BadRequest(new { code = "INVALID_COLLABORATOR_SCOPE" });

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({CatalogLock})", ct);
        var catalog = await DynamicAccessEndpoints.LoadRawAsync(db, ct);
        if (request.ExpectedVersion != catalog.Version)
            return Results.Conflict(new { code = "STALE_COLLABORATOR_CATALOG" });
        var endsOn = request.EffectiveTo ?? DateOnly.MaxValue;
        if (Project(catalog, view).Any(a =>
                a.IsActive && a.Subject == subject && a.Category == category &&
                a.EffectiveFrom <= endsOn &&
                (a.EffectiveTo ?? DateOnly.MaxValue) >= request.EffectiveFrom))
            return Results.Conflict(new { code = "COLLABORATOR_PERIOD_OVERLAP" });

        var code = CodePrefix(view) + Guid.NewGuid().ToString("N");
        catalog.Profiles.Add(new DynamicProfile(
            Guid.NewGuid(), code, "Colaborador limitado - " + view,
            "order", false, true, ["documents"],
            [new DynamicGrant(view, actions, [category])]));
        var assignment = new DynamicAssignment(
            Guid.NewGuid(), subject, code, null,
            request.EffectiveFrom, request.EffectiveTo, true);
        catalog.Assignments.Add(assignment);
        var next = catalog with { Version = catalog.Version + 1 };
        db.DynamicAccessSnapshots.Add(new DynamicAccessSnapshot
        {
            Version = next.Version,
            Payload = JsonSerializer.Serialize(next, JsonOptions)
        });
        audit.Add(ctx, "library.collaborator.assigned", "DynamicAccessCatalog",
            assignment.Id.ToString("D"), null, AuditResults.Success,
            new { area = view, category, actions, version = next.Version });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Ok(new { version = next.Version,
            assignmentId = assignment.Id, area = view,
            category, actions, assignment.EffectiveFrom,
            assignment.EffectiveTo, active = true });
    }

    private static async Task<IResult> RevokeAsync(
        string view, Guid assignmentId, int expectedVersion,
        HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        IAuditService audit, CancellationToken ct)
    {
        if (!CanAdminister(view, ctx, access)) return Results.Forbid();
        if (assignmentId == Guid.Empty)
            return Results.BadRequest(new { code = "INVALID_ASSIGNMENT_ID" });
        ctx.Response.Headers.CacheControl = "private, no-store";
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({CatalogLock})", ct);
        var catalog = await DynamicAccessEndpoints.LoadRawAsync(db, ct);
        if (expectedVersion != catalog.Version)
            return Results.Conflict(new { code = "STALE_COLLABORATOR_CATALOG" });

        var allowedIds = Project(catalog, view).Where(a => a.IsActive)
            .Select(a => a.AssignmentId).ToHashSet();
        var index = catalog.Assignments.FindIndex(a =>
            a.Id == assignmentId && allowedIds.Contains(a.Id));
        if (index < 0) return Results.NotFound();
        var prior = catalog.Assignments[index];
        catalog.Assignments[index] = prior with { IsActive = false };
        var next = catalog with { Version = catalog.Version + 1 };
        db.DynamicAccessSnapshots.Add(new DynamicAccessSnapshot
        {
            Version = next.Version,
            Payload = JsonSerializer.Serialize(next, JsonOptions)
        });
        audit.Add(ctx, "library.collaborator.revoked", "DynamicAccessCatalog",
            assignmentId.ToString("D"), null, AuditResults.Success,
            new { area = view, version = next.Version });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Ok(new { version = next.Version,
            assignmentId, revoked = true });
    }
}

public sealed record DocumentAreaCollaboratorRequest(
    string? Subject,
    string? Category,
    string[]? Actions,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int ExpectedVersion);

public sealed record DocumentAreaCollaboratorDto(
    Guid AssignmentId,
    string Subject,
    string Area,
    string Category,
    string[] Actions,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive);
