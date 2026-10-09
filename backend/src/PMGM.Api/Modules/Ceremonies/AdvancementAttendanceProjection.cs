using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Proyección de hechos observados, NO dictamen de autorización. Las excusas se
/// conservan separadas de las presencias hasta que exista regla normativa.
/// </summary>
public static class AdvancementAttendanceProjection
{
    public static string? SourceGrade(string ceremonyType)
        => ceremonyType switch
        {
            CeremonyCodes.Type.WageIncrease => LodgeManagementCodes.Grade.Apprentice,
            CeremonyCodes.Type.Exaltation => LodgeManagementCodes.Grade.Fellowcraft,
            _ => null
        };

    public static string? StartEvent(string ceremonyType)
        => ceremonyType switch
        {
            CeremonyCodes.Type.WageIncrease => MembershipCodes.DegreeEvent.Initiation,
            CeremonyCodes.Type.Exaltation => MembershipCodes.DegreeEvent.WageIncrease,
            _ => null
        };

    /// <summary>
    /// La última anotación gana, con desempate estable por Id. Un ausente o un
    /// excusado posterior revoca una presencia anterior de la misma actividad.
    /// </summary>
    public static AttendanceCounts Count(
        IReadOnlyCollection<Guid> heldActivityIds,
        IEnumerable<AttendanceEvent> records)
    {
        var permitted = heldActivityIds.ToHashSet();
        var latest = records
            .Where(x => permitted.Contains(x.ActivityId))
            .GroupBy(x => x.ActivityId)
            .Select(g => g.OrderByDescending(x => x.RecordedAtUtc)
                          .ThenByDescending(x => x.Id).First())
            .ToArray();

        return new AttendanceCounts(
            Held: permitted.Count,
            Recorded: latest.Length,
            Present: latest.Count(x => x.Status == LodgeManagementCodes.AttendanceStatus.Present),
            Excused: latest.Count(x => x.Status == LodgeManagementCodes.AttendanceStatus.Excused),
            Absent: latest.Count(x => x.Status == LodgeManagementCodes.AttendanceStatus.Absent),
            PresentIds: latest.Where(x => x.Status == LodgeManagementCodes.AttendanceStatus.Present)
                              .Select(x => x.ActivityId).Order().ToArray());
    }

    public static async Task<AdvancementAttendanceResult> GetAsync(
        Guid organizationId,
        Guid memberId,
        string ceremonyType,
        DateOnly cutoff,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext lodgeDb,
        CancellationToken cancellationToken)
    {
        var grade = SourceGrade(ceremonyType);
        var eventType = StartEvent(ceremonyType);
        if (grade is null || eventType is null)
            return new("not_applicable", "La ceremonia no exige progresión por este circuito.", null);

        var active = await institutionalDb.Memberships.AsNoTracking().AnyAsync(
            x => x.MemberId == memberId &&
                 x.OrganizationId == organizationId &&
                 x.Status == MembershipCodes.MembershipStatus.Active &&
                 x.EndDate == null &&
                 (x.StartDate == null || x.StartDate <= cutoff),
            cancellationToken);
        if (!active)
            return new("missing_active_membership", "No consta pertenencia activa del hermano al Taller.", null);

        // Se consulta la fecha del grado a lo largo del histórico institucional.
        // No se exige que el evento haya ocurrido en el Taller actual: los
        // traslados conservan la fecha de iniciación/aumento.
        var start = await institutionalDb.DegreeEvents.AsNoTracking()
            .Where(x => x.MemberId == memberId &&
                        x.EventType == eventType &&
                        x.EffectiveDate <= cutoff)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => (DateOnly?)x.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (start is null)
            return new("missing_degree_start", "No consta evento institucional de inicio del grado evaluado.", null);

        var meetings = await lodgeDb.LodgeMeetings.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId &&
                        x.Grade == grade &&
                        x.MeetingDate > start.Value &&
                        x.MeetingDate <= cutoff &&
                        (x.Status == LodgeManagementCodes.MeetingStatus.Held ||
                         x.Status == LodgeManagementCodes.MeetingStatus.Closed))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var meetingRows = await lodgeDb.LodgeAttendanceRecords.AsNoTracking()
            .Where(x => x.MemberId == memberId && meetings.Contains(x.MeetingId))
            .Select(x => new AttendanceEvent(x.Id, x.MeetingId, x.Status, x.RecordedAtUtc))
            .ToListAsync(cancellationToken);

        var instructions = await lodgeDb.LodgeInstructionSessions.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId &&
                        x.Grade == grade &&
                        x.InstructionDate > start.Value &&
                        x.InstructionDate <= cutoff &&
                        x.Status == LodgeManagementCodes.InstructionStatus.Held)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var instructionRows = await lodgeDb.LodgeInstructionAttendanceRecords.AsNoTracking()
            .Where(x => x.MemberId == memberId && instructions.Contains(x.InstructionSessionId))
            .Select(x => new AttendanceEvent(x.Id, x.InstructionSessionId, x.Status, x.RecordedAtUtc))
            .ToListAsync(cancellationToken);

        return new("ready", null, new AdvancementAttendanceSnapshot(
            memberId,
            organizationId,
            ceremonyType,
            grade,
            start.Value,
            cutoff,
            Count(meetings, meetingRows),
            Count(instructions, instructionRows)));
    }
}

public sealed record AttendanceEvent(Guid Id, Guid ActivityId, string Status, DateTimeOffset RecordedAtUtc);

public sealed record AttendanceCounts(
    int Held,
    int Recorded,
    int Present,
    int Excused,
    int Absent,
    IReadOnlyList<Guid> PresentIds);

public sealed record AdvancementAttendanceSnapshot(
    Guid MemberId,
    Guid OrganizationId,
    string CeremonyType,
    string SourceGrade,
    DateOnly GradeStartDate,
    DateOnly AsOf,
    AttendanceCounts Meetings,
    AttendanceCounts Instructions);

public sealed record AdvancementAttendanceResult(
    string Status,
    string? Reason,
    AdvancementAttendanceSnapshot? Snapshot);
