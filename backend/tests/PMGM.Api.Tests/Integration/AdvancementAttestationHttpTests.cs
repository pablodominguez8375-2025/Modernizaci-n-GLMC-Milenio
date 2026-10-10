using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AdvancementAttestationHttpTests
{
    [Fact]
    public async Task MissingPresentationEvidenceNeverCountsAndCannotAuthorizeAscension()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connection);
        using var client = factory.CreateClient();
        var person = new Person { FirstNames = "Integración", LastNames = "Ascensos" };
        var org = new Organization { Name = "Taller CI de elegibilidad", Type = "workshop" };
        var member = new Member { PersonId = person.Id, Person = person, CurrentDegree = "apprentice" };
        var request = new CeremonyRequest
        {
            OrganizationId = org.Id, Organization = org, MemberId = member.Id, Member = member,
            CeremonyType = CeremonyCodes.Type.WageIncrease,
            Status = CeremonyCodes.RequestStatus.UnderReview
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.CeremonyRequests.Add(request);
            await db.SaveChangesAsync(ct);
        }
        try
        {
            var list = await client.GetAsync(
                $"/api/ceremonias/solicitudes/{request.Id}/avance/constancias", ct);
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.True(list.Headers.CacheControl?.NoStore);
            var parsed = await list.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Empty(parsed.GetProperty("records").EnumerateArray());

            var unknownKind = await client.PostAsJsonAsync(
                $"/api/ceremonias/solicitudes/{request.Id}/avance/constancias",
                new
                {
                    workPaperDocumentId = Guid.NewGuid(),
                    workPaperVersionId = Guid.NewGuid(),
                    meetingId = Guid.NewGuid(),
                    extractVersionId = Guid.NewGuid(),
                    fullMinuteVersionId = Guid.NewGuid(),
                    workKind = "forged_third_category",
                    presentationDate = new DateOnly(2026, 9, 1)
                }, ct);
            Assert.Equal(HttpStatusCode.BadRequest, unknownKind.StatusCode);

            var eligibility = await client.GetAsync(
                $"/api/ceremonias/solicitudes/{request.Id}/elegibilidad", ct);
            Assert.Equal(HttpStatusCode.OK, eligibility.StatusCode);
            var body = await eligibility.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.False(body.GetProperty("canAuthorize").GetBoolean());

            var authorize = await client.PostAsync(
                $"/api/ceremonias/solicitudes/{request.Id}/autorizar", null, ct);
            Assert.Equal(HttpStatusCode.Conflict, authorize.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            Assert.False(await db.AdvancementPaperAttestations.AsNoTracking()
                .AnyAsync(x => x.CeremonyRequestId == request.Id, ct));
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.CeremonyValidations.Where(x => x.CeremonyRequestId == request.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.CeremonyRequests.Where(x => x.Id == request.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.Members.Where(x => x.Id == member.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.People.Where(x => x.Id == person.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.Organizations.Where(x => x.Id == org.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
        }
    }
}
