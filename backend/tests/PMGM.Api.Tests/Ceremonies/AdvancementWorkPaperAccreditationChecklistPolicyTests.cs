using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementWorkPaperAccreditationChecklistPolicyTests
{
    private static WorkPaperReviewCandidate Paper(
        Guid? versionId,
        bool verified,
        IReadOnlyList<Guid>? held = null,
        IReadOnlyList<Guid>? extracts = null,
        IReadOnlyList<Guid>? minutes = null)
        => new(
            Guid.NewGuid(), "Trabajo para revisión", versionId, "available",
            verified, false, false, "Revisión institucional pendiente.",
            held, extracts, minutes);

    [Fact]
    public void SameMeetingCompletePacket_IsReadyOnlyForHumanReview()
    {
        var meeting = Guid.NewGuid();
        var paper = Paper(Guid.NewGuid(), true, [meeting], [meeting], [meeting]);

        var checklist = AdvancementWorkPaperAccreditationChecklistPolicy.Build(paper);

        Assert.True(checklist.DocumentaryPacketReadyForReview);
        Assert.Equal([meeting], checklist.SameMeetingPacketIds);
        Assert.False(checklist.PresentationCertified);
        Assert.False(checklist.ApprovalCertified);
        Assert.Contains("human_verification_of_presentation", checklist.PendingEvidenceCodes);
        Assert.Contains("human_verification_of_chamber_approval", checklist.PendingEvidenceCodes);
    }

    [Fact]
    public void ExtractAndMinutesFromDifferentMeetings_CannotFormOnePacket()
    {
        var meetingA = Guid.NewGuid();
        var meetingB = Guid.NewGuid();
        var checklist = AdvancementWorkPaperAccreditationChecklistPolicy.Build(
            Paper(Guid.NewGuid(), true,
                [meetingA, meetingB], [meetingA], [meetingB]));

        Assert.False(checklist.DocumentaryPacketReadyForReview);
        Assert.Empty(checklist.SameMeetingPacketIds);
        Assert.Contains("extract_and_full_minutes_same_meeting", checklist.PendingEvidenceCodes);
        Assert.False(checklist.PresentationCertified);
    }

    [Fact]
    public void MissingPaperVersionOrMeeting_FailsClosed()
    {
        var meeting = Guid.NewGuid();
        var noVersion = AdvancementWorkPaperAccreditationChecklistPolicy.Build(
            Paper(null, false, [meeting], [meeting], [meeting]));
        var noMeeting = AdvancementWorkPaperAccreditationChecklistPolicy.Build(
            Paper(Guid.NewGuid(), true, [], [meeting], [meeting]));

        Assert.False(noVersion.DocumentaryPacketReadyForReview);
        Assert.Contains("usable_work_paper_version", noVersion.PendingEvidenceCodes);
        Assert.False(noMeeting.DocumentaryPacketReadyForReview);
        Assert.Contains("held_meeting_link", noMeeting.PendingEvidenceCodes);
    }

    [Fact]
    public void ExtractOrFullMinutesMissing_PointsToIndependentEvidenceGaps()
    {
        var meeting = Guid.NewGuid();
        var onlyExtract = AdvancementWorkPaperAccreditationChecklistPolicy.Build(
            Paper(Guid.NewGuid(), true, [meeting], [meeting], []));
        var onlyMinutes = AdvancementWorkPaperAccreditationChecklistPolicy.Build(
            Paper(Guid.NewGuid(), true, [meeting], [], [meeting]));

        Assert.Contains("reviewable_full_minutes_same_meeting", onlyExtract.PendingEvidenceCodes);
        Assert.Contains("submitted_extract_same_meeting", onlyMinutes.PendingEvidenceCodes);
        Assert.False(onlyExtract.DocumentaryPacketReadyForReview);
        Assert.False(onlyMinutes.DocumentaryPacketReadyForReview);
    }

    [Fact]
    public void DuplicateEvidenceIds_DoNotInflatePacketOrInstitutionalCount()
    {
        var meeting = Guid.NewGuid();
        var paperA = Paper(Guid.NewGuid(), true,
            [meeting, meeting], [meeting, meeting], [meeting, meeting]);
        var paperB = Paper(Guid.NewGuid(), true, [Guid.NewGuid()], [], []);
        var summary = AdvancementWorkPaperReviewPolicy.Summarize([paperA, paperB]);

        Assert.Equal(2, summary.TotalDocuments);
        Assert.Equal(0, summary.ConfirmedPresented);
        Assert.False(summary.PresentationEvidenceAvailable);
        Assert.NotNull(summary.EvidenceChecklists);
        Assert.Equal(2, summary.EvidenceChecklists.Count);
        var ready = Assert.Single(summary.EvidenceChecklists.Where(x => x.DocumentaryPacketReadyForReview));
        Assert.Equal([meeting], ready.SameMeetingPacketIds);
        Assert.False(ready.PresentationCertified);
        Assert.False(ready.ApprovalCertified);
    }

    [Fact]
    public void InconsistentEvidenceNeverAuthorizesCeremoniesEvenWithTwoDocuments()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var summary = AdvancementWorkPaperReviewPolicy.Summarize([
            Paper(Guid.NewGuid(), true, [a], [a], [a]),
            Paper(Guid.NewGuid(), true, [b], [b], [b])
        ]);

        Assert.Equal(2, summary.EvidenceChecklists!.Count(x => x.DocumentaryPacketReadyForReview));
        Assert.Equal(0, summary.ConfirmedPresented);
        Assert.All(summary.Items, item => Assert.False(item.PresentationVerified));
        Assert.All(summary.EvidenceChecklists, item => Assert.False(item.ApprovalCertified));
    }
}
