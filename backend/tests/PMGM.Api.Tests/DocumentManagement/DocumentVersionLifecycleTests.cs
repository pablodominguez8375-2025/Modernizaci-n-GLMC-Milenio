using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.DocumentManagement;

public sealed class DocumentVersionLifecycleTests
{
    private const string ValidSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Pending_upload_requires_valid_sha_to_become_uploaded()
    {
        var rejected = DocumentVersionLifecycle.CanTransition(
            DocumentManagementCodes.ProcessingStatus.PendingUpload,
            DocumentManagementCodes.ProcessingStatus.Uploaded,
            null,
            null);
        Assert.False(rejected.Allowed);

        var accepted = DocumentVersionLifecycle.CanTransition(
            DocumentManagementCodes.ProcessingStatus.PendingUpload,
            DocumentManagementCodes.ProcessingStatus.Uploaded,
            ValidSha,
            null);
        Assert.True(accepted.Allowed);
    }

    [Fact]
    public void Available_requires_scan_evidence_and_cannot_be_skipped()
    {
        var skip = DocumentVersionLifecycle.CanTransition(
            DocumentManagementCodes.ProcessingStatus.Uploaded,
            DocumentManagementCodes.ProcessingStatus.Available,
            ValidSha,
            "scan-001");
        Assert.False(skip.Allowed);

        var withoutScan = DocumentVersionLifecycle.CanTransition(
            DocumentManagementCodes.ProcessingStatus.Scanning,
            DocumentManagementCodes.ProcessingStatus.Available,
            ValidSha,
            null);
        Assert.False(withoutScan.Allowed);

        var accepted = DocumentVersionLifecycle.CanTransition(
            DocumentManagementCodes.ProcessingStatus.Scanning,
            DocumentManagementCodes.ProcessingStatus.Available,
            ValidSha,
            "scan-001");
        Assert.True(accepted.Allowed);
    }

    [Fact]
    public void Available_is_terminal()
    {
        var decision = DocumentVersionLifecycle.CanTransition(
            DocumentManagementCodes.ProcessingStatus.Available,
            DocumentManagementCodes.ProcessingStatus.Uploaded,
            ValidSha,
            "scan-001");

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void Object_key_is_opaque_and_library_projection_exposes_no_storage_secrets()
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var objectKey = DocumentObjectKeyFactory.Create(documentId, versionId);

        Assert.Contains(documentId.ToString("N"), objectKey);
        Assert.Contains(versionId.ToString("N"), objectKey);
        Assert.DoesNotContain(".pdf", objectKey, StringComparison.OrdinalIgnoreCase);

        var libraryProperties = typeof(LibraryDocumentDto).GetProperties().Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", libraryProperties);
        Assert.DoesNotContain("Sha256", libraryProperties);
        Assert.DoesNotContain("OriginalFileName", libraryProperties);
        Assert.DoesNotContain("ScanReference", libraryProperties);
    }
}
