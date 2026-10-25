using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.DocumentManagement;

/// <summary>
/// #588 — categorías de la Biblioteca Virtual. Las ve todo usuario con acceso a la Biblioteca (solo las
/// activas); las administra únicamente el perfil Bibliotecario (no el administrador de Gran Logia ni el
/// Máster). Las categorías base no se desactivan; una categoría con publicaciones no se desactiva.
/// </summary>
public static class LibraryCategoryEndpoints
{
    public const int MaxCategories = 40;
    public sealed record CreateCategoryRequest(string? Label);
    public sealed record UpdateCategoryRequest(string? Label, bool? Active);
    public sealed record ReorderCategoriesRequest(string[]? Codes);

    public static IEndpointRouteBuilder MapLibraryCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/biblioteca/categorias").WithTags("Biblioteca Virtual").RequireAuthorization();
        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync);
        group.MapPut("/orden", ReorderAsync);
        group.MapPut("/{code}", UpdateAsync);
        return endpoints;
    }

    public static string Slug(string label)
    {
        var normalized = label.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        var slug = Regex.Replace(sb.ToString().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        return slug.Length == 0 ? "categoria" : slug[..Math.Min(slug.Length, 36)];
    }

    private static bool CanManage(HttpContext ctx, IInstitutionalAccessService access)
        => LibraryMagazineMetadataAuthorization.IsLibrarianProfile(ctx.User, access);

    private static async Task<IResult> ListAsync(HttpContext ctx, DocumentManagementDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        var manage = CanManage(ctx, access);
        var rows = await db.LibraryCategories.AsNoTracking().Where(x => manage || x.Active).OrderBy(x => x.Position).ThenBy(x => x.Label).ToListAsync(ct);
        var counts = await db.InstitutionalDocuments.AsNoTracking().GroupBy(x => x.DocumentType).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return Results.Ok(new
        {
            canManage = manage,
            items = rows.Select(x => new { value = x.Code, label = x.Label, @base = x.IsBase, active = x.Active, position = x.Position, publications = manage ? counts.GetValueOrDefault(x.Code) : 0 })
        });
    }

    private static async Task<IResult> CreateAsync(CreateCategoryRequest request, HttpContext ctx, DocumentManagementDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!CanManage(ctx, access)) return Results.Forbid();
        var label = (request.Label ?? string.Empty).Trim();
        if (label.Length is < 2 or > 60) return Results.BadRequest(new { code = "LIBRARY_CATEGORY_LABEL_INVALID", message = "El nombre debe tener entre 2 y 60 caracteres." });
        var all = await db.LibraryCategories.ToListAsync(ct);
        if (all.Count >= MaxCategories) return Results.Conflict(new { code = "LIBRARY_CATEGORY_LIMIT", message = "Se alcanzó el máximo de categorías." });
        if (all.Any(x => string.Equals(x.Label, label, StringComparison.OrdinalIgnoreCase)))
            return Results.Conflict(new { code = "LIBRARY_CATEGORY_DUPLICATE", message = "Ya existe una categoría con ese nombre." });
        var code = Slug(label); var candidate = code; var n = 2;
        while (all.Any(x => x.Code == candidate)) candidate = $"{code[..Math.Min(code.Length, 36)]}_{n++}";
        var category = new LibraryCategory { Code = candidate, Label = label, Position = all.Count == 0 ? 1 : all.Max(x => x.Position) + 1, Active = true, IsBase = false, UpdatedBySubject = ctx.User.FindFirst("sub")?.Value };
        db.LibraryCategories.Add(category);
        db.AuditEvents.Add(AuditEventFactory.Create(ctx, "library.category.created", nameof(LibraryCategory), category.Id.ToString(), null, AuditResults.Success, new { category.Code }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { value = category.Code, label = category.Label, @base = false, active = true, position = category.Position });
    }

    private static async Task<IResult> UpdateAsync(string code, UpdateCategoryRequest request, HttpContext ctx, DocumentManagementDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!CanManage(ctx, access)) return Results.Forbid();
        var category = await db.LibraryCategories.FirstOrDefaultAsync(x => x.Code == code, ct);
        if (category is null) return Results.NotFound();
        var changes = new List<string>();
        if (request.Label is not null)
        {
            var label = request.Label.Trim();
            if (label.Length is < 2 or > 60) return Results.BadRequest(new { code = "LIBRARY_CATEGORY_LABEL_INVALID", message = "El nombre debe tener entre 2 y 60 caracteres." });
            if (await db.LibraryCategories.AnyAsync(x => x.Id != category.Id && x.Label.ToLower() == label.ToLower(), ct))
                return Results.Conflict(new { code = "LIBRARY_CATEGORY_DUPLICATE", message = "Ya existe una categoría con ese nombre." });
            if (label != category.Label) { category.Label = label; changes.Add("label"); }
        }
        if (request.Active is not null && request.Active != category.Active)
        {
            if (request.Active == false)
            {
                if (category.IsBase) return Results.Conflict(new { code = "LIBRARY_CATEGORY_BASE_PROTECTED", message = "Las categorías base no se desactivan." });
                if (await db.InstitutionalDocuments.AnyAsync(x => x.DocumentType == category.Code, ct))
                    return Results.Conflict(new { code = "LIBRARY_CATEGORY_IN_USE", message = "Tiene publicaciones: retírelas o muévalas antes de desactivarla." });
            }
            category.Active = request.Active.Value; changes.Add("active");
        }
        if (changes.Count == 0) return Results.Ok(new { value = category.Code, label = category.Label, @base = category.IsBase, active = category.Active, position = category.Position });
        category.UpdatedAtUtc = DateTimeOffset.UtcNow; category.UpdatedBySubject = ctx.User.FindFirst("sub")?.Value;
        db.AuditEvents.Add(AuditEventFactory.Create(ctx, "library.category.updated", nameof(LibraryCategory), category.Id.ToString(), null, AuditResults.Success, new { category.Code, Changes = changes, category.Active }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { value = category.Code, label = category.Label, @base = category.IsBase, active = category.Active, position = category.Position });
    }

    private static async Task<IResult> ReorderAsync(ReorderCategoriesRequest request, HttpContext ctx, DocumentManagementDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!CanManage(ctx, access)) return Results.Forbid();
        var codes = request.Codes ?? [];
        var all = await db.LibraryCategories.ToListAsync(ct);
        if (codes.Length != all.Count || codes.Distinct().Count() != codes.Length || codes.Any(c => all.All(x => x.Code != c)))
            return Results.BadRequest(new { code = "LIBRARY_CATEGORY_ORDER_INVALID", message = "El orden debe incluir cada categoría exactamente una vez." });
        for (var i = 0; i < codes.Length; i++) all.First(x => x.Code == codes[i]).Position = i + 1;
        db.AuditEvents.Add(AuditEventFactory.Create(ctx, "library.category.reordered", nameof(LibraryCategory), "all", null, AuditResults.Success, new { Count = codes.Length }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { reordered = true });
    }
}
