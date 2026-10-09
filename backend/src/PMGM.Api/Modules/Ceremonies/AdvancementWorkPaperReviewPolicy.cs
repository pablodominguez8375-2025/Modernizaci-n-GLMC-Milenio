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

    public static WorkPaperReviewSummary Summarize(IEnumerable<WorkPaperReviewCandidate> candidates)
    {
        var items = candidates.OrderBy(x => x.DocumentId).ToArray();
        return new WorkPaperReviewSummary(
            items.Length,
            items.Count(x => x.ContentVerified),
            0,
            false,
            "Ningún archivo presentado queda acreditado sin vínculo institucional con Tenida.",
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
    string Reason);

public sealed record WorkPaperReviewSummary(
    int TotalDocuments,
    int ReviewableDocuments,
    int ConfirmedPresented,
    bool PresentationEvidenceAvailable,
    string Reason,
    IReadOnlyList<WorkPaperReviewCandidate> Items);
