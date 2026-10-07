using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Audit;

namespace PMGM.Api.Modules.Authorization;

// Extra technical restriction. Institutional handlers remain the authority for every operation.
public static class DynamicViewAccess
{
    public static bool Allows(DynamicAccessCatalog catalog, string? subject, string view, string action, Guid? organization, DateOnly today)
        => DynamicAccessEndpoints.AllowedActions.Contains(action) &&
           (!catalog.Assignments.Any(a => a.Subject == subject && a.OrganizationId == organization) ||
            DynamicAccessEndpoints.IsGranted(catalog, subject, view, action, organization, today));

    // A collection with no organization selector must not leak a denied context. All historical
    // enrollments are checked, including revoked/expired/future ones. Scoped routes use their source.
    public static Guid?[] AggregateScopes(DynamicAccessCatalog catalog, ClaimsPrincipal user)
        => catalog.Assignments.Where(a => a.Subject == DynamicTreasuryAccess.Subject(user))
            .Select(a => a.OrganizationId).DefaultIfEmpty(null).Distinct().ToArray();

    public static string? ViewFor(string route)
    {
        route = route.ToLowerInvariant();
        // Existing financial guards resolve origin IDs and expose separate institutional projections.
        if (route.EndsWith("/derecho/pagos") || route.StartsWith("/api/tesoreria") || route.StartsWith("/api/hospitalaria") ||
            route.StartsWith("/api/gestion-logial/tesoreria") || route.StartsWith("/api/gestion-logial/hospitalaria")) return null;
        // Bootstrap/session/catalog and minimal cross-domain lookups remain independently authorized.
        if (route.StartsWith("/api/session") || route.StartsWith("/api/system/access") || route == "/api/system/info" ||
            route == "/api/institutional/organizations/options" || route.StartsWith("/api/institutional-projections")) return null;
        if (route.StartsWith("/api/member-self") || route.StartsWith("/api/membership/me") || route.StartsWith("/api/gestion-logial/mi-ficha")) return "member";
        if (route.StartsWith("/api/members")) return "members";
        if (route.StartsWith("/api/institutional/organizations")) return "lodgeprofile";
        if (route.StartsWith("/api/gestion-logial") || route.StartsWith("/api/secretaria")) return "lodge";
        if (route.StartsWith("/api/regimen-interior") || route.StartsWith("/api/reporting")) return "regimen";
        if (route.StartsWith("/api/admisiones")) return "admissions";
        if (route.StartsWith("/api/insinuados/regimen-interior")) return "regimen";
        if (route.StartsWith("/api/insinuados")) return route.Contains("/flujo") || route.Contains("/deliberacion-inicial") ||
            route.Contains("/antecedentes") || route.Contains("/entrevistas") || route.Contains("/revision-tercer-grado") ||
            route.Contains("/balotaje") || route.Contains("/solicitud-iniciacion") ? "initiationcircuit" : "candidateprofile";
        if (route.StartsWith("/api/candidate-publications") || route == "/api/ceremonias/portal-insinuados") return "candidates";
        if (route.StartsWith("/api/institutional/ceremonias")) return "ceremonies";
        if (route.StartsWith("/api/ceremonias")) return route.Contains("/publicacion-insinuado") || route.Contains("/revision-publicacion-insinuado") ||
            route.Contains("/aprobar-publicacion-insinuado") ? "candidateprofile" : "ceremonies";
        if (route.StartsWith("/api/gran-secretaria") || route.StartsWith("/api/institutional/gran-secretaria")) return "secretariat";
        if (route.StartsWith("/api/biblioteca")) return "library";
        if (route.StartsWith("/api/documentos")) return "documentmanager";
        if (route.StartsWith("/api/grand-archive")) return "grandarchive";
        if (route.StartsWith("/api/calendar")) return "calendar";
        if (route.StartsWith("/api/notifications")) return "notifications";
        if (route.StartsWith("/api/system/audit")) return "access-review";
        if (route.StartsWith("/api/system") || route.StartsWith("/api/platform/bootstrap") || route.StartsWith("/api/privacy")) return "system";
        return null;
    }

    public static string ActionFor(string method, string route, string handler)
    {
        // Download/export is a read. Print is independently authorized and audited below.
        if (method is "GET" or "HEAD" || handler.StartsWith("Plan")) return "view";
        if (method == "DELETE" || handler.StartsWith("Cancel") || handler.StartsWith("Withdraw") || handler.StartsWith("Unpublish")) return "delete";
        if (route.StartsWith("/api/biblioteca/mis-planchas/") && route.EndsWith("/versiones")) return "edit";
        if (handler.StartsWith("Upload") || handler.StartsWith("Upsert") || handler.StartsWith("Import")) return "write";
        if (handler.StartsWith("Update") || handler.StartsWith("Correct") || method is "PUT" or "PATCH") return "edit";
        if (handler.StartsWith("Create") || handler.StartsWith("Register") || handler.StartsWith("Open") || handler.StartsWith("Appoint")) return "create";
        return "write";
    }

    public static IEndpointRouteBuilder MapViewAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/session/view-access", async (HttpContext http, PmgmDbContext db, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
            var scopes = AggregateScopes(catalog, http.User);
            var views = catalog.Menus.SelectMany(m => m.Views).ToDictionary(v => v.Code, v =>
                DynamicAccessEndpoints.AllowedActions.Where(a => scopes.All(org => Allows(catalog,
                    DynamicTreasuryAccess.Subject(http.User), v.Code, a, org, DynamicAccessEndpoints.Today()))).ToArray());
            // Only this subject's restrictions; never catalog, assignments, subjects or role escalation.
            return Results.Ok(new { catalog.Version, views });
        }).RequireAuthorization();
        endpoints.MapPost("/api/session/views/{viewCode}/print", async (string viewCode, Guid? organizationId,
            HttpContext http, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct) =>
        {
            var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
            if (!catalog.Menus.Any(m => m.IsActive && m.Views.Any(v => v.Code == viewCode && v.IsActive)) ||
                !CanView(http.User, access, viewCode, organizationId)) return Results.Forbid();
            var scopes = organizationId is Guid org ? new Guid?[] { org } : AggregateScopes(catalog, http.User);
            if (!scopes.All(org => Allows(catalog, DynamicTreasuryAccess.Subject(http.User), viewCode, "print", org, DynamicAccessEndpoints.Today()))) return Results.Forbid();
            audit.Add(http, "system.access.view_print_requested", "DynamicView", viewCode, organizationId, AuditResults.Success);
            await db.SaveChangesAsync(ct);
            http.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new { allowed = true });
        }).RequireAuthorization();
        return endpoints;
    }

    private static bool CanView(ClaimsPrincipal user, IInstitutionalAccessService access, string view, Guid? org)
    {
        bool Local(Func<ClaimsPrincipal, Guid, bool> check) => org is Guid id ? check(user, id) :
            user.FindAll(InstitutionalClaims.Organization).Any(c => Guid.TryParse(c.Value, out var claimed) && check(user, claimed));
        return view switch
        {
            "member" or "calendar" or "notifications" or "candidates" => user.Identity?.IsAuthenticated == true,
            "lodgeprofile" => Local(access.CanReadLodgeCouncilSummary) || Local(access.CanManageWorkshopProfile) ||
                org is null && access.HasOrderScope(user) && access.CanReadOrganization(user, Guid.Empty),
            "lodge" => Local(access.CanReadLodgeSecretariat) || Local(access.CanManageOrganization) ||
                Local(access.CanParticipateInLodgeCouncil) || Local((u, id) => Enumerable.Range(1,3).Any(degree => access.CanManageLodgeInstruction(u,id,degree))) ||
                org is null && access.HasOrderScope(user) && access.HasRole(user, InstitutionalRoles.GranLogiaAdmin),
            "members" => Local(access.CanReadOrganization) || org is null && access.HasOrderScope(user) && access.CanReadOrganization(user, Guid.Empty),
            "regimen" => access.CanRunRegimenInteriorReports(user),
            "ceremonies" => access.CanEvaluateCeremonies(user) || Local(access.CanReviewCeremonies),
            "candidateprofile" or "initiationcircuit" or "admissions" => access.CanManageGrandSecretariat(user) || Local(access.CanManageOrganization),
            "secretariat" => access.CanManageGrandSecretariat(user),
            "library" => org is null || Local(access.CanReadOrganizationLibrary),
            "documentmanager" => access.CanManageDocuments(user, org),
            "grandarchive" => access.CanManageGrandArchive(user),
            "treasury" => access.CanManageTreasuryRegularity(user),
            "lodgetreasury" => Local(access.CanManageLodgeTreasury) || Local(access.CanApproveLodgeExpenses),
            "hospitalaria" => org is null ? access.CanManageHospitalariaRegularity(user) : Local(access.CanReadLodgeHospitalaria),
            "system" or "access-review" => access.CanConfigureSystem(user),
            _ => false
        };
    }
}

public sealed class DynamicViewAccessFilter(string handler) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var http = invocation.HttpContext;
        var endpoint = http.GetEndpoint();
        var route = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "";
        var view = DynamicViewAccess.ViewFor(route);
        if (view is null || endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null || http.User.Identity?.IsAuthenticated != true)
            return await next(invocation);
        var db = http.RequestServices.GetRequiredService<PmgmDbContext>();
        var catalog = await DynamicAccessEndpoints.LoadAsync(db, http.RequestAborted);
        // Resolve stored resource before trusting selectors. A query/body organization cannot redirect an ID.
        var stored = await ResourceScope(http, db);
        if (stored.Missing) return Results.NotFound();
        Guid?[] scopes;
        if (stored.Found) scopes = [stored.Organization];
        else if (Guid.TryParse(http.Request.RouteValues["organizationId"]?.ToString(), out var routeOrg)) scopes = [routeOrg];
        else if (route.StartsWith("/api/institutional/organizations/") && Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var profileOrg)) scopes = [profileOrg];
        else
        {
            var hasResourceId = http.Request.RouteValues.Keys.Any(k => k.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
            var bodyOrQuery = hasResourceId ? [] : invocation.Arguments.Select(arg => arg?.GetType().GetProperty("OrganizationId")?.GetValue(arg)).OfType<Guid>().Distinct().ToArray();
            if (bodyOrQuery.Length == 0 && !hasResourceId &&
                Guid.TryParse(http.Request.Query["organizationId"], out var queryOrg)) bodyOrQuery = [queryOrg];
            scopes = bodyOrQuery.Length > 0 ? bodyOrQuery.Select(id => (Guid?)id).ToArray() : DynamicViewAccess.AggregateScopes(catalog, http.User);
        }
        // Order-wide enrollment restricts every domain operation even when a record has a Taller.
        if (catalog.Assignments.Any(a => a.Subject == DynamicTreasuryAccess.Subject(http.User) && a.OrganizationId is null))
            scopes = scopes.Append(null).Distinct().ToArray();
        var action = DynamicViewAccess.ActionFor(http.Request.Method, route, handler);
        if (!scopes.All(org => DynamicViewAccess.Allows(catalog, DynamicTreasuryAccess.Subject(http.User), view, action, org, DynamicAccessEndpoints.Today())))
            return Results.Forbid();
        http.Response.Headers.CacheControl = "private, no-store";
        return await next(invocation);
    }

    private sealed record Scope(bool Found, bool Missing, Guid? Organization);
    private static async Task<Scope> ResourceScope(HttpContext http, PmgmDbContext db)
    {
        var ct = http.RequestAborted;
        Guid? Id(string key) => Guid.TryParse(http.Request.RouteValues[key]?.ToString(), out var value) ? value : null;
        var route = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "";
        var lodge = http.RequestServices.GetRequiredService<LodgeManagementDbContext>();
        if (Id("meetingId") is Guid meeting && route.StartsWith("/api/gestion-logial/"))
            return From(await lodge.LodgeMeetings.Where(x => x.Id == meeting).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct));
        if (Id("meetingId") is Guid adminMeeting && route.StartsWith("/api/secretaria/reuniones/"))
            return From(await db.LodgeAdministrativeMeetings.Where(x => x.Id == adminMeeting).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct));
        if (Id("sessionId") is Guid session && route.Contains("/consejo/"))
            return From(await lodge.LodgeCouncilSessions.Where(x => x.Id == session).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct));
        if (Id("instructionId") is Guid instruction)
            return From(await lodge.LodgeInstructionSessions.Where(x => x.Id == instruction).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct));
        if (Id("intakeId") is Guid intake)
            return From(await db.HistoricalMemberIntakes.Where(x => x.Id == intake).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct));
        if (Id("caseId") is Guid admission && route.StartsWith("/api/admisiones"))
            return From(await http.RequestServices.GetRequiredService<AdmissionsDbContext>().AdmissionCases.Where(x => x.Id == admission).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct));
        if (Id("requestId") is Guid request && (route.StartsWith("/api/ceremonias") || route.StartsWith("/api/insinuados")))
            return From(await db.CeremonyRequests.Where(x => x.Id == request).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct));
        if (Id("requestId") is Guid withdrawal && route.StartsWith("/api/gestion-logial/retiros"))
            return From(await db.MemberWithdrawalRequests.Where(x => x.Id == withdrawal).Select(x => (Guid?)x.OriginOrganizationId).SingleOrDefaultAsync(ct));
        var documents = http.RequestServices.GetRequiredService<DocumentManagementDbContext>();
        // Nullable organization is legitimate for an Order document, so preserve existence separately.
        if (Id("versionId") is Guid version && (route.StartsWith("/api/documentos") || route.StartsWith("/api/biblioteca")))
        {
            var row = await documents.DocumentVersions.Where(x => x.Id == version).Select(x => new { x.Document.OrganizationId }).SingleOrDefaultAsync(ct);
            return new(true, row is null, row?.OrganizationId);
        }
        if (Id("documentId") is Guid document && (route.StartsWith("/api/documentos") || route.StartsWith("/api/biblioteca")))
        {
            var row = await documents.InstitutionalDocuments.Where(x => x.Id == document).Select(x => new { x.OrganizationId }).SingleOrDefaultAsync(ct);
            return new(true, row is null, row?.OrganizationId);
        }
        if (Id("collectionId") is Guid collection)
        {
            var row = await documents.DocumentCollections.Where(x => x.Id == collection).Select(x => new { x.OrganizationId }).SingleOrDefaultAsync(ct);
            return new(true, row is null, row?.OrganizationId);
        }
        return new(false, false, null);
        static Scope From(Guid? org) => new(true, org is null, org);
    }
}
