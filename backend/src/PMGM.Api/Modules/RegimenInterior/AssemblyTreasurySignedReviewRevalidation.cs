using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Reconsulta C10 y vuelve a validar firma y clave pública al tiempo de lectura.
/// Un sello almacenado no es un certificado GT de deuda histórica o derecho a voto.
/// No publica endpoint ni datos del emisor: el consumidor aporta bytes de evidencia
/// recuperados por un adaptador confiable de almacenamiento (todavía pendiente).
/// </summary>
public sealed class AssemblyTreasurySignedReviewRevalidation(
    RegimenInteriorDbContext db,
    AssemblyGrandTreasuryServerTrustRegistry trustRegistry)
{
    public async Task<AssemblySignedReviewRevalidationResult> VerifyAsync(
        Guid assemblyId, Guid workshopId, Guid attestationId,
        ReadOnlyMemory<byte> documentBytes, DateTimeOffset verifiedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (assemblyId == Guid.Empty || workshopId == Guid.Empty ||
            attestationId == Guid.Empty || documentBytes.IsEmpty)
            return AssemblySignedReviewRevalidationResult.Blocked("INVALID_REQUEST_SCOPE_OR_DOCUMENT");

        var record = await db.AssemblyTreasurySignedReviews.AsNoTracking()
            .Include(x => x.ProposalEvent)
            .Include(x => x.ReviewEvent)
            .SingleOrDefaultAsync(x => x.Id == attestationId &&
                x.AssemblyId == assemblyId && x.OrganizationId == workshopId,
                cancellationToken);
        if (record is null)
            return AssemblySignedReviewRevalidationResult.Blocked("ATTESTATION_NOT_FOUND");

        return Check(record, record.ProposalEvent, record.ReviewEvent,
            assemblyId, workshopId, documentBytes.Span, trustRegistry, verifiedAtUtc);
    }

    public static AssemblySignedReviewRevalidationResult Check(
        AssemblyTreasurySignedReviewRecord? record,
        AssemblyTreasuryCertificationEventRecord? proposal,
        AssemblyTreasuryCertificationEventRecord? review,
        Guid assemblyId, Guid workshopId,
        ReadOnlySpan<byte> documentBytes,
        AssemblyGrandTreasuryServerTrustRegistry trustRegistry,
        DateTimeOffset verifiedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(trustRegistry);
        if (record is null || proposal is null || review is null)
            return AssemblySignedReviewRevalidationResult.Blocked("MISSING_LINKED_ACTS");

        if (documentBytes.IsEmpty || assemblyId == Guid.Empty || workshopId == Guid.Empty ||
            record.Id == Guid.Empty ||
            record.AssemblyId != assemblyId || record.OrganizationId != workshopId ||
            proposal.AssemblyId != assemblyId || proposal.OrganizationId != workshopId ||
            review.AssemblyId != assemblyId || review.OrganizationId != workshopId ||
            record.ProposalEventId != proposal.Id || record.ReviewEventId != review.Id ||
            review.ProposalEventId != proposal.Id ||
            proposal.Action != AssemblyTreasuryCertificationEventPolicy.Proposed ||
            review.Action != AssemblyTreasuryCertificationEventPolicy.Reviewed ||
            string.IsNullOrWhiteSpace(proposal.ActorSubject) ||
            string.IsNullOrWhiteSpace(review.ActorSubject) ||
            proposal.ActorSubject == review.ActorSubject ||
            !string.Equals(record.ReviewerSubject, review.ActorSubject, StringComparison.Ordinal) ||
            !string.Equals(record.DocumentSha256Hex, proposal.EvidenceDigestSha256,
                StringComparison.Ordinal) ||
            record.SignedAtUtc < review.OccurredAtUtc ||
            record.SignedAtUtc > record.RecordedAtUtc ||
            record.RecordedAtUtc > verifiedAtUtc ||
            string.IsNullOrWhiteSpace(record.TrustedKeyId) ||
            string.IsNullOrWhiteSpace(record.SignatureBase64))
            return AssemblySignedReviewRevalidationResult.Blocked("ATTESTATION_LINK_OR_SEQUENCE_INVALID");

        // Construcción exclusivamente desde los campos INMUTABLES leídos de BD.
        // Una clave inicialmente válida puede haber sido revocada; consultar
        // de nuevo siempre el registro de confianza del servidor.
        var envelope = new AssemblyGrandTreasuryReviewSignature(
            record.AssemblyId, record.OrganizationId, record.ProposalEventId,
            record.ReviewEventId, record.ReviewerSubject, record.TrustedKeyId,
            record.SignedAtUtc, record.SignatureBase64);
        var verification = trustRegistry.Verify(proposal, review, envelope,
            documentBytes, verifiedAtUtc);

        if (!verification.CryptographicallyVerified)
            return new AssemblySignedReviewRevalidationResult(false, false,
                verification.BlockingReasons.Count == 0
                    ? ["SIGNATURE_REVALIDATION_FAILED"]
                    : verification.BlockingReasons);

        return new AssemblySignedReviewRevalidationResult(
            true, false, Array.Empty<string>());
    }
}

/// <summary>
/// Resultado mínimo sin identidades, montos, datos bancarios ni sentido de voto.
/// </summary>
public sealed record AssemblySignedReviewRevalidationResult(
    bool ValidForDocumentaryReview,
    bool MayAuthorizeSuffrage,
    IReadOnlyList<string> BlockingReasons)
{
    public static AssemblySignedReviewRevalidationResult Blocked(string reason) =>
        new(false, false, [reason]);
}
