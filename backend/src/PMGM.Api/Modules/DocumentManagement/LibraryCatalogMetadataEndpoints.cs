using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.DocumentManagement;

public static class LibraryCatalogMetadataEndpoints
{
    public static IEndpointRouteBuilder MapLibraryCatalogMetadataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
                "/api/documentos/{documentId:guid}/metadatos-biblioteca",
                SetMetadataAsync)
            .WithTags("Biblioteca Virtual")
            .RequireAuthorization();

        endpoints.MapGet("/api/biblioteca/editorial/revistas", ListEditableMagazinesAsync)
            .WithTags("Biblioteca Virtual")
            .RequireAuthorization();
        endpoints.MapPost("/api/biblioteca/editorial/revistas", CreateEditorialMagazineAsync)
            .WithTags("Biblioteca Virtual")
            .RequireAuthorization();
        endpoints.MapPost("/api/biblioteca/editorial/revistas/{documentId:guid}/publicar",
                PublishEditorialMagazineAsync)
            .WithTags("Biblioteca Virtual")
            .RequireAuthorization();
        endpoints.MapPost("/api/biblioteca/editorial/revistas/{documentId:guid}/retirar-publicacion",
                UnpublishEditorialMagazineAsync)
            .WithTags("Biblioteca Virtual")
            .RequireAuthorization();

        endpoints.MapGet("/api/biblioteca/editorial/revistas/{documentId:guid}/versiones",
                ListEditorialMagazineVersionsAsync)
            .WithTags("Biblioteca Virtual")
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ListEditorialMagazineVersionsAsync(
        Guid documentId,
        HttpContext ctx,
        PmgmDbContext institutionalDb,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken ct,
        int page = 1,
        int pageSize = 25)
    {
        if (page is < 1 or > 10000 || pageSize is < 1 or > 50)
            return Results.BadRequest(new { message = "Paginación de versiones inválida." });

        var doc = await db.InstitutionalDocuments.AsNoTracking().Include(d => d.Collection)
            .SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Results.NotFound();

        var catalog = await DynamicAccessEndpoints.LoadAsync(institutionalDb, ct);
        if (!LibraryMagazineMetadataAuthorization.CanStageMagazineContent(
                ctx.User, access, catalog, doc))
            return Results.Forbid();

        var subject = DynamicTreasuryAccess.Subject(ctx.User);
        // Bibliotecario y Comunicaciones institucional ven todas las versiones;
        // un colaborador sólo las que él mismo reservó.
        var isLibrarian = (access.CanManageLibrary(ctx.User, null) || access.CanManageCommunications(ctx.User)) &&
            DynamicViewAccess.Allows(catalog, subject, "library", "write",
                null, DynamicAccessEndpoints.Today());
        if (!isLibrarian && string.IsNullOrWhiteSpace(subject)) return Results.Forbid();

        var query = db.DocumentVersions.AsNoTracking().Where(v => v.DocumentId == documentId);
        if (!isLibrarian) query = query.Where(v => v.CreatedBySubject == subject);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(v => v.VersionNumber)
            .ThenBy(v => v.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => new EditorialMagazineVersionDto(
                v.Id, v.VersionNumber, v.ProcessingStatus,
                v.OriginalFileName, v.ContentType, v.SizeBytes, v.CreatedAtUtc))
            .ToListAsync(ct);

        ctx.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new EditorialMagazineVersionsResponse(
            documentId, total, page, pageSize, items));
    }

    private static async Task<IResult> UnpublishEditorialMagazineAsync(
        Guid documentId,
        HttpContext ctx,
        PmgmDbContext institutionalDb,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken ct)
    {
        var document = await db.InstitutionalDocuments.Include(x => x.Collection)
            .SingleOrDefaultAsync(x => x.Id == documentId, ct);
        if (document is null) return Results.NotFound();

        var catalog = await DynamicAccessEndpoints.LoadAsync(institutionalDb, ct);
        if (!LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(
                ctx.User, access, catalog, document))
            return Results.Forbid();

        if (document.Status != DocumentManagementCodes.DocumentStatus.Published ||
            document.PublishedVersionId is null)
            return Results.Conflict(new { message = "La publicación no tiene una versión publicada." });

        var previousVersionId = document.PublishedVersionId.Value;
        document.PublishedVersionId = null;
        document.PublishedAtUtc = null;
        // Borrador permite nuevas correcciones y carga de versiones por
        // colaboradores vigentes; la anterior versión se conserva y audita.
        document.Status = DocumentManagementCodes.DocumentStatus.Draft;
        db.AuditEvents.Add(AuditEventFactory.Create(
            ctx, $"library.editorial.{LibraryEditorialTypes.Canonical(document.DocumentType)}_unpublished",
            nameof(InstitutionalDocument), document.Id.ToString(),
            null, AuditResults.Success,
            new { PreviousVersionId = previousVersionId, document.Status }));
        await db.SaveChangesAsync(ct);

        ctx.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { document.Id, document.Status, document.PublishedVersionId });
    }

    private static async Task<IResult> PublishEditorialMagazineAsync(
        Guid documentId,
        PublishEditorialMagazineRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        IDocumentObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var document = await db.InstitutionalDocuments
            .Include(x => x.Collection)
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document is null) return Results.NotFound();

        var catalog = await DynamicAccessEndpoints.LoadAsync(institutionalDb, cancellationToken);
        if (!LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(
                httpContext.User, access, catalog, document))
            return Results.Forbid();

        if (request.VersionId == Guid.Empty)
            return Results.BadRequest(new { message = "VersionId obligatorio." });

        var version = await db.DocumentVersions.SingleOrDefaultAsync(
            x => x.DocumentId == document.Id && x.Id == request.VersionId,
            cancellationToken);
        if (version is null) return Results.NotFound();

        // No hay publicación hasta que el servidor escaneó limpiamente el archivo.
        if (version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available ||
            !DocumentIntegrity.IsValidSha256(version.Sha256) ||
            string.IsNullOrWhiteSpace(version.ScanReference) ||
            (!version.ScanReference.StartsWith("clamav:clean:", StringComparison.Ordinal) &&
             !version.ScanReference.StartsWith("clamav:test-clean:", StringComparison.Ordinal)) ||
            version.ObjectKey != DocumentObjectKeyFactory.Create(document.Id, version.Id))
            return Results.Conflict(new { message = "La versión no dispone de integridad y escaneo limpio verificables." });

        if (!await objectStore.ExistsAsync(version.ObjectKey, cancellationToken))
            return Results.Conflict(new { message = "El archivo de la versión no está almacenado." });

        // Verificación final contra los bytes realmente persistidos: nunca
        // confiar sólo en metadatos ni en la declaración del cliente.
        await using (var objectContent = await objectStore.OpenReadAsync(version.ObjectKey, cancellationToken))
        {
            var actual = await DocumentContentIntegrity.ComputeAsync(objectContent, cancellationToken);
            if (actual.SizeBytes != version.SizeBytes ||
                !string.Equals(actual.Sha256, version.Sha256, StringComparison.OrdinalIgnoreCase))
                return Results.Conflict(new { message = "El contenido almacenado no coincide con su huella de integridad." });
        }

        document.PublishedVersionId = version.Id;
        document.PublishedAtUtc = DateTimeOffset.UtcNow;
        document.Status = DocumentManagementCodes.DocumentStatus.Published;
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            $"library.editorial.{LibraryEditorialTypes.Canonical(document.DocumentType)}_published",
            nameof(InstitutionalDocument),
            document.Id.ToString(), null, AuditResults.Success,
            new { document.CollectionId, VersionId = version.Id, version.VersionNumber, document.Status }));
        await db.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            document.Id, document.Status,
            document.PublishedVersionId, document.PublishedAtUtc
        });
    }

    private static async Task<IResult> CreateEditorialMagazineAsync(
        CreateEditorialMagazineRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        // Sin type, classification, accessPolicy, status o grado elegidos
        // por el cliente. El servidor fija esos valores y sólo crea borrador.
        // Único dato de tipo que el cliente puede elegir: Revista (por omisión)
        // o Boletín. Cualquier otro valor se rechaza antes de autorizar.
        var documentType = string.IsNullOrWhiteSpace(request.DocumentType)
            ? LibraryEditorialTypes.Magazine
            : LibraryEditorialTypes.Canonical(request.DocumentType);
        if (documentType is null)
            return Results.BadRequest(new { message = "La publicación editorial debe ser Revista o Boletín." });

        var catalog = await DynamicAccessEndpoints.LoadAsync(institutionalDb, cancellationToken);
        if (!LibraryMagazineMetadataAuthorization.CanCreateEditorial(
                httpContext.User, access, catalog, documentType))
            return Results.Forbid();

        var title = request.Title?.Trim();
        if (request.CollectionId == Guid.Empty || string.IsNullOrWhiteSpace(title) ||
            title.Length > 240 || title.Any(char.IsControl))
            return Results.BadRequest(new { message = "Colección y título válido (máximo 240 caracteres) son obligatorios." });

        var collection = await db.DocumentCollections
            .SingleOrDefaultAsync(x => x.Id == request.CollectionId, cancellationToken);
        if (collection is null) return Results.NotFound();
        if (collection.Scope != DocumentManagementCodes.Scope.Order ||
            collection.OrganizationId is not null ||
            collection.Status != DocumentManagementCodes.CollectionStatus.Active)
            return Results.Forbid();

        var document = new InstitutionalDocument
        {
            CollectionId = collection.Id,
            Collection = collection,
            OrganizationId = null,
            Title = title,
            DocumentType = documentType,
            Classification = DocumentManagementCodes.Classification.Internal,
            AccessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated,
            Status = DocumentManagementCodes.DocumentStatus.Draft,
            CreatedBySubject = DynamicTreasuryAccess.Subject(httpContext.User) ?? string.Empty
        };
        db.InstitutionalDocuments.Add(document);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            $"library.editorial.{documentType}_draft.created",
            nameof(InstitutionalDocument),
            document.Id.ToString(),
            null,
            AuditResults.Success,
            new { document.CollectionId, document.DocumentType, document.Status }));
        await db.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Created("/api/biblioteca/editorial/revistas",
            new EditorialMagazineCreatedDto(document.Id, document.Status));
    }

    private static async Task<IResult> ListEditableMagazinesAsync(
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 25)
    {
        if (page is < 1 or > 10000 || pageSize is < 1 or > 100)
            return Results.BadRequest(new { message = "Paginación fuera del rango autorizado." });

        var catalog = await DynamicAccessEndpoints.LoadAsync(
            institutionalDb, cancellationToken);
        if (!LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
                httpContext.User, access, catalog))
            return Results.Forbid();
        var includeMagazines = LibraryMagazineMetadataAuthorization.CanListEditable(
            httpContext.User, access, catalog, LibraryEditorialTypes.Magazine);
        var includeBulletins = LibraryMagazineMetadataAuthorization.CanListEditable(
            httpContext.User, access, catalog, LibraryEditorialTypes.Bulletin);

        // Una colección/organización indicada por el solicitante jamás amplía
        // el conjunto: filtros sobre las relaciones persistidas en PostgreSQL.
        var query = db.InstitutionalDocuments.AsNoTracking()
            .Where(document =>
                document.OrganizationId == null &&
                document.Collection.OrganizationId == null &&
                document.Collection.Scope == DocumentManagementCodes.Scope.Order &&
                document.Collection.Status == DocumentManagementCodes.CollectionStatus.Active &&
                document.Status != DocumentManagementCodes.DocumentStatus.Retired &&
                document.Classification == DocumentManagementCodes.Classification.Internal &&
                document.AccessPolicy == DocumentManagementCodes.AccessPolicy.LibraryAuthenticated &&
                ((includeMagazines &&
                  (document.DocumentType.ToLower() == "magazine" ||
                   document.DocumentType.ToLower() == "magazines" ||
                   document.DocumentType.ToLower() == "revista" ||
                   document.DocumentType.ToLower() == "revistas")) ||
                 (includeBulletins &&
                  (document.DocumentType.ToLower() == "bulletin" ||
                   document.DocumentType.ToLower() == "bulletins" ||
                   document.DocumentType.ToLower() == "boletin" ||
                   document.DocumentType.ToLower() == "boletines" ||
                   document.DocumentType.ToLower() == "boletín"))));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(document => document.CreatedAtUtc)
            .ThenBy(document => document.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(document => new EditorialMagazineDto(
                document.Id,
                document.Title,
                document.DocumentType,
                document.Collection.Name,
                document.Status,
                document.MinimumDegreeRequired,
                document.AuthorName,
                document.DocumentDate,
                document.Topic,
                document.Edition,
                document.ShortDescription,
                document.CreatedAtUtc,
                document.PublishedAtUtc))
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new EditorialMagazineListDto(total, page, pageSize, items));
    }

    private static async Task<IResult> SetMetadataAsync(
        Guid documentId,
        SetLibraryCatalogMetadataRequest request,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        PmgmDbContext institutionalDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (request.MinimumDegreeRequired is < 1 or > 3)
            return Results.BadRequest(new { message = "El grado de Biblioteca debe ser 1, 2, 3 o nulo para contenido general." });

        if (TooLong(request.AuthorName, 320) ||
            TooLong(request.AuthorLodgeName, 320) ||
            TooLong(request.Topic, 240) ||
            TooLong(request.Edition, 120) ||
            TooLong(request.ShortDescription, 1000) ||
            TooLong(request.AbstractText, 4000) ||
            TooLong(request.OfficialDocumentType, 120))
        {
            return Results.BadRequest(new { message = "Uno o más metadatos exceden el largo permitido." });
        }

        var document = await db.InstitutionalDocuments
            .Include(x => x.Collection)
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document is null) return Results.NotFound();
        if (!access.CanManageDocuments(httpContext.User, document.OrganizationId))
        {
            var catalog = await DynamicAccessEndpoints.LoadAsync(institutionalDb, cancellationToken);
            if (!LibraryMagazineMetadataAuthorization.CanEditMetadata(
                    httpContext.User, access, catalog, document))
                return Results.Forbid();
            // El colaborador edita datos editoriales, no el nivel de visibilidad.
            // Cambiar el grado mínimo requiere Bibliotecario o Gestor Documental
            // institucional; incluso elevar restricciones debe ser auditado.
            if (!access.CanManageLibrary(httpContext.User, null) &&
                request.MinimumDegreeRequired != document.MinimumDegreeRequired)
                return Results.Forbid();
        }
        if (document.Status == DocumentManagementCodes.DocumentStatus.Retired)
            return Results.Conflict(new { message = "No se pueden modificar los metadatos de un documento retirado." });

        // Decisión del PO (22-10-2026, #635): la revista institucional y el
        // Boletín mensual son de lectura para todos los grados (General).
        if (LibraryEditorialTypes.Canonical(document.DocumentType) is not null &&
            request.MinimumDegreeRequired is not null)
            return Results.BadRequest(new { message = "La revista y el boletín son visibles para todos los grados; no admiten grado mínimo." });

        var authorName = Normalize(request.AuthorName);
        var authorLodgeName = Normalize(request.AuthorLodgeName);
        var topic = Normalize(request.Topic);
        var edition = Normalize(request.Edition);
        var shortDescription = Normalize(request.ShortDescription);
        var abstractText = Normalize(request.AbstractText);
        var officialDocumentType = Normalize(request.OfficialDocumentType);
        var kind = ResolveCatalogKind(document.DocumentType);

        switch (kind)
        {
            case LibraryCatalogKind.WorkPaper:
                if (authorName is null || authorLodgeName is null || request.DocumentDate is null ||
                    shortDescription is null || request.MinimumDegreeRequired is null)
                {
                    return Results.BadRequest(new
                    {
                        message = "Una Plancha de Trabajo requiere hermano autor, Taller, fecha, grado y descripción corta."
                    });
                }
                if (abstractText is not null)
                    return Results.BadRequest(new { message = "Una Plancha de Trabajo no utiliza abstract; sólo descripción corta." });
                break;

            case LibraryCatalogKind.Book:
                if (authorName is null || topic is null || edition is null || abstractText is null)
                {
                    return Results.BadRequest(new
                    {
                        message = "Un Libro requiere autor, tema, edición y abstract. El grado puede ser 1, 2, 3 o General."
                    });
                }
                break;

            case LibraryCatalogKind.OfficialDocument:
                if (officialDocumentType is null)
                    return Results.BadRequest(new { message = "Un Documento Oficial requiere tipo documental, por ejemplo Reglamento, Constitución o Ritual." });
                break;

            case LibraryCatalogKind.Magazine:
                // Revistas: edición/número, fecha, tema, descripción y resumen opcionales.
                // No se exige el abstract propio de los libros ni se expone contenido privado.
                break;

            case LibraryCatalogKind.Bulletin:
                // Boletín: informe resumido mensual de actividades de la Orden.
                // El período (mes/año) es obligatorio y se normaliza al día 1.
                if (request.DocumentDate is null)
                    return Results.BadRequest(new { message = "Un Boletín requiere el mes y año que informa." });
                break;

            case LibraryCatalogKind.Video:
                // Categoría preparada para una habilitación posterior. Los metadatos
                // de tema/autor/fecha/descripción ya pueden almacenarse sin activar
                // reproducción ni publicación audiovisual específica.
                break;

            default:
                return Results.BadRequest(new
                {
                    message = "El tipo documental no corresponde a una categoría catalogable de Biblioteca Virtual."
                });
        }

        document.MinimumDegreeRequired = request.MinimumDegreeRequired;
        document.AuthorName = authorName;
        document.DocumentDate = kind == LibraryCatalogKind.Bulletin && request.DocumentDate is { } period
            ? new DateOnly(period.Year, period.Month, 1)
            : request.DocumentDate;

        if (kind == LibraryCatalogKind.WorkPaper)
        {
            document.AuthorLodgeName = authorLodgeName;
            document.ShortDescription = shortDescription;
            document.Topic = null;
            document.Edition = null;
            document.AbstractText = null;
            document.OfficialDocumentType = null;
        }
        else if (kind == LibraryCatalogKind.Book)
        {
            document.AuthorLodgeName = null;
            document.Topic = topic;
            document.Edition = edition;
            document.AbstractText = abstractText;
            document.ShortDescription = null;
            document.OfficialDocumentType = null;
        }
        else if (kind is LibraryCatalogKind.Magazine or LibraryCatalogKind.Bulletin)
        {
            document.AuthorLodgeName = null;
            document.Topic = topic;
            document.Edition = edition;
            document.ShortDescription = shortDescription;
            document.AbstractText = abstractText;
            document.OfficialDocumentType = null;
        }
        else if (kind == LibraryCatalogKind.OfficialDocument)
        {
            document.AuthorName = null;
            document.AuthorLodgeName = null;
            document.Topic = null;
            document.Edition = null;
            document.AbstractText = null;
            document.ShortDescription = null;
            document.OfficialDocumentType = officialDocumentType;
        }
        else
        {
            document.AuthorLodgeName = null;
            document.Topic = topic;
            document.Edition = null;
            document.AbstractText = null;
            document.ShortDescription = shortDescription;
            document.OfficialDocumentType = null;
        }

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "library.catalog_metadata.changed",
            nameof(InstitutionalDocument),
            document.Id.ToString(),
            document.OrganizationId,
            AuditResults.Success,
            new
            {
                CatalogKind = kind.ToString(),
                document.DocumentType,
                document.MinimumDegreeRequired,
                HasAuthor = document.AuthorName is not null,
                HasLodge = document.AuthorLodgeName is not null,
                HasShortDescription = document.ShortDescription is not null,
                HasAbstract = document.AbstractText is not null,
                HasOfficialDocumentType = document.OfficialDocumentType is not null
            }));

        await db.SaveChangesAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(ToDto(document, kind));
    }

    private static LibraryCatalogMetadataDto ToDto(InstitutionalDocument document, LibraryCatalogKind kind)
        => new(
            document.Id,
            kind.ToString(),
            document.MinimumDegreeRequired,
            document.AuthorName,
            document.AuthorLodgeName,
            document.DocumentDate,
            document.Topic,
            document.Edition,
            document.ShortDescription,
            document.AbstractText,
            document.OfficialDocumentType);

    internal static LibraryCatalogKind ResolveCatalogKind(string documentType)
    {
        var normalized = documentType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "work_paper" or "working_paper" or "plancha" or "plancha_de_trabajo" => LibraryCatalogKind.WorkPaper,
            "book" or "books" or "libro" or "libros" => LibraryCatalogKind.Book,
            "official_document" or "documento_oficial" or "regulation" or "reglamento" or
            "constitution" or "constitucion" or "ritual" => LibraryCatalogKind.OfficialDocument,
            "video" or "videos" => LibraryCatalogKind.Video,
            "magazine" or "magazines" or "revista" or "revistas" => LibraryCatalogKind.Magazine,
            "bulletin" or "bulletins" or "boletin" or "boletines" or "boletín" => LibraryCatalogKind.Bulletin,
            _ => LibraryCatalogKind.Other
        };
    }

    private static bool TooLong(string? value, int maximum)
        => value?.Trim().Length > maximum;

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

public enum LibraryCatalogKind
{
    Other,
    WorkPaper,
    Book,
    OfficialDocument,
    Video,
    Magazine,
    Bulletin
}

public sealed record SetLibraryCatalogMetadataRequest(
    int? MinimumDegreeRequired,
    string? AuthorName,
    string? AuthorLodgeName,
    DateOnly? DocumentDate,
    string? Topic,
    string? Edition,
    string? ShortDescription,
    string? AbstractText,
    string? OfficialDocumentType);

public sealed record LibraryCatalogMetadataDto(
    Guid DocumentId,
    string CatalogKind,
    int? MinimumDegreeRequired,
    string? AuthorName,
    string? AuthorLodgeName,
    DateOnly? DocumentDate,
    string? Topic,
    string? Edition,
    string? ShortDescription,
    string? AbstractText,
    string? OfficialDocumentType);

public sealed record EditorialMagazineDto(
    Guid DocumentId,
    string Title,
    string DocumentType,
    string CollectionName,
    string Status,
    int? MinimumDegreeRequired,
    string? AuthorName,
    DateOnly? DocumentDate,
    string? Topic,
    string? Edition,
    string? ShortDescription,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PublishedAtUtc);

public sealed record EditorialMagazineListDto(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<EditorialMagazineDto> Items);

public sealed record CreateEditorialMagazineRequest(Guid CollectionId, string? Title, string? DocumentType = null);
public sealed record EditorialMagazineCreatedDto(Guid DocumentId, string Status);

public sealed record EditorialMagazineVersionDto(
    Guid Id, int VersionNumber, string ProcessingStatus,
    string OriginalFileName, string ContentType, long SizeBytes,
    DateTimeOffset CreatedAtUtc);
public sealed record EditorialMagazineVersionsResponse(
    Guid DocumentId, int Total, int Page, int PageSize,
    IReadOnlyList<EditorialMagazineVersionDto> Items);

public sealed record PublishEditorialMagazineRequest(Guid VersionId);
