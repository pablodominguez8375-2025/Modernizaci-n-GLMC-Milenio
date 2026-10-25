using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>Directorio privado de recomendantes, independiente del patrocinio.</summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidateRecommenderDirectoryHttpTests
{
    [Fact]
    public async Task Origin_search_allows_active_non_master_from_another_workshop_but_not_inactive_or_unprivileged()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();

        var host = new Organization {
            Name = "Taller receptor " + Guid.NewGuid().ToString("N"), Type = "workshop"
        };
        var remote = new Organization {
            Name = "Taller origen " + Guid.NewGuid().ToString("N"), Type = "workshop"
        };
        var sample = "Recom" + Guid.NewGuid().ToString("N")[..10];
        var eligiblePerson = new Person { FirstNames = sample, LastNames = "Compañera" };
        var inactivePerson = new Person { FirstNames = sample, LastNames = "Inactiva" };
        var eligible = new Member {
            Person = eligiblePerson, PersonId = eligiblePerson.Id, CurrentDegree = "fellow"
        };
        var inactive = new Member {
            Person = inactivePerson, PersonId = inactivePerson.Id, CurrentDegree = "master"
        };
        var membership = new Membership {
            Member = eligible, MemberId = eligible.Id, Organization = remote,
            OrganizationId = remote.Id, MembershipType = "regular",
            Status = MembershipCodes.MembershipStatus.Active
        };
        var inactiveMembership = new Membership {
            Member = inactive, MemberId = inactive.Id, Organization = remote,
            OrganizationId = remote.Id, MembershipType = "regular",
            Status = MembershipCodes.MembershipStatus.Active
        };
        var withdrawn = new InstitutionalStatusEvent {
            Member = inactive, MemberId = inactive.Id, Organization = remote,
            OrganizationId = remote.Id, EventType = MembershipCodes.InstitutionalStatus.Inactive,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2))
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.AddRange(host, remote, eligible, inactive, membership, inactiveMembership, withdrawn);
            await db.SaveChangesAsync(ct);
        }

        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", host.Id.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", "member");
        var route = "/api/insinuados/taller/organizaciones/" + host.Id + "/recomendantes";
        using var forbidden = await client.GetAsync(route + "?q=" + sample, ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);

        using var tooBroad = await client.GetAsync(route + "?q=Re", ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooBroad.StatusCode);
        using var wrongWorkshop = await client.GetAsync(
            "/api/insinuados/taller/organizaciones/" + remote.Id +
            "/recomendantes?q=" + sample, ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrongWorkshop.StatusCode);

        using var response = await client.GetAsync(route + "?q=" + sample, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var items = json.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(items, x => x.GetProperty("id").GetGuid() == eligible.Id);
        Assert.DoesNotContain(items, x => x.GetProperty("id").GetGuid() == inactive.Id);
        Assert.All(items, x => Assert.Equal(2, x.EnumerateObject().Count()));

        using var created = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes",
            new {
                organizationId = host.Id, firstNames = "Postulante",
                lastNames = "Sintético", rutOrInstitutionalId = Guid.NewGuid().ToString("N")[..12],
                originKind = "brother", recommenderMemberId = eligible.Id
            }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var requestId = createdJson.GetProperty("ceremonyRequestId").GetGuid();
        await using var check = factory.Services.CreateAsyncScope();
        var core = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var dossier = await core.CeremonyRequests.AsNoTracking()
            .SingleAsync(x => x.Id == requestId, ct);
        Assert.Equal(eligible.Id, dossier.CandidateRecommenderMemberId);
        Assert.Null(dossier.CandidateSponsorMemberId);
    }
}
