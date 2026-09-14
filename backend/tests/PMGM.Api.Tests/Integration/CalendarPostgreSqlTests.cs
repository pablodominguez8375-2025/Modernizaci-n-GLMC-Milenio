using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.InstitutionalCalendar;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CalendarPostgreSqlTests
{
    [Fact]
    public async Task Calendar_migration_and_source_identity_work_on_postgresql()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new CalendarDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        Assert.Contains("20260909112500_AddInstitutionalCalendar", applied);

        var sourceId = $"qa-{Guid.NewGuid():N}";
        var calendarEvent = new InstitutionalCalendarEvent
        {
            Title = "Evento QA",
            EventType = "qa",
            StartsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            ScopeType = CalendarCodes.ScopeType.Order,
            Visibility = CalendarCodes.Visibility.Institutional,
            Status = CalendarCodes.Status.Confirmed,
            SourceModule = "qa",
            SourceEntityType = "integration-test",
            SourceEntityId = sourceId,
            SourceControlled = true
        };
        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync(cancellationToken);

        var stored = await db.CalendarEvents.AsNoTracking()
            .SingleAsync(x => x.SourceModule == "qa" && x.SourceEntityType == "integration-test" && x.SourceEntityId == sourceId,
                cancellationToken);

        Assert.Equal(calendarEvent.Id, stored.Id);
        Assert.Equal("America/Santiago", stored.TimeZoneId);
        Assert.True(stored.SourceControlled);
    }
}
