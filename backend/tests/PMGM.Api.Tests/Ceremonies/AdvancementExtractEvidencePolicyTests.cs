using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementExtractEvidencePolicyTests
{
    private readonly Guid _organization = Guid.NewGuid();

    private WorkPaperExtractVersion Valid() => new(
        Guid.NewGuid(),
        _organization,
        DocumentManagementCodes.DocumentStatus.Active,
        "application/pdf",
        DocumentManagementCodes.ProcessingStatus.Available,
        new string('a', 64),
        "CLAMAV-CLEAN",
        "documents/valid-version");

    [Fact]
    public void AvailablePdfOfSameLodgeWithIntegrityAndScan_IsReviewableNotAuthorization()
    {
        Assert.True(AdvancementExtractEvidencePolicy.IsReviewableExtract(Valid(), _organization));
    }

    [Fact]
    public void MissingVersionOrInstitutionalScope_FailsClosed()
    {
        Assert.False(AdvancementExtractEvidencePolicy.IsReviewableExtract(null, _organization));
        Assert.False(AdvancementExtractEvidencePolicy.IsReviewableExtract(Valid(), Guid.Empty));
        Assert.False(AdvancementExtractEvidencePolicy.IsReviewableExtract(
            Valid() with { Id = Guid.Empty }, _organization));
        Assert.False(AdvancementExtractEvidencePolicy.IsReviewableExtract(
            Valid() with { OrganizationId = null }, _organization));
        Assert.False(AdvancementExtractEvidencePolicy.IsReviewableExtract(
            Valid() with { OrganizationId = Guid.NewGuid() }, _organization));
    }

    [Fact]
    public void RetiredDocumentOrUnavailableVersion_DoesNotProveSubmittedExtract()
    {
        Assert.False(AdvancementExtractEvidencePolicy.IsReviewableExtract(
            Valid() with { DocumentStatus = DocumentManagementCodes.DocumentStatus.Retired }, _organization));
        Assert.False(AdvancementExtractEvidencePolicy.IsReviewableExtract(
            Valid() with { DocumentStatus = "unknown" }, _organization));
        foreach (var status in new[]
                 {
                     DocumentManagementCodes.ProcessingStatus.PendingUpload,
                     DocumentManagementCodes.ProcessingStatus.Uploaded,
                     DocumentManagementCodes.ProcessingStatus.Scanning,
                     DocumentManagementCodes.ProcessingStatus.Rejected
                 })
        {
            Assert.False(AdvancementExtractEvidencePolicy.IsReviewableExtract(
                Valid() with { ProcessingStatus = status }, _organization));
        }
    }

    [Fact]
    public void WrongMimeMissingIntegrityOrScanAndObject_FailsClosed()
    {
        var candidates = new[]
        {
            Valid() with { ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            Valid() with { Sha256 = null },
            Valid() with { Sha256 = "not-sha256" },
            Valid() with { ScanReference = " " },
            Valid() with { ObjectKey = null },
            Valid() with { ObjectKey = " " }
        };
        Assert.All(candidates, x =>
            Assert.False(AdvancementExtractEvidencePolicy.IsReviewableExtract(x, _organization)));
    }
}
