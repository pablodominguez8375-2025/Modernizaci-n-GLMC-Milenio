using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Barrera de entrada PREVIA a cualquier escritura de actos GT del padrón.
/// NO emplea DynamicGrandTreasuryAccess.Allows, cuyo fallback histórico para
/// usuarios no enrolados es insuficiente para estos actos electorales.
/// Sin endpoints, no escribe registros y no emite un certificado financiero.
/// </summary>
public static class AssemblyGrandTreasuryWriteAdmissionPolicy
{
    public static AssemblyGrandTreasuryWriteAdmissionResult AssessProposal(
        ClaimsPrincipal actor,
        DynamicAccessCatalog catalog,
        DateOnly operationDay,
        AssemblyTreasuryInspectionResult inspection,
        AssemblyGrandTreasuryEvidenceProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(catalog);
        var errors = new HashSet<string>(StringComparer.Ordinal);

        CheckWritePermission(actor, catalog, operationDay, errors);
        if (!AssemblyGrandTreasuryCertificationPreflight.Assess(actor, inspection, proposal)
                .DocumentsReadyForIndependentReview)
            errors.Add("PROPOSAL_DOCUMENTS_NOT_READY");

        return Build(errors);
    }

    public static AssemblyGrandTreasuryWriteAdmissionResult AssessSignedReview(
        ClaimsPrincipal reviewer,
        DynamicAccessCatalog catalog,
        DateOnly operationDay,
        AssemblyTreasuryCertificationEventRecord proposalEvent,
        AssemblyTreasuryCertificationEventRecord reviewEvent,
        AssemblyGrandTreasuryReviewSignature signature,
        ReadOnlySpan<byte> evidenceBytes,
        AssemblyGrandTreasuryServerTrustRegistry serverRegistry,
        DateTimeOffset verifiedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(reviewer);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(proposalEvent);
        ArgumentNullException.ThrowIfNull(reviewEvent);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(serverRegistry);

        var errors = new HashSet<string>(StringComparer.Ordinal);
        CheckWritePermission(reviewer, catalog, operationDay, errors);

        // El revisor conectado debe ser quien figura en el evento y la firma.
        // No basta con que los eventos C3 mencionen a otro usuario GT válido.
        var subject = reviewer.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject) ||
            !string.Equals(subject, reviewEvent.ActorSubject, StringComparison.Ordinal) ||
            !string.Equals(subject, signature.SignerSubject, StringComparison.Ordinal) ||
            string.Equals(subject, proposalEvent.ActorSubject, StringComparison.Ordinal))
            errors.Add("REVIEWER_ACTOR_MISMATCH");

        if (!serverRegistry.Verify(
                proposalEvent, reviewEvent, signature, evidenceBytes, verifiedAtUtc)
                .CryptographicallyVerified)
            errors.Add("SIGNED_REVIEW_NOT_VERIFIED");

        return Build(errors);
    }

    private static void CheckWritePermission(
        ClaimsPrincipal actor, DynamicAccessCatalog catalog, DateOnly operationDay,
        HashSet<string> errors)
    {
        if (!AssemblyGrandTreasuryCertificationAuthority.HasExplicitGrandTreasuryAuthority(actor))
            errors.Add("EXPLICIT_GRAND_TREASURY_ROLE_REQUIRED");

        var subject = actor.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject) ||
            !DynamicGrandTreasuryAccess.IsManaged(catalog, subject) ||
            !DynamicAccessEndpoints.IsGranted(
                catalog, subject, "treasury", "write", null, operationDay))
            errors.Add("EXPLICIT_DYNAMIC_WRITE_GRANT_REQUIRED");
    }

    private static AssemblyGrandTreasuryWriteAdmissionResult Build(
        HashSet<string> errors) =>
        new(errors.Count == 0,
            false, // No autoriza sufragio aun cuando pase todas las barreras.
            errors.OrderBy(x => x, StringComparer.Ordinal).ToArray());
}

public sealed record AssemblyGrandTreasuryWriteAdmissionResult(
    bool ReadyForAuditedProcessing,
    bool MayAuthorizeSuffrage,
    IReadOnlyList<string> BlockingReasons);
