using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.DocumentManagement;

public static class DocumentManagementQueryEndpoints
{
    public static IEndpointRouteBuilder MapDocumentManagementQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/documentos/colecciones/{collectionId:guid}/documentos", GetDocumentsAsync)
            .WithTags("Gestor Documental")
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetDocumentsAsync(
        Guid collectionId,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var collection = await db.DocumentCollections.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == collectionId, cancellationToken);
        if (collection is null) return Results.NotFound(new { message = "La colección indicada no existe." });
        if (!access.CanManageDocuments(httpContext.User, collection.OrganizationId)) return Results.Forbid();

        var items = await db.InstitutionalDocuments.AsNoTracking()
            .Where(x => x.CollectionId == collectionId)
            .OrderByDescending(x => x.PublishedAtUtc)
            .ThenBy(x => x.Title)
            .Take(1000)
            .Select(x => new DocumentListItemDto(
                x.Id,
                x.CollectionId,
                x.OrganizationId,
                x.Title,
                x.DocumentType,
                x.Classification,
                x.AccessPolicy,
                x.Status,
                x.PublishedVersionId,
                x.PublishedAtUtc))
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new DocumentListResponse(items.Count, items));
    }
}

public sealed record DocumentListItemDto(
    Guid Id,
    Guid CollectionId,
    Guid? OrganizationId,
    string Title,
    string DocumentType,
    string Classification,
    string AccessPolicy,
    string Status,
    Guid? PublishedVersionId,
    DateTimeOffset? PublishedAtUtc);

public sealed record DocumentListResponse(int Total, IReadOnlyCollection<DocumentListItemDto> Items);
