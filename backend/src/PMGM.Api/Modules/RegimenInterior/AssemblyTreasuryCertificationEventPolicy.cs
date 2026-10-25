using System.Security.Claims;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Construcción exclusivamente en servidor de eventos de preparación/revisión.
/// No persiste automáticamente: un futuro endpoint deberá comprobar el RBAC
/// dinámico write de Gran Tesorería, autenticidad documental y auditoría.
/// </summary>
public static class AssemblyTreasuryCertificationEventPolicy
{
    public const string Proposed = "proposed";
    public const string Reviewed = "reviewed";
    public const string Rejected = "rejected";

    public static AssemblyTreasuryCertificationEventRecord? BuildProposal(
        ClaimsPrincipal actor, Guid assemblyId, AssemblyTreasuryInspectionResult inspection,
        AssemblyGrandTreasuryEvidenceProposal proposal)
    {
        if (assemblyId == Guid.Empty ||
            !AssemblyGrandTreasuryCertificationPreflight.Assess(actor, inspection, proposal)
                .DocumentsReadyForIndependentReview)
            return null;
        var actorSubject = actor.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(actorSubject)) return null;

        return new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = assemblyId,
            OrganizationId = proposal.OrganizationId,
            Action = Proposed,
            ActorSubject = actorSubject,
            CoverageFrom = proposal.CoverageFrom,
            CutoffDate = proposal.Cutoff,
            Currency = proposal.Currency,
            OpeningPendingAmount = proposal.OpeningPendingAmount,
            SourceEvidenceReference = proposal.CertificationActReference,
            EvidenceDigestSha256 = proposal.EvidenceDigestSha256.ToLowerInvariant()
        };
    }

    /// <summary>
    /// Segundo funcionario explícito GT, diferente del autor original.
    /// Reviewed NO equivale a certificado GT autorizado.
    /// </summary>
    public static AssemblyTreasuryCertificationEventRecord? BuildIndependentReview(
        ClaimsPrincipal reviewer, Guid assemblyId, Guid organizationId,
        AssemblyTreasuryCertificationEventRecord proposal,
        string action, string evidenceReference, string reviewNote)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (!AssemblyGrandTreasuryCertificationAuthority.HasExplicitGrandTreasuryAuthority(reviewer) ||
            reviewer.FindFirst("sub")?.Value is not { Length: > 0 } reviewerSubject ||
            reviewerSubject == proposal.ActorSubject ||
            assemblyId == Guid.Empty || organizationId == Guid.Empty ||
            proposal.Id == Guid.Empty || proposal.Action != Proposed ||
            proposal.AssemblyId != assemblyId || proposal.OrganizationId != organizationId ||
            action is not (Reviewed or Rejected) ||
            string.IsNullOrWhiteSpace(evidenceReference) ||
            string.IsNullOrWhiteSpace(reviewNote) ||
            evidenceReference.Length > 500 || reviewNote.Length > 2000)
            return null;

        return new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = assemblyId,
            OrganizationId = organizationId,
            Action = action,
            ActorSubject = reviewerSubject,
            ProposalEventId = proposal.Id,
            ReviewEvidenceReference = evidenceReference,
            ReviewNote = reviewNote
        };
    }
}
