using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.DocumentManagement;

public static class MemberWorkPaperEndpoints
{
    public static bool IsWorkPaper(string? documentType) => string.Equals(documentType, "work_paper", StringComparison.OrdinalIgnoreCase);

    public static IEndpointRouteBuilder MapMemberWorkPaperEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/biblioteca/mis-planchas")
            .WithTags("Planchas de Trabajo")
            .RequireAuthorization();
        group.MapGet("", GetMineAsync);
        group.MapGet("/versiones/{versionId:guid}", GetByVersionAsync);
        group.MapPost("", CreateAsync);
        group.MapPost("/{documentId:guid}/versiones", CreateVersionAsync);
        return endpoints;
    }

    private static async Task<IResult> GetByVersionAsync(
        Guid versionId,
        HttpContext context,
        PmgmDbContext institutionalDb,
        DocumentManagementDbContext documentsDb,
        IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver memberResolver,
        CancellationToken cancellationToken)
    {
        var version = await documentsDb.DocumentVersions.AsNoTracking().Include(x => x.Document)
            .SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);
        if (version is null || !IsWorkPaper(version.Document.DocumentType)) return Results.NotFound();
        if (!await CanManageAsync(version.Document, context, institutionalDb, access, memberResolver, cancellationToken)) return Results.Forbid();
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new MemberWorkPaperLinkDto(version.DocumentId, version.Document.AuthorMemberId, version.Document.Title, version.Document.ShortDescription ?? string.Empty));
    }

    private static async Task<IResult> GetMineAsync(
        HttpContext context,
        PmgmDbContext institutionalDb,
        DocumentManagementDbContext documentsDb,
        IInstitutionalMemberContextResolver memberResolver,
        CancellationToken cancellationToken)
    {
        var member = await memberResolver.ResolveAsync(context.User, cancellationToken);
        if (member is null) return Results.Forbid();
        var activeOrganizations = await institutionalDb.Memberships.AsNoTracking()
            .Where(x => x.MemberId == member.MemberId && x.Status == MembershipCodes.MembershipStatus.Active && x.EndDate == null)
            .Select(x => x.OrganizationId).ToListAsync(cancellationToken);
        var items = await documentsDb.InstitutionalDocuments.AsNoTracking()
            .Where(x => x.DocumentType == "work_paper" && x.AuthorMemberId == member.MemberId &&
                        x.OrganizationId != null && activeOrganizations.Contains(x.OrganizationId.Value))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new MemberWorkPaperDto(
                x.Id, x.Title, x.ShortDescription ?? string.Empty, x.MinimumDegreeRequired ?? 1,
                x.Status, x.PublishedVersionId,
                x.Versions.OrderByDescending(v => v.VersionNumber).Select(v => new MemberWorkPaperVersionDto(
                    v.Id, v.VersionNumber, v.OriginalFileName, v.ProcessingStatus, v.CreatedAtUtc,
                    v.Id == x.PublishedVersionId)).ToArray()))
            .ToListAsync(cancellationToken);
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new MemberWorkPapersResponse(items));
    }

    private static async Task<IResult> CreateAsync(
        CreateMemberWorkPaperRequest request,
        HttpContext context,
        PmgmDbContext institutionalDb,
        DocumentManagementDbContext documentsDb,
        IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver memberResolver,
        CancellationToken cancellationToken)
    {
        var organizationId = request.OrganizationId;
        var ownMember = await memberResolver.ResolveAsync(context.User, cancellationToken);
        var isSecretariat = access.HasRole(context.User, InstitutionalRoles.TallerSecretaria) &&
                            access.CanManageLodgeSecretariat(context.User, organizationId);
        var authorId = isSecretariat && request.AuthorMemberId is not null
            ? request.AuthorMemberId.Value
            : ownMember?.MemberId;
        if (authorId is null || (!isSecretariat && request.AuthorMemberId is not null && request.AuthorMemberId != ownMember?.MemberId))
            return Results.Forbid();

        var author = await GetActiveAuthorAsync(institutionalDb, authorId.Value, organizationId, cancellationToken);
        if (author is null) return Results.BadRequest(new { message = "El autor debe pertenecer actualmente al Taller indicado." });
        var degree = await ResolveDegreeAsync(institutionalDb, authorId.Value, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        if (degree is null) return Results.Conflict(new { message = "El autor no tiene un grado institucional vigente." });
        var title = NormalizeRequired(request.Title, 240);
        var description = NormalizeRequired(request.ShortDescription, 300);
        var filename = NormalizeFileName(request.OriginalFileName);
        var contentType = DocumentContentTypePolicy.Normalize(request.ContentType);
        if (title is null || description is null || filename is null || contentType is null || request.SizeBytes <= 0)
            return Results.BadRequest(new { message = "Título, descripción breve, archivo y tamaño válido son obligatorios." });
        if (!DocumentContentTypePolicy.TryValidateMetadata(filename, contentType, out _, out var fileError))
            return Results.BadRequest(new { message = fileError ?? "Sólo se aceptan PDF y DOCX." });

        var organization = await institutionalDb.Organizations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == organizationId && x.Type == "workshop", cancellationToken);
        if (organization is null) return Results.NotFound(new { message = "El Taller indicado no existe." });
        var collectionCode = $"WORKPAPERS-{organizationId:N}";
        var collection = await documentsDb.DocumentCollections.SingleOrDefaultAsync(x => x.Code == collectionCode, cancellationToken);
        if (collection is null)
        {
            collection = new DocumentCollection { Code = collectionCode, Name = "Planchas de Trabajo", Description = "Trabajos de los Hermanos publicados por grado en Biblioteca Virtual.", Scope = DocumentManagementCodes.Scope.Organization, OrganizationId = organizationId, CreatedBySubject = GetSubject(context.User) };
            documentsDb.DocumentCollections.Add(collection);
        }

        var document = new InstitutionalDocument
        {
            Collection = collection,
            OrganizationId = organizationId,
            AuthorMemberId = authorId,
            Title = title,
            DocumentType = "work_paper",
            Classification = DocumentManagementCodes.Classification.Confidential,
            AccessPolicy = DocumentManagementCodes.AccessPolicy.OrganizationAuthenticated,
            MinimumDegreeRequired = degree,
            AuthorName = $"{author.Member.Person.FirstNames} {author.Member.Person.LastNames}".Trim(),
            AuthorLodgeName = organization.Name,
            ShortDescription = description,
            Status = DocumentManagementCodes.DocumentStatus.Draft,
            CreatedBySubject = GetSubject(context.User)
        };
        documentsDb.InstitutionalDocuments.Add(document);
        var version = NewVersion(document, filename, contentType, request.SizeBytes, degree, context.User, title: title, description: description);
        documentsDb.DocumentVersions.Add(version);
        documentsDb.AuditEvents.Add(AuditEventFactory.Create(context, "library.work_paper.submitted", nameof(InstitutionalDocument), document.Id.ToString(), organizationId, AuditResults.Success,
            new { document.AuthorMemberId, document.DocumentType, document.MinimumDegreeRequired, Version = version.VersionNumber, UploadedByAuthor = ownMember?.MemberId == authorId }));
        await documentsDb.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/documentos/{document.Id}/versiones/{version.Id}", new MemberWorkPaperCreatedDto(document.Id, version.Id, version.VersionNumber, version.ProcessingStatus));
    }

    private static async Task<IResult> CreateVersionAsync(
        Guid documentId,
        CreateMemberWorkPaperVersionRequest request,
        HttpContext context,
        PmgmDbContext institutionalDb,
        DocumentManagementDbContext documentsDb,
        IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver memberResolver,
        CancellationToken cancellationToken)
    {
        var document = await documentsDb.InstitutionalDocuments.SingleOrDefaultAsync(x => x.Id == documentId && x.DocumentType == "work_paper", cancellationToken);
        if (document is null) return Results.NotFound();
        if (!await CanManageAsync(document, context, institutionalDb, access, memberResolver, cancellationToken)) return Results.Forbid();
        if (document.OrganizationId is null || document.AuthorMemberId is null) return Results.Conflict(new { message = "La plancha no tiene autoría o Taller asociado." });
        if (await documentsDb.DocumentVersions.AnyAsync(x => x.DocumentId == documentId &&
                (x.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.PendingUpload ||
                 x.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Uploaded ||
                 x.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Scanning), cancellationToken))
            return Results.Conflict(new { message = "Espera a que termine el análisis de la versión en curso antes de cargar otra." });
        var author = await GetActiveAuthorAsync(institutionalDb, document.AuthorMemberId.Value, document.OrganizationId.Value, cancellationToken);
        if (author is null) return Results.Conflict(new { message = "El autor ya no pertenece activamente a este Taller." });
        var degree = await ResolveDegreeAsync(institutionalDb, document.AuthorMemberId.Value, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        if (degree is null) return Results.Conflict(new { message = "El autor no tiene un grado institucional vigente." });
        var title = NormalizeRequired(request.Title, 240);
        var description = NormalizeRequired(request.ShortDescription, 300);
        var filename = NormalizeFileName(request.OriginalFileName);
        var contentType = DocumentContentTypePolicy.Normalize(request.ContentType);
        if (title is null || description is null || filename is null || contentType is null || request.SizeBytes <= 0)
            return Results.BadRequest(new { message = "Título, descripción breve, archivo y tamaño válido son obligatorios." });
        if (!DocumentContentTypePolicy.TryValidateMetadata(filename, contentType, out _, out var fileError))
            return Results.BadRequest(new { message = fileError ?? "Sólo se aceptan PDF y DOCX." });

        var lastVersion = await documentsDb.DocumentVersions.Where(x => x.DocumentId == documentId).MaxAsync(x => (int?)x.VersionNumber, cancellationToken) ?? 0;
        var version = NewVersion(document, filename, contentType, request.SizeBytes, degree, context.User, lastVersion + 1, title, description);
        documentsDb.DocumentVersions.Add(version);
        documentsDb.AuditEvents.Add(AuditEventFactory.Create(context, "library.work_paper.version_submitted", nameof(DocumentVersion), version.Id.ToString(), document.OrganizationId, AuditResults.Success,
            new { version.DocumentId, version.VersionNumber, document.AuthorMemberId, AuthorEffectiveDegreeAtUpload = degree }));
        await documentsDb.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/documentos/{document.Id}/versiones/{version.Id}", new MemberWorkPaperCreatedDto(document.Id, version.Id, version.VersionNumber, version.ProcessingStatus));
    }

    public static async Task<bool> CanManageAsync(
        InstitutionalDocument document,
        HttpContext context,
        PmgmDbContext institutionalDb,
        IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver resolver,
        CancellationToken cancellationToken)
    {
        if (!IsWorkPaper(document.DocumentType) || document.OrganizationId is null || document.AuthorMemberId is null) return false;
        if (access.HasRole(context.User, InstitutionalRoles.TallerSecretaria) && access.CanManageLodgeSecretariat(context.User, document.OrganizationId.Value)) return true;
        var member = await resolver.ResolveAsync(context.User, cancellationToken);
        if (member?.MemberId != document.AuthorMemberId) return false;
        return await GetActiveAuthorAsync(institutionalDb, member.MemberId, document.OrganizationId.Value, cancellationToken) is not null;
    }

    private static async Task<ActiveAuthor?> GetActiveAuthorAsync(PmgmDbContext db, Guid memberId, Guid organizationId, CancellationToken cancellationToken)
    {
        var membership = await db.Memberships.AsNoTracking().Include(x => x.Member).ThenInclude(x => x.Person)
            .SingleOrDefaultAsync(x => x.MemberId == memberId && x.OrganizationId == organizationId &&
                                       x.Status == MembershipCodes.MembershipStatus.Active && x.EndDate == null, cancellationToken);
        return membership is null ? null : new ActiveAuthor(membership.Member, membership.Member.Person, organizationId);
    }

    private static async Task<int?> ResolveDegreeAsync(PmgmDbContext db, Guid memberId, DateOnly asOf, CancellationToken cancellationToken)
    {
        var values = await db.DegreeEvents.AsNoTracking().Where(x => x.MemberId == memberId && x.EffectiveDate <= asOf)
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.RecordedAtUtc).Select(x => x.Degree).ToListAsync(cancellationToken);
        foreach (var value in values) if (InstitutionalDegree.TryParse(value, out var degree)) return degree;
        return null;
    }

    private static DocumentVersion NewVersion(InstitutionalDocument document, string filename, string contentType, long size, int degree, ClaimsPrincipal user, int number = 1, string? title = null, string? description = null)
    {
        var version = new DocumentVersion { Document = document, VersionNumber = number, OriginalFileName = filename, ContentType = contentType, SizeBytes = size, AuthorEffectiveDegreeAtUpload = degree, SubmittedTitle = title, SubmittedShortDescription = description, ProcessingStatus = DocumentManagementCodes.ProcessingStatus.PendingUpload, CreatedBySubject = GetSubject(user) };
        version.ObjectKey = DocumentObjectKeyFactory.Create(document.Id, version.Id);
        return version;
    }

    private static string GetSubject(ClaimsPrincipal user) => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    private static string? NormalizeRequired(string? value, int max) { var v = value?.Trim(); return string.IsNullOrWhiteSpace(v) || v.Length > max ? null : v; }
    private static string? NormalizeFileName(string? value) { var v = Path.GetFileName(value?.Trim()); return string.IsNullOrWhiteSpace(v) || v.Length > 500 ? null : v; }

    private sealed record ActiveAuthor(PMGM.Api.Modules.Membership.Entities.Member Member, PMGM.Api.Modules.Core.Entities.Person Person, Guid OrganizationId);
}

public sealed record CreateMemberWorkPaperRequest(Guid OrganizationId, Guid? AuthorMemberId, string Title, string ShortDescription, string OriginalFileName, string ContentType, long SizeBytes);
public sealed record CreateMemberWorkPaperVersionRequest(string Title, string ShortDescription, string OriginalFileName, string ContentType, long SizeBytes);
public sealed record MemberWorkPapersResponse(IReadOnlyList<MemberWorkPaperDto> Items);
public sealed record MemberWorkPaperDto(Guid Id, string Title, string ShortDescription, int MinimumDegreeRequired, string Status, Guid? PublishedVersionId, IReadOnlyList<MemberWorkPaperVersionDto> Versions);
public sealed record MemberWorkPaperVersionDto(Guid Id, int VersionNumber, string OriginalFileName, string ProcessingStatus, DateTimeOffset CreatedAtUtc, bool IsCurrent);
public sealed record MemberWorkPaperCreatedDto(Guid DocumentId, Guid VersionId, int VersionNumber, string ProcessingStatus);
public sealed record MemberWorkPaperLinkDto(Guid DocumentId, Guid? AuthorMemberId, string Title, string ShortDescription);
