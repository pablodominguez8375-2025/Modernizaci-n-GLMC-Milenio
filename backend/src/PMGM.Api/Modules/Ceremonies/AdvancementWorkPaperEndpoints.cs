using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.SecretariatOperations;

namespace PMGM.Api.Modules.Ceremonies;

public static class AdvancementWorkPaperEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
        => group.MapGet("/solicitudes/{requestId:guid}/avance/planchas", GetAsync);

    private static async Task<IResult> GetAsync(
        Guid requestId,
        HttpContext context,
        PmgmDbContext db,
        LodgeManagementDbContext lodgeDb,
        DocumentManagementDbContext documentsDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .Where(x => x.Id == requestId)
            .Select(x => new { x.OrganizationId, x.MemberId, x.CeremonyType })
            .SingleOrDefaultAsync(cancellationToken);
        if (ceremony is null) return Results.NotFound();

        if (!access.CanEvaluateCeremonies(context.User) &&
            !access.CanManageLodgeSecretariat(context.User, ceremony.OrganizationId))
            return Results.Forbid();

        var sourceGrade = AdvancementAttendanceProjection.SourceGrade(ceremony.CeremonyType);
        if (sourceGrade is null)
            return Results.BadRequest(new { message = "Esta consulta sólo corresponde a Aumento de Salario o Exaltación." });
        if (ceremony.MemberId is null)
            return Results.Conflict(new { message = "La solicitud no identifica al hermano del Taller." });

        var sourceDegree = LodgeManagementCodes.Grade.ToNumeric(sourceGrade);
        if (sourceDegree is null)
            return Results.Conflict(new { message = "El grado de procedencia no está reconocido." });

        var cutoff = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var attendance = await AdvancementAttendanceProjection.GetAsync(
            ceremony.OrganizationId, ceremony.MemberId.Value,
            ceremony.CeremonyType, cutoff, db, lodgeDb, cancellationToken);

        context.Response.Headers.CacheControl = "private, no-store";
        if (attendance.Status != "ready" || attendance.Snapshot is null)
            return Results.Ok(new
            {
                requestId,
                attendance.Status,
                attendance.Reason,
                workPapers = AdvancementWorkPaperReviewPolicy.Summarize(Array.Empty<WorkPaperReviewCandidate>()),
                authorizesCeremony = false
            });

        var docs = await documentsDb.InstitutionalDocuments.AsNoTracking()
            .Include(x => x.Versions)
            .Where(x => x.DocumentType == "work_paper" &&
                        x.AuthorMemberId == ceremony.MemberId.Value &&
                        x.OrganizationId == ceremony.OrganizationId &&
                        x.MinimumDegreeRequired == sourceDegree.Value &&
                        x.Status != DocumentManagementCodes.DocumentStatus.Retired)
            .ToListAsync(cancellationToken);

        var from = attendance.Snapshot.GradeStartDate;
        var candidates = docs.Select(document =>
        {
            var versions = document.Versions.Select(version =>
                new WorkPaperReviewVersion(
                    version.Id,
                    version.VersionNumber,
                    DateOnly.FromDateTime(
                        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(version.CreatedAtUtc, "America/Santiago").DateTime),
                    version.AuthorEffectiveDegreeAtUpload,
                    version.ProcessingStatus,
                    version.Sha256,
                    version.ScanReference,
                    document.PublishedVersionId == version.Id));

            return AdvancementWorkPaperReviewPolicy.Assess(
                document.Id, document.Title,
                document.Status == DocumentManagementCodes.DocumentStatus.Published,
                from, cutoff, sourceDegree.Value, versions);
        }).ToArray();

        // La referencia a una plancha puede guardarse antes de la Tenida.
        // Sólo se expone el vínculo cuando la actividad efectivamente ocurrió
        // y se corresponde con Taller, grado, fecha, autor y versión.
        var secretariatLinks = await db.LodgeSecretariatRecords.AsNoTracking()
            .Where(x => x.OrganizationId == ceremony.OrganizationId &&
                        x.RecordType == SecretariatOperationsCodes.RecordType.LodgeMeeting &&
                        x.WorkPaperAuthorMemberId == ceremony.MemberId.Value &&
                        x.WorkPaperDocumentVersionId != null &&
                        x.EventDate > from &&
                        x.EventDate <= cutoff)
            .Select(x => new
            {
                x.SourceRecordId,
                x.EventDate,
                x.WorkPaperDocumentVersionId,
                x.ExtractDocumentVersionId,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var linkedMeetingIds = secretariatLinks.Select(x => x.SourceRecordId).Distinct().ToArray();
        var validMeetings = await lodgeDb.LodgeMeetings.AsNoTracking()
            .Where(x => linkedMeetingIds.Contains(x.Id) &&
                        x.OrganizationId == ceremony.OrganizationId &&
                        x.Grade == sourceGrade &&
                        x.CeremonyType == null &&
                        x.MeetingDate > from &&
                        x.MeetingDate <= cutoff &&
                        (x.Status == LodgeManagementCodes.MeetingStatus.Held ||
                         x.Status == LodgeManagementCodes.MeetingStatus.Closed))
            .Select(x => new { x.Id, x.MeetingDate })
            .ToListAsync(cancellationToken);
        var meetingDates = validMeetings.ToDictionary(x => x.Id, x => x.MeetingDate);

        // El estado submitted/received del registro de Secretaría no basta:
        // comprobar la versión de extracto PDF contra su fuente documental viva.
        // El vínculo se conserva aunque el extracto no reúna estas garantías,
        // pero nunca se declara remitido sobre una referencia huérfana o inválida.
        var extractVersionIds = secretariatLinks
            .Where(x => x.ExtractDocumentVersionId is not null)
            .Select(x => x.ExtractDocumentVersionId!.Value)
            .Distinct()
            .ToArray();
        var extractVersions = await documentsDb.DocumentVersions.AsNoTracking()
            .Where(x => extractVersionIds.Contains(x.Id))
            .Select(x => new WorkPaperExtractVersion(
                x.Id, x.Document.OrganizationId, x.Document.Status, x.ContentType,
                x.ProcessingStatus, x.Sha256, x.ScanReference, x.ObjectKey))
            .ToListAsync(cancellationToken);
        var reviewableExtractIds = extractVersions
            .Where(x => AdvancementExtractEvidencePolicy.IsReviewableExtract(x, ceremony.OrganizationId))
            .Select(x => x.Id)
            .ToHashSet();

        var links = secretariatLinks
            .Where(x => meetingDates.TryGetValue(x.SourceRecordId, out var day) && day == x.EventDate)
            .Select(x => new WorkPaperMeetingLink(
                x.SourceRecordId,
                x.WorkPaperDocumentVersionId!.Value,
                true,
                x.ExtractDocumentVersionId is not null &&
                reviewableExtractIds.Contains(x.ExtractDocumentVersionId.Value) &&
                (x.Status == SecretariatOperationsCodes.SubmissionStatus.Submitted ||
                 x.Status == SecretariatOperationsCodes.SubmissionStatus.Received)))
            .ToArray();
        var withMeetings = candidates
            .Select(x => AdvancementWorkPaperReviewPolicy.AttachMeetingLinks(x, links))
            .ToArray();

        return Results.Ok(new
        {
            requestId,
            gradeStartDate = from,
            asOf = cutoff,
            sourceGrade,
            workPapers = AdvancementWorkPaperReviewPolicy.Summarize(withMeetings),
            authorizesCeremony = false
        });
    }
}
