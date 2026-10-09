using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementWorkPaperReviewPolicyTests
{
    private static readonly DateOnly GradeStart = new(2026, 1, 15);
    private static readonly DateOnly AsOf = new(2026, 10, 9);

    [Fact]
    public void CleanAvailableDocument_IsOnlyReviewable_NeverAutomaticallyPresented()
    {
        var documentId = Guid.NewGuid();
        var version = ReadyVersion(1, true);
        var candidate = AdvancementWorkPaperReviewPolicy.Assess(
            documentId, "Plancha simbólica", true, GradeStart, AsOf, 1, [version]);
        var summary = AdvancementWorkPaperReviewPolicy.Summarize([candidate]);

        Assert.True(candidate.ContentVerified);
        Assert.True(candidate.PublishedToLibrary);
        Assert.False(candidate.PresentationVerified);
        Assert.Equal(version.Id, candidate.ReviewVersionId);
        Assert.Equal(1, summary.ReviewableDocuments);
        Assert.Equal(0, summary.ConfirmedPresented);
        Assert.False(summary.PresentationEvidenceAvailable);
    }

    [Fact]
    public void UploadedPendingOrMalwareRejectedCannotBeReviewable()
    {
        var pending = ReadyVersion(1, false) with { ProcessingStatus = DocumentManagementCodes.ProcessingStatus.PendingUpload };
        var rejected = ReadyVersion(2, false) with { ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Rejected };
        var invalidScan = ReadyVersion(3, false) with { ScanReference = null };
        foreach (var version in new[] { pending, rejected, invalidScan })
        {
            var result = AdvancementWorkPaperReviewPolicy.Assess(
                Guid.NewGuid(), "Borrador", false, GradeStart, AsOf, 1, [version]);
            Assert.False(result.ContentVerified);
            Assert.False(result.PresentationVerified);
            Assert.Null(result.ReviewVersionId);
        }
    }

    [Fact]
    public void WrongGradeAndOutOfPeriodVersionsAreExcluded()
    {
        var wrongGrade = ReadyVersion(1, false) with { AuthorDegreeAtUpload = 2 };
        var beforeStart = ReadyVersion(2, false) with { CreatedOn = new DateOnly(2026, 1, 1) };
        var afterCutoff = ReadyVersion(3, false) with { CreatedOn = new DateOnly(2026, 11, 1) };
        var candidate = AdvancementWorkPaperReviewPolicy.Assess(
            Guid.NewGuid(), "Fuera de período", false, GradeStart, AsOf, 1,
            [wrongGrade, beforeStart, afterCutoff]);

        Assert.False(candidate.ContentVerified);
        Assert.Null(candidate.LatestVersionStatus);
    }

    [Fact]
    public void RejectedNewVersion_DoesNotHideEarlierValidatedVersion()
    {
        var older = ReadyVersion(1, true);
        var newer = ReadyVersion(2, false) with { ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Rejected };
        var result = AdvancementWorkPaperReviewPolicy.Assess(
            Guid.NewGuid(), "Trabajo versionado", true, GradeStart, AsOf, 1, [older, newer]);

        Assert.True(result.ContentVerified);
        Assert.True(result.PublishedToLibrary);
        Assert.Equal(older.Id, result.ReviewVersionId);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Rejected, result.LatestVersionStatus);
        Assert.False(result.PresentationVerified);
    }

    [Fact]
    public void NonPublishedCleanVersion_StillRequiresInstitutionalPresentation()
    {
        var item = AdvancementWorkPaperReviewPolicy.Assess(
            Guid.NewGuid(), "Sin publicación", false, GradeStart, AsOf, 2, [ReadyVersion(1, false) with { AuthorDegreeAtUpload = 2 }]);

        Assert.True(item.ContentVerified);
        Assert.False(item.PublishedToLibrary);
        Assert.False(item.PresentationVerified);
    }

    [Fact]
    public void HeldMeetingLink_IsVisibleWithoutCertifyingPresentation()
    {
        var version = ReadyVersion(1, false);
        var paper = AdvancementWorkPaperReviewPolicy.Assess(
            Guid.NewGuid(), "Plancha vinculada", false, GradeStart, AsOf, 1, [version]);
        var meetingId = Guid.NewGuid();
        var link = new WorkPaperMeetingLink(meetingId, version.Id, true, true);
        var result = AdvancementWorkPaperReviewPolicy.AttachMeetingLinks(paper, [link]);

        Assert.Equal([meetingId], result.LinkedHeldMeetingIds);
        Assert.Equal([meetingId], result.SubmittedExtractMeetingIds);
        Assert.False(result.PresentationVerified);
        Assert.Equal(0, AdvancementWorkPaperReviewPolicy.Summarize([result]).ConfirmedPresented);
    }

    [Fact]
    public void ScheduledOrWrongVersionLink_IsExcludedEvenWithWorkPaper()
    {
        var version = ReadyVersion(1, false);
        var paper = AdvancementWorkPaperReviewPolicy.Assess(
            Guid.NewGuid(), "Prueba", false, GradeStart, AsOf, 1, [version]);
        var links = new[]
        {
            new WorkPaperMeetingLink(Guid.NewGuid(), version.Id, false, true),
            new WorkPaperMeetingLink(Guid.NewGuid(), Guid.NewGuid(), true, true)
        };
        var result = AdvancementWorkPaperReviewPolicy.AttachMeetingLinks(paper, links);

        Assert.Empty(result.LinkedHeldMeetingIds!);
        Assert.Empty(result.SubmittedExtractMeetingIds!);
    }

    [Fact]
    public void DuplicateMeetingLinks_DoNotInflateLinkedMeetings()
    {
        var version = ReadyVersion(1, false);
        var paper = AdvancementWorkPaperReviewPolicy.Assess(
            Guid.NewGuid(), "Duplicada", false, GradeStart, AsOf, 1, [version]);
        var meetingId = Guid.NewGuid();
        var item = new WorkPaperMeetingLink(meetingId, version.Id, true, false);
        var result = AdvancementWorkPaperReviewPolicy.AttachMeetingLinks(paper, [item, item]);

        Assert.Single(result.LinkedHeldMeetingIds!);
        Assert.Empty(result.SubmittedExtractMeetingIds!);
    }

    private static WorkPaperReviewVersion ReadyVersion(int number, bool published)
        => new(
            Guid.NewGuid(), number, new DateOnly(2026, 8, 7), 1,
            DocumentManagementCodes.ProcessingStatus.Available,
            new string('a', 64),
            "SCAN-APPROVED",
            published);
}
