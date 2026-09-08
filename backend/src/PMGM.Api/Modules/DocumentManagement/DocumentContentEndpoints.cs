using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.DocumentManagement;

public static class DocumentContentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentContentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var versions = endpoints.MapGroup("/api/documentos/versiones")
            .WithTags("Contenido Documental")
            .RequireAuthorization();

        versions.MapPut("/{versionId:guid}/contenido", UploadContentAsync);
        versions.MapPost("/{versionId:guid}/analizar", ScanContentAsync);
        versions.MapGet("/{versionId:guid}/contenido", DownloadManagedContentAsync);

        endpoints.MapGet("/api/biblioteca/{documentId:guid}/contenido", DownloadLibraryContentAsync)
            .WithTags("Biblioteca Virtual")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> UploadContentAsync(
        Guid versionId,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        IDocumentObjectStore objectStore,
        IOptions<DocumentStorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        var version = await db.DocumentVersions
            .Include(x => x.Document)
            .SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);

        if (version is null) return Results.NotFound(new { message = "La versión indicada no existe." });
        if (!access.CanManageDocuments(httpContext.User, version.Document.OrganizationId)) return Results.Forbid();
        if (version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.PendingUpload)
            return Results.Conflict(new { message = "Esta versión ya inició su ciclo de carga y no puede sobrescribirse." });

        var contentLength = httpContext.Request.ContentLength;
        if (contentLength is null)
            return Results.Json(new { message = "La carga requiere Content-Length." }, statusCode: StatusCodes.Status411LengthRequired);
        if (contentLength <= 0 || contentLength != version.SizeBytes)
            return Results.BadRequest(new { message = "El tamaño recibido no coincide con el tamaño reservado para la versión." });
        if (contentLength > storageOptions.Value.MaxUploadBytes)
            return Results.Json(new { message = "El archivo excede el tamaño máximo permitido." }, statusCode: StatusCodes.Status413PayloadTooLarge);

        var receivedContentType = NormalizeContentType(httpContext.Request.ContentType);
        var expectedContentType = NormalizeContentType(version.ContentType);
        if (receivedContentType is null || expectedContentType is null ||
            !string.Equals(receivedContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(
                new { message = "El tipo MIME recibido no coincide con el tipo registrado para la versión." },
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        if (await objectStore.ExistsAsync(version.ObjectKey, cancellationToken))
            return Results.Conflict(new { message = "Ya existe contenido físico para esta versión; no se permite sobrescritura." });

        await objectStore.StoreAsync(version.ObjectKey, httpContext.Request.Body, expectedContentType, cancellationToken);

        DocumentContentIntegrityResult integrity;
        await using (var persisted = await objectStore.OpenReadAsync(version.ObjectKey, cancellationToken))
        {
            integrity = await DocumentContentIntegrity.ComputeAsync(persisted, cancellationToken);
        }

        if (integrity.SizeBytes != version.SizeBytes)
        {
            await objectStore.DeleteAsync(version.ObjectKey, cancellationToken);
            return Results.BadRequest(new { message = "La verificación posterior a la carga detectó un tamaño inconsistente." });
        }

        var transition = DocumentVersionLifecycle.CanTransition(
            version.ProcessingStatus,
            DocumentManagementCodes.ProcessingStatus.Uploaded,
            integrity.Sha256,
            version.ScanReference);

        if (!transition.Allowed)
        {
            await objectStore.DeleteAsync(version.ObjectKey, cancellationToken);
            return Results.Conflict(new { message = transition.Error });
        }

        version.Sha256 = integrity.Sha256;
        version.ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Uploaded;

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "documents.content.uploaded",
            nameof(DocumentVersion),
            version.Id.ToString(),
            version.Document.OrganizationId,
            AuditResults.Success,
            new
            {
                version.DocumentId,
                version.VersionNumber,
                SizeBytes = integrity.SizeBytes,
                version.ProcessingStatus,
                HasIntegrityHash = true
            }));

        await db.SaveChangesAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";

        return Results.Ok(new
        {
            version.Id,
            version.DocumentId,
            version.VersionNumber,
            version.ProcessingStatus,
            SizeBytes = integrity.SizeBytes,
            HasIntegrityHash = true
        });
    }

    private static async Task<IResult> ScanContentAsync(
        Guid versionId,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        IDocumentObjectStore objectStore,
        IDocumentMalwareScanner scanner,
        CancellationToken cancellationToken)
    {
        var version = await db.DocumentVersions
            .Include(x => x.Document)
            .SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);

        if (version is null) return Results.NotFound(new { message = "La versión indicada no existe." });
        if (!access.CanManageDocuments(httpContext.User, version.Document.OrganizationId)) return Results.Forbid();

        if (version.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Uploaded)
        {
            var startDecision = DocumentVersionLifecycle.CanTransition(
                version.ProcessingStatus,
                DocumentManagementCodes.ProcessingStatus.Scanning,
                version.Sha256,
                version.ScanReference);

            if (!startDecision.Allowed) return Results.Conflict(new { message = startDecision.Error });

            version.ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Scanning;
            db.AuditEvents.Add(AuditEventFactory.Create(
                httpContext,
                "documents.content.scan_started",
                nameof(DocumentVersion),
                version.Id.ToString(),
                version.Document.OrganizationId,
                AuditResults.Success,
                new { version.DocumentId, version.VersionNumber, version.ProcessingStatus }));
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Scanning)
        {
            return Results.Conflict(new { message = "Sólo una versión cargada o un análisis interrumpido puede analizarse." });
        }

        if (!await objectStore.ExistsAsync(version.ObjectKey, cancellationToken))
            return Results.Conflict(new { message = "El objeto físico asociado a la versión no existe." });

        DocumentMalwareScanResult scanResult;
        try
        {
            await using var content = await objectStore.OpenReadAsync(version.ObjectKey, cancellationToken);
            scanResult = await scanner.ScanAsync(content, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or SocketException or InvalidDataException)
        {
            db.AuditEvents.Add(AuditEventFactory.Create(
                httpContext,
                "documents.content.scan_unavailable",
                nameof(DocumentVersion),
                version.Id.ToString(),
                version.Document.OrganizationId,
                AuditResults.Observed,
                new { version.DocumentId, version.VersionNumber, version.ProcessingStatus }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Json(
                new { message = "El servicio de análisis no pudo completar la revisión. La versión permanece en estado scanning para reintento." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var targetStatus = scanResult.IsClean
            ? DocumentManagementCodes.ProcessingStatus.Available
            : DocumentManagementCodes.ProcessingStatus.Rejected;

        var finishDecision = DocumentVersionLifecycle.CanTransition(
            version.ProcessingStatus,
            targetStatus,
            version.Sha256,
            scanResult.EvidenceReference);

        if (!finishDecision.Allowed) return Results.Conflict(new { message = finishDecision.Error });

        version.ScanReference = scanResult.EvidenceReference;
        version.ProcessingStatus = targetStatus;

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            scanResult.IsClean ? "documents.content.scan_clean" : "documents.content.scan_rejected",
            nameof(DocumentVersion),
            version.Id.ToString(),
            version.Document.OrganizationId,
            scanResult.IsClean ? AuditResults.Success : AuditResults.Rejected,
            new
            {
                version.DocumentId,
                version.VersionNumber,
                version.ProcessingStatus,
                ScanCompleted = true,
                Clean = scanResult.IsClean
            }));

        await db.SaveChangesAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";

        return Results.Ok(new
        {
            version.Id,
            version.DocumentId,
            version.VersionNumber,
            version.ProcessingStatus,
            Clean = scanResult.IsClean
        });
    }

    private static async Task<IResult> DownloadManagedContentAsync(
        Guid versionId,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        IDocumentObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var version = await db.DocumentVersions
            .Include(x => x.Document)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);

        if (version is null) return Results.NotFound();
        if (!access.CanManageDocuments(httpContext.User, version.Document.OrganizationId)) return Results.Forbid();
        if (version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available)
            return Results.Conflict(new { message = "El contenido sólo puede descargarse cuando la versión está disponible." });
        if (!await objectStore.ExistsAsync(version.ObjectKey, cancellationToken))
            return Results.NotFound(new { message = "El objeto físico de la versión no está disponible." });

        await AuditSensitiveDownloadAsync(httpContext, db, version, "documents.content.downloaded", cancellationToken);

        var content = await objectStore.OpenReadAsync(version.ObjectKey, cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Stream(content, version.ContentType, version.OriginalFileName, enableRangeProcessing: false);
    }

    private static async Task<IResult> DownloadLibraryContentAsync(
        Guid documentId,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        IDocumentObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var document = await db.InstitutionalDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken);

        if (document is null ||
            document.Status != DocumentManagementCodes.DocumentStatus.Published ||
            document.PublishedVersionId is null)
        {
            return Results.NotFound();
        }

        if (!CanReadPublishedDocument(httpContext, access, document)) return Results.Forbid();

        var version = await db.DocumentVersions.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == document.PublishedVersionId.Value && x.DocumentId == document.Id,
                cancellationToken);

        if (version is null || version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available)
            return Results.NotFound();
        if (!await objectStore.ExistsAsync(version.ObjectKey, cancellationToken))
            return Results.NotFound(new { message = "El contenido publicado no está disponible físicamente." });

        version.Document = document;
        await AuditSensitiveDownloadAsync(httpContext, db, version, "library.content.downloaded", cancellationToken);

        var content = await objectStore.OpenReadAsync(version.ObjectKey, cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Stream(content, version.ContentType, version.OriginalFileName, enableRangeProcessing: false);
    }

    private static bool CanReadPublishedDocument(
        HttpContext httpContext,
        IInstitutionalAccessService access,
        InstitutionalDocument document)
    {
        if (document.AccessPolicy == DocumentManagementCodes.AccessPolicy.LibraryAuthenticated) return true;

        return document.AccessPolicy == DocumentManagementCodes.AccessPolicy.OrganizationAuthenticated &&
               document.OrganizationId is not null &&
               access.CanReadOrganizationLibrary(httpContext.User, document.OrganizationId.Value);
    }

    private static async Task AuditSensitiveDownloadAsync(
        HttpContext httpContext,
        DocumentManagementDbContext db,
        DocumentVersion version,
        string action,
        CancellationToken cancellationToken)
    {
        var classification = version.Document.Classification;
        if (classification is not (DocumentManagementCodes.Classification.Sensitive or DocumentManagementCodes.Classification.Restricted))
            return;

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            action,
            nameof(DocumentVersion),
            version.Id.ToString(),
            version.Document.OrganizationId,
            AuditResults.Success,
            new
            {
                version.DocumentId,
                version.VersionNumber,
                Classification = classification,
                Authorized = true
            }));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeContentType(string? value)
    {
        var normalized = value?.Split(';', 2, StringSplitOptions.TrimEntries)[0].Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.ToLowerInvariant();
    }
}
