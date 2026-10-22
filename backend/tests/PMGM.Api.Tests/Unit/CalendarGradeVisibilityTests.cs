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
    [InlineData("apprentice", 1, true)]
    [InlineData("fellow_craft", 1, false)]
    [InlineData("master", 1, false)]
    [InlineData("apprentice", 2, false)]
    [InlineData("fellow_craft", 2, true)]
    [InlineData("master", 2, false)]
    [InlineData("apprentice", 3, false)]
    [InlineData("fellow_craft", 3, false)]
    [InlineData("master", 3, true)]
    public void Instruction_is_visible_only_to_own_exact_degree(
        string grade, int effectiveDegree, bool expected)
    {
        var lodge = Guid.NewGuid();
        var reader = new CalendarGradeReader(false, effectiveDegree,
            new HashSet<Guid> { lodge }, new HashSet<Guid>());
        var instruction = BuildEvent(lodge, grade);
        instruction.EventType = "lodge_instruction_day";
        Assert.Equal(expected, reader.CanSee(instruction));
    }

    [Fact]
    public void Current_officer_and_institutional_administrator_keep_instruction_access()
    {
        var lodge = Guid.NewGuid();
        var otherLodge = Guid.NewGuid();
        var instruction = BuildEvent(lodge, CalendarCodes.Grade.Master);
        instruction.EventType = "lodge_instruction_day";
        var currentOfficer = new CalendarGradeReader(false, 1,
            new HashSet<Guid> { lodge }, new HashSet<Guid> { lodge });
        var expiredOfficer = new CalendarGradeReader(false, 1,
            new HashSet<Guid> { lodge }, new HashSet<Guid>());
        var administrator = new CalendarGradeReader(true, null,
            new HashSet<Guid>(), new HashSet<Guid>());
        Assert.True(currentOfficer.CanSee(instruction));
        Assert.False(expiredOfficer.CanSee(instruction));
        Assert.True(administrator.CanSee(instruction));
        instruction.OrganizationId = otherLodge;
        Assert.False(currentOfficer.CanSee(instruction));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("all")]
    [InlineData("invalid_source_grade")]
    public void Ungraded_or_invalid_instruction_is_never_general_for_regular_members(string? grade)
    {
        var lodge = Guid.NewGuid();
        var instruction = BuildEvent(lodge, grade);
        instruction.EventType = "lodge_instruction_day";
        foreach (var degree in new[] { 1, 2, 3 })
        {
            var regular = new CalendarGradeReader(false, degree,
                new HashSet<Guid> { lodge }, new HashSet<Guid>());
            Assert.False(regular.CanSee(instruction));
        }

        var officer = new CalendarGradeReader(false, 1,
            new HashSet<Guid> { lodge }, new HashSet<Guid> { lodge });
        var expiredOfficer = new CalendarGradeReader(false, 1,
            new HashSet<Guid> { lodge }, new HashSet<Guid>());
        var outsider = new CalendarGradeReader(false, 3,
            new HashSet<Guid>(), new HashSet<Guid> { lodge });
        var institutionalAdmin = new CalendarGradeReader(true, null,
            new HashSet<Guid>(), new HashSet<Guid>());

        Assert.True(officer.CanSee(instruction));
        Assert.False(expiredOfficer.CanSee(instruction));
        Assert.False(outsider.CanSee(instruction));
        Assert.True(institutionalAdmin.CanSee(instruction));
    }

    [Fact]
    public void Instruction_without_valid_lodge_is_not_revealed_by_other_active_membership()
    {
        var currentLodge = Guid.NewGuid();
        var instruction = BuildEvent(null, CalendarCodes.Grade.Apprentice);
        instruction.EventType = "lodge_instruction_day";
        var apprentice = new CalendarGradeReader(false, 1,
            new HashSet<Guid> { currentLodge }, new HashSet<Guid>());
        Assert.False(apprentice.CanSee(instruction));

        instruction.OrganizationId = Guid.NewGuid();
        Assert.False(apprentice.CanSee(instruction));

        // Una tenida general preserva su visibilidad anterior.
        instruction.EventType = "lodge_meeting_day";
        instruction.Grade = CalendarCodes.Grade.All;
        instruction.OrganizationId = currentLodge;
        Assert.True(apprentice.CanSee(instruction));
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
