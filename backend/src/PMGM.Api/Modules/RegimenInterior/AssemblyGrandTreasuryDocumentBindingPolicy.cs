using System.Security.Cryptography;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Verifica la correspondencia entre el documento y dos actos documentales GT.
/// Un hash coincidente NO es una firma ni una certificación institucional.
/// </summary>
public static class AssemblyGrandTreasuryDocumentBindingPolicy
{
    public static AssemblyDocumentBindingResult Check(
        Guid assemblyId, Guid workshopId,
        AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review,
        ReadOnlySpan<byte> documentBytes)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(review);
        var faults = new List<string>();

        if (assemblyId == Guid.Empty || workshopId == Guid.Empty ||
            proposal.Id == Guid.Empty || review.Id == Guid.Empty ||
            proposal.AssemblyId != assemblyId || review.AssemblyId != assemblyId ||
            proposal.OrganizationId != workshopId || review.OrganizationId != workshopId ||
            proposal.Action != AssemblyTreasuryCertificationEventPolicy.Proposed ||
            review.Action != AssemblyTreasuryCertificationEventPolicy.Reviewed ||
            review.ProposalEventId != proposal.Id)
            faults.Add("ACTS_MISMATCH");

        if (string.IsNullOrWhiteSpace(proposal.ActorSubject) ||
            string.IsNullOrWhiteSpace(review.ActorSubject) ||
            proposal.ActorSubject == review.ActorSubject ||
            review.OccurredAtUtc < proposal.OccurredAtUtc)
            faults.Add("INDEPENDENT_REVIEW_NOT_PROVEN");

        if (string.IsNullOrWhiteSpace(review.ReviewEvidenceReference) ||
            string.IsNullOrWhiteSpace(review.ReviewNote) ||
            proposal.CoverageFrom is null || proposal.CutoffDate is null ||
            proposal.CoverageFrom > proposal.CutoffDate ||
            string.IsNullOrWhiteSpace(proposal.SourceEvidenceReference))
            faults.Add("DOCUMENT_PROVENANCE_MISSING");

        var hex = proposal.EvidenceDigestSha256;
        if (hex is not { Length: 64 } ||
            !hex.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
            faults.Add("DOCUMENT_HASH_INVALID");
        else if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(documentBytes), Convert.FromHexString(hex)))
            faults.Add("DOCUMENT_BYTES_DIFFER");

        return new AssemblyDocumentBindingResult(
            faults.Count == 0,
            false,
            faults.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
}

public sealed record AssemblyDocumentBindingResult(
    bool ConsistentForIndependentVerification,
    bool MayAuthorizeSuffrage,
    IReadOnlyList<string> BlockingReasons);
