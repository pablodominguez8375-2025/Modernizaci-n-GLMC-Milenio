using PMGM.Api.Modules.DocumentManagement;

namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Selección documental preliminar. Aun una versión disponible, íntegra y
/// publicada NO prueba que se haya presentado una plancha en una Tenida.
/// </summary>
public static class AdvancementWorkPaperReviewPolicy
{
    public static WorkPaperReviewCandidate Assess(
        Guid documentId,
        string title,
        bool published,
        DateOnly gradeStart,
        DateOnly cutoff,
        int sourceDegree,
        IEnumerable<WorkPaperReviewVersion> versions)
    {
        var related = versions
            .Where(v => v.AuthorDegreeAtUpload == sourceDegree &&
                        v.CreatedOn > gradeStart &&
                        v.CreatedOn <= cutoff)
            .OrderByDescending(v => v.VersionNumber)
            .ThenByDescending(v => v.Id)
            .ToArray();

        var usable = related.FirstOrDefault(v =>
            v.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Available &&
            DocumentIntegrity.IsValidSha256(v.Sha256) &&
            !string.IsNullOrWhiteSpace(v.ScanReference));

        var latest = related.FirstOrDefault();
        var reason = usable is null
            ? "Sin versión del grado y período con análisis e integridad completos."
            : "Archivo apto para revisión; falta constancia institucional de presentación en Tenida.";

        return new WorkPaperReviewCandidate(
            documentId,
            title,
            usable?.Id,
            latest?.ProcessingStatus,
            usable is not null,
            published && usable is not null && related.Any(v => v.Id == usable.Id && v.IsPublishedVersion),
            false,
            reason);
    }

    /// <summary>
    /// Un vínculo comprobable a Tenida celebrada es evidencia para revisión,
    /// pero NO es confirmación de presentación ni incrementa mínimos.
    /// </summary>
    public static WorkPaperReviewCandidate AttachMeetingLinks(
        WorkPaperReviewCandidate candidate,
        IEnumerable<WorkPaperMeetingLink> links)
    {
        if (candidate.ReviewVersionId is null || !candidate.ContentVerified)
            return candidate with
            {
                LinkedHeldMeetingIds = Array.Empty<Guid>(),
                SubmittedExtractMeetingIds = Array.Empty<Guid>()
            };

        var eligible = links
            .Where(x => x.WorkPaperVersionId == candidate.ReviewVersionId &&
                        x.IsHeldNonCeremonial &&
                        x.MeetingId != Guid.Empty)
            .GroupBy(x => x.MeetingId)
            .Select(group => new
            {
                MeetingId = group.Key,
                ExtractSubmitted = group.Any(link => link.ExtractSubmitted)
            })
            .ToArray();

        return candidate with
        {
            LinkedHeldMeetingIds = eligible.Select(x => x.MeetingId).Order().ToArray(),
            SubmittedExtractMeetingIds = eligible
                .Where(x => x.ExtractSubmitted)
                .Select(x => x.MeetingId).Order().ToArray()
        };
    }

    public static WorkPaperReviewSummary Summarize(IEnumerable<WorkPaperReviewCandidate> candidates)
    {
        var items = candidates.OrderBy(x => x.DocumentId).ToArray();
        return new WorkPaperReviewSummary(
            items.Length,
            items.Count(x => x.ContentVerified),
            0,
            false,
            "Un vínculo a Tenida o Extracto remitido no certifica presentación de la plancha.",
            items);
    }
}

public sealed record WorkPaperReviewVersion(
    Guid Id,
    int VersionNumber,
    DateOnly CreatedOn,
    int? AuthorDegreeAtUpload,
    string ProcessingStatus,
    string? Sha256,
    string? ScanReference,
    bool IsPublishedVersion);

public sealed record WorkPaperReviewCandidate(
    Guid DocumentId,
    string Title,
    Guid? ReviewVersionId,
    string? LatestVersionStatus,
    bool ContentVerified,
    bool PublishedToLibrary,
    bool PresentationVerified,
    string Reason,
    IReadOnlyList<Guid>? LinkedHeldMeetingIds = null,
    IReadOnlyList<Guid>? SubmittedExtractMeetingIds = null);

public sealed record WorkPaperMeetingLink(
    Guid MeetingId,
    Guid WorkPaperVersionId,
    bool IsHeldNonCeremonial,
    bool ExtractSubmitted);

public sealed record WorkPaperReviewSummary(
    int TotalDocuments,
    int ReviewableDocuments,
    int ConfirmedPresented,
    bool PresentationEvidenceAvailable,
    string Reason,
    IReadOnlyList<WorkPaperReviewCandidate> Items);
