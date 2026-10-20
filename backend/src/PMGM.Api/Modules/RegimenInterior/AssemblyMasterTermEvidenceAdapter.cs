using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Adaptador de prueba de procedencia para períodos V.M. presentes en
/// Membership: EndDate NO equivale a cumplimiento estatutario del período.
/// La cobertura y actas provienen de un servicio institucional confiable,
/// nunca de campos de una solicitud HTTP.
/// </summary>
public static class AssemblyMasterTermEvidenceAdapter
{
    public static AssemblyMasterTermEvidenceResult Classify(
        Guid memberId,
        DateOnly cutoff,
        DateTimeOffset verifiedAtUtc,
        string canonicalMasterOfficeCode,
        bool officialOfficeRegistryComplete,
        IReadOnlyCollection<OfficeAssignment> recordedAssignments,
        IReadOnlyCollection<AssemblyMasterTermAttestation> officialAttestations)
    {
        ArgumentNullException.ThrowIfNull(recordedAssignments);
        ArgumentNullException.ThrowIfNull(officialAttestations);
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        if (memberId == Guid.Empty || cutoff == DateOnly.MinValue ||
            string.IsNullOrWhiteSpace(canonicalMasterOfficeCode) ||
            !officialOfficeRegistryComplete)
            reasons.Add("CANONICAL_MASTER_OFFICE_SOURCE_MISSING");

        if (reasons.Count != 0)
            return AssemblyMasterTermEvidenceResult.Pending(reasons);

        var terms = recordedAssignments
            .Where(x => x.MemberId == memberId &&
                string.Equals(x.OfficeType, canonicalMasterOfficeCode, StringComparison.Ordinal) &&
                x.StartDate <= cutoff)
            .ToArray();

        if (terms.GroupBy(x => x.Id).Any(x => x.Key == Guid.Empty || x.Count() > 1) ||
            officialAttestations.GroupBy(x => x.OfficeAssignmentId).Any(x => x.Count() > 1))
            reasons.Add("DUPLICATED_MASTER_OFFICE_EVIDENCE");

        if (officialAttestations.Any(x => x.MemberId != memberId ||
            x.OfficeAssignmentId == Guid.Empty ||
            x.OrganizationId == Guid.Empty ||
            !terms.Any(t => t.Id == x.OfficeAssignmentId)))
            reasons.Add("FOREIGN_OR_UNRELATED_ATTESTATION");

        var joined = new List<AssemblyMasterOfficeEvidence>();
        foreach (var term in terms)
        {
            var evidence = officialAttestations.Where(x => x.OfficeAssignmentId == term.Id).ToArray();
            if (term.StartDate is null || term.Id == Guid.Empty ||
                term.OrganizationId == Guid.Empty ||
                string.IsNullOrWhiteSpace(term.EvidenceReference) ||
                term.EndDate is not null && term.EndDate < term.StartDate ||
                evidence.Length != 1)
            {
                reasons.Add("MASTER_TERM_NOT_ACCREDITED");
                continue;
            }

            var attestation = evidence[0];
            if (!attestation.InstitutionallyVerified ||
                attestation.MemberId != term.MemberId ||
                attestation.OrganizationId != term.OrganizationId ||
                attestation.StartDate != term.StartDate ||
                attestation.EndDate != term.EndDate ||
                !string.Equals(attestation.OfficeEvidenceReference, term.EvidenceReference,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(attestation.CertificationActReference) ||
                attestation.CertifiedAtUtc == default ||
                attestation.CertifiedAtUtc > verifiedAtUtc ||
                attestation.EvidenceCutoff != cutoff ||
                attestation.FullTermCompleted &&
                    (term.EndDate is null || term.EndDate > cutoff) ||
                attestation.CurrentServiceVerified &&
                    (term.EndDate is not null && term.EndDate < cutoff) ||
                (term.EndDate is null || term.EndDate >= cutoff) &&
                    !attestation.CurrentServiceVerified)
            {
                reasons.Add("MASTER_TERM_NOT_ACCREDITED");
                continue;
            }

            joined.Add(new AssemblyMasterOfficeEvidence(
                term.Id, term.StartDate.Value, term.EndDate, true,
                attestation.CertificationActReference,
                attestation.FullTermCompleted,
                attestation.CurrentServiceVerified));
        }

        if (reasons.Count != 0)
            return AssemblyMasterTermEvidenceResult.Pending(reasons);

        var status = AssemblyAccreditedCategoryPolicy.Classify(joined, cutoff);
        return new AssemblyMasterTermEvidenceResult(
            true, status, Array.Empty<string>());
    }
}

public sealed record AssemblyMasterTermAttestation(
    Guid OfficeAssignmentId, Guid MemberId, Guid OrganizationId,
    DateOnly StartDate, DateOnly? EndDate, DateOnly EvidenceCutoff,
    string OfficeEvidenceReference, string CertificationActReference,
    bool InstitutionallyVerified, bool FullTermCompleted,
    bool CurrentServiceVerified, DateTimeOffset CertifiedAtUtc);

public sealed record AssemblyMasterTermEvidenceResult(
    bool OfficialCoverageValidated,
    AssemblyMemberCategoryDecision? Category,
    IReadOnlyList<string> BlockingReasons)
{
    public static AssemblyMasterTermEvidenceResult Pending(IEnumerable<string> reasons) =>
        new(false, null, reasons.OrderBy(x => x, StringComparer.Ordinal).ToArray());
}
