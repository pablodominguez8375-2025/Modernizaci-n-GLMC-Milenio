using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.DocumentManagement;

public static class LibraryCatalogEndpoints
{
    public static IEndpointRouteBuilder MapLibraryCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var library = endpoints.MapGroup("/api/biblioteca")
            .WithTags("Biblioteca Virtual")
            .RequireAuthorization();

        library.MapGet("/buscar", SearchAsync);
        library.MapGet("/facetas", GetFacetsAsync);

        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        string? q,
        Guid? collectionId,
        string? documentType,
        int? fromYear,
        int? toYear,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 24;
        if (currentPage < 1)
            return Results.BadRequest(new { message = "La página debe ser mayor o igual a 1." });
        if (currentPageSize is < 1 or > 50)
            return Results.BadRequest(new { message = "El tamaño de página debe estar entre 1 y 50." });
        if (!IsValidYear(fromYear) || !IsValidYear(toYear))
            return Results.BadRequest(new { message = "El año de publicación debe estar entre 1600 y 2100." });
        if (fromYear is not null && toYear is not null && fromYear > toYear)
            return Results.BadRequest(new { message = "El año inicial no puede ser posterior al año final." });

        var normalizedQuery = NormalizeOptional(q, 120);
        if (q is not null && normalizedQuery is null)
            return Results.BadRequest(new { message = "El texto de búsqueda excede el máximo permitido." });

        var normalizedDocumentType = NormalizeOptional(documentType, 100);
        if (documentType is not null && normalizedDocumentType is null)
            return Results.BadRequest(new { message = "El tipo documental excede el máximo permitido." });

        var query = BuildVisibleCatalogQuery(httpContext.User, db, access);

        if (collectionId is not null)
            query = query.Where(x => x.CollectionId == collectionId.Value);

        if (normalizedDocumentType is not null)
        {
            var documentTypeNeedle = normalizedDocumentType.ToLowerInvariant();
            query = query.Where(x => x.DocumentType.ToLower() == documentTypeNeedle);
        }

        if (normalizedQuery is not null)
        {
            var searchNeedle = normalizedQuery.ToLowerInvariant();
            query = query.Where(x =>
                x.Title.ToLower().Contains(searchNeedle) ||
                x.CollectionName.ToLower().Contains(searchNeedle) ||
                x.DocumentType.ToLower().Contains(searchNeedle));
        }

        if (fromYear is not null)
        {
            var from = new DateTimeOffset(fromYear.Value, 1, 1, 0, 0, 0, TimeSpan.Zero);
            query = query.Where(x => x.PublishedAtUtc >= from);
        }

        if (toYear is not null)
        {
            var untilExclusive = new DateTimeOffset(toYear.Value + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
            query = query.Where(x => x.PublishedAtUtc < untilExclusive);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.PublishedAtUtc)
            .ThenBy(x => x.Title)
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .Select(x => new LibraryCatalogItemDto(
                x.Id,
                x.Title,
                x.DocumentType,
                x.CollectionId,
                x.CollectionName,
                x.VersionNumber,
                x.ContentType,
                x.SizeBytes,
                x.PublishedAtUtc))
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LibraryCatalogResponse(total, currentPage, currentPageSize, items));
    }

    private static async Task<IResult> GetFacetsAsync(
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var query = BuildVisibleCatalogQuery(httpContext.User, db, access);

        var collections = await query
            .GroupBy(x => new { x.CollectionId, x.CollectionName })
            .Select(group => new LibraryFacetItemDto(
                group.Key.CollectionId.ToString(),
                group.Key.CollectionName,
                group.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label)
            .Take(200)
            .ToListAsync(cancellationToken);

        var documentTypes = await query
            .GroupBy(x => x.DocumentType)
            .Select(group => new LibraryFacetItemDto(
                group.Key,
                group.Key,
                group.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label)
            .Take(200)
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LibraryFacetsResponse(collections, documentTypes));
    }

    private static IQueryable<LibraryCatalogQueryRow> BuildVisibleCatalogQuery(
        ClaimsPrincipal user,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access)
    {
        var canReadAllOrganizations = access.HasOrderScope(user) &&
            access.HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.DocumentManager);
        var organizationIds = GetOrganizationIds(user);

        var documents = db.InstitutionalDocuments.AsNoTracking()
            .Where(document =>
                document.Status == DocumentManagementCodes.DocumentStatus.Published &&
                document.PublishedVersionId != null &&
                (document.AccessPolicy == DocumentManagementCodes.AccessPolicy.LibraryAuthenticated ||
                 (document.AccessPolicy == DocumentManagementCodes.AccessPolicy.OrganizationAuthenticated &&
                  document.OrganizationId != null &&
                  (canReadAllOrganizations || organizationIds.Contains(document.OrganizationId.Value)))));

        return
            from document in documents
            join collection in db.DocumentCollections.AsNoTracking()
                on document.CollectionId equals collection.Id
            join version in db.DocumentVersions.AsNoTracking()
                on document.PublishedVersionId equals (Guid?)version.Id
            where version.DocumentId == document.Id &&
                  version.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Available
            select new LibraryCatalogQueryRow(
                document.Id,
                document.Title,
                document.DocumentType,
                collection.Id,
                collection.Name,
                version.VersionNumber,
                version.ContentType,
                version.SizeBytes,
                document.PublishedAtUtc!.Value);
    }

    private static HashSet<Guid> GetOrganizationIds(ClaimsPrincipal user)
        => user.Claims
            .Where(claim => claim.Type == InstitutionalClaims.Organization)
            .Select(claim => Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (value is null) return null;
        var normalized = value.Trim();
        if (normalized.Length == 0) return null;
        return normalized.Length <= maxLength ? normalized : null;
    }

    private static bool IsValidYear(int? year)
        => year is null or >= 1600 and <= 2100;

    private sealed record LibraryCatalogQueryRow(
        Guid Id,
        string Title,
        string DocumentType,
        Guid CollectionId,
        string CollectionName,
        int VersionNumber,
        string ContentType,
        long SizeBytes,
        DateTimeOffset PublishedAtUtc);
}

public sealed record LibraryCatalogItemDto(
    Guid Id,
    string Title,
    string DocumentType,
    Guid CollectionId,
    string CollectionName,
    int VersionNumber,
    string ContentType,
    long SizeBytes,
    DateTimeOffset PublishedAtUtc);

public sealed record LibraryCatalogResponse(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyCollection<LibraryCatalogItemDto> Items);

public sealed record LibraryFacetItemDto(string Value, string Label, int Count);

public sealed record LibraryFacetsResponse(
    IReadOnlyCollection<LibraryFacetItemDto> Collections,
    IReadOnlyCollection<LibraryFacetItemDto> DocumentTypes);
