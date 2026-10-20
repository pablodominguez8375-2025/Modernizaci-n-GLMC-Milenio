using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Preflight de cierre de padrón. Su entrada DEBE provenir exclusivamente de
/// servicios backend que certifiquen cobertura de las fuentes institucionales;
/// no admite afirmaciones suministradas desde una solicitud HTTP.
/// No firma ni cierra un padrón y nunca habilita votación por sí misma.
/// </summary>
public static class AssemblyClosurePreflightPolicy
{
    public const string SnapshotFormat = "PMGM-GRAN-ASAMBLEA-PADRON-V1";

    public static AssemblyClosurePreflightResult Assess(
        AssemblyRecord assembly,
        IReadOnlyCollection<AssemblyClosureCandidateEvidence> candidates,
        AssemblyClosureOfficialCoverage coverage,
        DateOnly operationDay)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(coverage);
        var failures = new HashSet<string>(StringComparer.Ordinal);

        if (assembly.Id == Guid.Empty ||
            assembly.CutoffDate == DateOnly.MinValue ||
            assembly.CutoffDate > operationDay ||
            assembly.CutoffDate > assembly.MeetingDate ||
            assembly.Status != "draft" ||
            assembly.ClosedAtUtc is not null ||
            assembly.ClosedBySubject is not null ||
            assembly.ClosureEvidenceDigest is not null)
            failures.Add("ASSEMBLY_NOT_READY_FOR_CLOSURE");

        if (coverage.AssemblyId != assembly.Id ||
            coverage.CutoffDate != assembly.CutoffDate ||
            !coverage.AllExpectedAssemblyMembersEnumerated ||
            !coverage.FullEligibilityHistoryResolved ||
            !coverage.InstitutionalSourcesVerified ||
            string.IsNullOrWhiteSpace(coverage.OfficialRosterReference))
            failures.Add("OFFICIAL_ASSEMBLY_COVERAGE_MISSING");

        if (candidates.Count == 0)
            failures.Add("EMPTY_ASSEMBLY_ROSTER");

        if (candidates.GroupBy(x => x.Member.PersonId).Any(g => g.Key == Guid.Empty || g.Count() > 1) ||
            candidates.GroupBy(x => x.Member.Id).Any(g => g.Key == Guid.Empty || g.Count() > 1))
            failures.Add("DUPLICATE_OR_INVALID_ASSEMBLY_MEMBER");

        var voters = new List<AssemblyClosureVoter>();
        foreach (var item in candidates)
        {
            if (!MemberAndSourcesValid(assembly, item))
            {
                failures.Add("MEMBER_OR_SOURCE_EVIDENCE_INCOMPLETE");
                continue;
            }

            var decision = item.LatestEligibility;
            if (decision.Status is not (
                    AssemblyVoterEligibilityPolicy.Eligible or
                    AssemblyVoterEligibilityPolicy.Ineligible or
                    AssemblyVoterEligibilityPolicy.AttendanceOnly) ||
                string.IsNullOrWhiteSpace(decision.ReasonCode) ||
                string.IsNullOrWhiteSpace(decision.EvaluatedBySubject) ||
                decision.EvaluatedAtUtc > item.EvaluatedAsOfUtc ||
                decision.EvaluatedAtUtc < item.Member.VerifiedAtUtc)
            {
                failures.Add("PENDING_OR_UNTRUSTED_ELIGIBILITY");
                continue;
            }

            if (decision.MayVote != (decision.Status == AssemblyVoterEligibilityPolicy.Eligible) ||
                decision.MayVote && !decision.MayAttend ||
                decision.Status == AssemblyVoterEligibilityPolicy.Ineligible && decision.MayAttend ||
                decision.Status == AssemblyVoterEligibilityPolicy.AttendanceOnly && !decision.MayAttend ||
                decision.GrandTreasuryPendingAmount is null ||
                decision.GrandTreasuryPendingAmount < 0m ||
                decision.GrandTreasuryAsOfDate != assembly.CutoffDate ||
                !string.Equals(decision.GrandTreasuryCurrency, item.Sources.TreasuryCurrency,
                    StringComparison.Ordinal) ||
                !string.Equals(decision.GrandTreasurySourceReference, item.Sources.TreasuryReference,
                    StringComparison.Ordinal) ||
                !string.Equals(decision.InstitutionalRightsSourceReference, item.Sources.RegimenReference,
                    StringComparison.Ordinal) ||
                !string.Equals(decision.HonorRightsSourceReference, item.Sources.HonorReference,
                    StringComparison.Ordinal))
            {
                failures.Add("ELIGIBILITY_FINANCIAL_OR_RIGHTS_MISMATCH");
                continue;
            }

            if (decision.MayVote)
            {
                // Las excepciones por deuda deben estar expresamente resueltas
                // por Régimen Interior y comprobadas por una fuente autorizada.
                if (decision.GrandTreasuryPendingAmount > 0m &&
                    !item.Sources.ValidDebtExceptionCertified)
                {
                    failures.Add("UNRESOLVED_POSITIVE_TREASURY_DEBT");
                    continue;
                }

                voters.Add(new AssemblyClosureVoter(item.Member.PersonId,
                    item.Member.OrganizationId, item.Member.Category,
                    item.Member.SourceOfficeAssignmentId));
            }
        }

        if (failures.Count != 0)
            return new AssemblyClosurePreflightResult(false, false, null, 0,
                failures.OrderBy(x => x, StringComparer.Ordinal).ToArray());

        var ordered = voters
            .OrderBy(x => x.PersonId)
            .ThenBy(x => x.OrganizationId)
            .ThenBy(x => x.Category, StringComparer.Ordinal)
            .ToArray();

        // Digest del estado de elegibilidad de TODOS los candidatos (no solo
        // los habilitados); sin RUT, nombres, expedientes ni sentido de voto.
        // Hash es material de verificación, no firma legal ni cierre.
        var payload = new StringBuilder();
        AddField(payload, SnapshotFormat);
        AddField(payload, assembly.Id.ToString("D"));
        AddField(payload, assembly.CutoffDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddField(payload, coverage.OfficialRosterReference);

        foreach (var item in candidates.OrderBy(x => x.Member.PersonId))
        {
            AddField(payload, item.Member.PersonId.ToString("D"));
            AddField(payload, item.Member.OrganizationId.ToString("D"));
            AddField(payload, item.Member.Category);
            AddField(payload, item.Member.SourceOfficeAssignmentId.ToString("D"));
            AddField(payload, item.Member.SourceEvidenceReference);
            AddField(payload, item.LatestEligibility.Status);
            AddField(payload, item.LatestEligibility.ReasonCode);
            AddField(payload, item.LatestEligibility.MayVote ? "1" : "0");
            AddField(payload, item.LatestEligibility.GrandTreasuryPendingAmount!.Value.ToString("0.00", CultureInfo.InvariantCulture));
            AddField(payload, item.Sources.TreasuryReference);
            AddField(payload, item.Sources.RegimenReference);
            AddField(payload, item.Sources.HonorReference);
        }

        return new AssemblyClosurePreflightResult(
            true,
            false,
            Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(payload.ToString()))).ToLowerInvariant(),
            ordered.Length,
            Array.Empty<string>());
    }

    private static bool MemberAndSourcesValid(
        AssemblyRecord assembly, AssemblyClosureCandidateEvidence item)
    {
        var member = item.Member;
        var eligibility = item.LatestEligibility;
        var sources = item.Sources;
        return member.Id != Guid.Empty && member.AssemblyId == assembly.Id &&
            member.PersonId != Guid.Empty && member.MemberId != Guid.Empty &&
            member.OrganizationId != Guid.Empty &&
            member.SourceOfficeAssignmentId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(member.SourceEvidenceReference) &&
            member.Category is (
                AssemblyMemberCategoryPolicy.Representative or
                AssemblyMemberCategoryPolicy.PermanentAssemblyMember) &&
            (member.Category != AssemblyMemberCategoryPolicy.PermanentAssemblyMember ||
             member.FullTermVerified) &&
            member.VerifiedAtUtc <= item.EvaluatedAsOfUtc &&
            eligibility.AssemblyId == assembly.Id &&
            eligibility.MemberStatusId == member.Id &&
            sources.PersonId == member.PersonId &&
            sources.OrganizationId == member.OrganizationId &&
            sources.CutoffDate == assembly.CutoffDate &&
            sources.MembershipVerified &&
            sources.MasterPeriodsVerified &&
            sources.GrandTreasuryHistoryCertified &&
            sources.HonorCoverageComplete &&
            sources.RegimenCoverageComplete &&
            sources.LatestDecisionResolved &&
            !string.IsNullOrWhiteSpace(sources.TreasuryReference) &&
            !string.IsNullOrWhiteSpace(sources.TreasuryCurrency) &&
            !string.IsNullOrWhiteSpace(sources.RegimenReference) &&
            !string.IsNullOrWhiteSpace(sources.HonorReference) &&
            !string.IsNullOrWhiteSpace(sources.MembershipReference) &&
            !string.IsNullOrWhiteSpace(sources.MasterPeriodsReference);
    }

    private static void AddField(StringBuilder builder, string value) =>
        builder.Append(Encoding.UTF8.GetByteCount(value))
            .Append(':').Append(value);
}

/// <summary>Declaración interna de completitud emitida por conectores institucionales.</summary>
public sealed record AssemblyClosureOfficialCoverage(
    Guid AssemblyId, DateOnly CutoffDate,
    bool AllExpectedAssemblyMembersEnumerated,
    bool FullEligibilityHistoryResolved,
    bool InstitutionalSourcesVerified,
    string OfficialRosterReference);

public sealed record AssemblyClosureMemberSources(
    Guid PersonId, Guid OrganizationId, DateOnly CutoffDate,
    bool MembershipVerified, bool MasterPeriodsVerified,
    bool GrandTreasuryHistoryCertified, bool HonorCoverageComplete,
    bool RegimenCoverageComplete, bool LatestDecisionResolved,
    string MembershipReference, string MasterPeriodsReference,
    string TreasuryReference, string TreasuryCurrency,
    string HonorReference, string RegimenReference,
    bool ValidDebtExceptionCertified);

public sealed record AssemblyClosureCandidateEvidence(
    AssemblyMemberStatusRecord Member,
    AssemblyEligibilityRecord LatestEligibility,
    AssemblyClosureMemberSources Sources,
    DateTimeOffset EvaluatedAsOfUtc);

public sealed record AssemblyClosureVoter(
    Guid PersonId, Guid OrganizationId, string Category, Guid SourceOfficeAssignmentId);

public sealed record AssemblyClosurePreflightResult(
    bool DocumentaryPreflightReady,
    bool MayPerformClosure,
    string? CandidateSnapshotDigestSha256,
    int VotersCount,
    IReadOnlyList<string> BlockingReasons);
