namespace PMGM.Api.Tests;

/// <summary>
/// Test-only guard for scenarios that capture "today" (America/Santiago) and later send it to a server
/// rule that requires <c>date == Today()</c>. If the run starts inside the last seconds before Santiago
/// midnight, waits until just after midnight so the captured date cannot go stale mid-test.
/// Production code and its semantics are untouched; the clock and delay are injectable for unit tests.
/// </summary>
public static class SantiagoMidnightGuard
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan SettleAfterMidnight = TimeSpan.FromSeconds(2);

    public static TimeSpan RemainingUntilMidnight(DateTime utcNow)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
        var nextDay = local.Date.AddDays(1);
        // Cross-check with the real UTC offset at the next local midnight (DST-safe).
        var nextUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextDay, DateTimeKind.Unspecified), tz);
        return nextUtc - DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    public static async Task WaitIfNearMidnightAsync(
        TimeSpan? window = null,
        Func<DateTime>? utcNow = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        CancellationToken cancellationToken = default)
    {
        var clock = utcNow ?? (() => DateTime.UtcNow);
        var wait = delay ?? ((span, token) => Task.Delay(span, token));
        var remaining = RemainingUntilMidnight(clock());
        if (remaining <= window.GetValueOrDefault(DefaultWindow))
            await wait(remaining + SettleAfterMidnight, cancellationToken);
    }
}
