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
public sealed class AdvancementRuleHttpTests
{
    [Fact]
    public async Task GrandLodgeAdmin_CreatesAndReadsVersionedMinimumsWithAudit()
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

        // Fecha futura intencional: el caso no altera la regla real de 2026.
        var from = new DateOnly(2099, 1, 1);
        Guid? createdRuleId = null;
        try
        {
            var invalid = await client.PostAsJsonAsync("/api/ceremonias/reglas/avance", new
            {
                ceremonyType = CeremonyCodes.Type.Exaltation,
                effectiveFrom = from,
                minimumMeetingAttendance = -1,
                minimumInstructionAttendance = 3,
                minimumWorkPapers = 1,
                sourceReference = "ACTA-CI-2099"
            }, cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

            var noSource = await client.PostAsJsonAsync("/api/ceremonias/reglas/avance", new
            {
                ceremonyType = CeremonyCodes.Type.Exaltation,
                effectiveFrom = from,
                minimumMeetingAttendance = 5,
                minimumInstructionAttendance = 3,
                minimumWorkPapers = 1,
                sourceReference = " "
            }, cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, noSource.StatusCode);

            var request = new
            {
                ceremonyType = CeremonyCodes.Type.Exaltation,
                effectiveFrom = from,
                minimumMeetingAttendance = 5,
                minimumInstructionAttendance = 3,
                minimumWorkPapers = 1,
                sourceReference = "ACTA-CI-2099"
            };
            var create = await client.PostAsJsonAsync("/api/ceremonias/reglas/avance", request, cancellationToken);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            createdRuleId = created.GetProperty("ruleId").GetGuid();
            Assert.Equal(5, created.GetProperty("minimumMeetingAttendance").GetInt32());
            Assert.Equal("ACTA-CI-2099", created.GetProperty("sourceReference").GetString());
            Assert.False(created.TryGetProperty("memberId", out _));

            var duplicate = await client.PostAsJsonAsync("/api/ceremonias/reglas/avance", request, cancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

            var history = await client.GetAsync(
                "/api/ceremonias/reglas/avance?ceremonyType=exaltation&asOf=2099-01-01", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, history.StatusCode);
            var json = await history.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            Assert.Equal(createdRuleId.Value, json.GetProperty("ruleId").GetGuid());
            Assert.Equal(createdRuleId.Value.ToString("N"), json.GetProperty("version").GetString());
            Assert.Equal(3, json.GetProperty("minimumInstructionAttendance").GetInt32());
            Assert.Equal(1, json.GetProperty("minimumWorkPapers").GetInt32());
            Assert.Contains("no-store", history.Headers.CacheControl?.ToString() ?? string.Empty);

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var logged = await db.AuditEvents.AsNoTracking().AnyAsync(
                x => x.EntityId == createdRuleId.Value.ToString() &&
                     x.Action == "ceremony.rule.advancement.versioned", cancellationToken);
            Assert.True(logged);
        }
        finally
        {
            if (createdRuleId is not null)
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
