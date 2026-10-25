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
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>#588 — categorías persistentes de la Biblioteca: sólo el Bibliotecario las administra.</summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class DynamicProfilesMasterOnlyHttpTests
{
    [Fact]
    public async Task Only_the_Master_reads_creates_and_edits_dynamic_profiles()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(cs);
        using var factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = SyntheticRoleAuth.Name;
                    o.DefaultChallengeScheme = SyntheticRoleAuth.Name;
                    o.DefaultForbidScheme = SyntheticRoleAuth.Name;
                }).AddScheme<AuthenticationSchemeOptions, SyntheticRoleAuth>(SyntheticRoleAuth.Name, _ => { })));
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
        using var client = factory.CreateClient();
        void As(string role)
        {
            client.DefaultRequestHeaders.Remove("X-Role");
            client.DefaultRequestHeaders.Add("X-Role", role);
        }

        As(InstitutionalRoles.PlatformSuperAdmin);
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/system/access/catalog", ct);
        var version = catalog.GetProperty("version").GetInt32();
        Assert.Contains(catalog.GetProperty("menus").EnumerateArray().SelectMany(m => m.GetProperty("views").EnumerateArray()),
            v => v.GetProperty("code").GetString() == "officialdocuments");
        var code = "master-" + Guid.NewGuid().ToString("N")[..8];
        var created = await client.PostAsJsonAsync("/api/system/access/profiles",
            new { code, name = "Perfil del Master", scope = "order", menuCodes = new[] { "documents" }, expectedVersion = version }, ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        // Ningún otro perfil, ni siquiera el administrador de Gran Logia, lee ni modifica perfiles o asignaciones.
        foreach (var role in new[] { InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.Librarian, InstitutionalRoles.Communications })
        {
            As(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/system/access/catalog", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/system/access/profiles",
                new { code = code + "x", name = "Intruso", scope = "order", menuCodes = new[] { "documents" }, expectedVersion = version + 1 }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/system/access/profiles/{code}",
                new { name = "Cambiado", scope = "order", menuCodes = new[] { "documents" }, expectedVersion = version + 1 }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/system/access/profiles/{code}?expectedVersion={version + 1}", ct)).StatusCode);
        }
    }

    [Fact]
    public void Catalog_saved_before_a_view_existed_receives_it_without_touching_profiles()
    {
        var defaults = DynamicAccessEndpoints.CreateCatalog();
        var legacy = defaults with
        {
            Menus = defaults.Menus.Select(m => m with { Views = m.Views.Where(v => v.Code != "officialdocuments").ToList() }).ToList(),
            Profiles = [.. defaults.Profiles]
        };
        var merged = DynamicAccessEndpoints.WithMissingDefaults(legacy);
        Assert.Contains(merged.Menus.SelectMany(m => m.Views), v => v.Code == "officialdocuments");
        Assert.Equal(legacy.Profiles, merged.Profiles);
        Assert.Equal(legacy.Assignments, merged.Assignments);
    }

    private sealed class SyntheticRoleAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "DynamicProfilesMasterOnlySynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Role"].FirstOrDefault() ?? "reader";
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", role), new Claim("name", role),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, role)
            ], Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
        }
    }
}

