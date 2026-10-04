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
using PMGM.Api.Modules.Core.Entities;
using Xunit;
namespace PMGM.Api.Tests.Integration;
[Collection(PostgresIntegrationCollection.Name)]
public sealed class DynamicAccessHttpTests
{
    private static readonly string? Connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory(string connection)
    {
        var root = new PmgmWebApplicationFactory(connection);
        return root.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddAuthentication(o => { o.DefaultAuthenticateScheme = Auth.Name; o.DefaultChallengeScheme = Auth.Name; o.DefaultForbidScheme = Auth.Name; }).AddScheme<AuthenticationSchemeOptions, Auth>(Auth.Name, _ => { })));
    }
    private static async Task<int> Version(HttpClient client, CancellationToken ct) => (await client.GetFromJsonAsync<JsonElement>("/api/system/access/catalog", ct)).GetProperty("version").GetInt32();
    [Fact]
    public async Task Persist_grant_scope_revoke_print_and_soft_delete_are_audited()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return; var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection); using var client = factory.CreateClient();
        var org = new Organization { Name = "Acceso QA", Type = "workshop" };
        await using (var scope = factory.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await db.Database.MigrateAsync(ct); db.Organizations.Add(org); await db.SaveChangesAsync(ct); }
        var code = "qa-" + Guid.NewGuid().ToString("N"); var version = await Version(client, ct);
        var response = await client.PostAsJsonAsync("/api/system/access/profiles", new { code, name = "Perfil QA", scope = "lodge", menuCodes = new[] { "personal" }, expectedVersion = version }, ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        version++; Assert.Equal(version, await Version(client, ct));
        var invalid = await client.PutAsJsonAsync($"/api/system/access/profiles/{code}/grants", new { grants = new[] { new { viewCode = "calendar", actions = new[] { "print" } } }, expectedVersion = version }, ct); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); Assert.Equal(version, await Version(client, ct));
        var valid = await client.PutAsJsonAsync($"/api/system/access/profiles/{code}/grants", new { grants = new[] { new { viewCode = "calendar", actions = new[] { "view", "print" } } }, expectedVersion = version }, ct); Assert.Equal(HttpStatusCode.OK, valid.StatusCode); version++;
        var assigned = await client.PostAsJsonAsync("/api/system/access/assignments", new { subject = "dynamic-ci", profileCode = code, organizationId = org.Id, effectiveFrom = "2020-01-01", effectiveTo = (string?)null, expectedVersion = version }, ct); Assert.Equal(HttpStatusCode.OK, assigned.StatusCode); version++;
        var cat = await assigned.Content.ReadFromJsonAsync<JsonElement>(ct); var id = cat.GetProperty("assignments").EnumerateArray().Single(a => a.GetProperty("profileCode").GetString() == code).GetProperty("id").GetGuid();
        async Task<bool> Granted(Guid? organization, string action = "print") { var json = await client.GetFromJsonAsync<JsonElement>($"/api/system/access/evaluate?viewCode=calendar&action={action}" + (organization is null ? "" : "&organizationId=" + organization), ct); return json.GetProperty("allowed").GetBoolean(); }
        Assert.True(await Granted(org.Id)); Assert.False(await Granted(Guid.NewGuid())); Assert.False(await Granted(null)); Assert.False(await Granted(org.Id, "edit"));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/system/access/print", null, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/system/access/assignments/{id}?expectedVersion={version}", ct)).StatusCode); version++; Assert.False(await Granted(org.Id));
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/system/access/profiles/{code}?expectedVersion={version}", ct)).StatusCode);
        await using var check = factory.Services.CreateAsyncScope(); var dbCheck = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.True(await dbCheck.AuditEvents.AnyAsync(a => a.Action == "system.access.review_print_requested", ct));
        Assert.True(await dbCheck.DynamicAccessSnapshots.AnyAsync(s => s.Version == version + 1 && s.Payload.Contains(code), ct));
    }
    [Fact]
    public async Task Parallel_stale_writers_preserve_one_committed_snapshot_and_protected_profiles()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return; var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection); using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
        var version = await Version(client, ct);
        Task<HttpResponseMessage> Create(string code) => client.PostAsJsonAsync("/api/system/access/profiles", new { code, name = "Concurrente QA", scope = "order", menuCodes = Array.Empty<string>(), expectedVersion = version }, ct);
        var responses = await Task.WhenAll(Create("qa-" + Guid.NewGuid().ToString("N")), Create("qa-" + Guid.NewGuid().ToString("N")));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK)); Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict)); Assert.Equal(version + 1, await Version(client, ct));
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/system/access/profiles/system-0?expectedVersion={version + 1}", ct)).StatusCode); Assert.Equal(version + 1, await Version(client, ct));
    }
    [Fact]
    public async Task Treasurer_and_anonymous_cannot_administer_catalog_or_print()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return; var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection); using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Dynamic-Role", InstitutionalRoles.TallerTesoreria);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/system/access/catalog", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/system/access/print", null, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/system/access/profiles", new { code = "qa-test", name = "QA", scope = "order", menuCodes = Array.Empty<string>(), expectedVersion = 0 }, ct)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Dynamic-Anonymous", "true"); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/system/access/catalog", ct)).StatusCode);
    }
    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "DynamicAccess-CI";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey("X-Dynamic-Anonymous")) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[] { new Claim("sub", "dynamic-ci"), new Claim(InstitutionalClaims.Scope, "order"), new Claim(InstitutionalClaims.Role, Request.Headers["X-Dynamic-Role"].FirstOrDefault() ?? InstitutionalRoles.GranLogiaAdmin) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Name)), Name)));
        }
    }
}
