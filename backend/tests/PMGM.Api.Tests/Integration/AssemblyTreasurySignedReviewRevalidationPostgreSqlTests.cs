using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PMGM.Api.Data;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyTreasurySignedReviewRevalidationPostgreSqlTests
{
    [Fact]
    public async Task Read_only_revalidation_is_workshop_scoped_and_uses_server_key()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<RegimenInteriorDbContext>()
            .UseNpgsql(cs).Options;
        await using var db = new RegimenInteriorDbContext(options);
        await db.Database.MigrateAsync(ct);

        var moment = DateTimeOffset.UtcNow.AddMinutes(-1);
        var content = Encoding.UTF8.GetBytes("Solo datos sintéticos C11, sin RUT");
        using var rsa = RSA.Create(2048);
        var workshopId = Guid.NewGuid();
        var assembly = new AssemblyRecord
        {
            Name = "Asamblea revalidación C11 sintética",
            MeetingDate = new DateOnly(2026, 12, 30),
            CutoffDate = new DateOnly(2026, 10, 19),
            CreatedBySubject = "test-c11"
        };
        var proposal = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = assembly.Id, OrganizationId = workshopId,
            Action = "proposed", ActorSubject = "gt-primero",
            OccurredAtUtc = moment.AddMinutes(-10),
            CoverageFrom = new DateOnly(2026, 1, 1),
            CutoffDate = assembly.CutoffDate, Currency = "CLP",
            OpeningPendingAmount = 0m,
            SourceEvidenceReference = "GT-C11-PROPUESTA-SINTETICA",
            EvidenceDigestSha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()
        };
        db.AddRange(assembly, proposal);
        await db.SaveChangesAsync(ct);

        var review = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = assembly.Id, OrganizationId = workshopId,
            Action = "reviewed", ActorSubject = "gt-segundo",
            OccurredAtUtc = moment.AddMinutes(-5),
            ProposalEventId = proposal.Id,
            ReviewEvidenceReference = "GT-C11-REVISION-SINTETICA",
            ReviewNote = "Segundo funcionario acredita lectura documental"
        };
        db.Add(review);
        await db.SaveChangesAsync(ct);

        var envelope = new AssemblyGrandTreasuryReviewSignature(
            assembly.Id, workshopId, proposal.Id, review.Id,
            "gt-segundo", "c11-public", moment.AddMinutes(-2), "");
        var signatureBytes = rsa.SignData(
            AssemblyGrandTreasuryReviewSignaturePolicy.BuildCanonicalPayload(
                proposal, review, envelope), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var record = new AssemblyTreasurySignedReviewRecord
        {
            AssemblyId = assembly.Id, OrganizationId = workshopId,
            ProposalEventId = proposal.Id, ReviewEventId = review.Id,
            ReviewerSubject = "gt-segundo", TrustedKeyId = "c11-public",
            DocumentSha256Hex = proposal.EvidenceDigestSha256!,
            SignatureBase64 = Convert.ToBase64String(signatureBytes),
            SignedAtUtc = envelope.SignedAtUtc, RecordedAtUtc = moment
        };
        db.AssemblyTreasurySignedReviews.Add(record);
        await db.SaveChangesAsync(ct);

        const string prefix = AssemblyGrandTreasuryServerTrustRegistry.ConfigurationSection + ":0:";
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [prefix + "KeyId"] = "c11-public",
                [prefix + "ReviewerSubject"] = "gt-segundo",
                [prefix + "PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem(),
                [prefix + "Approved"] = "true",
                [prefix + "ValidFromUtc"] = moment.AddDays(-1).ToString("O"),
                [prefix + "ValidUntilUtc"] = moment.AddDays(1).ToString("O")
            }).Build();
        var registry = new AssemblyGrandTreasuryServerTrustRegistry(config);
        var service = new AssemblyTreasurySignedReviewRevalidation(db, registry);

        var valid = await service.VerifyAsync(
            assembly.Id, workshopId, record.Id, content, DateTimeOffset.UtcNow, ct);
        Assert.True(valid.ValidForDocumentaryReview);
        Assert.False(valid.MayAuthorizeSuffrage);

        var wrongWorkshop = await service.VerifyAsync(
            assembly.Id, Guid.NewGuid(), record.Id, content, DateTimeOffset.UtcNow, ct);
        Assert.Equal("ATTESTATION_NOT_FOUND", Assert.Single(wrongWorkshop.BlockingReasons));
        var wrongAssembly = await service.VerifyAsync(
            Guid.NewGuid(), workshopId, record.Id, content, DateTimeOffset.UtcNow, ct);
        Assert.Equal("ATTESTATION_NOT_FOUND", Assert.Single(wrongAssembly.BlockingReasons));

        var alteredDocument = await service.VerifyAsync(
            assembly.Id, workshopId, record.Id,
            Encoding.UTF8.GetBytes("Otros bytes"), DateTimeOffset.UtcNow, ct);
        Assert.False(alteredDocument.ValidForDocumentaryReview);
        Assert.False(alteredDocument.MayAuthorizeSuffrage);

        // Un administrador puede revocar la clave institucional de confianza
        // sin mutar la constancia append-only previamente almacenada.
        config[prefix + "RevokedAtUtc"] = DateTimeOffset.UtcNow.ToString("O");
        var revoked = await service.VerifyAsync(
            assembly.Id, workshopId, record.Id, content,
            DateTimeOffset.UtcNow.AddSeconds(1), ct);
        Assert.False(revoked.ValidForDocumentaryReview);
        Assert.Contains("SIGNING_KEY_NOT_TRUSTED", revoked.BlockingReasons);
    }
}
