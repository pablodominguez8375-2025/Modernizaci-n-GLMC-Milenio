using Xunit;

namespace PMGM.Api.Tests;

public sealed class SantiagoMidnightGuardTests
{
    // 2026-11-20 03:00:00Z == 2026-11-20 00:00:00 America/Santiago (UTC-3, summer time).
    private static readonly DateTime SantiagoMidnightUtc = new(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Remaining_time_is_measured_against_the_next_santiago_midnight()
    {
        Assert.Equal(TimeSpan.FromSeconds(4),
            SantiagoMidnightGuard.RemainingUntilMidnight(SantiagoMidnightUtc.AddSeconds(-4)));
        Assert.Equal(TimeSpan.FromHours(24),
            SantiagoMidnightGuard.RemainingUntilMidnight(SantiagoMidnightUtc));
    }

    [Fact]
    public async Task Waits_past_midnight_when_the_run_starts_inside_the_window()
    {
        TimeSpan? waited = null;
        await SantiagoMidnightGuard.WaitIfNearMidnightAsync(
            utcNow: () => SantiagoMidnightUtc.AddSeconds(-4),
            delay: (span, _) => { waited = span; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromSeconds(6), waited);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(-3600)]
    [InlineData(5)]
    public async Task Does_not_wait_outside_the_window(int secondsFromMidnight)
    {
        var waited = false;
        await SantiagoMidnightGuard.WaitIfNearMidnightAsync(
            utcNow: () => SantiagoMidnightUtc.AddSeconds(secondsFromMidnight),
            delay: (_, _) => { waited = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(waited);
    }

    [Fact]
    public async Task Window_boundary_is_inclusive()
    {
        var waited = false;
        await SantiagoMidnightGuard.WaitIfNearMidnightAsync(
            utcNow: () => SantiagoMidnightUtc.AddSeconds(-90),
            delay: (_, _) => { waited = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(waited);
    }

    [Fact]
    public void Remaining_time_is_correct_across_a_dst_change()
    {
        // Chile ends summer time on the first Saturday of April at 24:00 local (clocks go back at 00:00 Sunday).
        // Whatever the exact rule, the remaining time must equal the real UTC distance to the next local midnight.
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        for (var day = new DateTime(2027, 4, 1, 0, 0, 0, DateTimeKind.Utc); day < new DateTime(2027, 4, 10, 0, 0, 0, DateTimeKind.Utc); day = day.AddHours(5))
        {
            var remaining = SantiagoMidnightGuard.RemainingUntilMidnight(day);
            var local = TimeZoneInfo.ConvertTimeFromUtc(day, tz);
            var nextLocalMidnight = TimeZoneInfo.ConvertTimeFromUtc(day + remaining, tz);
            Assert.Equal(TimeSpan.Zero, nextLocalMidnight.TimeOfDay);
            Assert.Equal(local.Date.AddDays(1), nextLocalMidnight.Date);
        }
    }
}
