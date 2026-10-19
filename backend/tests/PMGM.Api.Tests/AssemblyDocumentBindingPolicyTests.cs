using System.Security.Cryptography;
using System.Text;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyDocumentBindingPolicyTests
{
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes("Documento sintético");
    private static (AssemblyTreasuryCertificationEventRecord p, AssemblyTreasuryCertificationEventRecord r) Acts()
    {
        var asm = Guid.NewGuid();
        var lodge = Guid.NewGuid();
        var p = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = asm, OrganizationId = lodge, Action = "proposed",
            ActorSubject = "tesorero-a", CoverageFrom = new DateOnly(2026, 1, 1),
            CutoffDate = new DateOnly(2026, 10, 19), SourceEvidenceReference = "doc-sintetico",
            EvidenceDigestSha256 = Convert.ToHexString(SHA256.HashData(Bytes)).ToLowerInvariant()
        };
        var r = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = asm, OrganizationId = lodge, Action = "reviewed",
            ActorSubject = "tesorero-b", ProposalEventId = p.Id,
            ReviewEvidenceReference = "revision-sintetica", ReviewNote = "Acreditar soporte",
            OccurredAtUtc = p.OccurredAtUtc.AddMinutes(1)
        };
        return (p, r);
    }

    [Fact]
    public void Correct_document_remains_non_electoral()
    {
        var (p, r) = Acts();
        var result = AssemblyGrandTreasuryDocumentBindingPolicy.Check(
            p.AssemblyId, p.OrganizationId, p, r, Bytes);
        Assert.True(result.ConsistentForIndependentVerification);
        Assert.False(result.MayAuthorizeSuffrage);
    }

    [Fact]
    public void Tampered_document_and_same_reviewer_are_rejected()
    {
        var (p, r) = Acts();
        var changed = AssemblyGrandTreasuryDocumentBindingPolicy.Check(
            p.AssemblyId, p.OrganizationId, p, r, Encoding.UTF8.GetBytes("Cambiado"));
        Assert.Contains("DOCUMENT_BYTES_DIFFER", changed.BlockingReasons);
        r.ActorSubject = p.ActorSubject;
        var self = AssemblyGrandTreasuryDocumentBindingPolicy.Check(
            p.AssemblyId, p.OrganizationId, p, r, Bytes);
        Assert.Contains("INDEPENDENT_REVIEW_NOT_PROVEN", self.BlockingReasons);
        Assert.False(self.MayAuthorizeSuffrage);
    }

    [Fact]
    public void Cross_workshop_and_invalid_hash_are_rejected()
    {
        var (p, r) = Acts();
        Assert.Contains("ACTS_MISMATCH",
            AssemblyGrandTreasuryDocumentBindingPolicy.Check(
                p.AssemblyId, Guid.NewGuid(), p, r, Bytes).BlockingReasons);
        p.EvidenceDigestSha256 = "no-es-sha256";
        Assert.Contains("DOCUMENT_HASH_INVALID",
            AssemblyGrandTreasuryDocumentBindingPolicy.Check(
                p.AssemblyId, p.OrganizationId, p, r, Bytes).BlockingReasons);
    }
}
