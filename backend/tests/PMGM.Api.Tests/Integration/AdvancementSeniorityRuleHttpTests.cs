using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AdvancementSeniorityRuleHttpTests
{
    [Fact]
    public async Task GrandLodgeAdminCanVersionAndReadFutureSeniorityRuleWithoutAuthorizing()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        // Prueba aislada a futuro, sin activar valores de prueba en 2026.
        var effectiveFrom = new DateOnly(2098, 2, 1);
        Guid? createdRuleId = null;
        try
        {
            var invalid = await client.PostAsJsonAsync("/api/ceremonias/reglas/avance/antiguedad", new
            {
                ceremonyType = CeremonyCodes.Type.WageIncrease,
                effectiveFrom,
                minimumCompleteMonths = 0,
                sourceReference = "RES-TEST"
            }, cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

            var noAuthority = await client.PostAsJsonAsync("/api/ceremonias/reglas/avance/antiguedad", new
            {
                ceremonyType = CeremonyCodes.Type.WageIncrease,
                effectiveFrom,
                minimumCompleteMonths = 17,
                sourceReference = " "
            }, cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, noAuthority.StatusCode);

            var request = new
            {
                ceremonyType = CeremonyCodes.Type.WageIncrease,
                effectiveFrom,
                minimumCompleteMonths = 17,
                sourceReference = "RES-TEST-2098"
            };
            var createdResponse = await client.PostAsJsonAsync(
                "/api/ceremonias/reglas/avance/antiguedad", request, cancellationToken);
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
            var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            createdRuleId = created.GetProperty("ruleId").GetGuid();
            Assert.Equal(17, created.GetProperty("minimumCompleteMonths").GetInt32());
            Assert.False(created.GetProperty("institutionalContinuityCertified").GetBoolean());
            Assert.False(created.GetProperty("authorizesCeremony").GetBoolean());

            var duplicate = await client.PostAsJsonAsync(
                "/api/ceremonias/reglas/avance/antiguedad", request, cancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

            var historical = await client.GetAsync(
                $"/api/ceremonias/reglas/avance/antiguedad?ceremonyType={Uri.EscapeDataString(CeremonyCodes.Type.WageIncrease)}&asOf=2098-02-01",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, historical.StatusCode);
            var document = await historical.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.Equal(createdRuleId.Value, document.GetProperty("ruleId").GetGuid());
            Assert.Equal(createdRuleId.Value.ToString("N"), document.GetProperty("version").GetString());
            Assert.Equal(17, document.GetProperty("minimumCompleteMonths").GetInt32());
            Assert.False(document.GetProperty("authorizesCeremony").GetBoolean());
            Assert.Contains("no-store", historical.Headers.CacheControl?.ToString() ?? string.Empty);

            await using var verify = factory.Services.CreateAsyncScope();
            var dbVerify = verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var auditPresent = await dbVerify.AuditEvents.AsNoTracking().AnyAsync(
                x => x.EntityId == createdRuleId.Value.ToString() &&
                     x.Action == "ceremony.rule.advancement.seniority.versioned", cancellationToken);
            Assert.True(auditPresent);
        }
        finally
        {
            if (createdRuleId.HasValue)
            {
                await using var scope = factory.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
                await db.InstitutionalRuleSettings
                    .Where(x => x.Id == createdRuleId.Value)
                    .ExecuteDeleteAsync(CancellationToken.None);
            }
        }
    }
}
