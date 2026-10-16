using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.InstitutionalCalendar;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CalendarGradeAccessPostgreSqlTests
{
    [Fact]
    public async Task Office_access_requires_active_membership_and_dated_current_assignment()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new PmgmDbContext(options);
        await db.Database.MigrateAsync(ct);

        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Santiago")).DateTime);
        var org = new Organization { Name = "Taller prueba grados", Type = "lodge" };
        var person = new Person { FirstNames = "Hermano", LastNames = "Sintético" };
        var member = new Member { Person = person, PersonId = person.Id, CurrentDegree = "apprentice" };
        var membership = new Membership
        {
            Member = member, MemberId = member.Id,
            Organization = org, OrganizationId = org.Id,
            MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active,
            StartDate = localToday.AddDays(-30)
        };
        var office = new OfficeAssignment
        {
            Member = member, MemberId = member.Id,
            Organization = org, OrganizationId = org.Id,
            OfficeType = "lodge_treasury", Period = "2026",
            StartDate = localToday.AddDays(-7)
        };
        db.Organizations.Add(org);
        db.Members.Add(member);
        db.Memberships.Add(membership);
        db.OfficeAssignments.Add(office);
        await db.SaveChangesAsync(ct);

        var claim = new Claim(InstitutionalClaims.Organization, org.Id.ToString());
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { claim }, "test"));
        var service = new CalendarGradeVisibilityService(
            db, new FixedResolver(member.Id, 1), new InstitutionalAccessService());

        var reader = await service.ForUserAsync(user, ct);
        Assert.True(reader.CanSee(Event(org.Id, CalendarCodes.Grade.Master)));

        // Un cargo vencido no permite ver grados superiores.
        office.EndDate = localToday.AddDays(-1);
        await db.SaveChangesAsync(ct);
        reader = await service.ForUserAsync(user, ct);
        Assert.False(reader.CanSee(Event(org.Id, CalendarCodes.Grade.Master)));
        Assert.True(reader.CanSee(Event(org.Id, CalendarCodes.Grade.Apprentice)));

        // La pertenencia cerrada revoca también visibilidad de eventos graduados.
        membership.Status = MembershipCodes.MembershipStatus.Closed;
        membership.EndDate = localToday.AddDays(-1);
        await db.SaveChangesAsync(ct);
        reader = await service.ForUserAsync(user, ct);
        Assert.False(reader.CanSee(Event(org.Id, CalendarCodes.Grade.Apprentice)));
    }

    private static InstitutionalCalendarEvent Event(Guid lodgeId, string grade) => new()
    {
        Title = "Tenida grado QA",
        EventType = "lodge_meeting_day",
        Grade = grade,
        OrganizationId = lodgeId,
        StartsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
        EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
        ScopeType = CalendarCodes.ScopeType.Lodge,
        Visibility = CalendarCodes.Visibility.Lodge,
        Status = CalendarCodes.Status.Confirmed,
        SourceModule = "qa",
        SourceEntityType = "meeting",
        SourceEntityId = Guid.NewGuid().ToString("N")
    };

    private sealed class FixedResolver(Guid memberId, int degree) : IInstitutionalMemberContextResolver
    {
        public Task<InstitutionalMemberContext?> ResolveAsync(
            ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult<InstitutionalMemberContext?>(new InstitutionalMemberContext(memberId, degree));
    }
}
