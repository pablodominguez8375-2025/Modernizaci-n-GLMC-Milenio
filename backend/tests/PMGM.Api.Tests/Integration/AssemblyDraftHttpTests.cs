using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyDraftHttpTests
{
    [Fact]
    public async Task Grand_secretariat_creates_audited_draft_but_unrelated_roles_cannot_create_or_list()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(cs)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<RegimenInteriorDbContext>();
                services.RemoveAll<DbContextOptions<RegimenInteriorDbContext>>();
                services.AddDbContext<RegimenInteriorDbContext>(o => o.UseNpgsql(cs));
                services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = AssemblyDraftTestAuth.SchemeName;
                    o.DefaultChallengeScheme = AssemblyDraftTestAuth.SchemeName;
                    o.DefaultForbidScheme = AssemblyDraftTestAuth.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, AssemblyDraftTestAuth>(
                    AssemblyDraftTestAuth.SchemeName, _ => { });
            }));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
            await scope.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>()
                .Database.MigrateAsync(ct);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Assembly-Role", InstitutionalRoles.GranSecretaria);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var request = new
        {
            name = "Asamblea de ensayo " + suffix,
            meetingDate = "2026-12-20",
            cutoffDate = "2026-10-19"
        };
        using var invalid = await client.PostAsJsonAsync(
            "/api/regimen-interior/asambleas",
            new
            {
                name = "Invalida",
                meetingDate = "2026-10-19",
                cutoffDate = "2026-12-20"
            }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var created = await client.PostAsJsonAsync(
            "/api/regimen-interior/asambleas", request, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var assemblyId = json.GetProperty("id").GetGuid();
        Assert.True(json.GetProperty("provisional").GetBoolean());
        Assert.False(json.GetProperty("mayVoteConfirmed").GetBoolean());
        Assert.False(json.GetProperty("officiallyClosed").GetBoolean());
        Assert.Equal("draft", json.GetProperty("status").GetString());
        Assert.EndsWith($"/asambleas/{assemblyId}/revision",
            created.Headers.Location?.ToString(), StringComparison.Ordinal);

        using var listings = await client.GetAsync("/api/regimen-interior/asambleas", ct);
        Assert.Equal(HttpStatusCode.OK, listings.StatusCode);
        var body = await listings.Content.ReadAsStringAsync(ct);
        Assert.Contains(assemblyId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RutAtCutoff", body, StringComparison.OrdinalIgnoreCase);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var app = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var ri = scope.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>();
            Assert.True(await ri.Assemblies.AnyAsync(x => x.Id == assemblyId && x.Status == "draft", ct));
            Assert.True(await app.AuditEvents.AnyAsync(x =>
                x.Action == "regimen.asamblea.draft.created" &&
                x.EntityId == assemblyId.ToString("D"), ct));
        }

        client.DefaultRequestHeaders.Remove("X-Assembly-Role");
        client.DefaultRequestHeaders.Add("X-Assembly-Role", InstitutionalRoles.GranTesoreria);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/regimen-interior/asambleas", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/regimen-interior/asambleas", request, ct)).StatusCode);
    }

    private sealed class AssemblyDraftTestAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "AssemblyDraftSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Assembly-Role"].ToString();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "assembly-draft-test"),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, role)
            ], SchemeName));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
