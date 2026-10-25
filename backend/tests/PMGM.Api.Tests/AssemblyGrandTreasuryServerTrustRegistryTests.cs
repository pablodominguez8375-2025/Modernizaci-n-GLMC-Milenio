using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyGrandTreasuryServerTrustRegistryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Document = Encoding.UTF8.GetBytes("Expediente sintético GT C7");

    private static (AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review) Acts()
    {
        var proposal = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(),
            Action = "proposed", ActorSubject = "gt-preparador",
            OccurredAtUtc = Now.AddMinutes(-10),
            CoverageFrom = new DateOnly(2026, 1, 1),
            CutoffDate = new DateOnly(2026, 10, 19),
            Currency = "CLP", OpeningPendingAmount = 100m,
            SourceEvidenceReference = "GT-REF-SINTETICA",
            EvidenceDigestSha256 = Convert.ToHexString(SHA256.HashData(Document)).ToLowerInvariant()
        };
        var review = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = proposal.AssemblyId, OrganizationId = proposal.OrganizationId,
            Action = "reviewed", ActorSubject = "gt-revisor",
            ProposalEventId = proposal.Id, OccurredAtUtc = Now.AddMinutes(-5),
            ReviewEvidenceReference = "GT-REV-FICTICIA",
            ReviewNote = "Revisión documental únicamente"
        };
        return (proposal, review);
    }

    private static AssemblyGrandTreasuryReviewSignature Sign(
        AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review, RSA rsa)
    {
        var envelope = new AssemblyGrandTreasuryReviewSignature(
            proposal.AssemblyId, proposal.OrganizationId, proposal.Id, review.Id,
            review.ActorSubject, "gt-clave-uno", Now.AddMinutes(-2), "");
        var bytes = AssemblyGrandTreasuryReviewSignaturePolicy
            .BuildCanonicalPayload(proposal, review, envelope);
        return envelope with
        {
            SignatureBase64 = Convert.ToBase64String(
                rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
        };
    }

    private static Dictionary<string, string?> Entries(RSA rsa)
    {
        const string root = AssemblyGrandTreasuryServerTrustRegistry.ConfigurationSection + ":0:";
        return new Dictionary<string, string?>
        {
            [root + "KeyId"] = "gt-clave-uno",
            [root + "ReviewerSubject"] = "gt-revisor",
            [root + "PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem(),
            [root + "ValidFromUtc"] = Now.AddDays(-1).ToString("O"),
            [root + "ValidUntilUtc"] = Now.AddDays(1).ToString("O"),
            [root + "Approved"] = "true"
        };
    }

    private static AssemblyGrandTreasuryServerTrustRegistry Registry(
        IDictionary<string, string?> values) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    private static AssemblyGrandTreasuryReviewSignatureResult Check(
        AssemblyGrandTreasuryServerTrustRegistry registry,
        AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review,
        AssemblyGrandTreasuryReviewSignature signature) =>
        registry.Verify(proposal, review, signature, Document, Now);

    [Fact]
    public void Server_registered_public_key_verifies_without_enabling_suffrage()
    {
        using var rsa = RSA.Create(2048);
        var (proposal, review) = Acts();
        var envelope = Sign(proposal, review, rsa);
        var result = Check(Registry(Entries(rsa)), proposal, review, envelope);
        Assert.True(result.CryptographicallyVerified);
        Assert.False(result.MayAuthorizeSuffrage);
        Assert.Empty(result.BlockingReasons);
    }

    [Fact]
    public void Absent_unapproved_or_duplicate_server_registry_fails_closed()
    {
        using var rsa = RSA.Create(2048);
        var (p, r) = Acts();
        var signed = Sign(p, r, rsa);
        var empty = Check(Registry(new Dictionary<string, string?>()), p, r, signed);
        Assert.Contains("SERVER_TRUST_REGISTRY_NOT_READY", empty.BlockingReasons);

        var config = Entries(rsa);
        config[AssemblyGrandTreasuryServerTrustRegistry.ConfigurationSection + ":0:Approved"] = "false";
        Assert.Contains("SERVER_TRUST_REGISTRY_NOT_READY",
            Check(Registry(config), p, r, signed).BlockingReasons);

        config = Entries(rsa);
        const string first = AssemblyGrandTreasuryServerTrustRegistry.ConfigurationSection + ":0:";
        const string second = AssemblyGrandTreasuryServerTrustRegistry.ConfigurationSection + ":1:";
        foreach (var (name, value) in Entries(rsa))
            config[second + name[first.Length..]] = value;
        Assert.Contains("SERVER_TRUST_REGISTRY_NOT_READY",
            Check(Registry(config), p, r, signed).BlockingReasons);
    }

    [Fact]
    public void Revoked_expired_wrong_subject_or_private_pem_are_untrusted()
    {
        using var rsa = RSA.Create(2048);
        var (p, r) = Acts();
        var signed = Sign(p, r, rsa);
        const string prefix = AssemblyGrandTreasuryServerTrustRegistry.ConfigurationSection + ":0:";

        var revoked = Entries(rsa);
        revoked[prefix + "RevokedAtUtc"] = Now.AddHours(-1).ToString("O");
        Assert.Contains("SIGNING_KEY_NOT_TRUSTED",
            Check(Registry(revoked), p, r, signed).BlockingReasons);

        var expired = Entries(rsa);
        expired[prefix + "ValidUntilUtc"] = Now.AddDays(-2).ToString("O");
        Assert.Contains("SERVER_TRUST_REGISTRY_NOT_READY",
            Check(Registry(expired), p, r, signed).BlockingReasons);

        var wrong = Entries(rsa);
        wrong[prefix + "ReviewerSubject"] = "gt-otro";
        Assert.Contains("SIGNING_KEY_NOT_TRUSTED",
            Check(Registry(wrong), p, r, signed).BlockingReasons);

        var privatePem = Entries(rsa);
        privatePem[prefix + "PublicKeyPem"] = rsa.ExportPkcs8PrivateKeyPem();
        Assert.Contains("SERVER_TRUST_REGISTRY_NOT_READY",
            Check(Registry(privatePem), p, r, signed).BlockingReasons);
    }

    [Fact]
    public void Non_iso_date_and_key_substitution_fail_closed()
    {
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        var (p, r) = Acts();
        var signed = Sign(p, r, rsa);
        const string prefix = AssemblyGrandTreasuryServerTrustRegistry.ConfigurationSection + ":0:";

        var malformed = Entries(rsa);
        malformed[prefix + "ValidFromUtc"] = "2026-10-19";
        Assert.Contains("SERVER_TRUST_REGISTRY_NOT_READY",
            Check(Registry(malformed), p, r, signed).BlockingReasons);

        var replaced = Entries(other);
        Assert.Contains("INVALID_SIGNATURE",
            Check(Registry(replaced), p, r, signed).BlockingReasons);
    }
}
