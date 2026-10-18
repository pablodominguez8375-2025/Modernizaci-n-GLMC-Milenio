using PMGM.Api.Modules.InstitutionalCalendar;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;
using PMGM.Api.Modules.Notifications;
using Xunit;

namespace PMGM.Api.Tests.Notifications;

public sealed class CalendarNotificationRecipientGuardTests
{
    [Theory]
    [InlineData("apprentice", 1, true)]
    [InlineData("fellow_craft", 1, false)]
    [InlineData("master", 1, false)]
    [InlineData("apprentice", 2, true)]
    [InlineData("fellow_craft", 2, true)]
    [InlineData("master", 2, false)]
    [InlineData("apprentice", 3, true)]
    [InlineData("fellow_craft", 3, true)]
    [InlineData("master", 3, true)]
    public void Calendar_notice_respects_degree(string grade, int degree, bool permitted)
    {
        var lodge = Guid.NewGuid();
        var reader = new CalendarGradeReader(false, degree, new HashSet<Guid> { lodge },
            new HashSet<Guid>());
        Assert.Equal(permitted, CalendarNotificationRecipientGuard.CanDeliver(
            reader, Event(lodge, grade), [NotificationCodes.Channel.Internal]));
    }

    [Theory]
    [InlineData("apprentice", 1, true)]
    [InlineData("fellow_craft", 1, false)]
    [InlineData("master", 1, false)]
    [InlineData("apprentice", 2, false)]
    [InlineData("fellow_craft", 2, true)]
    [InlineData("master", 2, false)]
    [InlineData("apprentice", 3, false)]
    [InlineData("fellow_craft", 3, false)]
    [InlineData("master", 3, true)]
    public void Instruction_notice_requires_exact_degree(string grade, int effectiveDegree, bool permitted)
    {
        var lodge = Guid.NewGuid();
        var reader = new CalendarGradeReader(false, effectiveDegree,
            new HashSet<Guid> { lodge }, new HashSet<Guid>());
        var evt = Event(lodge, grade);
        evt.EventType = "lodge_instruction_day";
        Assert.Equal(permitted, CalendarNotificationRecipientGuard.CanDeliver(
            reader, evt, [NotificationCodes.Channel.Internal]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("all")]
    public void General_lodge_notice_requires_active_membership(string? grade)
    {
        var lodge = Guid.NewGuid();
        var valid = new CalendarGradeReader(false, 1, new HashSet<Guid> { lodge }, new HashSet<Guid>());
        var outsider = new CalendarGradeReader(false, 3, new HashSet<Guid> { Guid.NewGuid() },
            new HashSet<Guid>());
        var retired = new CalendarGradeReader(false, 3, new HashSet<Guid>(), new HashSet<Guid>());
        Assert.True(CalendarNotificationRecipientGuard.CanDeliver(
            valid, Event(lodge, grade), [NotificationCodes.Channel.Internal]));
        Assert.False(CalendarNotificationRecipientGuard.CanDeliver(
            outsider, Event(lodge, grade), [NotificationCodes.Channel.Internal]));
        Assert.False(CalendarNotificationRecipientGuard.CanDeliver(
            retired, Event(lodge, grade), [NotificationCodes.Channel.Internal]));
    }

    [Fact]
    public void Officer_only_bypasses_degree_in_active_own_lodge()
    {
        var lodge = Guid.NewGuid();
        var another = Guid.NewGuid();
        var reader = new CalendarGradeReader(false, 1, new HashSet<Guid> { lodge },
            new HashSet<Guid> { lodge });
        Assert.True(CalendarNotificationRecipientGuard.CanDeliver(
            reader, Event(lodge, "master"), [NotificationCodes.Channel.Internal]));
        Assert.False(CalendarNotificationRecipientGuard.CanDeliver(
            reader, Event(another, "master"), [NotificationCodes.Channel.Internal]));
        var expired = new CalendarGradeReader(false, 1, new HashSet<Guid> { lodge },
            new HashSet<Guid>());
        Assert.False(CalendarNotificationRecipientGuard.CanDeliver(
            expired, Event(lodge, "master"), [NotificationCodes.Channel.Internal]));
    }

    [Fact]
    public void Rejects_email_and_restricted_or_institutional_events_without_verified_delivery_binding()
    {
        var lodge = Guid.NewGuid();
        var reader = new CalendarGradeReader(false, 3, new HashSet<Guid> { lodge },
            new HashSet<Guid>());
        Assert.False(CalendarNotificationRecipientGuard.CanDeliver(
            reader, Event(lodge, "apprentice"), [NotificationCodes.Channel.Email]));
        Assert.False(CalendarNotificationRecipientGuard.CanDeliver(
            reader, Event(lodge, null), [NotificationCodes.Channel.Internal, NotificationCodes.Channel.Email]));
        Assert.False(CalendarNotificationRecipientGuard.CanDeliver(
            reader, Event(lodge, "apprentice"), []));
        Assert.False(CalendarNotificationRecipientGuard.CanDeliver(
            reader, null, [NotificationCodes.Channel.Internal]));
        var restricted = Event(lodge, "apprentice");
        restricted.Visibility = CalendarCodes.Visibility.Restricted;
        Assert.False(CalendarNotificationRecipientGuard.CanDeliver(
            reader, restricted, [NotificationCodes.Channel.Internal]));
        var orderEvent = Event(lodge, null);
        orderEvent.ScopeType = CalendarCodes.ScopeType.GrandLodge;
        orderEvent.Visibility = CalendarCodes.Visibility.Institutional;
        Assert.False(CalendarNotificationRecipientGuard.CanDeliver(
            reader, orderEvent, [NotificationCodes.Channel.Internal]));
    }

    [Theory]
    [InlineData("calendar.event", null, null, null, true)]
    [InlineData("random", "calendar_event", null, null, true)]
    [InlineData("random", "lodge-instruction", null, null, true)]
    [InlineData("random", null, "/agenda/mes", null, true)]
    [InlineData("random", null, null, "lodge.meeting.reminder", true)]
    [InlineData("candidate.interview.assigned", "interview", "/insinuados",
        "candidate.interview.assigned", false)]
    public void Classifies_calendar_routes_and_types(string type, string? resource,
        string? url, string? template, bool expected)
    {
        Assert.Equal(expected, CalendarNotificationRecipientGuard.IsProtected(
            type, resource, url, template));
    }

    private static InstitutionalCalendarEvent Event(Guid organizationId, string? grade) => new()
    {
        Title = "Evento sintético", EventType = "lodge_meeting_day",
        Grade = grade, OrganizationId = organizationId,
        ScopeType = CalendarCodes.ScopeType.Lodge,
        Visibility = CalendarCodes.Visibility.Lodge,
        Status = CalendarCodes.Status.Confirmed,
        StartsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
        EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
        SourceModule = "qa", SourceEntityType = "meeting",
        SourceEntityId = Guid.NewGuid().ToString("N")
    };
}
