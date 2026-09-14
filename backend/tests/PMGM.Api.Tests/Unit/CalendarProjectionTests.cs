using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.InstitutionalCalendar;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class CalendarProjectionTests
{
    private readonly InstitutionalAccessService _access = new();

    [Fact]
    public void Restricted_lodge_event_is_projected_as_occupied_for_regular_lodge_member()
    {
        var organizationId = Guid.NewGuid();
        var service = new InstitutionalCalendarProjectionService(_access);
        var calendarEvent = BuildRestrictedLodgeEvent(organizationId);
        var user = BuildUser(new Claim(InstitutionalClaims.Organization, organizationId.ToString()));

        var result = service.Project(calendarEvent, user);

        Assert.NotNull(result);
        Assert.True(result.IsMasked);
        Assert.Equal("Ocupado", result.Title);
        Assert.Equal("occupancy", result.EventType);
        Assert.Null(result.SourceEntityId);
        Assert.Null(result.ResponsibleSubject);
    }

    [Fact]
    public void Lodge_admin_can_see_restricted_event_details_in_own_lodge()
    {
        var organizationId = Guid.NewGuid();
        var service = new InstitutionalCalendarProjectionService(_access);
        var calendarEvent = BuildRestrictedLodgeEvent(organizationId);
        var user = BuildUser(
            new Claim(InstitutionalClaims.Organization, organizationId.ToString()),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin));

        var result = service.Project(calendarEvent, user);

        Assert.NotNull(result);
        Assert.False(result.IsMasked);
        Assert.Equal("Ceremonia reservada", result.Title);
        Assert.Equal("ceremony", result.EventType);
        Assert.Equal("CER-123", result.SourceEntityId);
    }

    [Fact]
    public void Member_from_another_lodge_cannot_see_event_or_occupancy()
    {
        var service = new InstitutionalCalendarProjectionService(_access);
        var calendarEvent = BuildRestrictedLodgeEvent(Guid.NewGuid());
        var user = BuildUser(new Claim(InstitutionalClaims.Organization, Guid.NewGuid().ToString()));

        var result = service.Project(calendarEvent, user);

        Assert.Null(result);
    }

    [Fact]
    public void Institutional_order_event_is_visible_to_authenticated_lodge_member()
    {
        var service = new InstitutionalCalendarProjectionService(_access);
        var calendarEvent = new InstitutionalCalendarEvent
        {
            Title = "Aniversario institucional",
            EventType = "commemoration",
            StartsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            ScopeType = CalendarCodes.ScopeType.Order,
            Visibility = CalendarCodes.Visibility.Institutional,
            Status = CalendarCodes.Status.Confirmed,
            SourceModule = "grand-secretariat",
            SourceEntityType = "institutional-event",
            SourceEntityId = "ANN-001",
            SourceControlled = true
        };
        var user = BuildUser(new Claim(InstitutionalClaims.Organization, Guid.NewGuid().ToString()));

        var result = service.Project(calendarEvent, user);

        Assert.NotNull(result);
        Assert.False(result.IsMasked);
        Assert.Equal("Aniversario institucional", result.Title);
    }

    private static InstitutionalCalendarEvent BuildRestrictedLodgeEvent(Guid organizationId)
        => new()
        {
            Title = "Ceremonia reservada",
            EventType = "ceremony",
            StartsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            LocationDisplay = "Templo 1",
            SpaceId = Guid.NewGuid(),
            OrganizationId = organizationId,
            ScopeType = CalendarCodes.ScopeType.Lodge,
            Visibility = CalendarCodes.Visibility.Restricted,
            Status = CalendarCodes.Status.Confirmed,
            SourceModule = "ceremonies",
            SourceEntityType = "ceremony-request",
            SourceEntityId = "CER-123",
            SourceControlled = true,
            OccupancyOnlyWhenRestricted = true,
            ResponsibleSubject = "subject-123"
        };

    private static ClaimsPrincipal BuildUser(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));
}
