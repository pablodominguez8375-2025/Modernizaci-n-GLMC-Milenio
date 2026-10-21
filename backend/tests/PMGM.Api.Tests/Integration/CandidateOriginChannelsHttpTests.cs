using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidateOriginChannelsHttpTests
{
    [Fact]
    public async Task Social_media_source_persists_and_can_be_audited_on_correction()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var workshop = new Organization
        {
            Name = "Taller origen red social " + Guid.NewGuid().ToString("N"),
            Type = "workshop"
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Organizations.Add(workshop);
            await db.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", workshop.Id.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);
        using var created = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes",
            new
            {
                organizationId = workshop.Id, firstNames = "Persona", lastNames = "Sintética",
                rutOrInstitutionalId = Guid.NewGuid().ToString("N")[..12],
                originKind = "social_media"
            }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var requestId = json.GetProperty("ceremonyRequestId").GetGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var ceremony = await db.CeremonyRequests.AsNoTracking().SingleAsync(x => x.Id == requestId, ct);
            Assert.Equal("social_media", ceremony.CandidateOriginKind);
            Assert.Null(ceremony.CandidateRecommenderMemberId);
            Assert.Null(ceremony.CandidateOriginOtherDescription);
            Assert.Null(ceremony.CandidateSponsorMemberId);
        }

        using var invalid = await client.PutAsJsonAsync(
            "/api/insinuados/solicitudes/" + requestId + "/origen/corregir",
            new
            {
                originKind = "social_media", originOtherDescription = "Red social no permitida aquí",
                reason = "Ajuste de origen verificable con respaldo documental."
            }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        const string reason = "Se verificó que la persona llegó desde la página institucional.";
        using var corrected = await client.PutAsJsonAsync(
            "/api/insinuados/solicitudes/" + requestId + "/origen/corregir",
            new { originKind = "internet", reason }, ct);
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        var result = await corrected.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var correctionId = result.GetProperty("correctionId").GetGuid();
        await using var verify = factory.Services.CreateAsyncScope();
        var verifiedDb = verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var row = await verifiedDb.CeremonyRequests.AsNoTracking()
            .SingleAsync(x => x.Id == requestId, ct);
        Assert.Equal("internet", row.CandidateOriginKind);
        var history = await verifiedDb.CandidateOriginCorrections.AsNoTracking()
            .SingleAsync(x => x.Id == correctionId, ct);
        Assert.Equal("social_media", history.PreviousKind);
        Assert.Equal("internet", history.NewKind);
        Assert.Equal(reason, history.Reason);
    }
}
