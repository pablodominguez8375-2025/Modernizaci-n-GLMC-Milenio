using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CeremonyAuthorizationStateHttpTests
{
    [Fact]
    public async Task FailedAuthorizationLeavesCorrectableCaseOpenAndClosedCasesCannotBeReauthorized()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var organization = new Organization {
            Name = $"Taller CI Ciclo Ceremonias {Guid.NewGuid():N}", Type = "workshop" };
        var statuses = new[] {
            CeremonyCodes.RequestStatus.UnderReview,
            CeremonyCodes.RequestStatus.Rejected,
            CeremonyCodes.RequestStatus.Completed,
            CeremonyCodes.RequestStatus.Authorized
        };
        var ceremonies = statuses.Select(status => new CeremonyRequest {
            Organization = organization, OrganizationId = organization.Id,
            CeremonyType = CeremonyCodes.Type.Affiliation, Status = status
        }).ToArray();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Organizations.Add(organization);
            db.CeremonyRequests.AddRange(ceremonies);
            await db.SaveChangesAsync(ct);
        }

        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", organization.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Publication-Test-Scope", "order");
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.GranLogiaAdmin);

        var open = ceremonies[0];
        var missing = await client.PostAsync($"/api/ceremonias/solicitudes/{open.Id}/autorizar", null, ct);
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        var missingJson = await missing.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.False(missingJson.GetProperty("eligibility").GetProperty("canAuthorize").GetBoolean());

        foreach (var closed in ceremonies.Skip(1))
        {
            var response = await client.PostAsync($"/api/ceremonias/solicitudes/{closed.Id}/autorizar", null, ct);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal(CeremonyAuthorizationStatePolicy.RequirementCode, json.GetProperty("code").GetString());
            Assert.Equal(closed.Status, json.GetProperty("requestStatus").GetString());

            var get = await client.GetAsync($"/api/ceremonias/solicitudes/{closed.Id}/elegibilidad", ct);
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var eligibility = await get.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.False(eligibility.GetProperty("canAuthorize").GetBoolean());
            Assert.Contains(eligibility.GetProperty("requirements").EnumerateArray(),
                r => r.GetProperty("code").GetString() == CeremonyAuthorizationStatePolicy.RequirementCode);

            var matrix = await client.GetAsync($"/api/ceremonias/solicitudes/{closed.Id}/matriz-habilitacion", ct);
            Assert.Equal(HttpStatusCode.OK, matrix.StatusCode);
            var matrixJson = await matrix.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.False(matrixJson.GetProperty("canAuthorize").GetBoolean());
        }

        var queue = await client.GetAsync("/api/institutional/ceremonias/bandeja", ct);
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        var queueJson = await queue.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var rows = queueJson.GetProperty("items").EnumerateArray()
            .Where(x => ceremonies.Select(c => c.Id).Contains(x.GetProperty("requestId").GetGuid())).ToArray();
        Assert.Equal(ceremonies.Length, rows.Length);
        foreach (var closed in ceremonies.Skip(1))
        {
            var row = Assert.Single(rows, x => x.GetProperty("requestId").GetGuid() == closed.Id);
            Assert.False(row.GetProperty("eligibility").GetProperty("canAuthorize").GetBoolean());
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var persisted = await db.CeremonyRequests.AsNoTracking()
                .Where(x => ceremonies.Select(y => y.Id).Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Status, ct);
            Assert.Equal(CeremonyCodes.RequestStatus.Observed, persisted[open.Id]);
            foreach (var closed in ceremonies.Skip(1))
                Assert.Equal(closed.Status, persisted[closed.Id]);
            Assert.True(await db.AuditEvents.AsNoTracking().AnyAsync(
                x => x.Action == "ceremony.authorization.blocked" &&
                     x.EntityId == open.Id.ToString(), ct));
        }
    }
}
