using System.Text.Json;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class CandidatePublicationEvidenceSnapshotTests
{
    [Fact]
    public void Approval_audit_preserves_photo_and_policy_without_private_profile_fields()
    {
        var photo = Guid.NewGuid();
        var policy = CandidatePublicationEvidenceStore.DefaultPolicy() with { VersionId = Guid.NewGuid() };
        var metadata = AuditMetadataSanitizer.Serialize(new
        {
            publicationEvidence = new CandidatePublicationFrozenEvidence(1, photo, policy),
            RutOrInstitutionalId = "not-in-snapshot"
        });
        var read = Assert.IsType<CandidatePublicationFrozenEvidence>(CandidatePublicationEvidenceStore.ReadMetadata(metadata));
        Assert.Equal(photo, read.PhotoVersionId);
        Assert.Equal(policy.VersionId, read.Policy.VersionId);
        Assert.Equal(policy.VisibleFields, read.Policy.VisibleFields);
        using var json = JsonDocument.Parse(metadata!);
        var snapshot = json.RootElement.GetProperty("publicationEvidence");
        Assert.Equal(3, snapshot.EnumerateObject().Count());
        Assert.DoesNotContain("RutOrInstitutionalId", snapshot.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{\"reviewId\":\"legacy\"}")]
    public void Missing_evidence_is_legacy_and_does_not_invent_a_photo(string? metadata)
        => Assert.Null(CandidatePublicationEvidenceStore.ReadMetadata(metadata));

    [Theory]
    [InlineData("{\"publicationEvidence\":null}")]
    [InlineData("{\"publicationEvidence\":{\"schemaVersion\":99}}")]
    [InlineData("not-json")]
    [InlineData("[]")]
    public void Invalid_evidence_cannot_fall_back_to_a_mutable_photo(string metadata)
    {
        var read = Assert.IsType<CandidatePublicationFrozenEvidence>(CandidatePublicationEvidenceStore.ReadMetadata(metadata));
        Assert.Equal(0, read.SchemaVersion);
        Assert.Null(read.PhotoVersionId);
    }

    [Theory]
    [InlineData("Fotografía|Nombre completo|Taller", true)]
    [InlineData(" Taller | nombre completo | fotografía ", true)]
    [InlineData("Fotografía|Nombre completo|Taller|RUT", false)]
    [InlineData("Fotografía|Taller", false)]
    [InlineData("Fotografía|Fotografía|Taller", false)]
    public void Field_policy_accepts_only_the_three_approved_fields(string value, bool allowed)
        => Assert.Equal(allowed ? CandidatePublicationEvidenceStore.DefaultFields : null,
            CandidatePublicationEvidenceStore.NormalizeFields(value));
}
