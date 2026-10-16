using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.InstitutionalCalendar;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;
using PMGM.Api.Modules.Notifications;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CalendarNotificationRecipientPostgreSqlTests
{
    [Fact]
    public async Task Calendar_notice_requires_active_link_and_matching_degree_before_queue()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var ct = TestContext.Current.CancellationToken;
        var dbOptions = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options;
        var calendarOptions = new DbContextOptionsBuilder<CalendarDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new PmgmDbContext(dbOptions);
        await using var calendar = new CalendarDbContext(calendarOptions);
        await db.Database.MigrateAsync(ct);
        await calendar.Database.MigrateAsync(ct);

        var lodge = new Organization { Name = "Taller avisos sintéticos", Type = "lodge" };
        var person = new Person { FirstNames = "Persona", LastNames = "Sintética" };
        var member = new Member { Person = person, PersonId = person.Id, CurrentDegree = "apprentice" };
        db.Organizations.Add(lodge);
        db.Members.Add(member);
        await db.SaveChangesAsync(ct);

        var subject = $"qa-calendar-{Guid.NewGuid():N}";
        var issuer = $"https://identity.example.invalid/{Guid.NewGuid():N}";
        var linkId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO core.member_identity_links
                ("Id", "MemberId", "Issuer", "Subject", "CreatedAtUtc", "CreatedBySubject")
            VALUES ({linkId}, {member.Id}, {issuer}, {subject}, {now}, {"qa-calendar"})
            """, ct);

        var evt = new InstitutionalCalendarEvent
        {
            Title = "Instrucción sintética", EventType = "lodge_instruction_day",
            ScopeType = CalendarCodes.ScopeType.Lodge,
            Visibility = CalendarCodes.Visibility.Lodge,
            OrganizationId = lodge.Id, Grade = CalendarCodes.Grade.Apprentice,
            Status = CalendarCodes.Status.Confirmed,
            StartsAtUtc = now.AddDays(1), EndsAtUtc = now.AddDays(1).AddHours(1),
            SourceModule = "qa", SourceEntityType = "instruction",
            SourceEntityId = Guid.NewGuid().ToString("N")
        };
        calendar.CalendarEvents.Add(evt);
        await calendar.SaveChangesAsync(ct);

        var sender = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", issuer), new Claim("sub", "qa-sender")], "qa"));
        var reader = new CalendarGradeReader(false, 1,
            new HashSet<Guid> { lodge.Id }, new HashSet<Guid>());
        var visibility = new FixedGradeVisibilityService(reader);
        var request = CreateRequest(subject, evt.Id, NotificationCodes.Channel.Internal);

        Assert.True(await CalendarNotificationRecipientGuard.AuthorizeQueueAsync(
            request, sender, db, calendar, visibility, ct));
        Assert.False(await CalendarNotificationRecipientGuard.AuthorizeQueueAsync(
            CreateRequest(subject, evt.Id, NotificationCodes.Channel.Email),
            sender, db, calendar, visibility, ct));
        Assert.False(await CalendarNotificationRecipientGuard.AuthorizeQueueAsync(
            request with { RecipientEmail = "other@example.invalid" },
            sender, db, calendar, visibility, ct));
        Assert.False(await CalendarNotificationRecipientGuard.AuthorizeQueueAsync(
            request with { SourceEventId = Guid.NewGuid().ToString("D") },
            sender, db, calendar, visibility, ct));
        var wrongIssuer = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "https://untrusted.example.invalid"),
             new Claim("sub", "qa-sender")], "qa"));
        Assert.False(await CalendarNotificationRecipientGuard.AuthorizeQueueAsync(
            request, wrongIssuer, db, calendar, visibility, ct));

        evt.Grade = CalendarCodes.Grade.Master;
        await calendar.SaveChangesAsync(ct);
        Assert.False(await CalendarNotificationRecipientGuard.AuthorizeQueueAsync(
            request, sender, db, calendar, visibility, ct));

        evt.Grade = CalendarCodes.Grade.Apprentice;
        await calendar.SaveChangesAsync(ct);
        Assert.False(await CalendarNotificationRecipientGuard.AuthorizeQueueAsync(
            CreateRequest("unknown-subject", evt.Id, NotificationCodes.Channel.Internal),
            sender, db, calendar, visibility, ct));
        Assert.False(await CalendarNotificationRecipientGuard.AuthorizeQueueAsync(
            CreateRequest(subject, Guid.NewGuid(), NotificationCodes.Channel.Internal),
            sender, db, calendar, visibility, ct));

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE core.member_identity_links SET "RevokedAtUtc"={DateTimeOffset.UtcNow}
            WHERE "Id"={linkId}
            """, ct);
        Assert.False(await CalendarNotificationRecipientGuard.AuthorizeQueueAsync(
            request, sender, db, calendar, visibility, ct));
    }

    private static QueueNotificationRequest CreateRequest(string subject, Guid eventId, string channel)
        => new("calendar.event", null, "calendar.event", subject, null,
            [channel], new Dictionary<string, string?>(),
            $"qa-calendar:{Guid.NewGuid():N}", eventId.ToString("D"),
            "/calendar", false, null, false, null, "calendar_event", eventId.ToString("D"));

    private sealed class FixedGradeVisibilityService(CalendarGradeReader reader)
        : ICalendarGradeVisibilityService
    {
        public Task<CalendarGradeReader> ForUserAsync(
            ClaimsPrincipal user, CancellationToken cancellationToken)
            => Task.FromResult(reader);
    }
}
