using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.SecretariatOperations;

namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Valida evidencia viva de tres almacenes; la sola existencia de un GUID de
/// archivo/acta jamás constituye presentación, resolución ni autorización.
/// </summary>
public static class AdvancementPaperEvidenceValidator
{
    public static async Task<bool> IsLiveAsync(
        AdvancementPaperAttestation evidence, string ceremonyType,
        DateOnly gradeStart, DateOnly cutoff,
        PmgmDbContext db, LodgeManagementDbContext lodgeDb,
        DocumentManagementDbContext documentsDb, CancellationToken ct)
    {
        var sourceGrade = AdvancementAttendanceProjection.SourceGrade(ceremonyType);
        var sourceDegree = sourceGrade is null ? null : LodgeManagementCodes.Grade.ToNumeric(sourceGrade);
        if (sourceGrade is null || sourceDegree is null ||
            evidence.CeremonyRequestId == Guid.Empty || evidence.OrganizationId == Guid.Empty ||
            evidence.MemberId == Guid.Empty || evidence.PresentationDate <= gradeStart ||
            evidence.PresentationDate > cutoff || !AdvancementCertifiedPaperPolicy.ValidKind(evidence.WorkKind))
            return false;

        var meeting = await lodgeDb.LodgeMeetings.AsNoTracking().AnyAsync(x =>
            x.Id == evidence.MeetingId && x.OrganizationId == evidence.OrganizationId &&
            x.Grade == sourceGrade && x.CeremonyType == null &&
            (x.Status == LodgeManagementCodes.MeetingStatus.Held ||
             x.Status == LodgeManagementCodes.MeetingStatus.Closed) &&
            x.MeetingDate == evidence.PresentationDate, ct);
        if (!meeting) return false;

        var link = await db.LodgeSecretariatRecords.AsNoTracking().AnyAsync(x =>
            x.OrganizationId == evidence.OrganizationId &&
            x.RecordType == SecretariatOperationsCodes.RecordType.LodgeMeeting &&
            x.WorkPaperAuthorMemberId == evidence.MemberId &&
            x.SourceRecordId == evidence.MeetingId &&
            x.EventDate == evidence.PresentationDate &&
            x.WorkPaperDocumentVersionId == evidence.WorkPaperVersionId &&
            x.ExtractDocumentVersionId == evidence.ExtractVersionId &&
            x.FullMinuteDocumentVersionId == evidence.FullMinuteVersionId &&
            (x.Status == SecretariatOperationsCodes.SubmissionStatus.Submitted ||
             x.Status == SecretariatOperationsCodes.SubmissionStatus.Received), ct);
        if (!link) return false;

        var paper = await documentsDb.DocumentVersions.AsNoTracking()
            .Where(x => x.Id == evidence.WorkPaperVersionId &&
                x.DocumentId == evidence.WorkPaperDocumentId)
            .Select(x => new
            {
                x.Document.OrganizationId, x.Document.AuthorMemberId,
                x.Document.DocumentType, x.Document.MinimumDegreeRequired,
                x.Document.Status, x.AuthorEffectiveDegreeAtUpload,
                x.ProcessingStatus, x.Sha256, x.ScanReference, x.ObjectKey,
                x.CreatedAtUtc
            }).SingleOrDefaultAsync(ct);
        if (paper is null || paper.OrganizationId != evidence.OrganizationId ||
            paper.AuthorMemberId != evidence.MemberId || paper.DocumentType != "work_paper" ||
            paper.MinimumDegreeRequired != sourceDegree || paper.AuthorEffectiveDegreeAtUpload != sourceDegree ||
            paper.Status == DocumentManagementCodes.DocumentStatus.Retired ||
            paper.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available ||
            !DocumentIntegrity.IsValidSha256(paper.Sha256) ||
            string.IsNullOrWhiteSpace(paper.ScanReference) ||
            string.IsNullOrWhiteSpace(paper.ObjectKey))
            return false;
        var uploadedOn = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(paper.CreatedAtUtc, "America/Santiago").DateTime);
        if (uploadedOn <= gradeStart || uploadedOn > evidence.PresentationDate)
            return false;

        var versions = await documentsDb.DocumentVersions.AsNoTracking()
            .Where(x => x.Id == evidence.ExtractVersionId || x.Id == evidence.FullMinuteVersionId)
            .Select(x => new
            {
                x.Id, x.Document.OrganizationId, x.Document.Status,
                x.ContentType, x.ProcessingStatus, x.Sha256, x.ScanReference, x.ObjectKey
            }).ToListAsync(ct);
        var extract = versions.SingleOrDefault(x => x.Id == evidence.ExtractVersionId);
        var minute = versions.SingleOrDefault(x => x.Id == evidence.FullMinuteVersionId);
        return evidence.ExtractVersionId != evidence.FullMinuteVersionId &&
            extract is not null && minute is not null &&
            AdvancementExtractEvidencePolicy.IsReviewableExtract(
                new WorkPaperExtractVersion(extract.Id, extract.OrganizationId, extract.Status,
                    extract.ContentType, extract.ProcessingStatus, extract.Sha256,
                    extract.ScanReference, extract.ObjectKey), evidence.OrganizationId) &&
            AdvancementMeetingMinuteEvidencePolicy.IsReviewableFullMinute(
                new WorkPaperMeetingMinuteVersion(minute.Id, minute.OrganizationId, minute.Status,
                    minute.ContentType, minute.ProcessingStatus, minute.Sha256,
                    minute.ScanReference, minute.ObjectKey), evidence.OrganizationId);
    }
}
