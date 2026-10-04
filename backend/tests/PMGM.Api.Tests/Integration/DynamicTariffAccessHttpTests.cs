using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Treasury;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class DynamicTariffAccessHttpTests
{
    [Fact]
    public async Task Order_readonly_creation_authority_and_revocation_are_enforced_on_real_decree_endpoints()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(connection);
        using var factory = root.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddAuthentication(o =>
        { o.DefaultAuthenticateScheme = Auth.Name; o.DefaultChallengeScheme = Auth.Name; o.DefaultForbidScheme = Auth.Name; })
            .AddScheme<AuthenticationSchemeOptions, Auth>(Auth.Name, _ => { })));
        using var admin = factory.CreateClient(); using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
        const string url = "/api/tesoreria/tarifarios/decretos";
        var subject = "tariff-" + Guid.NewGuid().ToString("N"); var code = "qa-" + Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("X-Tariff-Subject", subject);
        var before = await admin.GetFromJsonAsync<JsonElement>(url, ct);
        var tariffVersion = before.GetProperty("version").GetInt32();
        var version = (await admin.GetFromJsonAsync<JsonElement>("/api/system/access/catalog", ct)).GetProperty("version").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/system/access/profiles", new { code, name = "Consulta decreto QA", scope = "order", menuCodes = new[] { "treasury" }, expectedVersion = version++ }, ct)).StatusCode);
        async Task Grant(string[] actions) => Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/system/access/profiles/{code}/grants", new { grants = new[] { new { viewCode = "treasury", actions } }, expectedVersion = version++ }, ct)).StatusCode);
        await Grant(["view"]);
        var assigned = await admin.PostAsJsonAsync("/api/system/access/assignments", new { subject, profileCode = code, organizationId = (Guid?)null, effectiveFrom = "2020-01-01", effectiveTo = (string?)null, expectedVersion = version++ }, ct);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var assignment = (await assigned.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("assignments").EnumerateArray().Single(x => x.GetProperty("profileCode").GetString() == code).GetProperty("id").GetGuid();
        var projectionResponse = await client.GetAsync(url + "/acceso", ct);
        Assert.True(projectionResponse.Headers.CacheControl!.Private); Assert.True(projectionResponse.Headers.CacheControl.NoStore);
        var projection = await projectionResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(projection.GetProperty("managed").GetBoolean());
        Assert.Equal(new[] { "view" }, projection.GetProperty("actions").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.False(projection.TryGetProperty("subject", out _)); Assert.False(projection.TryGetProperty("assignments", out _));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url, ct)).StatusCode);
        var seed = GrandTreasuryTariffSeed.Load();
        var request = new RegisterTariffRequest(tariffVersion, "QA-PERM-" + Guid.NewGuid(), GrandTreasuryTariff.Today(), new DateOnly(2999, 2, 1), null,
            "Referencia sintética QA", "published", seed.Rates, seed.CeremonyRights, seed.Unemployment);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(url, request, ct)).StatusCode);
        Assert.Equal(tariffVersion, (await admin.GetFromJsonAsync<JsonElement>(url, ct)).GetProperty("version").GetInt32());
        await Grant(["view", "create"]);
        client.DefaultRequestHeaders.Add("X-Tariff-Role", InstitutionalRoles.TallerTesoreria);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url + "/acceso", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(url, request, ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Tariff-Role");
        var created = await client.PostAsJsonAsync(url, request, ct); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = (await created.Content.ReadFromJsonAsync<TariffVersionDto>(ct))!;
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/system/access/assignments/{assignment}?expectedVersion={version}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(url, request with { ExpectedVersion = item.Version }, ct)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>(url + "/acceso", ct)).GetProperty("actions").EnumerateArray());
        await using var check = factory.Services.CreateAsyncScope(); var db = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal(1, await db.GrandTreasuryTariffVersions.CountAsync(x => x.Number == request.Number, ct));
        Assert.True(await db.AuditEvents.AnyAsync(x => x.Action == "treasury.tariff.version_registered" && x.EntityId == item.Id.ToString(), ct));
    }

    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "TariffAccess-CI";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim("sub", Request.Headers["X-Tariff-Subject"].FirstOrDefault() ?? "tariff-admin-ci"),
                new Claim(InstitutionalClaims.Scope, "order"), new Claim(InstitutionalClaims.Role, Request.Headers["X-Tariff-Role"].FirstOrDefault() ?? InstitutionalRoles.GranLogiaAdmin) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Name)), Name)));
        }
    }
}
