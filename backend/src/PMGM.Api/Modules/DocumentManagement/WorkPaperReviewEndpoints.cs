using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.DocumentManagement;

public static class WorkPaperReviewStatus
{
    public const string Pending = "pending_review";
    public const string Returned = "returned";
    public const string Approved = "approved";
}

/// <summary>
/// #588 — revisión de planchas por el Bibliotecario (Opción B del PO). El Taller envía; nada es visible
/// en la Biblioteca hasta que el Bibliotecario aprueba. Al devolver, el motivo queda visible para el
/// Taller en «Mis planchas». Solo el perfil Bibliotecario (no el administrador de Gran Logia ni el Máster).
/// El grado de la plancha sigue siendo el del autor al subirla (lo exige la acreditación de avance).
/// </summary>
public static class WorkPaperReviewEndpoints
{
    public sealed record ReturnRequest(string? Reason);

    public static IEndpointRouteBuilder MapWorkPaperReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/biblioteca/revision").WithTags("Planchas de Trabajo").RequireAuthorization();
        group.MapGet("", ListPendingAsync);
        group.MapGet("/{documentId:guid}/contenido", DownloadPendingAsync);
        group.MapPost("/{documentId:guid}/aprobar", ApproveWorkPaperAsync);
        group.MapPost("/{documentId:guid}/devolver", ReturnWorkPaperAsync);
        return endpoints;
    }

    private static bool IsReviewer(HttpContext ctx, IInstitutionalAccessService access)
        => LibraryMagazineMetadataAuthorization.IsLibrarianProfile(ctx.User, access);

    private static async Task<IResult> ListPendingAsync(
        HttpContext ctx, DocumentManagementDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!IsReviewer(ctx, access)) return Results.Forbid();
        var rows = await db.InstitutionalDocuments.AsNoTracking()
            .Where(x => x.DocumentType == "work_paper" && x.ReviewStatus == WorkPaperReviewStatus.Pending && x.ReviewVersionId != null)
            .OrderBy(x => x.ReviewSubmittedAtUtc).ThenBy(x => x.Id)
            .Take(200)
            .Select(x => new
            {
                x.Id, x.Title, x.AuthorName, lodge = x.AuthorLodgeName, description = x.ShortDescription,
                submittedAtUtc = x.ReviewSubmittedAtUtc, versionId = x.ReviewVersionId, degree = x.MinimumDegreeRequired,
                replacesPublished = x.PublishedVersionId != null
            }).ToListAsync(ct);
        var versionIds = rows.Select(r => r.versionId!.Value).ToArray();
        var versions = await db.DocumentVersions.AsNoTracking().Where(v => versionIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);
        ctx.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            items = rows.Where(r => versions.ContainsKey(r.versionId!.Value)).Select(r =>
            {
                var v = versions[r.versionId!.Value];
                return new
                {
                    r.Id, r.Title, authorName = r.AuthorName, r.lodge, r.description, r.submittedAtUtc, r.degree,
                    r.replacesPublished, versionId = v.Id, fileName = v.OriginalFileName, v.SizeBytes
                };
            })
        });
    }

    private static async Task<IResult> DownloadPendingAsync(
        Guid documentId, HttpContext ctx, DocumentManagementDbContext db, IInstitutionalAccessService access,
        IDocumentObjectStore objectStore, CancellationToken ct)
    {
        if (!IsReviewer(ctx, access)) return Results.Forbid();
        var document = await db.InstitutionalDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId, ct);
        if (document is null || document.DocumentType != "work_paper" ||
            document.ReviewStatus != WorkPaperReviewStatus.Pending || document.ReviewVersionId is null)
            return Results.NotFound();
        var version = await db.DocumentVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == document.ReviewVersionId && x.DocumentId == document.Id, ct);
        if (version is null || version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available ||
            !await objectStore.ExistsAsync(version.ObjectKey, ct))
            return Results.NotFound();
        db.AuditEvents.Add(AuditEventFactory.Create(ctx, "library.work_paper.review_downloaded", nameof(DocumentVersion),
            version.Id.ToString(), document.OrganizationId, AuditResults.Success, new { version.DocumentId, version.VersionNumber }));
        await db.SaveChangesAsync(ct);
        var content = await objectStore.OpenReadAsync(version.ObjectKey, ct);
        ctx.Response.Headers.CacheControl = "private, no-store";
        return Results.Stream(content, version.ContentType, version.OriginalFileName, enableRangeProcessing: false);
    }

    private static async Task<IResult> ApproveWorkPaperAsync(
        Guid documentId, HttpContext ctx, DocumentManagementDbContext db, IInstitutionalAccessService access,
        IDocumentObjectStore objectStore, CancellationToken ct)
    {
        if (!IsReviewer(ctx, access)) return Results.Forbid();
        var document = await db.InstitutionalDocuments.SingleOrDefaultAsync(x => x.Id == documentId && x.DocumentType == "work_paper", ct);
        if (document is null) return Results.NotFound();
        if (document.ReviewStatus != WorkPaperReviewStatus.Pending || document.ReviewVersionId is null)
            return Results.Conflict(new { message = "La plancha no está pendiente de revisión." });
        if (document.Status == DocumentManagementCodes.DocumentStatus.Retired)
            return Results.Conflict(new { message = "La plancha está retirada." });
        var version = await db.DocumentVersions.SingleOrDefaultAsync(x => x.Id == document.ReviewVersionId && x.DocumentId == document.Id, ct);
        // Mismas garantías que cualquier publicación: disponible, huella válida, escaneo limpio, objeto presente.
        if (version is null || version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available ||
            !DocumentIntegrity.IsValidSha256(version.Sha256) || string.IsNullOrWhiteSpace(version.ScanReference) ||
            (!version.ScanReference.StartsWith("clamav:clean:", StringComparison.Ordinal) &&
             !version.ScanReference.StartsWith("clamav:test-clean:", StringComparison.Ordinal)) ||
            version.ObjectKey != DocumentObjectKeyFactory.Create(document.Id, version.Id))
            return Results.Conflict(new { message = "La versión no dispone de integridad y escaneo limpio verificables." });
        if (!await objectStore.ExistsAsync(version.ObjectKey, ct))
            return Results.Conflict(new { message = "El archivo de la versión no está almacenado." });

        var previous = document.PublishedVersionId;
        document.Title = version.SubmittedTitle ?? document.Title;
        document.ShortDescription = version.SubmittedShortDescription ?? document.ShortDescription;
        document.PublishedVersionId = version.Id;
        document.PublishedAtUtc = DateTimeOffset.UtcNow;
        document.Status = DocumentManagementCodes.DocumentStatus.Published;
        if (version.AuthorEffectiveDegreeAtUpload is >= 1)
            document.MinimumDegreeRequired = version.AuthorEffectiveDegreeAtUpload;
        document.ReviewStatus = WorkPaperReviewStatus.Approved;
        document.ReviewReason = null;
        document.ReviewedAtUtc = DateTimeOffset.UtcNow;
        document.ReviewedBySubject = DynamicTreasuryAccess.Subject(ctx.User);
        db.AuditEvents.Add(AuditEventFactory.Create(ctx, "library.work_paper.approved", nameof(InstitutionalDocument),
            document.Id.ToString(), document.OrganizationId, AuditResults.Success,
            new { VersionId = version.Id, version.VersionNumber, PreviousVersionId = previous, document.MinimumDegreeRequired }));
        await db.SaveChangesAsync(ct);
        ctx.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { document.Id, document.Status, document.ReviewStatus, document.PublishedVersionId });
    }

    private static async Task<IResult> ReturnWorkPaperAsync(
        Guid documentId, ReturnRequest request, HttpContext ctx, DocumentManagementDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!IsReviewer(ctx, access)) return Results.Forbid();
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length is < 10 or > 500 || reason.Any(char.IsControl))
            return Results.BadRequest(new { message = "Indica el motivo de la devolución (10 a 500 caracteres)." });
        var document = await db.InstitutionalDocuments.SingleOrDefaultAsync(x => x.Id == documentId && x.DocumentType == "work_paper", ct);
        if (document is null) return Results.NotFound();
        if (document.ReviewStatus != WorkPaperReviewStatus.Pending)
            return Results.Conflict(new { message = "La plancha no está pendiente de revisión." });
        var versionId = document.ReviewVersionId;
        document.ReviewStatus = WorkPaperReviewStatus.Returned;
        document.ReviewReason = reason;
        document.ReviewedAtUtc = DateTimeOffset.UtcNow;
        document.ReviewedBySubject = DynamicTreasuryAccess.Subject(ctx.User);
        db.AuditEvents.Add(AuditEventFactory.Create(ctx, "library.work_paper.returned", nameof(InstitutionalDocument),
            document.Id.ToString(), document.OrganizationId, AuditResults.Success, new { VersionId = versionId, Reason = reason }));
        await db.SaveChangesAsync(ct);
        ctx.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { document.Id, document.Status, document.ReviewStatus });
    }
}
