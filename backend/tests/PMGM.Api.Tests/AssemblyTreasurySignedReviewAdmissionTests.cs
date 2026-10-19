using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyTreasurySignedReviewAdmissionTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 10, 19);
    private static readonly byte[] Document = Encoding.UTF8.GetBytes("Expediente GT sintético, sin datos reales");

    private static ClaimsPrincipal Reviewer(string role = InstitutionalRoles.GranTesoreria) =>
        new(new ClaimsIdentity([
            new Claim("sub", "gt-revisor-c10"),
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, role)
        ], "TestIdentity"));

    private static DynamicAccessCatalog Access(bool write = true)
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        catalog.Profiles.Add(new DynamicProfile(Guid.NewGuid(), "gt-c10", "GT revisión",
            "order", false, true, ["treasury"],
            [new DynamicGrant("treasury", write ? ["view", "write"] : ["view"])]));
        catalog.Assignments.Add(new DynamicAssignment(Guid.NewGuid(), "gt-revisor-c10",
            "gt-c10", null, Day.AddDays(-2), null, true));
        return catalog;
    }

    private static (AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review) Events()
    {
        var proposal = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(),
            Action = "proposed", ActorSubject = "gt-emisor-c10",
            OccurredAtUtc = At.AddMinutes(-9),
            CoverageFrom = new DateOnly(2026, 1, 1),
            CutoffDate = Day, Currency = "CLP", OpeningPendingAmount = 0m,
            SourceEvidenceReference = "GT-RES-SINTETICA",
            EvidenceDigestSha256 = Convert.ToHexString(SHA256.HashData(Document)).ToLowerInvariant()
        };
        var review = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = proposal.AssemblyId, OrganizationId = proposal.OrganizationId,
            Action = "reviewed", ActorSubject = "gt-revisor-c10",
            OccurredAtUtc = At.AddMinutes(-5),
            ProposalEventId = proposal.Id,
            ReviewEvidenceReference = "GT-REV-C10",
            ReviewNote = "Segundo funcionario, sin certificado definitivo"
        };
        return (proposal, review);
    }

    private static (AssemblyGrandTreasuryReviewSignature signature,
        AssemblyGrandTreasuryServerTrustRegistry registry) Signature(
        AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review, RSA rsa)
    {
        var unsigned = new AssemblyGrandTreasuryReviewSignature(
            proposal.AssemblyId, proposal.OrganizationId, proposal.Id, review.Id,
            review.ActorSubject, "gt-c10-public-key", At.AddMinutes(-2), "");
        var signed = unsigned with { SignatureBase64 = Convert.ToBase64String(
            rsa.SignData(AssemblyGrandTreasuryReviewSignaturePolicy.BuildCanonicalPayload(
                proposal, review, unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) };
        const string path = AssemblyGrandTreasuryServerTrustRegistry.ConfigurationSection + ":0:";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [path + "KeyId"] = "gt-c10-public-key",
                [path + "ReviewerSubject"] = "gt-revisor-c10",
                [path + "PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem(),
                [path + "ValidFromUtc"] = At.AddDays(-1).ToString("O"),
                [path + "ValidUntilUtc"] = At.AddDays(1).ToString("O"),
                [path + "Approved"] = "true"
            }).Build();
        return (signed, new AssemblyGrandTreasuryServerTrustRegistry(configuration));
    }

    [Fact]
    public void Signed_independent_review_is_prepared_but_not_certified_or_voting()
    {
        using var rsa = RSA.Create(2048);
        var (p, r) = Events();
        var (signature, registry) = Signature(p, r, rsa);
        var record = AssemblyTreasurySignedReviewAdmission.Prepare(
            Reviewer(), Access(), Day, p, r, signature, Document, registry, At);
        Assert.NotNull(record);
        Assert.Equal(r.Id, record.ReviewEventId);
        Assert.Equal(p.Id, record.ProposalEventId);
        Assert.Equal("gt-revisor-c10", record.ReviewerSubject);
        Assert.Equal(p.EvidenceDigestSha256, record.DocumentSha256Hex);
    }

    [Fact]
    public void No_grant_wrong_role_changed_document_and_bad_signature_are_rejected()
    {
        using var rsa = RSA.Create(2048);
        var (p, r) = Events();
        var (signature, registry) = Signature(p, r, rsa);
        AssemblyTreasurySignedReviewRecord? Prepare(
            ClaimsPrincipal actor, DynamicAccessCatalog catalog,
            AssemblyGrandTreasuryReviewSignature envelope, byte[] bytes) =>
            AssemblyTreasurySignedReviewAdmission.Prepare(
                actor, catalog, Day, p, r, envelope, bytes, registry, At);
        Assert.Null(Prepare(Reviewer(), DynamicAccessEndpoints.CreateCatalog(), signature, Document));
        Assert.Null(Prepare(Reviewer(), Access(false), signature, Document));
        Assert.Null(Prepare(Reviewer(InstitutionalRoles.PlatformSuperAdmin), Access(), signature, Document));
        Assert.Null(Prepare(Reviewer(), Access(), signature, Encoding.UTF8.GetBytes("alterado")));
        Assert.Null(Prepare(Reviewer(), Access(),
            signature with { SignatureBase64 = "no-firma" }, Document));
    }
}
