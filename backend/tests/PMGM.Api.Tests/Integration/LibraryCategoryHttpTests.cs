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
public sealed class LibraryCategoryHttpTests
{
    [Fact]
    public async Task Librarian_manages_categories_and_others_only_read_active()
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
        {
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
            await scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>().Database.MigrateAsync(ct);
        }
        using var client = factory.CreateClient();
        void As(string role)
        {
            client.DefaultRequestHeaders.Remove("X-Role");
            client.DefaultRequestHeaders.Add("X-Role", role);
        }
        static string Unique() => "Cat " + Guid.NewGuid().ToString("N")[..8];

        As(InstitutionalRoles.Librarian);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/categorias", ct);
        Assert.True(list.GetProperty("canManage").GetBoolean());
        Assert.True(list.GetProperty("items").GetArrayLength() >= 4);

        var label = Unique();
        var created = await client.PostAsJsonAsync("/api/biblioteca/categorias", new { label }, ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var code = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("value").GetString()!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/biblioteca/categorias", new { label = label.ToUpperInvariant() }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/biblioteca/categorias", new { label = "x" }, ct)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/biblioteca/categorias/{code}", new { label = Unique() }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/biblioteca/categorias/book", new { active = false }, ct)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/biblioteca/categorias/{code}", new { active = false }, ct)).StatusCode);
        As(InstitutionalRoles.Communications);
        var visible = await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/categorias", ct);
        Assert.False(visible.GetProperty("canManage").GetBoolean());
        Assert.DoesNotContain(visible.GetProperty("items").EnumerateArray(), i => i.GetProperty("value").GetString() == code);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/biblioteca/categorias", new { label = Unique() }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/biblioteca/categorias/{code}", new { active = true }, ct)).StatusCode);

        As(InstitutionalRoles.Librarian);
        var all = (await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/categorias", ct)).GetProperty("items")
            .EnumerateArray().Select(i => i.GetProperty("value").GetString()!).Reverse().ToArray();
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/biblioteca/categorias/orden", new { codes = all }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/biblioteca/categorias/orden", new { codes = all.Skip(1).ToArray() }, ct)).StatusCode);
    }

    private sealed class SyntheticRoleAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "LibraryCategorySynthetic";
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
