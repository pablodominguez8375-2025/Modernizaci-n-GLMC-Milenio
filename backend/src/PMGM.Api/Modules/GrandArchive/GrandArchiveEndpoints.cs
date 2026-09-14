using System.Security.Claims;
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
