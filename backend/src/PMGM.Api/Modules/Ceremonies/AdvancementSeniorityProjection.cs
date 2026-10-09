using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Evidencia cronológica NO habilitante. Antigüedad desde un evento de grado
/// no equivale, por sí sola, a continuidad institucional comprobada.
/// </summary>
public static class AdvancementSeniorityProjection
{
    public static int CompleteMonths(DateOnly gradeStart, DateOnly asOf)
    {
        if (asOf < gradeStart) return 0;
        var months = (asOf.Year - gradeStart.Year) * 12 + asOf.Month - gradeStart.Month;
        return gradeStart.AddMonths(months) > asOf ? months - 1 : months;
    }

    public static AdvancementSenioritySnapshot Assess(
        DateOnly gradeStart,
        DateOnly asOf,
        IEnumerable<SeniorityMembershipPeriod> memberships,
        IEnumerable<SeniorityStatusEvent> statusEvents)
    {
        if (gradeStart > asOf)
            return new(gradeStart, asOf, 0, "invalid_grade_start",
                "La fecha de inicio del grado es posterior al corte; revisar el expediente.", 0, false, false);

        var months = CompleteMonths(gradeStart, asOf);
        var facts = memberships.ToArray();
        if (facts.Any(x => x.StartDate is null))
            return new(gradeStart, asOf, months, "indeterminate",
                "Hay una pertenencia histórica sin fecha inicial; revisar la continuidad.", facts.Length, false, false);

        var interrupted = statusEvents.Any(x => x.EffectiveDate >= gradeStart &&
            x.EffectiveDate <= asOf && x.Status is
                MembershipCodes.InstitutionalStatus.Inactive or
                MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal or
                MembershipCodes.InstitutionalStatus.ForcedWithdrawal or
                MembershipCodes.InstitutionalStatus.Deceased or
                MembershipCodes.InstitutionalStatus.PastActive);

        var ranges = facts
            .Where(x => x.StartDate is not null &&
                        (x.Status == MembershipCodes.MembershipStatus.Active ||
                         x.Status == MembershipCodes.MembershipStatus.Transferred ||
                         x.Status == MembershipCodes.MembershipStatus.Closed) &&
                        (x.EndDate is not null || x.Status == MembershipCodes.MembershipStatus.Active))
            .Select(x => (Start: x.StartDate!.Value, End: x.EndDate ?? asOf))
            .Where(x => x.End >= x.Start && x.End >= gradeStart && x.Start <= asOf)
            .OrderBy(x => x.Start).ThenBy(x => x.End)
            .ToArray();

        var coveredUntil = gradeStart.AddDays(-1);
        foreach (var range in ranges)
        {
            if (range.Start > coveredUntil.AddDays(1)) break;
            if (range.End > coveredUntil)
                coveredUntil = range.End > asOf ? asOf : range.End;
            if (coveredUntil >= asOf) break;
        }

        var complete = coveredUntil >= asOf;
        var status = interrupted ? "institutional_interruption" :
            complete ? "membership_dates_covered" : "membership_gap";
        var reason = interrupted
            ? "Existen hitos institucionales de interrupción posteriores al inicio del grado."
            : complete
                ? "Fechas de membresías cubren el período; requiere verificación institucional de antigüedad continuada."
                : "No se acredita cobertura completa de membresías entre el inicio del grado y la fecha de corte.";

        // Aunque la cobertura esté completa, no se emite un dictamen de
        // antigüedad mínima ni se sustituye la norma institucional.
        return new(gradeStart, asOf, months, status, reason, facts.Length,
            complete, interrupted);
    }

    public static async Task<AdvancementSenioritySnapshot> GetAsync(
        Guid memberId,
        DateOnly gradeStart,
        DateOnly cutoff,
        PmgmDbContext db,
        CancellationToken cancellationToken)
    {
        var memberships = await db.Memberships.AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .Select(x => new SeniorityMembershipPeriod(x.StartDate, x.EndDate, x.Status))
            .ToListAsync(cancellationToken);
        var events = await db.InstitutionalStatusEvents.AsNoTracking()
            .Where(x => x.MemberId == memberId && x.EffectiveDate >= gradeStart && x.EffectiveDate <= cutoff)
            .Select(x => new SeniorityStatusEvent(x.EffectiveDate, x.EventType))
            .ToListAsync(cancellationToken);
        return Assess(gradeStart, cutoff, memberships, events);
    }
}

public sealed record SeniorityMembershipPeriod(DateOnly? StartDate, DateOnly? EndDate, string Status);
public sealed record SeniorityStatusEvent(DateOnly EffectiveDate, string Status);

public sealed record AdvancementSenioritySnapshot(
    DateOnly GradeStartDate,
    DateOnly AsOf,
    int CompleteCalendarMonths,
    string ContinuityEvidenceStatus,
    string Reason,
    int MembershipRecordCount,
    bool MembershipDateCoverageComplete,
    bool HasInstitutionalInterruption);
