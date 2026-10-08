using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class InstitutionalStatusHttpWorkflowTests
{
    [Fact]
    public async Task Past_active_keeps_roster_sleep_closes_segment_and_reinstatement_creates_new_segment()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid memberId; Guid sourceId; Guid destinationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var source = new Organization { Name = $"Estado origen {suffix}", Number = $"EO{suffix[..2]}", Type = "workshop" };
            var destination = new Organization { Name = $"Estado destino {suffix}", Number = $"ED{suffix[..2]}", Type = "workshop" };
            var person = new Person { FirstNames = "Hermano", LastNames = $"Estado {suffix}" };
            var member = new Member { Person = person, InstitutionalNumber = $"EST-{suffix}" };
            var membership = new Membership { Member = member, Organization = source, MembershipType = "regular",
                StartDate = new DateOnly(2026, 1, 1), Status = MembershipCodes.MembershipStatus.Active };
            db.AddRange(source, destination, person, member, membership);
            await db.SaveChangesAsync(ct);
            memberId = member.Id; sourceId = source.Id; destinationId = destination.Id;
        }

        async Task<HttpResponseMessage> Transition(string eventType, DateOnly date, Guid workshop) =>
            await client.PostAsJsonAsync($"/api/regimen-interior/talleres/{workshop}/members/{memberId}/institutional-status", new
            {
                eventType, effectiveDate = date,
                reason = "Flujo CI", evidenceReference = $"CI-{eventType}", notes = "Dato sintético"
            }, ct);

        Assert.Equal(HttpStatusCode.Created, (await Transition("past_active", new DateOnly(2026, 2, 1), sourceId)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var current = await db.Memberships.SingleAsync(x => x.MemberId == memberId && x.Status == MembershipCodes.MembershipStatus.Active, ct);
            Assert.Equal(sourceId, current.OrganizationId);
            Assert.Null(current.EndDate);
        }

        Assert.Equal(HttpStatusCode.Created, (await Transition("active", new DateOnly(2026, 3, 1), sourceId)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Transition("voluntary_withdrawal", new DateOnly(2026, 4, 1), sourceId)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var source = await db.Memberships.SingleAsync(x => x.MemberId == memberId && x.OrganizationId == sourceId, ct);
            Assert.Equal(MembershipCodes.MembershipStatus.Closed, source.Status);
            Assert.Equal(new DateOnly(2026, 3, 31), source.EndDate);
            Assert.Equal(MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal, source.EndReason);
        }

        Assert.Equal(HttpStatusCode.Created, (await Transition("reinstated", new DateOnly(2026, 5, 1), destinationId)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var segments = await db.Memberships.Where(x => x.MemberId == memberId).OrderBy(x => x.StartDate).ToListAsync(ct);
            Assert.Equal(2, segments.Count);
            Assert.Equal(sourceId, segments[0].OrganizationId);
            Assert.Equal(destinationId, segments[1].OrganizationId);
            Assert.Equal(MembershipCodes.MembershipStatus.Active, segments[1].Status);
            Assert.Equal(4, await db.InstitutionalStatusEvents.CountAsync(x => x.MemberId == memberId, ct));
            Assert.Equal(4, await db.AuditEvents.CountAsync(x => x.Action == "membership.institutional_status.recorded" &&
                (x.OrganizationId == sourceId || x.OrganizationId == destinationId), ct));
        }
    }
}
