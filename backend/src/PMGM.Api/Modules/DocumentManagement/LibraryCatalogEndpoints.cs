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
        int? degree,
        string? topic,
        string? officialDocumentType,
        int? fromYear,
        int? toYear,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver memberContextResolver,
        CancellationToken cancellationToken)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 24;
        if (currentPage < 1)
            return Results.BadRequest(new { message = "La página debe ser mayor o igual a 1." });
        if (currentPageSize is < 1 or > 50)
            return Results.BadRequest(new { message = "El tamaño de página debe estar entre 1 y 50." });
        if (degree is < 1 or > 3)
            return Results.BadRequest(new { message = "El grado debe ser 1, 2 o 3." });
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

        var normalizedTopic = NormalizeOptional(topic, 240);
        if (topic is not null && normalizedTopic is null)
            return Results.BadRequest(new { message = "El tema excede el máximo permitido." });

        var normalizedOfficialDocumentType = NormalizeOptional(officialDocumentType, 120);
        if (officialDocumentType is not null && normalizedOfficialDocumentType is null)
            return Results.BadRequest(new { message = "El tipo de documento oficial excede el máximo permitido." });

        var memberContext = await memberContextResolver.ResolveAsync(httpContext.User, cancellationToken);
        var effectiveDegree = memberContext?.EffectiveDegree;
        var query = BuildVisibleCatalogQuery(httpContext.User, db, access, effectiveDegree);

        if (collectionId is not null)
            query = query.Where(x => x.CollectionId == collectionId.Value);

        if (normalizedDocumentType is not null)
            query = ApplyCatalogTypeFilter(query, normalizedDocumentType);

        if (degree is not null)
            query = query.Where(x => x.MinimumDegreeRequired == degree.Value);

        if (normalizedTopic is not null)
        {
            var topicNeedle = normalizedTopic.ToLowerInvariant();
            query = query.Where(x => x.Topic != null && x.Topic.ToLower() == topicNeedle);
        }

        if (normalizedOfficialDocumentType is not null)
        {
            var officialNeedle = normalizedOfficialDocumentType.ToLowerInvariant();
            query = query.Where(x => x.OfficialDocumentType != null && x.OfficialDocumentType.ToLower() == officialNeedle);
        }

        if (normalizedQuery is not null)
        {
            var searchNeedle = normalizedQuery.ToLowerInvariant();
            query = query.Where(x =>
                x.Title.ToLower().Contains(searchNeedle) ||
                x.CollectionName.ToLower().Contains(searchNeedle) ||
                x.DocumentType.ToLower().Contains(searchNeedle) ||
                (x.AuthorName != null && x.AuthorName.ToLower().Contains(searchNeedle)) ||
                (x.AuthorLodgeName != null && x.AuthorLodgeName.ToLower().Contains(searchNeedle)) ||
                (x.Topic != null && x.Topic.ToLower().Contains(searchNeedle)) ||
                (x.Edition != null && x.Edition.ToLower().Contains(searchNeedle)) ||
                (x.ShortDescription != null && x.ShortDescription.ToLower().Contains(searchNeedle)) ||
                (x.AbstractText != null && x.AbstractText.ToLower().Contains(searchNeedle)) ||
                (x.OfficialDocumentType != null && x.OfficialDocumentType.ToLower().Contains(searchNeedle)));
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
                x.PublishedAtUtc,
                x.MinimumDegreeRequired,
                x.AuthorName,
                x.AuthorLodgeName,
                x.DocumentDate,
                x.Topic,
                x.Edition,
                x.ShortDescription,
                x.AbstractText,
                x.OfficialDocumentType))
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LibraryCatalogResponse(total, currentPage, currentPageSize, items));
    }

    private static async Task<IResult> GetFacetsAsync(
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver memberContextResolver,
        CancellationToken cancellationToken)
    {
        var memberContext = await memberContextResolver.ResolveAsync(httpContext.User, cancellationToken);
        var effectiveDegree = memberContext?.EffectiveDegree;
        var query = BuildVisibleCatalogQuery(httpContext.User, db, access, effectiveDegree);

        var collectionRows = await query
            .GroupBy(x => new { x.CollectionId, x.CollectionName })
            .Select(group => new { Value = group.Key.CollectionId, Label = group.Key.CollectionName, Count = group.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label)
            .Take(200)
            .ToListAsync(cancellationToken);

        var collections = collectionRows
            .Select(x => new LibraryFacetItemDto(x.Value.ToString(), x.Label, x.Count))
            .ToList();

        var documentTypeRows = await query
            .GroupBy(x => x.DocumentType)
            .Select(group => new { Value = group.Key, Label = group.Key, Count = group.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label)
            .Take(200)
            .ToListAsync(cancellationToken);

        var documentTypes = documentTypeRows
            .Select(x => new LibraryFacetItemDto(x.Value, x.Label, x.Count))
            .ToList();

        var topicRows = await query
            .Where(x => x.Topic != null)
            .GroupBy(x => x.Topic!)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Value)
            .Take(200)
            .ToListAsync(cancellationToken);

        var topics = topicRows
            .Select(x => new LibraryFacetItemDto(x.Value, x.Value, x.Count))
            .ToList();

        var officialTypeRows = await query
            .Where(x => x.OfficialDocumentType != null)
            .GroupBy(x => x.OfficialDocumentType!)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Value)
            .Take(200)
            .ToListAsync(cancellationToken);

        var officialDocumentTypes = officialTypeRows
            .Select(x => new LibraryFacetItemDto(x.Value, x.Value, x.Count))
            .ToList();

        var degreeRows = await query
            .GroupBy(x => x.MinimumDegreeRequired)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .OrderBy(x => x.Value)
            .ToListAsync(cancellationToken);

        var degrees = degreeRows
            .Select(x => new LibraryFacetItemDto(
                x.Value?.ToString() ?? "general",
                x.Value is null ? "General" : $"{x.Value}° grado",
                x.Count))
            .ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LibraryFacetsResponse(collections, documentTypes, degrees, topics, officialDocumentTypes));
    }

    private static IQueryable<LibraryCatalogQueryRow> ApplyCatalogTypeFilter(
        IQueryable<LibraryCatalogQueryRow> query,
        string documentType)
    {
        var normalized = documentType.ToLowerInvariant();
        return normalized switch
        {
            "work_paper" => query.Where(x =>
                x.DocumentType.ToLower() == "work_paper" ||
                x.DocumentType.ToLower() == "working_paper" ||
                x.DocumentType.ToLower() == "plancha" ||
                x.DocumentType.ToLower() == "plancha_de_trabajo"),
            "book" => query.Where(x =>
                x.DocumentType.ToLower() == "book" ||
                x.DocumentType.ToLower() == "books" ||
                x.DocumentType.ToLower() == "libro" ||
                x.DocumentType.ToLower() == "libros"),
            "official_document" => query.Where(x =>
                x.DocumentType.ToLower() == "official_document" ||
                x.DocumentType.ToLower() == "documento_oficial" ||
                x.DocumentType.ToLower() == "regulation" ||
                x.DocumentType.ToLower() == "reglamento" ||
                x.DocumentType.ToLower() == "constitution" ||
                x.DocumentType.ToLower() == "constitucion" ||
                x.DocumentType.ToLower() == "ritual"),
            "video" => query.Where(x =>
                x.DocumentType.ToLower() == "video" || x.DocumentType.ToLower() == "videos"),
            _ => query.Where(x => x.DocumentType.ToLower() == normalized)
        };
    }

    private static IQueryable<LibraryCatalogQueryRow> BuildVisibleCatalogQuery(
        ClaimsPrincipal user,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        int? effectiveDegree)
    {
        var canReadAllOrganizations = access.HasOrderScope(user) &&
            access.HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.DocumentManager);
        var organizationIds = GetOrganizationIds(user);

        var documents = db.InstitutionalDocuments.AsNoTracking()
            .Where(document =>
                document.Status == DocumentManagementCodes.DocumentStatus.Published &&
                document.PublishedVersionId != null &&
                (document.MinimumDegreeRequired == null ||
                 (effectiveDegree != null && document.MinimumDegreeRequired <= effectiveDegree)) &&
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
            select new LibraryCatalogQueryRow
            {
                Id = document.Id,
                Title = document.Title,
                DocumentType = document.DocumentType,
                CollectionId = collection.Id,
                CollectionName = collection.Name,
                VersionNumber = version.VersionNumber,
                ContentType = version.ContentType,
                SizeBytes = version.SizeBytes,
                PublishedAtUtc = document.PublishedAtUtc!.Value,
                MinimumDegreeRequired = document.MinimumDegreeRequired,
                AuthorName = document.AuthorName,
                AuthorLodgeName = document.AuthorLodgeName,
                DocumentDate = document.DocumentDate,
                Topic = document.Topic,
                Edition = document.Edition,
                ShortDescription = document.ShortDescription,
                AbstractText = document.AbstractText,
                OfficialDocumentType = document.OfficialDocumentType
            };
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

    private sealed class LibraryCatalogQueryRow
    {
        public Guid Id { get; init; }
        public required string Title { get; init; }
        public required string DocumentType { get; init; }
        public Guid CollectionId { get; init; }
        public required string CollectionName { get; init; }
        public int VersionNumber { get; init; }
        public required string ContentType { get; init; }
        public long SizeBytes { get; init; }
        public DateTimeOffset PublishedAtUtc { get; init; }
        public int? MinimumDegreeRequired { get; init; }
        public string? AuthorName { get; init; }
        public string? AuthorLodgeName { get; init; }
        public DateOnly? DocumentDate { get; init; }
        public string? Topic { get; init; }
        public string? Edition { get; init; }
        public string? ShortDescription { get; init; }
        public string? AbstractText { get; init; }
        public string? OfficialDocumentType { get; init; }
    }
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
    DateTimeOffset PublishedAtUtc,
    int? MinimumDegreeRequired,
    string? AuthorName,
    string? AuthorLodgeName,
    DateOnly? DocumentDate,
    string? Topic,
    string? Edition,
    string? ShortDescription,
    string? AbstractText,
    string? OfficialDocumentType);

public sealed record LibraryCatalogResponse(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyCollection<LibraryCatalogItemDto> Items);

public sealed record LibraryFacetItemDto(string Value, string Label, int Count);

public sealed record LibraryFacetsResponse(
    IReadOnlyCollection<LibraryFacetItemDto> Collections,
    IReadOnlyCollection<LibraryFacetItemDto> DocumentTypes,
    IReadOnlyCollection<LibraryFacetItemDto> Degrees,
    IReadOnlyCollection<LibraryFacetItemDto> Topics,
    IReadOnlyCollection<LibraryFacetItemDto> OfficialDocumentTypes);
