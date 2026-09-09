using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.GrandSecretariat.Entities;
using PMGM.Api.Modules.InstitutionalCalendar;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.LodgeManagement.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CalendarSourceSyncPostgreSqlTests
{
    [Fact]
    public async Task Reconciliation_projects_sources_once_and_updates_existing_events()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var mainOptions = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options;
        var secretariatOptions = new DbContextOptionsBuilder<GrandSecretariatDbContext>().UseNpgsql(connectionString).Options;
        var lodgeOptions = new DbContextOptionsBuilder<LodgeManagementDbContext>().UseNpgsql(connectionString).Options;
        var calendarOptions = new DbContextOptionsBuilder<CalendarDbContext>().UseNpgsql(connectionString).Options;

        await using var institutionalDb = new PmgmDbContext(mainOptions);
        await using var secretariatDb = new GrandSecretariatDbContext(secretariatOptions);
        await using var lodgeDb = new LodgeManagementDbContext(lodgeOptions);
        await using var calendarDb = new CalendarDbContext(calendarOptions);

        await institutionalDb.Database.MigrateAsync(cancellationToken);
        await secretariatDb.Database.MigrateAsync(cancellationToken);
        await lodgeDb.Database.MigrateAsync(cancellationToken);
        await calendarDb.Database.MigrateAsync(cancellationToken);

        var organizationId = Guid.NewGuid();
        institutionalDb.Organizations.Add(new PMGM.Api.Modules.Core.Entities.Organization
        {
            Id = organizationId,
            Name = "Taller QA Sync",
            Type = "lodge"
        });

        var ceremony = new CeremonyRequest
        {
            OrganizationId = organizationId,
            CeremonyType = CeremonyCodes.Type.Initiation,
            ProposedDate = new DateOnly(2026, 9, 20),
            Status = CeremonyCodes.RequestStatus.Authorized
        };
        institutionalDb.CeremonyRequests.Add(ceremony);
        await institutionalDb.SaveChangesAsync(cancellationToken);

        var space = new InstitutionalSpace
        {
            Code = $"QA-{Guid.NewGuid():N}",
            Name = "Templo QA",
            SpaceType = GrandSecretariatCodes.SpaceType.Temple,
            Status = GrandSecretariatCodes.SpaceStatus.Active
        };
        secretariatDb.InstitutionalSpaces.Add(space);
        var reservation = new InstitutionalSpaceReservation
        {
            SpaceId = space.Id,
            OrganizationId = organizationId,
            CeremonyRequestId = ceremony.Id,
            Purpose = "Ceremonia QA",
            StartsAtUtc = new DateTimeOffset(2026, 9, 20, 22, 0, 0, TimeSpan.Zero),
            EndsAtUtc = new DateTimeOffset(2026, 9, 21, 1, 0, 0, TimeSpan.Zero),
            Status = GrandSecretariatCodes.ReservationStatus.Reserved
        };
        secretariatDb.SpaceReservations.Add(reservation);
        await secretariatDb.SaveChangesAsync(cancellationToken);

        var meeting = new LodgeMeeting
        {
            OrganizationId = organizationId,
            MeetingDate = new DateOnly(2026, 9, 22),
            MeetingType = LodgeManagementCodes.MeetingType.Regular,
            Grade = LodgeManagementCodes.Grade.All,
            Status = LodgeManagementCodes.MeetingStatus.Scheduled
        };
        lodgeDb.LodgeMeetings.Add(meeting);
        await lodgeDb.SaveChangesAsync(cancellationToken);

        var service = new InstitutionalCalendarSourceSyncService(institutionalDb, secretariatDb, lodgeDb, calendarDb);
        var first = await service.ReconcileAsync(cancellationToken);
        var second = await service.ReconcileAsync(cancellationToken);

        Assert.True(first.Created >= 3);
        Assert.Equal(0, second.Created);
        Assert.True(second.Updated >= 3);

        Assert.Equal(1, await calendarDb.CalendarEvents.CountAsync(
            x => x.SourceModule == "grand-secretariat" &&
                 x.SourceEntityType == "space-reservation" &&
                 x.SourceEntityId == reservation.Id.ToString("N"), cancellationToken));
        Assert.Equal(1, await calendarDb.CalendarEvents.CountAsync(
            x => x.SourceModule == "ceremonies" &&
                 x.SourceEntityId == ceremony.Id.ToString("N"), cancellationToken));
        Assert.Equal(1, await calendarDb.CalendarEvents.CountAsync(
            x => x.SourceModule == "lodge-management" &&
                 x.SourceEntityType == "lodge-meeting" &&
                 x.SourceEntityId == meeting.Id.ToString("N"), cancellationToken));

        var ceremonyProjection = await calendarDb.CalendarEvents.AsNoTracking().SingleAsync(
            x => x.SourceModule == "ceremonies" && x.SourceEntityId == ceremony.Id.ToString("N"), cancellationToken);
        var santiago = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        Assert.Equal(new DateOnly(2026, 9, 20), DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(ceremonyProjection.StartsAtUtc, santiago).DateTime));
        Assert.Equal(new DateOnly(2026, 9, 21), DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(ceremonyProjection.EndsAtUtc, santiago).DateTime));

        ceremony.ProposedDate = null;
        institutionalDb.CeremonyRequests.Update(ceremony);
        await institutionalDb.SaveChangesAsync(cancellationToken);
        await service.ReconcileAsync(cancellationToken);

        var cancelledProjection = await calendarDb.CalendarEvents.AsNoTracking().SingleAsync(
            x => x.SourceModule == "ceremonies" && x.SourceEntityId == ceremony.Id.ToString("N"), cancellationToken);
        Assert.Equal(CalendarCodes.Status.Cancelled, cancelledProjection.Status);
    }
}
