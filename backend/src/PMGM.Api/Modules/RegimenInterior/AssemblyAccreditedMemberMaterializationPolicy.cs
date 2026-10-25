using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// PREPARADOR PURO para el futuro adaptador de fuentes institucionales de Asamblea.
/// No obtiene credenciales, no valida firmas por sí mismo, no persiste ni habilita voto.
/// La cobertura y cada evidencia deben originarse en un servicio backend confiable,
/// nunca en un cuerpo HTTP ni en flags editables por Régimen Interior.
/// </summary>
public static class AssemblyAccreditedMemberMaterializationPolicy
{
    public const int MaxSourceCandidates = 10000;

    public static AssemblyAccreditedMemberMaterializationResult Prepare(
        AssemblyRecord assembly,
        IReadOnlyCollection<AssemblyVerifiedMemberIdentity> sourceMembers,
        bool institutionalSourceCoverageComplete,
        DateTimeOffset inspectedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(sourceMembers);

        var failures = new HashSet<string>(StringComparer.Ordinal);
        if (assembly.Id == Guid.Empty || assembly.Status != "draft" ||
            assembly.ClosedAtUtc is not null ||
            assembly.CutoffDate == DateOnly.MinValue ||
            assembly.MeetingDate == DateOnly.MinValue ||
            assembly.CutoffDate > assembly.MeetingDate ||
            inspectedAtUtc == default)
            failures.Add("ASSEMBLY_NOT_EDITABLE");

        if (!institutionalSourceCoverageComplete)
            failures.Add("INSTITUTIONAL_MEMBERSHIP_COVERAGE_MISSING");

        if (sourceMembers.Count is 0 or > MaxSourceCandidates)
            failures.Add("INVALID_SOURCE_ROSTER_SIZE");

        if (sourceMembers.Any(x => x is null || x.PersonId == Guid.Empty ||
            x.MemberId == Guid.Empty || x.OrganizationId == Guid.Empty) ||
            sourceMembers.GroupBy(x => x.PersonId).Any(g => g.Count() > 1) ||
            sourceMembers.GroupBy(x => x.MemberId).Any(g => g.Count() > 1))
            failures.Add("DUPLICATE_OR_INVALID_PERSON_SOURCE");

        var rows = new List<AssemblyMemberStatusRecord>();
        var officeIds = new HashSet<Guid>();
        if (failures.Count == 0)
        {
            foreach (var candidate in sourceMembers)
            {
                var attestation = candidate.MasterTerm;
                if (!candidate.IdentityVerified || !candidate.MembershipVerified ||
                    candidate.CutoffDate != assembly.CutoffDate ||
                    candidate.IdentityEvidenceCutoff != assembly.CutoffDate ||
                    candidate.MembershipEvidenceCutoff != assembly.CutoffDate ||
                    !NonEmpty(candidate.IdentityEvidenceReference, 500) ||
                    !NonEmpty(candidate.MembershipEvidenceReference, 500))
                {
                    failures.Add("MEMBERSHIP_OR_IDENTITY_SOURCE_UNVERIFIED");
                    continue;
                }

                if (attestation is null || !attestation.OfficialCoverageValidated ||
                    attestation.BlockingReasons.Count != 0 ||
                    attestation.Category is null)
                {
                    failures.Add("MASTER_OFFICE_COVERAGE_UNVERIFIED");
                    continue;
                }

                // Inactivos y personas que nunca ejercieron una VM acreditada
                // no son asambleístas; la exclusión exige fuente completa.
                if (!candidate.MemberActive || !attestation.Category.Qualifies)
                {
                    if (attestation.Category.Qualifies == false &&
                        (attestation.Category.Category is not null ||
                         attestation.AccreditedSource is not null))
                        failures.Add("INCONSISTENT_NON_MEMBER_CATEGORY");
                    continue;
                }

                var category = attestation.Category.Category;
                var accredited = attestation.AccreditedSource;
                if (category is not (AssemblyMemberCategoryPolicy.Representative or
                    AssemblyMemberCategoryPolicy.PermanentAssemblyMember) ||
                    accredited is null ||
                    accredited.OfficeAssignmentId == Guid.Empty ||
                    accredited.OrganizationId == Guid.Empty ||
                    accredited.StartDate > assembly.CutoffDate ||
                    accredited.EndDate is not null &&
                        (accredited.EndDate < accredited.StartDate ||
                         accredited.EndDate > assembly.CutoffDate &&
                         category == AssemblyMemberCategoryPolicy.PermanentAssemblyMember) ||
                    !NonEmpty(accredited.EvidenceReference, 500) ||
                    (category == AssemblyMemberCategoryPolicy.PermanentAssemblyMember &&
                     attestation.Category.CompletedTerms < 1) ||
                    (category == AssemblyMemberCategoryPolicy.Representative &&
                     (!attestation.Category.CurrentlyServing ||
                      attestation.Category.CompletedTerms != 0)) ||
                    !officeIds.Add(accredited.OfficeAssignmentId))
                {
                    failures.Add("ACCREDITED_MASTER_OFFICE_INVALID");
                    continue;
                }

                if (!NonEmpty(candidate.FirstNames, 240) ||
                    !NonEmpty(candidate.LastNames, 240) ||
                    !NonEmpty(candidate.OfficialIdentityNumber, 32) ||
                    !NonEmpty(candidate.WorkshopName, 240))
                {
                    failures.Add("PERSON_ROSTER_FIELDS_NOT_VERIFIED");
                    continue;
                }

                rows.Add(new AssemblyMemberStatusRecord
                {
                    AssemblyId = assembly.Id,
                    PersonId = candidate.PersonId,
                    MemberId = candidate.MemberId,
                    // El Taller de la afiliación al corte no tiene por qué ser
                    // el Taller histórico de la Venerable Maestría acreditada.
                    OrganizationId = candidate.OrganizationId,
                    Category = category,
                    SourceOfficeAssignmentId = accredited.OfficeAssignmentId,
                    SourceEvidenceReference = accredited.EvidenceReference.Trim(),
                    FullTermVerified =
                        category == AssemblyMemberCategoryPolicy.PermanentAssemblyMember,
                    LastNamesAtCutoff = candidate.LastNames.Trim(),
                    FirstNamesAtCutoff = candidate.FirstNames.Trim(),
                    RutAtCutoff = candidate.OfficialIdentityNumber.Trim(),
                    WorkshopAtCutoff = candidate.WorkshopName.Trim(),
                    VerifiedAtUtc = inspectedAtUtc
                });
            }
        }

        if (failures.Count == 0 && rows.Count == 0)
            failures.Add("NO_ACCREDITED_ASSEMBLY_MEMBERS");

        // Lote atómico: NUNCA permitir un padrón parcial por error de fuente.
        return failures.Count == 0
            ? new(true, rows.OrderBy(x => x.PersonId).ToArray(), [])
            : new(false, [], failures.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static bool NonEmpty(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;
}

/// <summary>
/// Snapshot sólo de servicios internos que ya validaron identidad, afiliación,
/// fechas, evidencia y cobertura. No enlazar directamente a contratos HTTP.
/// </summary>
public sealed record AssemblyVerifiedMemberIdentity(
    Guid PersonId, Guid MemberId, Guid OrganizationId,
    DateOnly CutoffDate, DateOnly IdentityEvidenceCutoff,
    DateOnly MembershipEvidenceCutoff,
    bool IdentityVerified, bool MembershipVerified, bool MemberActive,
    string IdentityEvidenceReference, string MembershipEvidenceReference,
    string FirstNames, string LastNames, string OfficialIdentityNumber,
    string WorkshopName, AssemblyMasterTermEvidenceResult MasterTerm);

public sealed record AssemblyAccreditedMemberMaterializationResult(
    bool Prepared,
    IReadOnlyList<AssemblyMemberStatusRecord> Members,
    IReadOnlyList<string> BlockingReasons);
