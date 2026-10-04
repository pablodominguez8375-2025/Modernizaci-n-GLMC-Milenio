using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Treasury;
using Xunit;
namespace PMGM.Api.Tests.Integration;
[Collection(PostgresIntegrationCollection.Name)]
public sealed class GrandTreasuryTariffHttpTests
{
    [Fact]
    public async Task Seed_contract_future_registration_concurrency_and_audit_roundtrip()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connection);
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
        var url = "/api/tesoreria/tarifarios/decretos";
        var before = await client.GetFromJsonAsync<JsonElement>(url, ct);
        var version = before.GetProperty("version").GetInt32();
        var seed = GrandTreasuryTariffSeed.Load();
        var effective = new DateOnly(2999, 1, 1);
        var request = new RegisterTariffRequest(version, "QA-" + Guid.NewGuid(), GrandTreasuryTariff.Today(), effective, null,
            "PDF QA sintético", "published", [new("normal", "santiago", 25000, "CLP")], seed.CeremonyRights, seed.Unemployment);
        var created = await client.PostAsJsonAsync(url, request, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = (await created.Content.ReadFromJsonAsync<TariffVersionDto>(ct))!;
        Assert.Equal(version + 1, item.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(url, request, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, request with { ExpectedVersion = version + 1, EffectiveFrom = GrandTreasuryTariff.Today() }, ct)).StatusCode);
        var original = await client.GetFromJsonAsync<JsonElement>("/api/tesoreria/tarifario-cuotas?asOf=2026-01-01", ct);
        Assert.Equal(seed.Id, original.GetProperty("id").GetGuid());
        var future = await client.GetFromJsonAsync<JsonElement>("/api/tesoreria/tarifario-cuotas?asOf=2999-01-01", ct);
        Assert.Equal(item.Id, future.GetProperty("id").GetGuid());
        Assert.Equal(25000m, future.GetProperty("items")[0].GetProperty("amount").GetDecimal());
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.True(await db.AuditEvents.AnyAsync(x => x.Action == "treasury.tariff.version_registered" && x.EntityId == item.Id.ToString(), ct));
        Assert.Equal(21000m, GrandTreasuryTariff.ToDto(await db.GrandTreasuryTariffVersions.SingleAsync(x => x.Id == seed.Id, ct)).Rates[0].Amount);
    }
}
