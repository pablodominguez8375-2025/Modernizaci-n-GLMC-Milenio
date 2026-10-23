using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.GrandArchive;

public static class GrandArchiveEndpoints
{
    public static IEndpointRouteBuilder MapGrandArchiveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/grand-archive")
            .WithTags("Gran Archivero")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapGet("/candidates", CandidatesAsync);
        group.MapGet("/{recordId:guid}", GetAsync);
        group.MapGet("/{recordId:guid}/content", ContentAsync);
        group.MapPost("/", RegisterAsync);
        group.MapPost("/{recordId:guid}/withdraw", WithdrawAsync);
        group.MapPost("/{recordId:guid}/publish", PublishAsync);
        group.MapPost("/{recordId:guid}/unpublish", UnpublishAsync);

        // Vista de lectura para todos los Hermanos (#653). Ruta fuera del prefijo /api/documentos
        // para no heredar las restricciones del Gestor Documental.
        var official = endpoints.MapGroup("/api/publicaciones-oficiales")
            .WithTags("Documentos oficiales")
            .RequireAuthorization();
        official.MapGet("/", ListOfficialAsync);
        official.MapGet("/{recordId:guid}", GetOfficialAsync);
        official.MapGet("/{recordId:guid}/content", OfficialContentAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        string? status,
        string? recordType,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? search,
        int? limit,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IGrandArchiveService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandArchive(httpContext.User)) return Results.Forbid();
        var normalizedStatus = Normalize(status);
        var normalizedType = Normalize(recordType);
        if (normalizedStatus is not null && !GrandArchiveCodes.Status.IsValid(normalizedStatus))
            return Results.BadRequest(new { message = "El estado archivístico indicado no es válido." });
        if (normalizedType is not null && !GrandArchiveCodes.RecordType.IsValid(normalizedType))
            return Results.BadRequest(new { message = "El tipo de registro archivístico no es válido." });
        if (fromDate is not null && toDate is not null && toDate < fromDate)
            return Results.BadRequest(new { message = "La fecha final no puede ser anterior a la fecha inicial." });
        if (search?.Length > 200)
            return Results.BadRequest(new { message = "La búsqueda supera el máximo permitido." });

        var response = await service.ListAsync(new GrandArchiveQuery(
            normalizedStatus,
            normalizedType,
            fromDate,
            toDate,
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            Math.Clamp(limit ?? 200, 1, 500)), cancellationToken);
        NoStore(httpContext);
        return Results.Ok(response);
    }

    private static async Task<IResult> CandidatesAsync(
        string? search,
        int? limit,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IGrandArchiveService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandArchive(httpContext.User)) return Results.Forbid();
        if (search?.Length > 200) return Results.BadRequest(new { message = "La búsqueda supera el máximo permitido." });
        var items = await service.CandidatesAsync(
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            Math.Clamp(limit ?? 100, 1, 250),
            cancellationToken);
        NoStore(httpContext);
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> GetAsync(
        Guid recordId,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IGrandArchiveService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandArchive(httpContext.User)) return Results.Forbid();
        var item = await service.GetAsync(recordId, cancellationToken);
        if (item is null) return Results.NotFound(new { message = "El registro archivístico no existe." });
        NoStore(httpContext);
        return Results.Ok(item);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterGrandArchiveRequest request,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IGrandArchiveService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandArchive(httpContext.User)) return Results.Forbid();
        var actor = Actor(httpContext.User);
        if (actor is null) return Results.Unauthorized();
        var recordType = Normalize(request.RecordType);
        if (string.IsNullOrWhiteSpace(request.ArchiveCode) || request.ArchiveCode.Trim().Length is < 3 or > 120)
            return Results.BadRequest(new { message = "El código archivístico debe tener entre 3 y 120 caracteres." });
        if (recordType is null || !GrandArchiveCodes.RecordType.IsValid(recordType))
            return Results.BadRequest(new { message = "El tipo de registro archivístico no es válido." });
        if (request.OriginatingBody?.Length > 240 || request.HistoricalPeriod?.Length > 160 || request.Description?.Length > 2000)
            return Results.BadRequest(new { message = "Uno o más campos de metadatos superan el máximo permitido." });

        try
        {
            var result = await service.RegisterAsync(new RegisterGrandArchiveCommand(
                request.DocumentId,
                request.DocumentVersionId,
                request.ArchiveCode,
                recordType,
                request.DocumentDate,
                request.OriginatingBody,
                request.HistoricalPeriod,
                request.Description), actor, cancellationToken);
            NoStore(httpContext);
            return Results.Created($"/api/grand-archive/{result.Id}", result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (GrandArchivePolicyException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (GrandArchiveDuplicateException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static async Task<IResult> WithdrawAsync(
        Guid recordId,
        WithdrawGrandArchiveRequest request,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IGrandArchiveService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandArchive(httpContext.User)) return Results.Forbid();
        var actor = Actor(httpContext.User);
        if (actor is null) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 10 or > 500)
            return Results.BadRequest(new { message = "El retiro requiere un motivo de 10 a 500 caracteres." });

        try
        {
            var result = await service.WithdrawAsync(recordId, request.Reason, actor, cancellationToken);
            NoStore(httpContext);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (GrandArchivePolicyException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static async Task<IResult> ContentAsync(
        Guid recordId,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IGrandArchiveService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandArchive(httpContext.User)) return Results.Forbid();
        try
        {
            var content = await service.OpenContentAsync(recordId, cancellationToken);
            if (content is null) return Results.NotFound(new { message = "El registro archivístico no existe." });
            NoStore(httpContext);
            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.Stream(content.Content, content.ContentType, fileDownloadName: content.FileName, enableRangeProcessing: false);
        }
        catch (GrandArchivePolicyException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (GrandArchiveSourceUnavailableException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> PublishAsync(
        Guid recordId,
        PublishGrandArchiveRequest request,
        HttpContext httpContext,
        IGrandArchiveService service,
        IOfficialDocumentNoticeService notices,
        IAuditService audit,
        PmgmDbContext db,
        CancellationToken cancellationToken)
    {
        // Solo el perfil Gran Archivero publica; ni el administrador de Gran Logia ni el Máster.
        if (!GrandArchivePolicy.IsGrandArchivist(httpContext.User)) return Results.Forbid();
        var actor = Actor(httpContext.User);
        if (actor is null) return Results.Unauthorized();
        if (!GrandArchivePolicy.IsValidDegree(request.MinimumDegree))
            return Results.BadRequest(new { message = "El grado mínimo debe ser 1 (todos), 2 (desde Compañero) o 3 (solo Maestro)." });
        try
        {
            var until = request.Until is null ? (DateTimeOffset?)null : GrandArchivePolicy.EndOfChileanDay(request.Until.Value);
            var result = await service.PublishAsync(recordId, request.MinimumDegree, until, actor, cancellationToken);
            audit.Add(httpContext, "grandarchive.published", "GrandArchiveRecord", recordId.ToString(), null, AuditResults.Success,
                new { result.ArchiveCode, minimumDegree = request.MinimumDegree, until = result.PublishedUntilUtc });
            await db.SaveChangesAsync(cancellationToken);
            // El aviso es complementario: si falla, la publicación ya está hecha y auditada.
            try
            {
                var notice = await notices.NotifyPublishedAsync(recordId, result.ArchiveCode, result.RecordType, result.Title, request.MinimumDegree, result.PublishedAtUtc ?? DateTimeOffset.UtcNow, cancellationToken);
                audit.Add(httpContext, "grandarchive.publication_notified", "GrandArchiveRecord", recordId.ToString(), null,
                    notice.Failed == 0 ? AuditResults.Success : AuditResults.Rejected,
                    new { result.ArchiveCode, notice.Eligible, notice.Queued, notice.Failed });
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
            {
                db.ChangeTracker.Clear();
            }
            NoStore(httpContext);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { message = ex.Message }); }
        catch (GrandArchivePolicyException ex) { return Results.Conflict(new { message = ex.Message }); }
        catch (GrandArchiveSourceUnavailableException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> UnpublishAsync(
        Guid recordId,
        UnpublishGrandArchiveRequest request,
        HttpContext httpContext,
        IGrandArchiveService service,
        IAuditService audit,
        PmgmDbContext db,
        CancellationToken cancellationToken)
    {
        if (!GrandArchivePolicy.IsGrandArchivist(httpContext.User)) return Results.Forbid();
        var actor = Actor(httpContext.User);
        if (actor is null) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 10 or > 500)
            return Results.BadRequest(new { message = "Retirar la publicación requiere un motivo de 10 a 500 caracteres." });
        try
        {
            var result = await service.UnpublishAsync(recordId, request.Reason, actor, cancellationToken);
            audit.Add(httpContext, "grandarchive.unpublished", "GrandArchiveRecord", recordId.ToString(), null, AuditResults.Success,
                new { result.ArchiveCode, reason = request.Reason.Trim() });
            await db.SaveChangesAsync(cancellationToken);
            NoStore(httpContext);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { message = ex.Message }); }
        catch (GrandArchivePolicyException ex) { return Results.Conflict(new { message = ex.Message }); }
    }

    // Grado efectivo del lector: quien administra el archivo ve todo; un usuario sin ficha de Hermano no ve nada.
    private static async Task<int> ViewerDegreeAsync(
        HttpContext httpContext, IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver resolver, CancellationToken cancellationToken)
    {
        if (access.CanManageGrandArchive(httpContext.User) || access.IsPlatformSuperAdmin(httpContext.User)) return 3;
        var member = await resolver.ResolveAsync(httpContext.User, cancellationToken);
        return member?.EffectiveDegree ?? 0;
    }

    private static async Task<IResult> ListOfficialAsync(
        string? recordType, int? year, string? search, int? limit,
        HttpContext httpContext, IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver resolver, IGrandArchiveService service,
        CancellationToken cancellationToken)
    {
        var normalizedType = Normalize(recordType);
        if (normalizedType is not null && !GrandArchiveCodes.RecordType.IsPublishable(normalizedType))
            return Results.BadRequest(new { message = "El tipo de documento no es válido." });
        if (year is not null && year is < 1800 or > 2200)
            return Results.BadRequest(new { message = "El año indicado no es válido." });
        if (search?.Length > 200) return Results.BadRequest(new { message = "La búsqueda supera el máximo permitido." });
        var degree = await ViewerDegreeAsync(httpContext, access, resolver, cancellationToken);
        var response = await service.ListPublishedAsync(new GrandArchiveQuery(
            null, normalizedType,
            year is null ? null : new DateOnly(year.Value, 1, 1),
            year is null ? null : new DateOnly(year.Value, 12, 31),
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            Math.Clamp(limit ?? 200, 1, 500)), degree, cancellationToken);
        NoStore(httpContext);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetOfficialAsync(
        Guid recordId, HttpContext httpContext, IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver resolver, IGrandArchiveService service,
        CancellationToken cancellationToken)
    {
        var degree = await ViewerDegreeAsync(httpContext, access, resolver, cancellationToken);
        var item = await service.GetPublishedAsync(recordId, degree, cancellationToken);
        NoStore(httpContext);
        return item is null ? Results.NotFound(new { message = "El documento no existe o no está disponible para su grado." }) : Results.Ok(item);
    }

    private static async Task<IResult> OfficialContentAsync(
        Guid recordId, HttpContext httpContext, IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver resolver, IGrandArchiveService service,
        CancellationToken cancellationToken)
    {
        var degree = await ViewerDegreeAsync(httpContext, access, resolver, cancellationToken);
        try
        {
            var content = await service.OpenPublishedContentAsync(recordId, degree, cancellationToken);
            NoStore(httpContext);
            if (content is null) return Results.NotFound(new { message = "El documento no existe o no está disponible para su grado." });
            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.Stream(content.Content, content.ContentType, fileDownloadName: content.FileName, enableRangeProcessing: false);
        }
        catch (GrandArchivePolicyException ex) { return Results.Conflict(new { message = ex.Message }); }
        catch (GrandArchiveSourceUnavailableException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static ArchiveActor? Actor(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject)) return null;
        var displayName = user.Identity?.Name ?? user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name);
        return new ArchiveActor(subject, displayName);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static void NoStore(HttpContext context)
        => context.Response.Headers.CacheControl = "private, no-store";
}

public sealed record RegisterGrandArchiveRequest(
    Guid DocumentId,
    Guid DocumentVersionId,
    string ArchiveCode,
    string RecordType,
    DateOnly? DocumentDate,
    string? OriginatingBody,
    string? HistoricalPeriod,
    string? Description);

public sealed record WithdrawGrandArchiveRequest(string Reason);

public sealed record PublishGrandArchiveRequest(int MinimumDegree, DateOnly? Until = null);
public sealed record UnpublishGrandArchiveRequest(string Reason);
