using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.DocumentManagement;

/// <summary>
/// #588 — carga del Bibliotecario en las categorías que gestiona (libros y las que cree). Crea solo el borrador,
/// con las mismas garantías de la línea editorial: colección de la Orden activa, clasificación/política fijadas por
/// el servidor, perfil Bibliotecario como titular y grado mínimo elegido por él (1, 2, 3 o General). La carga del
/// archivo, el escaneo antimalware y la publicación reutilizan la ruta de versiones y de publicación editorial,
/// que revalidan integridad, ClamAV limpio y huella antes de publicar.
/// </summary>
public static class LibraryManagedPublicationEndpoints
{
    public const string OrderCollectionCode = "BIBLIOTECA-ORDEN";

    public sealed record CreateManagedPublicationRequest(
        string? CategoryCode, string? Title, string? AuthorName, string? Topic, string? Edition,
        string? Description, int? MinimumDegreeRequired);

    public static IEndpointRouteBuilder MapLibraryManagedPublicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/biblioteca/gestion/publicaciones").WithTags("Biblioteca Virtual").RequireAuthorization();
        group.MapGet("", ListManagedPublicationsAsync);
        group.MapPost("", CreateManagedPublicationAsync);
        return endpoints;
    }

    private static bool IsLibrarian(HttpContext ctx, IInstitutionalAccessService access)
        => access.HasOrderScope(ctx.User) && LibraryMagazineMetadataAuthorization.IsLibrarianProfile(ctx.User, access);

    private static string? Clean(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max || v.Any(char.IsControl) ? string.Empty : v;
    }

    private static async Task<IResult> ListManagedPublicationsAsync(
        HttpContext ctx, DocumentManagementDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!IsLibrarian(ctx, access)) return Results.Forbid();
        var rows = await db.InstitutionalDocuments.AsNoTracking()
            .Where(x => x.OrganizationId == null && x.PublisherProfile == LibraryPublisherProfiles.Librarian &&
                        x.Collection.Scope == DocumentManagementCodes.Scope.Order &&
                        x.Status != DocumentManagementCodes.DocumentStatus.Retired &&
                        x.DocumentType != "work_paper" && x.DocumentType != "magazine" && x.DocumentType != "bulletin" &&
                        x.DocumentType != "official_document" && x.DocumentType != "video")
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(200)
            .Select(x => new
            {
                x.Id, x.Title, categoryCode = x.DocumentType, x.Status, degree = x.MinimumDegreeRequired,
                author = x.AuthorName, x.Topic, x.Edition, description = x.AbstractText ?? x.ShortDescription,
                x.CreatedAtUtc, x.PublishedAtUtc
            }).ToListAsync(ct);
        ctx.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { items = rows });
    }

    private static async Task<IResult> CreateManagedPublicationAsync(
        CreateManagedPublicationRequest request, HttpContext ctx, DocumentManagementDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!IsLibrarian(ctx, access)) return Results.Forbid();
        var code = request.CategoryCode?.Trim().ToLowerInvariant();
        if (!LibraryEditorialTypes.IsManagedType(code))
            return Results.BadRequest(new { message = "Esa categoría no se carga desde esta vía." });
        var category = await db.LibraryCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code, ct);
        if (category is null || !category.Active)
            return Results.BadRequest(new { message = "La categoría no existe o está desactivada." });

        var title = Clean(request.Title, 240);
        var author = Clean(request.AuthorName, 320);
        var topic = Clean(request.Topic, 240);
        var edition = Clean(request.Edition, 120);
        var description = Clean(request.Description, 4000);
        if (string.IsNullOrEmpty(title) || author == string.Empty || topic == string.Empty || edition == string.Empty || description == string.Empty)
            return Results.BadRequest(new { message = "Título obligatorio (máximo 240 caracteres) y datos sin caracteres de control ni exceso de largo." });
        if (request.MinimumDegreeRequired is < 1 or > 3)
            return Results.BadRequest(new { message = "El grado debe ser 1, 2, 3 o General." });
        if (code == "book" && (author is null || topic is null || edition is null || description is null))
            return Results.BadRequest(new { message = "Un libro requiere autor, tema, edición y abstract." });

        var collection = await db.DocumentCollections.SingleOrDefaultAsync(x => x.Code == OrderCollectionCode, ct);
        if (collection is null)
        {
            collection = new DocumentCollection
            {
                Code = OrderCollectionCode, Name = "Biblioteca de la Orden",
                Description = "Publicaciones que gestiona el Bibliotecario.",
                Scope = DocumentManagementCodes.Scope.Order, OrganizationId = null,
                CreatedBySubject = DynamicTreasuryAccess.Subject(ctx.User) ?? string.Empty
            };
            db.DocumentCollections.Add(collection);
        }
        if (collection.Scope != DocumentManagementCodes.Scope.Order || collection.OrganizationId is not null ||
            collection.Status != DocumentManagementCodes.CollectionStatus.Active)
            return Results.Conflict(new { message = "La colección de la Biblioteca de la Orden no está activa." });

        var document = new InstitutionalDocument
        {
            Collection = collection, CollectionId = collection.Id, OrganizationId = null,
            Title = title, DocumentType = category.Code,
            Classification = DocumentManagementCodes.Classification.Internal,
            AccessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated,
            MinimumDegreeRequired = request.MinimumDegreeRequired,
            AuthorName = author, Topic = topic, Edition = edition,
            AbstractText = code == "book" ? description : null,
            ShortDescription = code == "book" ? null : description,
            Status = DocumentManagementCodes.DocumentStatus.Draft,
            PublisherProfile = LibraryPublisherProfiles.Librarian,
            CreatedBySubject = DynamicTreasuryAccess.Subject(ctx.User) ?? string.Empty
        };
        db.InstitutionalDocuments.Add(document);
        db.AuditEvents.Add(AuditEventFactory.Create(ctx, $"library.managed.{category.Code}_draft.created",
            nameof(InstitutionalDocument), document.Id.ToString(), null, AuditResults.Success,
            new { document.CollectionId, document.DocumentType, document.MinimumDegreeRequired, document.Status }));
        await db.SaveChangesAsync(ct);
        ctx.Response.Headers.CacheControl = "private, no-store";
        return Results.Created("/api/biblioteca/gestion/publicaciones", new { documentId = document.Id, document.Status });
    }
}
