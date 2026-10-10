using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementCertifiedPaperPolicyTests
{
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid Workshop = Guid.NewGuid();
    private static readonly Guid Member = Guid.NewGuid();
    private static readonly DateOnly Start = new(2025, 12, 1);
    private static readonly DateOnly Cutoff = new(2026, 10, 9);

    private static AdvancementPaperAttestation Sample(string kind,
        string status = "approved", Guid? docId = null)
        => new()
        {
            CeremonyRequestId = RequestId, OrganizationId = Workshop, MemberId = Member,
            WorkPaperDocumentId = docId ?? Guid.NewGuid(),
            WorkPaperVersionId = Guid.NewGuid(), MeetingId = Guid.NewGuid(),
            ExtractVersionId = Guid.NewGuid(), FullMinuteVersionId = Guid.NewGuid(),
            WorkKind = kind, PresentationDate = new DateOnly(2026, 8, 1),
            Status = status, CouncilApprovalReference = "Acta de aprobación, folio 12",
            PresentedBySubject = "secretaria-taller",
            ReviewedBySubject = "regimen-interior",
            ReviewedAtUtc = DateTimeOffset.UtcNow
        };

    [Fact]
    public void TwoDifferentWorks_OnlyCountWhenBothReviewAndLiveSourceSucceed()
    {
        var a = Sample(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var b = Sample(AdvancementWorkPaperEvidencePolicy.MasonicGeneralCulture);
        var valid = new HashSet<Guid> { a.Id, b.Id };
        var result = AdvancementCertifiedPaperPolicy.Count(
            RequestId, Workshop, Member, Start, Cutoff, [a, b], valid);
        Assert.Equal(2, result.Count);
        Assert.True(result.TwoRequiredKinds);
        Assert.Equal(2, result.VerifiedDocumentIds.Count);
    }

    [Fact]
    public void TwoDocumentsFromSameCategory_CannotSubstituteTwoKinds()
    {
        var a = Sample(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var b = Sample(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var result = AdvancementCertifiedPaperPolicy.Count(
            RequestId, Workshop, Member, Start, Cutoff, [a, b],
            new HashSet<Guid> { a.Id, b.Id });
        Assert.Equal(2, result.Count);
        Assert.False(result.TwoRequiredKinds);
    }

    [Fact]
    public void SameDocumentDifferentVersion_CannotCountTwice()
    {
        var docId = Guid.NewGuid();
        var a = Sample(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism, docId: docId);
        var b = Sample(AdvancementWorkPaperEvidencePolicy.MasonicGeneralCulture, docId: docId);
        b.RecordedAtUtc = a.RecordedAtUtc.AddMinutes(2);
        var actual = AdvancementCertifiedPaperPolicy.Count(
            RequestId, Workshop, Member, Start, Cutoff, [a, b],
            new HashSet<Guid> { a.Id, b.Id });
        Assert.Equal(1, actual.Count);
        Assert.False(actual.TwoRequiredKinds);
    }

    [Fact]
    public void PendingReplacesOldApproval_FailsClosed()
    {
        var old = Sample(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var revised = Sample(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism,
            "pending", old.WorkPaperDocumentId);
        revised.RecordedAtUtc = old.RecordedAtUtc.AddSeconds(1);
        var actual = AdvancementCertifiedPaperPolicy.Count(
            RequestId, Workshop, Member, Start, Cutoff, [old, revised],
            new HashSet<Guid> { old.Id, revised.Id });
        Assert.Equal(0, actual.Count);
    }

    [Fact]
    public void UnverifiedOrSelfApprovedPaper_IsNotCredited()
    {
        var paper = Sample(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        paper.ReviewedBySubject = paper.PresentedBySubject;
        Assert.Equal(0, AdvancementCertifiedPaperPolicy.Count(
            RequestId, Workshop, Member, Start, Cutoff, [paper],
            new HashSet<Guid> { paper.Id }).Count);
        paper.ReviewedBySubject = "regimen-interior";
        Assert.Equal(0, AdvancementCertifiedPaperPolicy.Count(
            RequestId, Workshop, Member, Start, Cutoff, [paper],
            new HashSet<Guid>()).Count);
    }

    [Fact]
    public void CrossWorkshopAndMemberRecords_NeverContribute()
    {
        var paper = Sample(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var valid = new HashSet<Guid> { paper.Id };
        Assert.Equal(0, AdvancementCertifiedPaperPolicy.Count(
            RequestId, Guid.NewGuid(), Member, Start, Cutoff, [paper], valid).Count);
        Assert.Equal(0, AdvancementCertifiedPaperPolicy.Count(
            RequestId, Workshop, Guid.NewGuid(), Start, Cutoff, [paper], valid).Count);
        Assert.Equal(0, AdvancementCertifiedPaperPolicy.Count(
            Guid.NewGuid(), Workshop, Member, Start, Cutoff, [paper], valid).Count);
    }

    [Fact]
    public void OutOfGradePeriodEvidenceRejectedEvenWhenReviewerApproved()
    {
        var paper = Sample(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        paper.PresentationDate = Start;
        var actual = AdvancementCertifiedPaperPolicy.Count(
            RequestId, Workshop, Member, Start, Cutoff, [paper],
            new HashSet<Guid> { paper.Id });
        Assert.Equal(0, actual.Count);
    }

    [Fact]
    public void UnknownCategoryCannotQualifyAndDoesNotCount()
    {
        var paper = Sample("arbitrary_third_work");
        Assert.False(AdvancementCertifiedPaperPolicy.ValidKind(paper.WorkKind));
        Assert.Equal(0, AdvancementCertifiedPaperPolicy.Count(
            RequestId, Workshop, Member, Start, Cutoff, [paper],
            new HashSet<Guid> { paper.Id }).Count);
    }
}
