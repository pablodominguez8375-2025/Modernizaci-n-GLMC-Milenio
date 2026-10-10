using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Ceremonies;

public sealed record AdvancementAuthorizationSnapshot(
    Guid RuleId, string RuleVersion, Guid? SeniorityRuleId,
    DateOnly GradeStart, DateOnly AsOf,
    int MeetingAttendance, int InstructionAttendance,
    int CertifiedPaperCount, bool TwoDifferentKindsCertified,
    bool InstitutionalContinuityCertified,
    AdvancementThresholds Minimums,
    [property: JsonIgnore] IReadOnlyList<Guid> CertifiedPaperDocumentIds,
    [property: JsonIgnore] IReadOnlyList<Guid> VerifiedMeetingAttendanceIds,
    [property: JsonIgnore] IReadOnlyList<Guid> VerifiedInstructionAttendanceIds,
    AdvancementEligibilityDecision Decision);

/// <summary>
/// Única evaluación para GET /elegibilidad y POST /autorizar. Toda
/// evidencia documental se corrobora de nuevo al momento de autorizar.
/// </summary>
public static class AdvancementAuthorizationProjection
{
    public static async Task<AdvancementAuthorizationSnapshot?> EvaluateAsync(
        CeremonyRequest ceremony, DateOnly asOf,
        PmgmDbContext db, LodgeManagementDbContext lodgeDb,
        DocumentManagementDbContext documentsDb, CancellationToken ct)
    {
        if (AdvancementAttendanceProjection.SourceGrade(ceremony.CeremonyType) is null ||
            ceremony.MemberId is null) return null;
        var countCode = AdvancementRulePolicy.CodeFor(ceremony.CeremonyType);
        var seniorityCode = AdvancementSeniorityRulePolicy.CodeFor(ceremony.CeremonyType);
        if (countCode is null || seniorityCode is null) return null;

        var rules = await db.InstitutionalRuleSettings.AsNoTracking()
            .Where(x => (x.Code == countCode || x.Code == seniorityCode) &&
                x.Status == "active" && x.EffectiveFrom <= asOf &&
                (x.EffectiveTo == null || x.EffectiveTo >= asOf))
            .ToListAsync(ct);
        var countRule = AdvancementRulePolicy.Resolve(ceremony.CeremonyType, asOf, rules);
        var seniorityRule = AdvancementSeniorityRulePolicy.Resolve(ceremony.CeremonyType, asOf, rules);
        if (countRule is null || seniorityRule is null) return null;
        var attendance = await AdvancementAttendanceProjection.GetAsync(
            ceremony.OrganizationId, ceremony.MemberId.Value,
            ceremony.CeremonyType, asOf, db, lodgeDb, ct);
        if (attendance.Status != "ready" || attendance.Snapshot is null)
            return null;
        var gradeStart = attendance.Snapshot.GradeStartDate;

        var chronology = await AdvancementSeniorityProjection.GetAsync(
            ceremony.MemberId.Value, gradeStart, asOf, db, ct);
        var seniority = AdvancementSeniorityRulePolicy.Review(seniorityRule, chronology);
        var frozen = await db.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == ceremony.Id &&
                        x.ValidationType == "advancement_continuity" &&
                        x.AsOfDate <= asOf)
            .OrderByDescending(x => x.RecordedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        var versionReference = $"{seniorityRule.RuleId:N}|{gradeStart:yyyy-MM-dd}";
        var continuityCertified = seniority.ChronologicalThresholdReached &&
            frozen is not null &&
            frozen.Status == CeremonyCodes.ValidationStatus.Approved &&
            frozen.SourceReference == versionReference &&
            !string.IsNullOrWhiteSpace(frozen.Notes);

        var attestations = await db.AdvancementPaperAttestations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == ceremony.Id &&
                        x.OrganizationId == ceremony.OrganizationId &&
                        x.MemberId == ceremony.MemberId.Value)
            .ToListAsync(ct);
        var latest = attestations
            .GroupBy(x => x.WorkPaperDocumentId)
            .Select(g => g.OrderByDescending(x => x.RecordedAtUtc)
                          .ThenByDescending(x => x.Id).First())
            .ToArray();
        var validIds = new HashSet<Guid>();
        foreach (var item in latest.Where(x => x.Status == "approved"))
        {
            if (await AdvancementPaperEvidenceValidator.IsLiveAsync(
                item, ceremony.CeremonyType, gradeStart, asOf,
                db, lodgeDb, documentsDb, ct))
                validIds.Add(item.Id);
        }
        var papers = AdvancementCertifiedPaperPolicy.Count(
            ceremony.Id, ceremony.OrganizationId, ceremony.MemberId.Value,
            gradeStart, asOf, attestations, validIds);
        var evidence = new AdvancementEvidence(
            attendance.Snapshot.Meetings.Present,
            attendance.Snapshot.Instructions.Present,
            papers.Count);
        var decision = AdvancementEligibilityPolicy.Evaluate(
            ceremony.CeremonyType, countRule.Thresholds, evidence);
        // El mínimo parametrizable se aplica, y adicionalmente deben existir
        // los dos trabajos institucionales de clases diferentes. Las fechas
        // de membresía no se convierten automáticamente en un certificado.
        if (!papers.TwoRequiredKinds || !continuityCertified)
            decision = decision with { CanProceed = false,
                Mode = AdvancementEligibilityModes.Blocked };

        return new AdvancementAuthorizationSnapshot(
            countRule.RuleId, countRule.Thresholds.RuleVersion,
            seniorityRule.RuleId, gradeStart, asOf,
            evidence.MeetingAttendance, evidence.InstructionAttendance,
            papers.Count, papers.TwoRequiredKinds, continuityCertified,
            countRule.Thresholds,
            papers.VerifiedDocumentIds,
            attendance.Snapshot.Meetings.PresentIds,
            attendance.Snapshot.Instructions.PresentIds,
            decision);
    }
}
