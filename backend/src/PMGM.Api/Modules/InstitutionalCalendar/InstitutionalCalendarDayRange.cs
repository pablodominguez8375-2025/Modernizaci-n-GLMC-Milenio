namespace PMGM.Api.Modules.InstitutionalCalendar;

public static class InstitutionalCalendarDayRange
{
    private static readonly TimeZoneInfo Santiago = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");

    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) For(DateOnly date)
        => (StartOfDayUtc(date), StartOfDayUtc(date.AddDays(1)));

    private static DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // Chilean daylight-saving changes can skip local midnight. Use the first
        // valid local instant on that date as its UTC boundary.
        while (Santiago.IsInvalidTime(localStart))
        {
            localStart = localStart.AddMinutes(1);
        }

        if (Santiago.IsAmbiguousTime(localStart))
        {
            // The earlier UTC occurrence is the true start of the local day.
            var earliestUtcOffset = Santiago.GetAmbiguousTimeOffsets(localStart).Max();
            return new DateTimeOffset(localStart, earliestUtcOffset).ToUniversalTime();
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(localStart, Santiago);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
