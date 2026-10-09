using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementMeetingMinuteEvidencePolicyTests
{
    private readonly Guid _organizationId = Guid.NewGuid();

    private WorkPaperMeetingMinuteVersion Valid() => new(
        Guid.NewGuid(),
        _organizationId,
        DocumentManagementCodes.DocumentStatus.Active,
        "application/pdf",
        DocumentManagementCodes.ProcessingStatus.Available,
        new string('a', 64),
        "SCAN-OK",
        "documents/minute-version");

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void AvailableMinutesOfSameLodge_AreOnlyCandidatesForReview(string mimeType)
    {
        Assert.True(AdvancementMeetingMinuteEvidencePolicy.IsReviewableFullMinute(
            Valid() with { ContentType = mimeType }, _organizationId));
    }

    [Fact]
    public void MissingVersionAndWrongOrganization_FailClosed()
    {
        Assert.False(AdvancementMeetingMinuteEvidencePolicy.IsReviewableFullMinute(null, _organizationId));
        Assert.False(AdvancementMeetingMinuteEvidencePolicy.IsReviewableFullMinute(Valid(), Guid.Empty));
        Assert.False(AdvancementMeetingMinuteEvidencePolicy.IsReviewableFullMinute(
            Valid() with { Id = Guid.Empty }, _organizationId));
        Assert.False(AdvancementMeetingMinuteEvidencePolicy.IsReviewableFullMinute(
            Valid() with { OrganizationId = null }, _organizationId));
        Assert.False(AdvancementMeetingMinuteEvidencePolicy.IsReviewableFullMinute(
            Valid() with { OrganizationId = Guid.NewGuid() }, _organizationId));
    }

    [Fact]
    public void UnknownOrRetiredDocumentAndNonAvailableVersion_FailClosed()
    {
        foreach (var status in new[] { "invalid", DocumentManagementCodes.DocumentStatus.Retired })
        {
            Assert.False(AdvancementMeetingMinuteEvidencePolicy.IsReviewableFullMinute(
                Valid() with { DocumentStatus = status }, _organizationId));
        }

        foreach (var status in new[]
                 {
                     DocumentManagementCodes.ProcessingStatus.PendingUpload,
                     DocumentManagementCodes.ProcessingStatus.Uploaded,
                     DocumentManagementCodes.ProcessingStatus.Scanning,
                     DocumentManagementCodes.ProcessingStatus.Rejected,
                     "invalid"
                 })
        {
            Assert.False(AdvancementMeetingMinuteEvidencePolicy.IsReviewableFullMinute(
                Valid() with { ProcessingStatus = status }, _organizationId));
        }
    }

    [Fact]
    public void InvalidMimeShaScanOrObjectKey_DoesNotCountAsReviewableMinute()
    {
        var invalid = new[]
        {
            Valid() with { ContentType = "text/plain" },
            Valid() with { Sha256 = null },
            Valid() with { Sha256 = "bad-hash" },
            Valid() with { ScanReference = "" },
            Valid() with { ScanReference = " " },
            Valid() with { ObjectKey = null },
            Valid() with { ObjectKey = " " }
        };
        Assert.All(invalid, candidate =>
            Assert.False(AdvancementMeetingMinuteEvidencePolicy.IsReviewableFullMinute(candidate, _organizationId)));
    }
}
