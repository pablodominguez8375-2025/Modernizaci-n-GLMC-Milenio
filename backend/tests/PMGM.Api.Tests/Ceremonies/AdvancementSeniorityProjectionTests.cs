using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Membership;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementSeniorityProjectionTests
{
    [Theory]
    [InlineData(2026, 2, 14, 0)]
    [InlineData(2026, 2, 15, 1)]
    [InlineData(2026, 10, 9, 8)]
    [InlineData(2028, 1, 15, 24)]
    public void CompleteMonths_UsesAnniversary_NotMonthNumber(int year, int month, int day, int expected)
    {
        var result = AdvancementSeniorityProjection.CompleteMonths(
            new DateOnly(2026, 1, 15), new DateOnly(year, month, day));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DateBeforeStart_ProducesZeroAndInvalidGradeStatus()
    {
        var start = new DateOnly(2026, 7, 1);
        var until = new DateOnly(2026, 6, 1);
        Assert.Equal(0, AdvancementSeniorityProjection.CompleteMonths(start, until));
        Assert.Equal("invalid_grade_start",
            AdvancementSeniorityProjection.Assess(start, until, [], []).ContinuityEvidenceStatus);
    }

    [Fact]
    public void ConsecutiveTransferredAndActiveMemberships_CoverInterval()
    {
        var start = new DateOnly(2024, 10, 1);
        var cutoff = new DateOnly(2026, 10, 9);
        var result = AdvancementSeniorityProjection.Assess(start, cutoff,
        [
            new(new DateOnly(2024, 8, 1), new DateOnly(2025, 5, 31),
                MembershipCodes.MembershipStatus.Transferred),
            new(new DateOnly(2025, 6, 1), null, MembershipCodes.MembershipStatus.Active)
        ], []);

        Assert.Equal("membership_dates_covered", result.ContinuityEvidenceStatus);
        Assert.True(result.MembershipDateCoverageComplete);
        Assert.False(result.HasInstitutionalInterruption);
        Assert.Equal(24, result.CompleteCalendarMonths);
    }

    [Fact]
    public void GapDuringTransfer_DoesNotCountAsDocumentedContinuity()
    {
        var result = AdvancementSeniorityProjection.Assess(
            new DateOnly(2024, 10, 1),
            new DateOnly(2026, 10, 9),
            [
                new(new DateOnly(2024, 5, 1), new DateOnly(2025, 5, 31), MembershipCodes.MembershipStatus.Transferred),
                new(new DateOnly(2025, 6, 5), null, MembershipCodes.MembershipStatus.Active)
            ], []);

        Assert.Equal("membership_gap", result.ContinuityEvidenceStatus);
        Assert.False(result.MembershipDateCoverageComplete);
    }

    [Fact]
    public void ActiveCurrentMembershipButInstitutionalWithdrawal_IsFlagged()
    {
        var result = AdvancementSeniorityProjection.Assess(
            new DateOnly(2024, 1, 1),
            new DateOnly(2026, 10, 9),
            [new(new DateOnly(2023, 12, 1), null, MembershipCodes.MembershipStatus.Active)],
            [new(new DateOnly(2025, 8, 12), MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal)]);

        Assert.Equal("institutional_interruption", result.ContinuityEvidenceStatus);
        Assert.True(result.HasInstitutionalInterruption);
        Assert.True(result.MembershipDateCoverageComplete);
    }

    [Fact]
    public void HistoricalMembershipWithoutStart_IsIndeterminate()
    {
        var result = AdvancementSeniorityProjection.Assess(
            new DateOnly(2024, 1, 1),
            new DateOnly(2026, 10, 9),
            [
                new(null, new DateOnly(2025, 1, 1), MembershipCodes.MembershipStatus.Closed),
                new(new DateOnly(2025, 1, 2), null, MembershipCodes.MembershipStatus.Active)
            ], []);

        Assert.Equal("indeterminate", result.ContinuityEvidenceStatus);
        Assert.False(result.MembershipDateCoverageComplete);
    }

    [Fact]
    public void TerminalClosedMembershipWithoutEnd_DoesNotCoverFuture()
    {
        var result = AdvancementSeniorityProjection.Assess(
            new DateOnly(2024, 1, 1),
            new DateOnly(2026, 10, 9),
            [new(new DateOnly(2023, 1, 1), null, MembershipCodes.MembershipStatus.Closed)], []);

        Assert.Equal("membership_gap", result.ContinuityEvidenceStatus);
    }

    [Fact]
    public void MissingMemberships_DoesNotInventContinuity()
    {
        var result = AdvancementSeniorityProjection.Assess(
            new DateOnly(2024, 1, 1),
            new DateOnly(2026, 10, 9), [], []);
        Assert.Equal("membership_gap", result.ContinuityEvidenceStatus);
        Assert.Equal(0, result.MembershipRecordCount);
    }
}
