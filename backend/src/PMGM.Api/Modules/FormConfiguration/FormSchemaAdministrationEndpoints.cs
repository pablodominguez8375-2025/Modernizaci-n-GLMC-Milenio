using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.FormConfiguration;

/// <summary>
/// Restricted institutional administration. Neither a user-supplied core
/// catalogue nor a client-supplied scope string can authorize publication.
/// </summary>
public static class FormSchemaAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapFormSchemaAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/system/forms")
            .WithTags("Sistema — Formularios versionados")
            .RequireAuthorization();

        group.MapGet("/catalog", GetCatalog);
        group.MapGet("/{formCode}/versions", GetVersionsAsync);
        group.MapGet("/{formCode}/versions/{version:int}", GetVersionAsync);
        group.MapPost("/{formCode}/versions", PublishVersionAsync);
        return endpoints;
    }

    private static IResult GetCatalog(HttpContext http, IInstitutionalAccessService access)
    {
        if (!access.CanConfigureSystem(http.User)) return Results.Forbid();
        NoCache(http);
        return Results.Ok(new
        {
            forms = new[]
            {
                new
                {
                    formCode = FormSchemaInstitutionalCatalog.CandidateProfile,
                    description = "Ficha privada de Insinuado",
                    trustedCoreKeys = FormSchemaInstitutionalCatalog
                        .TrustedCoreKeys(FormSchemaInstitutionalCatalog.CandidateProfile)
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .ToArray()
                }
            },
            authorizedScope = "order_administration"
        });
    }

    private static async Task<IResult> GetVersionsAsync(
        string formCode, Guid? organizationId,
        HttpContext http, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(http.User)) return Results.Forbid();
        if (!FormSchemaInstitutionalCatalog.Supports(formCode))
            return Results.NotFound();
        if (!await ValidOrganizationAsync(db, organizationId, ct))
            return Results.BadRequest(new { message = "Se requiere un Taller válido." });

        NoCache(http);
        var scope = FormSchemaScope.ForOrganization(organizationId);
        var versions = await db.FormSchemaVersions.AsNoTracking()
            .Where(x => x.FormCode == formCode && x.ScopeKey == scope)
            .OrderByDescending(x => x.Version)
            .Select(x => new
            {
                x.Version, x.EffectiveFromUtc, x.PayloadSha256, x.RecordedAtUtc
            })
            .ToListAsync(ct);
        return Results.Ok(new { formCode, organizationId, total = versions.Count, versions });
    }

    private static async Task<IResult> GetVersionAsync(
        string formCode, int version, Guid? organizationId,
        HttpContext http, PmgmDbContext db,
        IInstitutionalAccessService access, FormSchemaVersionStore store, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(http.User)) return Results.Forbid();
        if (!FormSchemaInstitutionalCatalog.Supports(formCode) || version < 1)
            return Results.NotFound();
        if (!await ValidOrganizationAsync(db, organizationId, ct))
            return Results.BadRequest(new { message = "Se requiere un Taller válido." });

        NoCache(http);
        var schema = await store.ReadAsync(formCode, version, organizationId, ct);
        return schema is null ? Results.NotFound() : Results.Ok(schema);
    }

    private static async Task<IResult> PublishVersionAsync(
        string formCode, Guid? organizationId,
        FormSchemaPublishRequest request, HttpContext http,
        PmgmDbContext db, IInstitutionalAccessService access,
        FormSchemaVersionStore store, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(http.User)) return Results.Forbid();
        if (!FormSchemaInstitutionalCatalog.Supports(formCode))
            return Results.NotFound();
        if (!await ValidOrganizationAsync(db, organizationId, ct))
            return Results.BadRequest(new { message = "Se requiere un Taller válido." });

        var subject = http.User.FindFirstValue("sub") ??
            http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 320)
            return Results.Forbid();

        if (request.ExpectedPreviousVersion < 0 ||
            request.Version != request.ExpectedPreviousVersion + 1 ||
            request.Fields is null ||
            request.EffectiveFromUtc < DateTimeOffset.UtcNow.AddSeconds(-1))
            return Results.BadRequest(new
            {
                message = "Versión, vigencia futura y campos obligatorios no válidos."
            });

        var proposed = new FormSchemaSnapshot(
            formCode, request.Version, request.EffectiveFromUtc, request.Fields);
        var safety = FormSchemaInstitutionalCatalog.ValidateForPublication(proposed);
        if (!safety.IsValid)
            return Results.BadRequest(new { errors = safety.Errors });

        try
        {
            var correlation = AuditEventFactory.ResolveCorrelationId(
                http.TraceIdentifier, http.Request.Headers["X-Correlation-ID"].ToString());
            var published = await store.PublishAsync(proposed, organizationId,
                FormSchemaInstitutionalCatalog.TrustedCoreKeys(formCode),
                subject, correlation, ct, request.ExpectedPreviousVersion);

            var location = $"/api/system/forms/{Uri.EscapeDataString(formCode)}/versions/{published.Version}" +
                (organizationId is Guid org ? $"?organizationId={org:D}" : "");
            NoCache(http);
            return Results.Created(location, new
            {
                published.FormCode, published.Version, organizationId,
                published.EffectiveFromUtc, published.PayloadSha256,
                published.RecordedAtUtc
            });
        }
        catch (FormSchemaVersionConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (
            ex.Message.StartsWith("La versión no cumple las reglas de publicación:", StringComparison.Ordinal))
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<bool> ValidOrganizationAsync(
        PmgmDbContext db, Guid? organizationId, CancellationToken ct)
        => organizationId is null ||
           organizationId != Guid.Empty &&
           await db.Organizations.AsNoTracking().AnyAsync(
               x => x.Id == organizationId && x.Type == "workshop", ct);

    private static void NoCache(HttpContext http)
        => http.Response.Headers.CacheControl = "private, no-store";
}

public sealed record FormSchemaPublishRequest(
    int Version,
    DateTimeOffset EffectiveFromUtc,
    int ExpectedPreviousVersion,
    IReadOnlyList<FormFieldDefinition>? Fields);
