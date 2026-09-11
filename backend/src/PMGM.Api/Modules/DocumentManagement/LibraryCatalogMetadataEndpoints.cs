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

        return endpoints;
    }

    private static async Task<IResult> SetMetadataAsync(
        Guid documentId,
        SetLibraryCatalogMetadataRequest request,
        HttpContext httpContext,
        DocumentManagementDbContext db,
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
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document is null) return Results.NotFound();
        if (!access.CanManageDocuments(httpContext.User, document.OrganizationId)) return Results.Forbid();
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
    Video
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
