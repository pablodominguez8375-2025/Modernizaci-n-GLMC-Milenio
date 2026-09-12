using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.DocumentManagement;

public static class DocumentManagementEndpoints
{
    public static IEndpointRouteBuilder MapDocumentManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var documents = endpoints.MapGroup("/api/documentos")
            .WithTags("Gestor Documental")
            .RequireAuthorization();

        documents.MapGet("/colecciones", GetCollectionsAsync);
        documents.MapPost("/colecciones", CreateCollectionAsync);
        documents.MapPost("/colecciones/{collectionId:guid}/documentos", CreateDocumentAsync);
        documents.MapGet("/{documentId:guid}", GetDocumentAsync);
        documents.MapPost("/{documentId:guid}/versiones", CreateVersionAsync);
        documents.MapPost("/versiones/{versionId:guid}/estado", TransitionVersionAsync);
        documents.MapPost("/{documentId:guid}/publicar", PublishDocumentAsync);
        documents.MapPost("/{documentId:guid}/retirar-publicacion", UnpublishDocumentAsync);

        var library = endpoints.MapGroup("/api/biblioteca")
            .WithTags("Biblioteca Virtual")
            .RequireAuthorization();

        library.MapGet("", GetLibraryAsync);
        library.MapGet("/{documentId:guid}", GetLibraryDocumentAsync);

        return endpoints;
    }

    private static async Task<IResult> GetCollectionsAsync(
        Guid? organizationId,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (organizationId is null)
        {
            if (!access.CanManageDocuments(httpContext.User, null)) return Results.Forbid();
        }
        else if (!access.CanManageDocuments(httpContext.User, organizationId))
        {
            return Results.Forbid();
        }

        var query = db.DocumentCollections.AsNoTracking();
        if (organizationId is not null)
            query = query.Where(x => x.OrganizationId == organizationId);

        var items = await query
            .OrderBy(x => x.Name)
            .Take(500)
            .Select(x => new DocumentCollectionDto(
                x.Id,
                x.Code,
                x.Name,
                x.Description,
                x.Scope,
                x.OrganizationId,
                x.Status))
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new DocumentCollectionsResponse(items.Count, items));
    }

    private static async Task<IResult> CreateCollectionAsync(
        CreateDocumentCollectionRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var scope = request.Scope?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!DocumentManagementCodes.Scope.IsValid(scope))
            return Results.BadRequest(new { message = "El alcance de la colección no es válido." });

        Guid? organizationId = scope == DocumentManagementCodes.Scope.Organization
            ? request.OrganizationId
            : null;

        if (scope == DocumentManagementCodes.Scope.Organization && organizationId is null)
            return Results.BadRequest(new { message = "Las colecciones de Taller requieren organización." });
        if (scope == DocumentManagementCodes.Scope.Order && request.OrganizationId is not null)
            return Results.BadRequest(new { message = "Una colección de alcance Orden no debe asociarse a un Taller." });
        if (!access.CanManageDocuments(httpContext.User, organizationId)) return Results.Forbid();

        if (organizationId is not null)
        {
            var exists = await institutionalDb.Organizations.AsNoTracking()
                .AnyAsync(x => x.Id == organizationId && x.Type == "workshop", cancellationToken);
            if (!exists) return Results.NotFound(new { message = "El Taller indicado no existe." });
        }

        var code = NormalizeCode(request.Code);
        var name = NormalizeRequired(request.Name);
        if (code is null || name is null)
            return Results.BadRequest(new { message = "Código y nombre de colección son obligatorios." });

        if (await db.DocumentCollections.AnyAsync(x => x.Code == code, cancellationToken))
            return Results.Conflict(new { message = "Ya existe una colección con ese código." });

        var collection = new DocumentCollection
        {
            Code = code,
            Name = name,
            Description = NormalizeOptional(request.Description),
            Scope = scope,
            OrganizationId = organizationId,
            Status = DocumentManagementCodes.CollectionStatus.Active,
            CreatedBySubject = GetSubject(httpContext.User)
        };

        db.DocumentCollections.Add(collection);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "documents.collection.created",
            nameof(DocumentCollection),
            collection.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { collection.Code, collection.Scope, collection.Status }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/documentos/colecciones/{collection.Id}", ToCollectionDto(collection));
    }

    private static async Task<IResult> CreateDocumentAsync(
        Guid collectionId,
        CreateInstitutionalDocumentRequest request,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var collection = await db.DocumentCollections.SingleOrDefaultAsync(x => x.Id == collectionId, cancellationToken);
        if (collection is null) return Results.NotFound(new { message = "La colección indicada no existe." });
        if (!access.CanManageDocuments(httpContext.User, collection.OrganizationId)) return Results.Forbid();
        if (collection.Status != DocumentManagementCodes.CollectionStatus.Active)
            return Results.Conflict(new { message = "La colección no se encuentra activa." });

        var title = NormalizeRequired(request.Title);
        var documentType = NormalizeRequired(request.DocumentType);
        var classification = request.Classification?.Trim().ToLowerInvariant() ?? string.Empty;
        var accessPolicy = request.AccessPolicy?.Trim().ToLowerInvariant() ?? string.Empty;

        if (title is null || documentType is null)
            return Results.BadRequest(new { message = "Título y tipo documental son obligatorios." });
        if (!DocumentManagementCodes.Classification.IsValid(classification))
            return Results.BadRequest(new { message = "La clasificación documental no es válida." });
        if (!DocumentManagementCodes.AccessPolicy.IsValid(accessPolicy))
            return Results.BadRequest(new { message = "La política de acceso documental no es válida." });

        var document = new InstitutionalDocument
        {
            CollectionId = collection.Id,
            Collection = collection,
            OrganizationId = collection.OrganizationId,
            Title = title,
            DocumentType = documentType,
            Classification = classification,
            AccessPolicy = accessPolicy,
            Status = DocumentManagementCodes.DocumentStatus.Draft,
            CreatedBySubject = GetSubject(httpContext.User)
        };

        db.InstitutionalDocuments.Add(document);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "documents.document.created",
            nameof(InstitutionalDocument),
            document.Id.ToString(),
            document.OrganizationId,
            AuditResults.Success,
            new { document.CollectionId, document.DocumentType, document.AccessPolicy, document.Status }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/documentos/{document.Id}", ToDocumentDto(document, collection.Code, Array.Empty<DocumentVersion>()));
    }

    private static async Task<IResult> GetDocumentAsync(
        Guid documentId,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var document = await db.InstitutionalDocuments.AsNoTracking()
            .Include(x => x.Collection)
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document is null) return Results.NotFound();
        if (!access.CanManageDocuments(httpContext.User, document.OrganizationId)) return Results.Forbid();

        var versions = await db.DocumentVersions.AsNoTracking()
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(ToDocumentDto(document, document.Collection.Code, versions));
    }

    private static async Task<IResult> CreateVersionAsync(
        Guid documentId,
        CreateDocumentVersionRequest request,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var document = await db.InstitutionalDocuments.SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document is null) return Results.NotFound(new { message = "El documento indicado no existe." });
        if (!access.CanManageDocuments(httpContext.User, document.OrganizationId)) return Results.Forbid();
        if (document.Status == DocumentManagementCodes.DocumentStatus.Retired)
            return Results.Conflict(new { message = "No se pueden agregar versiones a un documento retirado." });

        var fileName = NormalizeFileName(request.OriginalFileName);
        var contentType = NormalizeRequired(request.ContentType);
        if (fileName is null || contentType is null)
            return Results.BadRequest(new { message = "Nombre de archivo y tipo MIME son obligatorios." });
        if (request.SizeBytes <= 0)
            return Results.BadRequest(new { message = "El tamaño del archivo debe ser mayor que cero." });

        var lastVersion = await db.DocumentVersions
            .Where(x => x.DocumentId == documentId)
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var version = new DocumentVersion
        {
            DocumentId = documentId,
            VersionNumber = checked(lastVersion + 1),
            OriginalFileName = fileName,
            ContentType = contentType,
            SizeBytes = request.SizeBytes,
            ProcessingStatus = DocumentManagementCodes.ProcessingStatus.PendingUpload,
            CreatedBySubject = GetSubject(httpContext.User)
        };
        version.ObjectKey = DocumentObjectKeyFactory.Create(documentId, version.Id);

        db.DocumentVersions.Add(version);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "documents.version.created",
            nameof(DocumentVersion),
            version.Id.ToString(),
            document.OrganizationId,
            AuditResults.Success,
            new { version.DocumentId, version.VersionNumber, version.ContentType, version.SizeBytes, version.ProcessingStatus }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/documentos/{documentId}/versiones/{version.Id}", ToVersionDto(version));
    }

    private static async Task<IResult> TransitionVersionAsync(
        Guid versionId,
        TransitionDocumentVersionRequest request,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var version = await db.DocumentVersions
            .Include(x => x.Document)
            .SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);
        if (version is null) return Results.NotFound(new { message = "La versión indicada no existe." });
        if (!access.CanManageDocuments(httpContext.User, version.Document.OrganizationId)) return Results.Forbid();

        var targetStatus = request.TargetStatus?.Trim().ToLowerInvariant() ?? string.Empty;
        var effectiveSha = string.IsNullOrWhiteSpace(request.Sha256) ? version.Sha256 : request.Sha256.Trim().ToLowerInvariant();
        var effectiveScanReference = string.IsNullOrWhiteSpace(request.ScanReference) ? version.ScanReference : request.ScanReference.Trim();
        var decision = DocumentVersionLifecycle.CanTransition(version.ProcessingStatus, targetStatus, effectiveSha, effectiveScanReference);
        if (!decision.Allowed) return Results.Conflict(new { message = decision.Error });

        var previousStatus = version.ProcessingStatus;
        version.ProcessingStatus = targetStatus;
        if (DocumentIntegrity.IsValidSha256(effectiveSha)) version.Sha256 = effectiveSha;
        if (!string.IsNullOrWhiteSpace(effectiveScanReference)) version.ScanReference = effectiveScanReference;

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "documents.version.status_changed",
            nameof(DocumentVersion),
            version.Id.ToString(),
            version.Document.OrganizationId,
            AuditResults.Success,
            new
            {
                version.DocumentId,
                version.VersionNumber,
                PreviousStatus = previousStatus,
                TargetStatus = version.ProcessingStatus,
                HasIntegrityHash = version.Sha256 is not null,
                ScanEvidenceRecorded = version.ScanReference is not null
            }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToVersionDto(version));
    }

    private static async Task<IResult> PublishDocumentAsync(
        Guid documentId,
        PublishDocumentRequest request,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var document = await db.InstitutionalDocuments
            .Include(x => x.Collection)
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document is null) return Results.NotFound(new { message = "El documento indicado no existe." });
        if (!access.CanManageDocuments(httpContext.User, document.OrganizationId)) return Results.Forbid();
        if (document.Status == DocumentManagementCodes.DocumentStatus.Retired)
            return Results.Conflict(new { message = "El documento se encuentra retirado." });
        if (!DocumentManagementCodes.AccessPolicy.CanPublish(document.AccessPolicy))
            return Results.Conflict(new { message = "La política de acceso de este documento no permite publicarlo en Biblioteca." });

        var version = await db.DocumentVersions.SingleOrDefaultAsync(
            x => x.Id == request.VersionId && x.DocumentId == documentId,
            cancellationToken);
        if (version is null) return Results.NotFound(new { message = "La versión indicada no pertenece al documento." });
        if (version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available ||
            !DocumentIntegrity.IsValidSha256(version.Sha256) ||
            string.IsNullOrWhiteSpace(version.ScanReference))
        {
            return Results.Conflict(new { message = "Sólo puede publicarse una versión disponible, íntegra y con escaneo registrado." });
        }

        document.PublishedVersionId = version.Id;
        document.PublishedAtUtc = DateTimeOffset.UtcNow;
        document.Status = DocumentManagementCodes.DocumentStatus.Published;

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "documents.document.published",
            nameof(InstitutionalDocument),
            document.Id.ToString(),
            document.OrganizationId,
            AuditResults.Success,
            new { VersionId = version.Id, version.VersionNumber, document.AccessPolicy, document.Status }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToLibraryDto(document, document.Collection.Name, version));
    }

    private static async Task<IResult> UnpublishDocumentAsync(
        Guid documentId,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var document = await db.InstitutionalDocuments.SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document is null) return Results.NotFound();
        if (!access.CanManageDocuments(httpContext.User, document.OrganizationId)) return Results.Forbid();
        if (document.PublishedVersionId is null)
            return Results.Conflict(new { message = "El documento no tiene una versión publicada." });

        var previousVersionId = document.PublishedVersionId;
        document.PublishedVersionId = null;
        document.PublishedAtUtc = null;
        document.Status = DocumentManagementCodes.DocumentStatus.Active;

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "documents.document.unpublished",
            nameof(InstitutionalDocument),
            document.Id.ToString(),
            document.OrganizationId,
            AuditResults.Success,
            new { PreviousVersionId = previousVersionId, document.Status }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { document.Id, document.Status });
    }

    private static async Task<IResult> GetLibraryAsync(
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var organizationIds = GetOrganizationIds(httpContext.User);
        var canReadAllOrganizations = access.CanManageDocuments(httpContext.User, null);

        var query =
            from document in db.InstitutionalDocuments.AsNoTracking()
            join collection in db.DocumentCollections.AsNoTracking() on document.CollectionId equals collection.Id
            join version in db.DocumentVersions.AsNoTracking() on document.PublishedVersionId equals version.Id
            where document.Status == DocumentManagementCodes.DocumentStatus.Published &&
                  version.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Available &&
                  (document.AccessPolicy == DocumentManagementCodes.AccessPolicy.LibraryAuthenticated ||
                   (document.AccessPolicy == DocumentManagementCodes.AccessPolicy.OrganizationAuthenticated &&
                    document.OrganizationId != null &&
                    (canReadAllOrganizations || organizationIds.Contains(document.OrganizationId.Value))))
            orderby document.PublishedAtUtc descending, document.Title
            select new LibraryDocumentDto(
                document.Id,
                document.Title,
                document.DocumentType,
                collection.Name,
                version.VersionNumber,
                version.ContentType,
                version.SizeBytes,
                document.PublishedAtUtc!.Value);

        var items = await query.Take(500).ToListAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LibraryDocumentsResponse(items.Count, items));
    }

    private static async Task<IResult> GetLibraryDocumentAsync(
        Guid documentId,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var row = await (
            from document in db.InstitutionalDocuments.AsNoTracking()
            join collection in db.DocumentCollections.AsNoTracking() on document.CollectionId equals collection.Id
            join version in db.DocumentVersions.AsNoTracking() on document.PublishedVersionId equals version.Id
            where document.Id == documentId &&
                  document.Status == DocumentManagementCodes.DocumentStatus.Published &&
                  version.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Available
            select new { document, CollectionName = collection.Name, version })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null) return Results.NotFound();
        if (!CanReadPublishedDocument(httpContext.User, access, row.document)) return Results.Forbid();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(ToLibraryDto(row.document, row.CollectionName, row.version));
    }

    private static bool CanReadPublishedDocument(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        InstitutionalDocument document)
    {
        if (document.AccessPolicy == DocumentManagementCodes.AccessPolicy.LibraryAuthenticated) return true;
        if (document.AccessPolicy != DocumentManagementCodes.AccessPolicy.OrganizationAuthenticated || document.OrganizationId is null) return false;
        return access.CanReadOrganizationLibrary(user, document.OrganizationId.Value);
    }

    private static DocumentCollectionDto ToCollectionDto(DocumentCollection value)
        => new(value.Id, value.Code, value.Name, value.Description, value.Scope, value.OrganizationId, value.Status);

    private static InstitutionalDocumentDto ToDocumentDto(
        InstitutionalDocument document,
        string collectionCode,
        IReadOnlyCollection<DocumentVersion> versions)
        => new(
            document.Id,
            document.CollectionId,
            collectionCode,
            document.OrganizationId,
            document.Title,
            document.DocumentType,
            document.Classification,
            document.AccessPolicy,
            document.Status,
            document.PublishedVersionId,
            document.PublishedAtUtc,
            versions.Select(ToVersionDto).ToArray());

    private static DocumentVersionDto ToVersionDto(DocumentVersion version)
        => new(
            version.Id,
            version.DocumentId,
            version.VersionNumber,
            version.OriginalFileName,
            version.ContentType,
            version.SizeBytes,
            version.ProcessingStatus,
            version.Sha256 is not null,
            version.ScanReference is not null,
            version.CreatedAtUtc);

    private static LibraryDocumentDto ToLibraryDto(
        InstitutionalDocument document,
        string collectionName,
        DocumentVersion version)
        => new(
            document.Id,
            document.Title,
            document.DocumentType,
            collectionName,
            version.VersionNumber,
            version.ContentType,
            version.SizeBytes,
            document.PublishedAtUtc ?? DateTimeOffset.UtcNow);

    private static string? NormalizeRequired(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOptional(string? value) => NormalizeRequired(value);

    private static string? NormalizeCode(string? value)
    {
        var normalized = NormalizeRequired(value)?.ToUpperInvariant();
        if (normalized is null || normalized.Length > 120) return null;
        return normalized.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.') ? normalized : null;
    }

    private static string? NormalizeFileName(string? value)
    {
        var normalized = NormalizeRequired(value);
        if (normalized is null || normalized.Length > 500) return null;
        if (!string.Equals(Path.GetFileName(normalized), normalized, StringComparison.Ordinal)) return null;
        if (normalized.Any(char.IsControl)) return null;
        return normalized;
    }

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub")
           ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? user.Identity?.Name
           ?? "authenticated-user";

    private static HashSet<Guid> GetOrganizationIds(ClaimsPrincipal user)
        => user.Claims
            .Where(x => x.Type == InstitutionalClaims.Organization)
            .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToHashSet();
}

public sealed record CreateDocumentCollectionRequest(
    string? Code,
    string? Name,
    string? Description,
    string? Scope,
    Guid? OrganizationId);

public sealed record CreateInstitutionalDocumentRequest(
    string? Title,
    string? DocumentType,
    string? Classification,
    string? AccessPolicy);

public sealed record CreateDocumentVersionRequest(
    string? OriginalFileName,
    string? ContentType,
    long SizeBytes);

public sealed record TransitionDocumentVersionRequest(
    string? TargetStatus,
    string? Sha256,
    string? ScanReference);

public sealed record PublishDocumentRequest(Guid VersionId);

public sealed record DocumentCollectionDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Scope,
    Guid? OrganizationId,
    string Status);

public sealed record DocumentCollectionsResponse(int Total, IReadOnlyCollection<DocumentCollectionDto> Items);

public sealed record DocumentVersionDto(
    Guid Id,
    Guid DocumentId,
    int VersionNumber,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string ProcessingStatus,
    bool HasIntegrityHash,
    bool ScanEvidenceRecorded,
    DateTimeOffset CreatedAtUtc);

public sealed record InstitutionalDocumentDto(
    Guid Id,
    Guid CollectionId,
    string CollectionCode,
    Guid? OrganizationId,
    string Title,
    string DocumentType,
    string Classification,
    string AccessPolicy,
    string Status,
    Guid? PublishedVersionId,
    DateTimeOffset? PublishedAtUtc,
    IReadOnlyCollection<DocumentVersionDto> Versions);

public sealed record LibraryDocumentDto(
    Guid Id,
    string Title,
    string DocumentType,
    string CollectionName,
    int VersionNumber,
    string ContentType,
    long SizeBytes,
    DateTimeOffset PublishedAtUtc);

public sealed record LibraryDocumentsResponse(int Total, IReadOnlyCollection<LibraryDocumentDto> Items);
