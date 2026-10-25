using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.InstitutionalCalendar;

/// <summary>
/// Contexto de lectura calculado una sola vez por solicitud desde identidad y datos institucionales.
/// Un cargo de Taller se considera vigente solo cuando existe una asignación activa en BD.
/// </summary>
public sealed record CalendarGradeReader(
    bool IsInstitutionalAdministrator,
    int? EffectiveDegree,
    IReadOnlySet<Guid> ActiveLodges,
    IReadOnlySet<Guid> OfficerLodges)
{
    public bool CanSee(InstitutionalCalendarEvent item)
    {
        if (IsInstitutionalAdministrator) return true;

        // First validate current lodge membership for EVERY lodge-bound event,
        // including historical/null and explicitly general ('all') events.
        // Grade is a SECOND filter, never a substitute for lodge membership.
        if ((item.ScopeType == CalendarCodes.ScopeType.Lodge ||
             item.Visibility == CalendarCodes.Visibility.Lodge) &&
            item.OrganizationId is Guid lodgeId && !ActiveLodges.Contains(lodgeId))
            return false;

        // La Docencia es estricta: sin Taller o sin grado concreto la
        // instrucción nunca se interpreta como un evento general. La excepción
        // de un cargo exige membresía vigente en ESE Taller; el administrador
        // institucional ya fue reconocido al inicio del método.
        if (item.EventType == "lodge_instruction_day")
        {
            if (item.OrganizationId is not Guid instructionLodge ||
                !ActiveLodges.Contains(instructionLodge))
                return false;
            if (OfficerLodges.Contains(instructionLodge))
                return true;

            var instructionDegree = CalendarCodes.Grade.ToNumeric(item.Grade);
            return instructionDegree is not null &&
                EffectiveDegree is not null &&
                EffectiveDegree.Value == instructionDegree.Value;
        }

        // Tenidas, ceremonias y demás eventos: generales o el propio grado
        // y los inferiores, conforme decisión institucional anterior.
        if (item.Grade is null or CalendarCodes.Grade.All) return true;
        if (ActiveLodges.Count == 0) return false;
        if (item.OrganizationId is Guid officerLodge && OfficerLodges.Contains(officerLodge))
            return true;

        var eventDegree = CalendarCodes.Grade.ToNumeric(item.Grade);
        if (eventDegree is null || EffectiveDegree is null) return false;
        return EffectiveDegree.Value >= eventDegree.Value;
    }
}

public interface ICalendarGradeVisibilityService
{
    Task<CalendarGradeReader> ForUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
}

public sealed class CalendarGradeVisibilityService(
    PmgmDbContext db,
    IInstitutionalMemberContextResolver memberResolver,
    IInstitutionalAccessService access) : ICalendarGradeVisibilityService
{
    public async Task<CalendarGradeReader> ForUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var institutionalAdmin = access.IsPlatformSuperAdmin(user) ||
            (access.HasOrderScope(user) && access.HasRole(user, InstitutionalRoles.GranLogiaAdmin));
        if (institutionalAdmin)
        {
            return new CalendarGradeReader(true, null, new HashSet<Guid>(), new HashSet<Guid>());
        }

        var member = await memberResolver.ResolveAsync(user, cancellationToken);
        if (member is null)
        {
            return new CalendarGradeReader(false, null, new HashSet<Guid>(), new HashSet<Guid>());
        }

        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Santiago")).DateTime);

        var activeLodges = (await db.Memberships.AsNoTracking()
            .Where(x => x.MemberId == member.MemberId &&
                        x.Status == MembershipCodes.MembershipStatus.Active &&
                        (x.StartDate == null || x.StartDate <= date) &&
                        (x.EndDate == null || x.EndDate >= date))
            .Select(x => x.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        var offices = (await db.OfficeAssignments.AsNoTracking()
            .Where(x => x.MemberId == member.MemberId &&
                        activeLodges.Contains(x.OrganizationId) &&
                        x.StartDate != null && x.StartDate <= date &&
                        (x.EndDate == null || x.EndDate >= date))
            .Select(x => x.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        return new CalendarGradeReader(false, member.EffectiveDegree, activeLodges, offices);
    }
}
