using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Ceremonies;

public sealed record AdvancementDispensationCheck(
    bool Valid, string Reason, DispensationEvidence? Evidence,
    IReadOnlyList<AdvancementDispensationReduction> Reductions);

public static class AdvancementDispensationEvidenceService
{
    public static async Task<AdvancementDispensationCheck> ValidateAsync(
        AdvancementDispensationCase row, CeremonyRequest ceremony, DateOnly asOf,
        AdvancementRuleSnapshot countRule, AdvancementSeniorityRuleSnapshot seniorityRule,
        AdvancementAttendanceSnapshot attendance, AdvancementSenioritySnapshot chronology,
        PmgmDbContext db, LodgeManagementDbContext lodgeDb, CancellationToken ct)
    {
        if (row.CeremonyRequestId != ceremony.Id || row.OrganizationId != ceremony.OrganizationId ||
            ceremony.MemberId is null || row.MemberId != ceremony.MemberId ||
            row.AdvancementRuleId != countRule.RuleId || row.SeniorityRuleId != seniorityRule.RuleId)
            return Invalid("La dispensa no corresponde al expediente o a las reglas vigentes.");

        var rules = await db.InstitutionalRuleSettings.AsNoTracking().Where(x =>
            x.Code == AdvancementDispensationRulePolicy.Code && x.Status == "active" &&
            x.EffectiveFrom <= asOf && (x.EffectiveTo == null || x.EffectiveTo >= asOf)).ToListAsync(ct);
        var institutionalRule = AdvancementDispensationRulePolicy.Resolve(asOf, rules);
        if (institutionalRule is null || institutionalRule.RuleId != row.InstitutionalRuleId)
            return Invalid("La regla versionada de dispensa no está vigente o no coincide.");

        List<Guid>? participantIds;
        List<AdvancementDispensationReduction>? reductions;
        try
        {
            participantIds = JsonSerializer.Deserialize<List<Guid>>(row.EligibleMasterIdsJson);
            reductions = JsonSerializer.Deserialize<List<AdvancementDispensationReduction>>(row.ReductionsJson);
        }
        catch (JsonException) { return Invalid("El expediente contiene evidencia serializada inválida."); }
        if (participantIds is null || reductions is null || participantIds.Count == 0 ||
            participantIds.Count != participantIds.Distinct().Count() ||
            reductions.Count != reductions.Select(x => x.RequirementCode).Distinct().Count())
            return Invalid("La nómina o las reducciones del expediente son inválidas.");

        var meeting = await lodgeDb.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == row.MeetingId && x.OrganizationId == ceremony.OrganizationId, ct);
        if (meeting is null || meeting.Grade != LodgeManagementCodes.Grade.Master ||
            !LodgeManagementCodes.MeetingStatus.IsHeld(meeting.Status) || meeting.MeetingDate > asOf)
            return Invalid("La Tenida citada no es una Cámara del Medio celebrada por el Taller.");
        var minute = await lodgeDb.LodgeMinutes.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == row.FullMinuteId && x.MeetingId == meeting.Id &&
            x.Status == LodgeManagementCodes.MinuteStatus.Approved, ct);
        var ballot = await lodgeDb.LodgeAnonymousBallots.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == row.BallotId && x.MeetingId == meeting.Id &&
            x.Status == LodgeManagementCodes.BallotStatus.Closed, ct);
        if (minute is null || ballot is null ||
            ballot.Subject != $"dispensation:{ceremony.Id:N}" ||
            ballot.EligibleCount != participantIds.Count ||
            ballot.PositiveCount != ballot.EligibleCount || ballot.NegativeCount != 0 ||
            row.EligibleCount != ballot.EligibleCount || row.PositiveCount != ballot.PositiveCount ||
            row.NegativeCount != ballot.NegativeCount)
            return Invalid("No consta acta aprobada y votación unánime vinculada a esta dispensa.");

        var attendanceRows = await lodgeDb.LodgeAttendanceRecords.AsNoTracking()
            .Where(x => x.MeetingId == meeting.Id).OrderByDescending(x => x.RecordedAtUtc)
            .ThenByDescending(x => x.Id).ToListAsync(ct);
        var present = attendanceRows.GroupBy(x => x.MemberId).Select(x => x.First())
            .Where(x => x.Status == LodgeManagementCodes.AttendanceStatus.Present)
            .Select(x => x.MemberId).ToHashSet();
        var memberships = await db.Memberships.AsNoTracking().Where(x =>
            x.OrganizationId == ceremony.OrganizationId && present.Contains(x.MemberId) &&
            x.Status == MembershipCodes.MembershipStatus.Active &&
            x.StartDate != null && x.StartDate <= meeting.MeetingDate &&
            x.EndDate == null)
            .Select(x => new { x.MemberId, x.Member.CurrentDegree }).ToListAsync(ct);
        var eligible = memberships.Where(x =>
            InstitutionalDegree.TryParse(x.CurrentDegree, out var degree) && degree >= 3)
            .Select(x => x.MemberId).ToHashSet();
        if (!eligible.SetEquals(participantIds))
            return Invalid("La nómina no coincide con todos los Maestros presentes y habilitados de la Cámara.");

        var reductionByCode = reductions.ToDictionary(x => x.RequirementCode, x => x.AuthorizedReduction);
        var seniorityReduction = reductionByCode.GetValueOrDefault(AdvancementDispensationReductionPolicy.SeniorityMonths);
        var meetingsReduction = reductionByCode.GetValueOrDefault(AdvancementRequirementCodes.MeetingAttendance);
        var instructionsReduction = reductionByCode.GetValueOrDefault(AdvancementRequirementCodes.InstructionAttendance);
        if (reductionByCode.Keys.Any(x => x is not AdvancementDispensationReductionPolicy.SeniorityMonths
                and not AdvancementRequirementCodes.MeetingAttendance
                and not AdvancementRequirementCodes.InstructionAttendance) ||
            reductions.All(x => x.AuthorizedReduction == 0) ||
            !AdvancementDispensationReductionPolicy.TryCalculateEffectiveMinimum(
                AdvancementDispensationReductionPolicy.SeniorityMonths,
                seniorityRule.MinimumCompleteMonths, seniorityReduction, out var effectiveSeniority) ||
            !AdvancementDispensationReductionPolicy.TryCalculateEffectiveMinimum(
                AdvancementRequirementCodes.MeetingAttendance,
                countRule.Thresholds.MinimumMeetingAttendance, meetingsReduction, out var effectiveMeetings) ||
            !AdvancementDispensationReductionPolicy.TryCalculateEffectiveMinimum(
                AdvancementRequirementCodes.InstructionAttendance,
                countRule.Thresholds.MinimumInstructionAttendance, instructionsReduction, out var effectiveInstructions))
            return Invalid("La reducción no respeta los requisitos dispensables ni el máximo del cincuenta por ciento.");

        if (!chronology.MembershipDateCoverageComplete || chronology.HasInstitutionalInterruption ||
            chronology.CompleteCalendarMonths < effectiveSeniority ||
            attendance.Meetings.Present < effectiveMeetings ||
            attendance.Instructions.Present < effectiveInstructions)
            return Invalid("La evidencia observada no alcanza los mínimos reducidos por la dispensa.");

        var internalStatus = row.Status switch
        {
            AdvancementDispensationCaseStatuses.Approved => CeremonyCodes.ValidationStatus.ExceptionApproved,
            AdvancementDispensationCaseStatuses.Rejected => CeremonyCodes.ValidationStatus.Rejected,
            _ => CeremonyCodes.ValidationStatus.Pending
        };
        var evidence = new DispensationEvidence(true, row.FullMinuteId.ToString("N"),
            internalStatus, row.ResolutionReference);
        return new(true, "Expediente de dispensa verificable.", evidence, reductions);
    }

    private static AdvancementDispensationCheck Invalid(string reason)
        => new(false, reason, null, Array.Empty<AdvancementDispensationReduction>());
}
