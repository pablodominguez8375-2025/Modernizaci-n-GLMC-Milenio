using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.LodgeManagement;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementAttendanceProjectionTests
{
    [Theory]
    [InlineData(CeremonyCodes.Type.WageIncrease, LodgeManagementCodes.Grade.Apprentice, "initiation")]
    [InlineData(CeremonyCodes.Type.Exaltation, LodgeManagementCodes.Grade.Fellowcraft, "wage_increase")]
    public void TransitionsUseSourceGradeAndItsInstitutionalStartEvent(string ceremony, string grade, string start)
    {
        Assert.Equal(grade, AdvancementAttendanceProjection.SourceGrade(ceremony));
        Assert.Equal(start, AdvancementAttendanceProjection.StartEvent(ceremony));
    }

    [Fact]
    public void InitiationNeverRequiresAdvancementProjection()
    {
        Assert.Null(AdvancementAttendanceProjection.SourceGrade(CeremonyCodes.Type.Initiation));
        Assert.Null(AdvancementAttendanceProjection.StartEvent(CeremonyCodes.Type.Initiation));
    }

    [Fact]
    public void LaterAttendanceCorrectionOverridesEarlierPresence()
    {
        var meeting = Guid.NewGuid();
        var otherMeeting = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var facts = new[]
        {
            new AttendanceEvent(Guid.NewGuid(), meeting, LodgeManagementCodes.AttendanceStatus.Present, now.AddHours(-1)),
            new AttendanceEvent(Guid.NewGuid(), meeting, LodgeManagementCodes.AttendanceStatus.Excused, now),
            new AttendanceEvent(Guid.NewGuid(), otherMeeting, LodgeManagementCodes.AttendanceStatus.Present, now)
        };

        var result = AdvancementAttendanceProjection.Count(new[] { meeting, otherMeeting }, facts);

        Assert.Equal(2, result.Held);
        Assert.Equal(2, result.Recorded);
        Assert.Equal(1, result.Present);
        Assert.Equal(1, result.Excused);
        Assert.Equal(0, result.Absent);
        Assert.Equal(new[] { otherMeeting }, result.PresentIds);
    }

    [Fact]
    public void ExcusedAndAbsentNeverCountAsPresent()
    {
        var excused = Guid.NewGuid();
        var absent = Guid.NewGuid();
        var notRecorded = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var result = AdvancementAttendanceProjection.Count(
            new[] { excused, absent, notRecorded },
            new[]
            {
                new AttendanceEvent(Guid.NewGuid(), excused, LodgeManagementCodes.AttendanceStatus.Excused, now),
                new AttendanceEvent(Guid.NewGuid(), absent, LodgeManagementCodes.AttendanceStatus.Absent, now)
            });

        Assert.Equal(3, result.Held);
        Assert.Equal(2, result.Recorded);
        Assert.Equal(0, result.Present);
        Assert.Equal(1, result.Excused);
        Assert.Equal(1, result.Absent);
        Assert.Empty(result.PresentIds);
    }

    [Fact]
    public void UnrelatedActivityAndOldEntriesAreIgnored()
    {
        var included = Guid.NewGuid();
        var outside = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var result = AdvancementAttendanceProjection.Count(
            new[] { included },
            new[]
            {
                new AttendanceEvent(Guid.NewGuid(), included, LodgeManagementCodes.AttendanceStatus.Absent, now.AddMinutes(-5)),
                new AttendanceEvent(Guid.NewGuid(), included, LodgeManagementCodes.AttendanceStatus.Present, now),
                new AttendanceEvent(Guid.NewGuid(), outside, LodgeManagementCodes.AttendanceStatus.Present, now)
            });

        Assert.Equal(1, result.Held);
        Assert.Equal(1, result.Present);
        Assert.Equal(0, result.Absent);
        Assert.Single(result.PresentIds);
        Assert.Equal(included, result.PresentIds[0]);
    }

    [Fact]
    public void DuplicateActivityIdsDoNotInflateEligibleMeetingCount()
    {
        var same = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var result = AdvancementAttendanceProjection.Count(
            new[] { same, same },
            new[] { new AttendanceEvent(Guid.NewGuid(), same, LodgeManagementCodes.AttendanceStatus.Present, now) });

        Assert.Equal(1, result.Held);
        Assert.Equal(1, result.Present);
    }
}
