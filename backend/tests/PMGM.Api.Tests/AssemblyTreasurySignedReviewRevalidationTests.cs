using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyTreasurySignedReviewRevalidationTests
{
    private static readonly DateTimeOffset Time = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Evidence = Encoding.UTF8.GetBytes("GT respaldo C11: solo datos sintéticos");
    private const string KeyId = "gt-c11-valid-key";

    private static (AssemblyTreasurySignedReviewRecord record,
        AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review,
        AssemblyGrandTreasuryServerTrustRegistry registry) Signed()
    {
        using var rsa = RSA.Create(2048);
        var proposal = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(),
            Action = "proposed", ActorSubject = "gt-proponente",
            OccurredAtUtc = Time.AddMinutes(-12),
            CoverageFrom = new DateOnly(2026, 1, 1),
            CutoffDate = new DateOnly(2026, 10, 19),
            Currency = "CLP", OpeningPendingAmount = 0m,
            SourceEvidenceReference = "GT-ACTO-C11",
            EvidenceDigestSha256 = Convert.ToHexString(SHA256.HashData(Evidence)).ToLowerInvariant()
        };
        var review = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = proposal.AssemblyId, OrganizationId = proposal.OrganizationId,
            Action = "reviewed", ActorSubject = "gt-revisor",
            ProposalEventId = proposal.Id, OccurredAtUtc = Time.AddMinutes(-8),
            ReviewEvidenceReference = "GT-REVISION-C11",
            ReviewNote = "Segundo funcionario verificó el respaldo"
        };
        var envelope = new AssemblyGrandTreasuryReviewSignature(
            proposal.AssemblyId, proposal.OrganizationId, proposal.Id, review.Id,
            review.ActorSubject, KeyId, Time.AddMinutes(-5), "");
        var signature = Convert.ToBase64String(rsa.SignData(
            AssemblyGrandTreasuryReviewSignaturePolicy.BuildCanonicalPayload(
                proposal, review, envelope), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        var record = new AssemblyTreasurySignedReviewRecord
        {
            AssemblyId = proposal.AssemblyId, OrganizationId = proposal.OrganizationId,
            ProposalEventId = proposal.Id, ReviewEventId = review.Id,
            ReviewerSubject = review.ActorSubject, TrustedKeyId = KeyId,
            DocumentSha256Hex = proposal.EvidenceDigestSha256!,
            SignatureBase64 = signature,
            SignedAtUtc = envelope.SignedAtUtc,
            RecordedAtUtc = Time.AddMinutes(-3)
        };
        const string prefix = AssemblyGrandTreasuryServerTrustRegistry.ConfigurationSection + ":0:";
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [prefix + "KeyId"] = KeyId,
                [prefix + "ReviewerSubject"] = "gt-revisor",
                [prefix + "PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem(),
                [prefix + "ValidFromUtc"] = Time.AddDays(-2).ToString("O"),
                [prefix + "ValidUntilUtc"] = Time.AddDays(2).ToString("O"),
                [prefix + "Approved"] = "true"
            }).Build();
        return (record, proposal, review, new(config));
    }

    private static AssemblySignedReviewRevalidationResult Check(
        AssemblyTreasurySignedReviewRecord record,
        AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review,
        AssemblyGrandTreasuryServerTrustRegistry registry, byte[]? evidence = null,
        Guid? lodge = null, DateTimeOffset? at = null) =>
        AssemblyTreasurySignedReviewRevalidation.Check(
            record, proposal, review, record.AssemblyId,
            lodge ?? record.OrganizationId, evidence ?? Evidence, registry, at ?? Time);

    [Fact]
    public void Original_verified_review_is_documentary_only_and_never_a_vote()
    {
        var (record, proposal, review, registry) = Signed();
        var result = Check(record, proposal, review, registry);
        Assert.True(result.ValidForDocumentaryReview);
        Assert.False(result.MayAuthorizeSuffrage);
        Assert.Empty(result.BlockingReasons);
    }

    [Fact]
    public void Changed_bytes_or_review_are_rejected()
    {
        var (record, proposal, review, registry) = Signed();
        var changed = Check(record, proposal, review, registry,
            Encoding.UTF8.GetBytes("Documento modificado"));
        Assert.False(changed.ValidForDocumentaryReview);
        Assert.Contains("UNVERIFIED_DOCUMENT_OR_REVIEW", changed.BlockingReasons);

        review.ReviewNote = "Nota alterada";
        var tampered = Check(record, proposal, review, registry);
        Assert.False(tampered.ValidForDocumentaryReview);
        Assert.Contains("INVALID_SIGNATURE", tampered.BlockingReasons);
    }

    [Fact]
    public void Scope_event_identity_and_hash_cannot_be_substituted()
    {
        var (record, proposal, review, registry) = Signed();
        Assert.Contains("ATTESTATION_LINK_OR_SEQUENCE_INVALID",
            Check(record, proposal, review, registry, lodge: Guid.NewGuid()).BlockingReasons);
        record.ReviewEventId = Guid.NewGuid();
        Assert.Contains("ATTESTATION_LINK_OR_SEQUENCE_INVALID",
            Check(record, proposal, review, registry).BlockingReasons);
        record.ReviewEventId = review.Id;
        record.ReviewerSubject = "otro-sujeto";
        Assert.Contains("ATTESTATION_LINK_OR_SEQUENCE_INVALID",
            Check(record, proposal, review, registry).BlockingReasons);
        record.ReviewerSubject = review.ActorSubject;
        record.DocumentSha256Hex = new string('0', 64);
        Assert.Contains("ATTESTATION_LINK_OR_SEQUENCE_INVALID",
            Check(record, proposal, review, registry).BlockingReasons);
    }

    [Fact]
    public void Revocation_after_signature_invalidates_subsequent_consultations()
    {
        var (record, proposal, review, _) = Signed();
        using var rsa = RSA.Create(2048);
        // Clave sustituta desde configuración ya no valida un sello antiguo.
        const string prefix = AssemblyGrandTreasuryServerTrustRegistry.ConfigurationSection + ":0:";
        var values = new Dictionary<string, string?>
        {
            [prefix + "KeyId"] = KeyId, [prefix + "ReviewerSubject"] = "gt-revisor",
            [prefix + "PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem(),
            [prefix + "ValidFromUtc"] = Time.AddDays(-2).ToString("O"),
            [prefix + "ValidUntilUtc"] = Time.AddDays(2).ToString("O"),
            [prefix + "RevokedAtUtc"] = Time.AddMinutes(-1).ToString("O"),
            [prefix + "Approved"] = "true"
        };
        var revoked = new AssemblyGrandTreasuryServerTrustRegistry(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        var result = Check(record, proposal, review, revoked);
        Assert.False(result.ValidForDocumentaryReview);
        Assert.Contains("SIGNING_KEY_NOT_TRUSTED", result.BlockingReasons);
        Assert.False(result.MayAuthorizeSuffrage);
    }

    [Fact]
    public void Missing_events_future_record_time_and_missing_document_fail_closed()
    {
        var (record, proposal, review, registry) = Signed();
        Assert.Contains("MISSING_LINKED_ACTS",
            AssemblyTreasurySignedReviewRevalidation.Check(record, null, review,
                record.AssemblyId, record.OrganizationId, Evidence, registry, Time)
                .BlockingReasons);
        Assert.Contains("ATTESTATION_LINK_OR_SEQUENCE_INVALID",
            AssemblyTreasurySignedReviewRevalidation.Check(record, proposal, review,
                Guid.Empty, record.OrganizationId, [], registry, Time)
                .BlockingReasons);
        record.SignedAtUtc = Time.AddMinutes(1);
        Assert.Contains("ATTESTATION_LINK_OR_SEQUENCE_INVALID",
            Check(record, proposal, review, registry).BlockingReasons);
    }
}
