using System.Security.Claims;
using System.Security.Cryptography;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Construye una constancia técnica SOLO tras C8+C7. No es un endpoint, no
/// autoriza escritura genérica en DB ni certifica deuda o sufragio.
/// </summary>
public static class AssemblyTreasurySignedReviewAdmission
{
    public static AssemblyTreasurySignedReviewRecord? Prepare(
        ClaimsPrincipal reviewer,
        DynamicAccessCatalog accessCatalog,
        DateOnly operationDay,
        AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review,
        AssemblyGrandTreasuryReviewSignature envelope,
        ReadOnlySpan<byte> documentBytes,
        AssemblyGrandTreasuryServerTrustRegistry trustedServerRegistry,
        DateTimeOffset verifiedAtUtc)
    {
        if (!AssemblyGrandTreasuryWriteAdmissionPolicy.AssessSignedReview(
                reviewer, accessCatalog, operationDay, proposal, review, envelope,
                documentBytes, trustedServerRegistry, verifiedAtUtc)
                .ReadyForAuditedProcessing)
            return null;

        var hex = Convert.ToHexString(SHA256.HashData(documentBytes)).ToLowerInvariant();
        if (envelope.AssemblyId == Guid.Empty ||
            envelope.OrganizationId == Guid.Empty ||
            envelope.ProposalEventId == Guid.Empty ||
            envelope.ReviewEventId == Guid.Empty ||
            envelope.SignatureBase64.Length > 2048 ||
            string.IsNullOrWhiteSpace(envelope.KeyId) || envelope.KeyId.Length > 100 ||
            envelope.SignerSubject.Length > 320 ||
            envelope.SignedAtUtc > verifiedAtUtc ||
            !string.Equals(proposal.EvidenceDigestSha256, hex, StringComparison.OrdinalIgnoreCase))
            return null;

        return new AssemblyTreasurySignedReviewRecord
        {
            AssemblyId = envelope.AssemblyId,
            OrganizationId = envelope.OrganizationId,
            ProposalEventId = envelope.ProposalEventId,
            ReviewEventId = envelope.ReviewEventId,
            ReviewerSubject = envelope.SignerSubject,
            TrustedKeyId = envelope.KeyId,
            DocumentSha256Hex = hex,
            SignatureBase64 = envelope.SignatureBase64,
            SignedAtUtc = envelope.SignedAtUtc,
            RecordedAtUtc = verifiedAtUtc
        };
    }
}
