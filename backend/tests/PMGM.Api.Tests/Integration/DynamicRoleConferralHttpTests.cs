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
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>Perfiles institucionales sin desarrollo: un perfil del Máster confiere un cargo base dentro de su alcance.</summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class DynamicRoleConferralHttpTests
{
    [Fact]
    public async Task A_profile_created_by_the_Master_confers_a_librarian_only_within_its_grants()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(cs);
        using var factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = Auth.Name; o.DefaultChallengeScheme = Auth.Name; o.DefaultForbidScheme = Auth.Name;
                }).AddScheme<AuthenticationSchemeOptions, Auth>(Auth.Name, _ => { })));
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
        var subject = "confers-" + Guid.NewGuid().ToString("N");
        using var master = factory.CreateClient(); master.DefaultRequestHeaders.Add("X-Role", InstitutionalRoles.PlatformSuperAdmin);
        using var person = factory.CreateClient(); person.DefaultRequestHeaders.Add("X-Role", "reader"); person.DefaultRequestHeaders.Add("X-Subject", subject);
        async Task<int> Version() => (await master.GetFromJsonAsync<JsonElement>("/api/system/access/catalog", ct)).GetProperty("version").GetInt32();
        var label = "Categoría " + Guid.NewGuid().ToString("N")[..8];

        // Sin perfil, quien no es Bibliotecario no crea categorías.
        Assert.Equal(HttpStatusCode.Forbidden, (await person.PostAsJsonAsync("/api/biblioteca/categorias", new { label }, ct)).StatusCode);

        var code = "biblio-" + Guid.NewGuid().ToString("N")[..8]; var v = await Version();
        // Solo cargos permitidos y coherentes con el alcance.
        Assert.Equal(HttpStatusCode.BadRequest, (await master.PostAsJsonAsync("/api/system/access/profiles", new { code, name = code, scope = "order", menuCodes = new[] { "documents" }, baseRoles = new[] { InstitutionalRoles.GranTesoreria }, expectedVersion = v }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await master.PostAsJsonAsync("/api/system/access/profiles", new { code, name = code, scope = "own-lodge", menuCodes = new[] { "documents" }, baseRoles = new[] { InstitutionalRoles.Librarian }, expectedVersion = v }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await master.PostAsJsonAsync("/api/system/access/profiles", new { code, name = code, scope = "order", menuCodes = new[] { "documents" }, baseRoles = new[] { InstitutionalRoles.Librarian }, expectedVersion = v++ }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await master.PutAsJsonAsync($"/api/system/access/profiles/{code}/grants", new { grants = new[] { new { viewCode = "library", actions = new[] { "view", "create", "write" } } }, expectedVersion = v++ }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await master.PostAsJsonAsync("/api/system/access/assignments", new { subject, profileCode = code, organizationId = (Guid?)null, effectiveFrom = "2020-01-01", effectiveTo = (string?)null, expectedVersion = v }, ct)).StatusCode);

        // Con el perfil asignado, el cargo conferido le permite crear categorías.
        Assert.Equal(HttpStatusCode.OK, (await person.PostAsJsonAsync("/api/biblioteca/categorias", new { label }, ct)).StatusCode);
        // El perfil no concede otras áreas: Gestión Documental no está en sus vistas.
        Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync("/api/system/access/catalog", ct)).StatusCode);
        // El Máster no hereda cargos.
        Assert.Equal(HttpStatusCode.Forbidden, (await master.PostAsJsonAsync("/api/biblioteca/categorias", new { label = label + " M" }, ct)).StatusCode);
    }

    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "DynamicRoleConferralSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Role"].FirstOrDefault() ?? "reader";
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", Request.Headers["X-Subject"].FirstOrDefault() ?? role),
                new Claim(InstitutionalClaims.Scope, "order"), new Claim(InstitutionalClaims.Role, role)
            ], Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
        }
    }
}
