using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.SecretariatOperations;

namespace PMGM.Api.Modules.GrandSecretariat;

public static class GrandSecretariatQueryEndpoints
{
    public static IEndpointRouteBuilder MapGrandSecretariatQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/gran-secretaria/reservas", GetReservationsAsync)
            .WithTags("Gran Secretaría")
            .RequireAuthorization();

        endpoints.MapGet("/api/gran-secretaria/tenidas", GetSubmittedTenidasAsync)
            .WithTags("Gran Secretaría")
            .RequireAuthorization();

        endpoints.MapGet("/api/gran-secretaria/tenidas/{recordId:guid}/extracto", DownloadSubmittedTenidaExtractAsync)
            .WithTags("Gran Secretaría")
            .RequireAuthorization();

        endpoints.MapPost("/api/gran-secretaria/tenidas/{recordId:guid}/revision", ReviewSubmittedTenidaAsync)
            .WithTags("Gran Secretaría")
            .RequireAuthorization();
        return endpoints;
    }


    private static async Task<IResult> GetSubmittedTenidasAsync(
        Guid? organizationId,
        DateOnly? from,
        DateOnly? to,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanReviewSecretariatSubmissions(httpContext.User)) return Results.Forbid();
        if (from is not null && to is not null && to < from)
            return Results.BadRequest(new { message = "La fecha final no puede ser anterior a la inicial." });

        var recordQuery = institutionalDb.LodgeSecretariatRecords
            .AsNoTracking()
            .Where(x =>
                x.RecordType == SecretariatOperationsCodes.RecordType.LodgeMeeting &&
                x.ExtractDocumentVersionId != null &&
                x.SubmittedAtUtc != null &&
                (x.Status == SecretariatOperationsCodes.SubmissionStatus.Submitted ||
                 x.Status == SecretariatOperationsCodes.SubmissionStatus.Received ||
                 x.Status == SecretariatOperationsCodes.SubmissionStatus.Observed));

        if (organizationId is not null) recordQuery = recordQuery.Where(x => x.OrganizationId == organizationId.Value);
        if (from is not null) recordQuery = recordQuery.Where(x => x.EventDate >= from.Value);
        if (to is not null) recordQuery = recordQuery.Where(x => x.EventDate <= to.Value);

        var records = await recordQuery
            .OrderByDescending(x => x.EventDate)
            .ThenByDescending(x => x.SubmittedAtUtc)
            .Take(500)
            .ToListAsync(cancellationToken);

        var organizationIds = records.Select(x => x.OrganizationId).Distinct().ToArray();
        var meetingIds = records.Select(x => x.SourceRecordId).Distinct().ToArray();

        var organizations = organizationIds.Length == 0
            ? new Dictionary<Guid, GrandSecretariatLodgeRef>()
            : await institutionalDb.Organizations
                .AsNoTracking()
                .Where(x => organizationIds.Contains(x.Id))
                .Select(x => new GrandSecretariatLodgeRef(x.Id, x.Name, x.Number))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var meetings = meetingIds.Length == 0
            ? new Dictionary<Guid, GrandSecretariatTenidaBasic>()
            : await lodgeDb.LodgeMeetings
                .AsNoTracking()
                .Where(x => meetingIds.Contains(x.Id))
                .Select(x => new GrandSecretariatTenidaBasic(
                    x.Id,
                    x.MeetingDate,
                    x.MeetingType,
                    x.Grade,
                    x.CeremonyType,
                    x.Modality,
                    x.Title,
                    x.Status))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var items = records
            .Where(x => organizations.ContainsKey(x.OrganizationId) && meetings.ContainsKey(x.SourceRecordId))
            .Select(x =>
            {
                var lodge = organizations[x.OrganizationId];
                var meeting = meetings[x.SourceRecordId];
                return new GrandSecretariatSubmittedTenidaDto(
                    x.Id,
                    lodge,
                    meeting.Id,
                    meeting.MeetingDate,
                    meeting.MeetingType,
                    meeting.Grade,
                    meeting.CeremonyType,
                    meeting.Modality,
                    meeting.Title,
                    meeting.Status,
                    x.ExtractDocumentVersionId!.Value,
                    x.Status,
                    x.SubmittedAtUtc!.Value,
                    x.ReviewedAtUtc,
                    x.ReviewNotes);
            })
            .ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new GrandSecretariatSubmittedTenidasResponse(items.Count, items));
    }

    private static async Task<IResult> DownloadSubmittedTenidaExtractAsync(
        Guid recordId,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        DocumentManagementDbContext documentDb,
        IInstitutionalAccessService access,
        IDocumentObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        if (!access.CanReviewSecretariatSubmissions(httpContext.User)) return Results.Forbid();

        var record = await institutionalDb.LodgeSecretariatRecords.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == recordId &&
                     x.RecordType == SecretariatOperationsCodes.RecordType.LodgeMeeting &&
                     x.ExtractDocumentVersionId != null &&
                     x.SubmittedAtUtc != null,
                cancellationToken);

        if (record is null) return Results.NotFound();

        var version = await documentDb.DocumentVersions
            .Include(x => x.Document)
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == record.ExtractDocumentVersionId!.Value &&
                     x.Document.OrganizationId == record.OrganizationId,
                cancellationToken);

        if (version is null ||
            version.ContentType != "application/pdf" ||
            version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available)
            return Results.NotFound(new { message = "El extracto PDF remitido no está disponible." });

        if (!await objectStore.ExistsAsync(version.ObjectKey, cancellationToken))
            return Results.NotFound(new { message = "El archivo físico del extracto no está disponible." });

        institutionalDb.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "grand_secretariat.tenida_extract.downloaded",
            "LodgeSecretariatRecord",
            record.Id.ToString(),
            record.OrganizationId,
            AuditResults.Success,
            new { record.SourceRecordId, ExtractVersionId = version.Id }));
        await institutionalDb.SaveChangesAsync(cancellationToken);

        var content = await objectStore.OpenReadAsync(version.ObjectKey, cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Stream(content, "application/pdf", version.OriginalFileName, enableRangeProcessing: false);
    }

    private static async Task<IResult> ReviewSubmittedTenidaAsync(
        Guid recordId,
        GrandSecretariatTenidaReviewRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanReviewSecretariatSubmissions(httpContext.User)) return Results.Forbid();
        if (request.Decision is not "received" and not "observed")
            return Results.BadRequest(new { message = "La decisión debe ser received u observed." });
        if (request.Decision == "observed" && string.IsNullOrWhiteSpace(request.Notes))
            return Results.BadRequest(new { message = "Una observación debe indicar el motivo." });

        var record = await db.LodgeSecretariatRecords.SingleOrDefaultAsync(
            x => x.Id == recordId &&
                 x.RecordType == SecretariatOperationsCodes.RecordType.LodgeMeeting &&
                 x.SubmittedAtUtc != null,
            cancellationToken);
        if (record is null) return Results.NotFound();

        record.Status = request.Decision == "received"
            ? SecretariatOperationsCodes.SubmissionStatus.Received
            : SecretariatOperationsCodes.SubmissionStatus.Observed;
        record.ReviewedBySubject = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? "unknown";
        record.ReviewedAtUtc = DateTimeOffset.UtcNow;
        record.ReviewNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            request.Decision == "received"
                ? "grand_secretariat.tenida_extract.received"
                : "grand_secretariat.tenida_extract.observed",
            "LodgeSecretariatRecord",
            record.Id.ToString(),
            record.OrganizationId,
            AuditResults.Success,
            new { record.SourceRecordId, record.Status }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            record.Id,
            record.SourceRecordId,
            record.OrganizationId,
            record.Status,
            record.ReviewedAtUtc,
            record.ReviewNotes
        });
    }

    private static async Task<IResult> GetReservationsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? ceremonyRequestId,
        HttpContext httpContext,
        GrandSecretariatDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandSecretariat(httpContext.User)) return Results.Forbid();
        if (toUtc <= fromUtc) return Results.BadRequest(new { message = "El término del período debe ser posterior al inicio." });
        if (toUtc - fromUtc > TimeSpan.FromDays(93)) return Results.BadRequest(new { message = "La consulta de reservas no puede exceder 93 días." });

        var query = db.SpaceReservations
            .AsNoTracking()
            .Where(x => x.StartsAtUtc < toUtc && x.EndsAtUtc > fromUtc);

        if (ceremonyRequestId is not null) query = query.Where(x => x.CeremonyRequestId == ceremonyRequestId.Value);

        var items = await query
            .OrderBy(x => x.StartsAtUtc)
            .ThenBy(x => x.Space.Name)
            .Select(x => new GrandSecretariatReservationDto(
                x.Id,
                x.SpaceId,
                x.Space.Name,
                x.OrganizationId,
                x.CeremonyRequestId,
                x.Purpose,
                x.StartsAtUtc,
                x.EndsAtUtc,
                x.Status))
            .Take(500)
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new GrandSecretariatReservationsResponse(items.Count, items));
    }
}

public sealed record GrandSecretariatReservationDto(
    Guid Id,
    Guid SpaceId,
    string SpaceName,
    Guid OrganizationId,
    Guid? CeremonyRequestId,
    string Purpose,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string Status);

public sealed record GrandSecretariatReservationsResponse(
    int Total,
    IReadOnlyList<GrandSecretariatReservationDto> Items);

public sealed record GrandSecretariatLodgeRef(Guid Id, string Name, string? Number);

public sealed record GrandSecretariatTenidaBasic(
    Guid Id,
    DateOnly MeetingDate,
    string MeetingType,
    string Grade,
    string? CeremonyType,
    string Modality,
    string? Title,
    string Status);

public sealed record GrandSecretariatSubmittedTenidaDto(
    Guid RecordId,
    GrandSecretariatLodgeRef Lodge,
    Guid MeetingId,
    DateOnly MeetingDate,
    string MeetingType,
    string Grade,
    string? CeremonyType,
    string Modality,
    string? Title,
    string MeetingStatus,
    Guid ExtractDocumentVersionId,
    string SubmissionStatus,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewNotes);

public sealed record GrandSecretariatSubmittedTenidasResponse(
    int Total,
    IReadOnlyList<GrandSecretariatSubmittedTenidaDto> Items);

public sealed record GrandSecretariatTenidaReviewRequest(string Decision, string? Notes);
