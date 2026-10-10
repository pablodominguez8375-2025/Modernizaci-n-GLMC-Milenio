using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Ceremonies;

public sealed record AdvancementCertifiedPapers(int Count, bool TwoRequiredKinds,
    IReadOnlyList<Guid> VerifiedDocumentIds);

/// <summary>Las constancias validadas por un revisor NO se infieren desde archivos.</summary>
public static class AdvancementCertifiedPaperPolicy
{
    public static bool ValidKind(string? value)
        => value is AdvancementWorkPaperEvidencePolicy.DegreeSymbolism or
                    AdvancementWorkPaperEvidencePolicy.MasonicGeneralCulture;

    public static AdvancementCertifiedPapers Count(
        Guid requestId, Guid organizationId, Guid memberId,
        DateOnly gradeStart, DateOnly asOf,
        IEnumerable<AdvancementPaperAttestation> records,
        IReadOnlySet<Guid> liveEvidenceVerifiedIds)
    {
        var latest = records.Where(x =>
            x.CeremonyRequestId == requestId && x.OrganizationId == organizationId &&
            x.MemberId == memberId && x.PresentationDate > gradeStart &&
            x.PresentationDate <= asOf && ValidKind(x.WorkKind))
            .GroupBy(x => x.WorkPaperDocumentId)
            .Select(group => group.OrderByDescending(x => x.RecordedAtUtc)
                                  .ThenByDescending(x => x.Id).First())
            .Where(x => x.Status == "approved" &&
                !string.IsNullOrWhiteSpace(x.CouncilApprovalReference) &&
                !string.IsNullOrWhiteSpace(x.ReviewedBySubject) &&
                !string.IsNullOrWhiteSpace(x.PresentedBySubject) &&
                x.ReviewedBySubject != x.PresentedBySubject &&
                liveEvidenceVerifiedIds.Contains(x.Id))
            .ToArray();
        var kinds = latest.Select(x => x.WorkKind).ToHashSet(StringComparer.Ordinal);
        return new(latest.Length,
            kinds.Contains(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism) &&
            kinds.Contains(AdvancementWorkPaperEvidencePolicy.MasonicGeneralCulture),
            latest.Select(x => x.WorkPaperDocumentId).Order().ToArray());
    }
}
