using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;

namespace PMGM.Api.Modules.InstitutionalCalendar;

public interface IInstitutionalCalendarProjectionService
{
    CalendarEventProjection? Project(InstitutionalCalendarEvent calendarEvent, ClaimsPrincipal user);
    bool CanManage(InstitutionalCalendarEvent calendarEvent, ClaimsPrincipal user);
    bool CanManage(Guid? organizationId, ClaimsPrincipal user);
}

public sealed class InstitutionalCalendarProjectionService(IInstitutionalAccessService access)
    : IInstitutionalCalendarProjectionService
{
    public CalendarEventProjection? Project(InstitutionalCalendarEvent calendarEvent, ClaimsPrincipal user)
    {
        if (!CanReadScope(calendarEvent, user))
        {
            return null;
        }

        var canSeeDetails = calendarEvent.Visibility switch
        {
            CalendarCodes.Visibility.Public => true,
            CalendarCodes.Visibility.Institutional => HasInstitutionalIdentity(user),
            CalendarCodes.Visibility.Lodge => calendarEvent.OrganizationId is not null &&
                                               access.CanReadOrganization(user, calendarEvent.OrganizationId.Value),
            CalendarCodes.Visibility.Restricted => CanManage(calendarEvent, user),
            _ => false
        };

        if (!canSeeDetails)
        {
            if (!calendarEvent.OccupancyOnlyWhenRestricted)
            {
                return null;
            }

            return new CalendarEventProjection(
                calendarEvent.Id,
                "Ocupado",
                "occupancy",
                calendarEvent.StartsAtUtc,
                calendarEvent.EndsAtUtc,
                calendarEvent.TimeZoneId,
                calendarEvent.LocationDisplay,
                calendarEvent.SpaceId,
                calendarEvent.OrganizationId,
                CalendarCodes.Visibility.Restricted,
                calendarEvent.Status,
                null,
                null,
                null,
                null,
                true);
        }

        return new CalendarEventProjection(
            calendarEvent.Id,
            calendarEvent.Title,
            calendarEvent.EventType,
            calendarEvent.StartsAtUtc,
            calendarEvent.EndsAtUtc,
            calendarEvent.TimeZoneId,
            calendarEvent.LocationDisplay,
            calendarEvent.SpaceId,
            calendarEvent.OrganizationId,
            calendarEvent.Visibility,
            calendarEvent.Status,
            calendarEvent.SourceModule,
            calendarEvent.SourceEntityType,
            calendarEvent.SourceEntityId,
            calendarEvent.ResponsibleSubject,
            false);
    }

    public bool CanManage(InstitutionalCalendarEvent calendarEvent, ClaimsPrincipal user)
        => CanManage(calendarEvent.OrganizationId, user);

    public bool CanManage(Guid? organizationId, ClaimsPrincipal user)
    {
        if (access.CanManageGrandSecretariat(user))
        {
            return true;
        }

        return organizationId is not null && access.CanManageOrganization(user, organizationId.Value);
    }

    private bool CanReadScope(InstitutionalCalendarEvent calendarEvent, ClaimsPrincipal user)
        => calendarEvent.ScopeType switch
        {
            CalendarCodes.ScopeType.Order or CalendarCodes.ScopeType.GrandLodge => HasInstitutionalIdentity(user),
            CalendarCodes.ScopeType.Lodge => calendarEvent.OrganizationId is not null &&
                                             access.CanReadOrganization(user, calendarEvent.OrganizationId.Value),
            CalendarCodes.ScopeType.Group => access.HasOrderScope(user),
            CalendarCodes.ScopeType.PrivateAdministrative => access.HasOrderScope(user),
            _ => false
        };

    private bool HasInstitutionalIdentity(ClaimsPrincipal user)
        => access.HasOrderScope(user) ||
           user.Claims.Any(x => x.Type == InstitutionalClaims.Organization && Guid.TryParse(x.Value, out _));
}

public sealed record CalendarEventProjection(
    Guid Id,
    string Title,
    string EventType,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string TimeZoneId,
    string? LocationDisplay,
    Guid? SpaceId,
    Guid? OrganizationId,
    string Visibility,
    string Status,
    string? SourceModule,
    string? SourceEntityType,
    string? SourceEntityId,
    string? ResponsibleSubject,
    bool IsMasked);
