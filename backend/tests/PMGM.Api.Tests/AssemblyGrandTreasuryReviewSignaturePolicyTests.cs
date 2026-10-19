using System.Security.Cryptography;
using System.Text;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyGrandTreasuryReviewSignaturePolicyTests
{
    private static readonly byte[] Document = Encoding.UTF8.GetBytes("Respaldo de prueba GT, sin identidad real");
    private static readonly DateTimeOffset At = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    private static (AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review) Acts()
    {
        var assemblyId = Guid.NewGuid();
        var lodgeId = Guid.NewGuid();
        var proposal = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = assemblyId,
            OrganizationId = lodgeId,
            Action = "proposed",
            ActorSubject = "gt-proponente",
            OccurredAtUtc = At,
            CoverageFrom = new DateOnly(2026, 1, 1),
            CutoffDate = new DateOnly(2026, 10, 19),
            Currency = "CLP",
            OpeningPendingAmount = 0m,
            SourceEvidenceReference = "GT-DOCUMENTO-SINTETICO",
            EvidenceDigestSha256 = Convert.ToHexString(SHA256.HashData(Document)).ToLowerInvariant()
        };
        var review = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = assemblyId,
            OrganizationId = lodgeId,
            Action = "reviewed",
            ActorSubject = "gt-revisor",
            OccurredAtUtc = At.AddMinutes(1),
            ProposalEventId = proposal.Id,
            ReviewEvidenceReference = "GT-REVISION-SINTETICA",
            ReviewNote = "Revisión de evidencia ficticia, no certificado financiero"
        };
        return (proposal, review);
    }

    private static AssemblyGrandTreasuryReviewSignature Sign(
        AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review,
        RSA rsa,
        DateTimeOffset? time = null)
    {
        var attestation = new AssemblyGrandTreasuryReviewSignature(
            proposal.AssemblyId, proposal.OrganizationId, proposal.Id, review.Id,
            review.ActorSubject, "gt-2026-qa", time ?? At.AddMinutes(2), "");
        var sig = rsa.SignData(
            AssemblyGrandTreasuryReviewSignaturePolicy.BuildCanonicalPayload(proposal, review, attestation),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return attestation with { SignatureBase64 = Convert.ToBase64String(sig) };
    }

    private static GrandTreasuryTrustedVerificationKey Key(
        RSA rsa, string reviewer = "gt-revisor", DateTimeOffset? revocation = null) =>
        new("gt-2026-qa", reviewer, rsa.ExportSubjectPublicKeyInfoPem(),
            At.AddHours(-1), At.AddHours(1), revocation);

    private static AssemblyGrandTreasuryReviewSignatureResult Verify(
        AssemblyTreasuryCertificationEventRecord p,
        AssemblyTreasuryCertificationEventRecord r,
        AssemblyGrandTreasuryReviewSignature sig,
        params GrandTreasuryTrustedVerificationKey[] trusted) =>
        AssemblyGrandTreasuryReviewSignaturePolicy.Verify(
            p, r, sig, Document, trusted, At.AddMinutes(3));

    [Fact]
    public void Cryptographically_valid_review_still_cannot_certify_balance_or_vote()
    {
        using var rsa = RSA.Create(2048);
        var (p, r) = Acts();
        var signed = Sign(p, r, rsa);
        var result = Verify(p, r, signed, Key(rsa));
        Assert.True(result.CryptographicallyVerified);
        Assert.False(result.MayAuthorizeSuffrage);
        Assert.Empty(result.BlockingReasons);
        Assert.Equal(
            AssemblyGrandTreasuryReviewSignaturePolicy.BuildCanonicalPayload(p, r, signed),
            AssemblyGrandTreasuryReviewSignaturePolicy.BuildCanonicalPayload(p, r, signed));
    }

    [Fact]
    public void Changed_document_and_changed_review_are_detected()
    {
        using var rsa = RSA.Create(2048);
        var (p, r) = Acts();
        var signed = Sign(p, r, rsa);
        var altered = AssemblyGrandTreasuryReviewSignaturePolicy.Verify(
            p, r, signed, Encoding.UTF8.GetBytes("cambiado"), [Key(rsa)], At.AddMinutes(3));
        Assert.Contains("UNVERIFIED_DOCUMENT_OR_REVIEW", altered.BlockingReasons);
        Assert.False(altered.CryptographicallyVerified);

        r.ReviewNote = "Se intentó modificar el acto después de firmarlo";
        Assert.Contains("INVALID_SIGNATURE", Verify(p, r, signed, Key(rsa)).BlockingReasons);
        r.ReviewNote = "Revisión de evidencia ficticia, no certificado financiero";
        p.SourceEvidenceReference = "Referencia alterada";
        Assert.Contains("INVALID_SIGNATURE", Verify(p, r, signed, Key(rsa)).BlockingReasons);
        p.SourceEvidenceReference = "GT-DOCUMENTO-SINTETICO";
        p.OpeningPendingAmount = 0.01m;
        Assert.Contains("INVALID_SIGNATURE", Verify(p, r, signed, Key(rsa)).BlockingReasons);
    }

    [Fact]
    public void Reused_signature_with_foreign_assembly_lodge_or_review_is_invalid()
    {
        using var rsa = RSA.Create(2048);
        var (p, r) = Acts();
        var signed = Sign(p, r, rsa);
        foreach (var switched in new[]
        {
            signed with { AssemblyId = Guid.NewGuid() },
            signed with { OrganizationId = Guid.NewGuid() },
            signed with { ProposalEventId = Guid.NewGuid() },
            signed with { ReviewEventId = Guid.NewGuid() },
            signed with { SignerSubject = "otro-revisor" }
        })
        {
            var outcome = Verify(p, r, switched, Key(rsa));
            Assert.False(outcome.CryptographicallyVerified);
            Assert.False(outcome.MayAuthorizeSuffrage);
        }
    }

    [Fact]
    public void Unknown_duplicate_expired_revoked_and_wrong_subject_keys_are_blocked()
    {
        using var rsa = RSA.Create(2048);
        var (p, r) = Acts();
        var signed = Sign(p, r, rsa);
        Assert.Contains("SIGNING_KEY_NOT_TRUSTED", Verify(p, r, signed).BlockingReasons);
        Assert.Contains("SIGNING_KEY_NOT_TRUSTED",
            Verify(p, r, signed, Key(rsa), Key(rsa)).BlockingReasons);
        Assert.Contains("SIGNING_KEY_NOT_TRUSTED",
            Verify(p, r, signed, Key(rsa, reviewer: "gt-invalido")).BlockingReasons);
        Assert.Contains("SIGNING_KEY_NOT_TRUSTED",
            Verify(p, r, signed,
                Key(rsa) with { ValidUntilUtc = At.AddMinutes(1) }).BlockingReasons);
        Assert.Contains("SIGNING_KEY_NOT_TRUSTED",
            Verify(p, r, signed,
                Key(rsa, revocation: At.AddMinutes(2).AddSeconds(30))).BlockingReasons);
    }

    [Fact]
    public void Corrupted_signature_and_untrusted_private_key_fail_verification()
    {
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        var (p, r) = Acts();
        var signed = Sign(p, r, rsa);
        Assert.Contains("INVALID_SIGNATURE",
            Verify(p, r, signed with { SignatureBase64 = "!!!" }, Key(rsa)).BlockingReasons);
        Assert.Contains("INVALID_SIGNATURE",
            Verify(p, r, signed, Key(other)).BlockingReasons);
        var earlier = signed with { SignedAtUtc = r.OccurredAtUtc.AddSeconds(-1) };
        Assert.Contains("INVALID_SIGNING_SCOPE_OR_TIME",
            Verify(p, r, earlier, Key(rsa)).BlockingReasons);
        var future = signed with { SignedAtUtc = At.AddHours(2) };
        Assert.Contains("INVALID_SIGNING_SCOPE_OR_TIME",
            Verify(p, r, future, Key(rsa)).BlockingReasons);
    }

    [Fact]
    public void Identity_or_independent_review_not_proven_is_never_accepted()
    {
        using var rsa = RSA.Create(2048);
        var (p, r) = Acts();
        var signed = Sign(p, r, rsa);
        r.ActorSubject = p.ActorSubject;
        Assert.Contains("UNVERIFIED_DOCUMENT_OR_REVIEW",
            Verify(p, r, signed, Key(rsa)).BlockingReasons);
        r.ActorSubject = "gt-revisor";
        r.Action = "rejected";
        Assert.Contains("UNVERIFIED_DOCUMENT_OR_REVIEW",
            Verify(p, r, signed, Key(rsa)).BlockingReasons);
    }
}
