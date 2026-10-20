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

        return endpoints;
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
        var catalog = await DynamicAccessEndpoints.LoadAsync(institutionalDb, cancellationToken);
        if (!LibraryMagazineMetadataAuthorization.CanCreateEditorialMagazine(
                httpContext.User, access, catalog))
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
            DocumentType = "magazine",
            Classification = DocumentManagementCodes.Classification.Internal,
            AccessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated,
            Status = DocumentManagementCodes.DocumentStatus.Draft,
            CreatedBySubject = DynamicTreasuryAccess.Subject(httpContext.User) ?? string.Empty
        };
        db.InstitutionalDocuments.Add(document);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "library.editorial.magazine_draft.created",
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
                (document.DocumentType.ToLower() == "magazine" ||
                 document.DocumentType.ToLower() == "magazines" ||
                 document.DocumentType.ToLower() == "revista" ||
                 document.DocumentType.ToLower() == "revistas"));

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
        document.DocumentDate = request.DocumentDate;

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
        else if (kind == LibraryCatalogKind.Magazine)
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
    Magazine
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

public sealed record CreateEditorialMagazineRequest(Guid CollectionId, string? Title);
public sealed record EditorialMagazineCreatedDto(Guid DocumentId, string Status);
