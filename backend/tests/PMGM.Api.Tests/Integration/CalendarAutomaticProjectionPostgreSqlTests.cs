using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.InstitutionalCalendar;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CalendarAutomaticProjectionPostgreSqlTests
{
    [Fact]
    public async Task Ceremony_save_automatically_projects_to_calendar()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var calendarOptions = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var calendarDb = new CalendarDbContext(calendarOptions);
        await calendarDb.Database.MigrateAsync(cancellationToken);

        var interceptor = new CalendarSourceProjectionInterceptor(
            calendarDb,
            NullLogger<CalendarSourceProjectionInterceptor>.Instance);
        var mainOptions = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using var institutionalDb = new PmgmDbContext(mainOptions);
        await institutionalDb.Database.MigrateAsync(cancellationToken);

        var organization = new Organization
        {
            Name = $"Taller auto QA {Guid.NewGuid():N}",
            Type = "lodge"
        };
        institutionalDb.Organizations.Add(organization);
        var ceremony = new CeremonyRequest
        {
            OrganizationId = organization.Id,
            CeremonyType = CeremonyCodes.Type.Exaltation,
            ProposedDate = new DateOnly(2026, 10, 4),
            Status = CeremonyCodes.RequestStatus.UnderReview
        };
        institutionalDb.CeremonyRequests.Add(ceremony);

        await institutionalDb.SaveChangesAsync(cancellationToken);

        var projection = await calendarDb.CalendarEvents.AsNoTracking().SingleAsync(
            x => x.SourceModule == "ceremonies" &&
                 x.SourceEntityType == "ceremony-request" &&
                 x.SourceEntityId == ceremony.Id.ToString("N"),
            cancellationToken);

        Assert.Equal(CalendarCodes.Status.Tentative, projection.Status);
        Assert.Equal(CalendarCodes.Visibility.Restricted, projection.Visibility);
        Assert.False(projection.OccupancyOnlyWhenRestricted);
        Assert.Equal(organization.Id, projection.OrganizationId);
    }
}
