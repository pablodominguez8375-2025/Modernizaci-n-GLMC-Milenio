using PMGM.Api.Modules.InstitutionalCalendar;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class CalendarGradeVisibilityTests
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
    public void Brother_sees_only_own_degree_and_lower(string grade, int effectiveDegree, bool expected)
    {
        var lodge = Guid.NewGuid();
        var reader = new CalendarGradeReader(false, effectiveDegree, new HashSet<Guid> { lodge }, new HashSet<Guid>());
        Assert.Equal(expected, reader.CanSee(BuildEvent(lodge, grade)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("all")]
    public void General_events_are_visible_even_without_degree(string? grade)
    {
        var lodge = Guid.NewGuid();
        var reader = new CalendarGradeReader(false, null, new HashSet<Guid> { lodge }, new HashSet<Guid>());
        Assert.True(reader.CanSee(BuildEvent(lodge, grade)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("all")]
    public void General_lodge_events_also_require_active_membership(string? grade)
    {
        var ownLodge = Guid.NewGuid();
        var anotherLodge = Guid.NewGuid();
        var reader = new CalendarGradeReader(false, 1,
            new HashSet<Guid> { ownLodge }, new HashSet<Guid>());
        Assert.True(reader.CanSee(BuildEvent(ownLodge, grade)));
        Assert.False(reader.CanSee(BuildEvent(anotherLodge, grade)));

        var inactiveReader = new CalendarGradeReader(false, 3,
            new HashSet<Guid>(), new HashSet<Guid> { anotherLodge });
        Assert.False(inactiveReader.CanSee(BuildEvent(anotherLodge, grade)));
    }

    [Fact]
    public void Institutional_general_event_with_organization_id_is_not_mistaken_for_lodge_event()
    {
        var institutionalOrganization = Guid.NewGuid();
        var reader = new CalendarGradeReader(false, 1,
            new HashSet<Guid> { Guid.NewGuid() }, new HashSet<Guid>());
        var institutionalEvent = BuildEvent(institutionalOrganization, null);
        institutionalEvent.ScopeType = CalendarCodes.ScopeType.GrandLodge;
        institutionalEvent.Visibility = CalendarCodes.Visibility.Institutional;
        Assert.True(reader.CanSee(institutionalEvent));
    }

    [Fact]
    public void Officer_with_current_assignment_can_see_all_degrees_only_in_own_lodge()
    {
        var lodge = Guid.NewGuid();
        var anotherLodge = Guid.NewGuid();
        var reader = new CalendarGradeReader(false, 1, new HashSet<Guid> { lodge }, new HashSet<Guid> { lodge });
        Assert.True(reader.CanSee(BuildEvent(lodge, CalendarCodes.Grade.Master)));
        Assert.False(reader.CanSee(BuildEvent(anotherLodge, CalendarCodes.Grade.Master)));
    }

    [Fact]
    public void Brother_from_another_lodge_cannot_see_graded_event_even_if_master()
    {
        var reader = new CalendarGradeReader(false, 3, new HashSet<Guid> { Guid.NewGuid() }, new HashSet<Guid>());
        Assert.False(reader.CanSee(BuildEvent(Guid.NewGuid(), CalendarCodes.Grade.Apprentice)));
    }

    [Fact]
    public void Unknown_grade_is_not_treated_as_general()
    {
        var reader = new CalendarGradeReader(false, 3, new HashSet<Guid> { Guid.NewGuid() }, new HashSet<Guid>());
        Assert.False(reader.CanSee(BuildEvent(null, "invalid_source_grade")));
    }

    [Fact]
    public void Historical_event_without_grade_is_general()
    {
        var reader = new CalendarGradeReader(false, 1, new HashSet<Guid>(), new HashSet<Guid>());
        Assert.True(reader.CanSee(BuildEvent(null, null)));
    }

    [Fact]
    public void Numeric_lodge_grade_is_not_directly_exposed_as_event_grade()
    {
        Assert.Null(CalendarCodes.Grade.FromLodgeGrade("all"));
        Assert.Equal(CalendarCodes.Grade.FellowCraft, CalendarCodes.Grade.FromLodgeGrade("fellowcraft"));
        Assert.Equal(CalendarCodes.Grade.Apprentice, CalendarCodes.Grade.FromCeremony("initiation"));
        Assert.Equal(CalendarCodes.Grade.Master, CalendarCodes.Grade.FromCeremony("exaltation"));
        Assert.False(CalendarCodes.Grade.IsValid("invalid_source_grade"));
    }

    private static InstitutionalCalendarEvent BuildEvent(Guid? lodge, string? grade)
        => new()
        {
            Title = "Evento de prueba",
            EventType = "lodge_meeting_day",
            Grade = grade,
            StartsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            OrganizationId = lodge,
            ScopeType = lodge is null ? CalendarCodes.ScopeType.Order : CalendarCodes.ScopeType.Lodge,
            Visibility = lodge is null ? CalendarCodes.Visibility.Institutional : CalendarCodes.Visibility.Lodge,
            Status = CalendarCodes.Status.Confirmed,
            SourceModule = "test",
            SourceEntityType = "test",
            SourceEntityId = Guid.NewGuid().ToString("N"),
            SourceControlled = true
        };
}
