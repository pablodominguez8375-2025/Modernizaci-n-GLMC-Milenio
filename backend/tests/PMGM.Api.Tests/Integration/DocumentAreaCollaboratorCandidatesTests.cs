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
using PMGM.Api.Modules.UserAccounts;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class DocumentAreaCollaboratorCandidatesTests
{
    [Fact]
    public async Task Only_the_area_owner_lists_member_names_without_contact_data()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var identity = new Identity(true);
        using var factory = Factory(cs, identity);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
        using var client = factory.CreateClient();

        // Colaborador sin rol de responsable: prohibido en ambas áreas.
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.TallerTesoreria);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/biblioteca/colaboradores/candidatos", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/documentos/colaboradores/candidatos", ct)).StatusCode);

        // Bibliotecario: lista la Biblioteca, no Gestión Documental.
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/documentos/colaboradores/candidatos", ct)).StatusCode);
        // Máster/superadmin: pueden administrar los colaboradores de ambas áreas (PO 25-10).
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.PlatformSuperAdmin);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/documentos/colaboradores/candidatos", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/biblioteca/colaboradores", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/documentos/colaboradores", ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        var response = await client.GetAsync("/api/biblioteca/colaboradores/candidatos", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
        var text = await response.Content.ReadAsStringAsync(ct);
        using var json = JsonDocument.Parse(text);
        Assert.True(json.RootElement.GetProperty("available").GetBoolean());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        // Solo Hermanos habilitados; sin Máster, Superusuario, deshabilitados ni el propio solicitante.
        Assert.Equal(new[] { "Ana Ficticia", "Beto Ficticio" }, items.Select(x => x.GetProperty("name").GetString()).ToArray());
        Assert.All(items, x => Assert.Equal(2, x.EnumerateObject().Count()));
        Assert.DoesNotContain("example.invalid", text);
        Assert.DoesNotContain("actor-test", text);

        // Gran Archivero: lista Gestión Documental.
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.GrandArchivist);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/documentos/colaboradores/candidatos", ct)).StatusCode);

        await using var scope2 = factory.Services.CreateAsyncScope();
        var db = scope2.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var audits = await db.AuditEvents.Where(a => a.Action == "library.collaborator.candidates_viewed").ToListAsync(ct);
        Assert.NotEmpty(audits);
        Assert.All(audits, a => Assert.DoesNotContain("Ficticia", a.MetadataJson ?? ""));
    }

    [Fact]
    public async Task Falls_back_to_empty_list_when_identity_is_unavailable()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(cs, new Identity(false));
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        var json = await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/colaboradores/candidatos", ct);
        Assert.False(json.GetProperty("available").GetBoolean());
        Assert.Empty(json.GetProperty("items").EnumerateArray());
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory(string cs, Identity identity)
        => new PmgmWebApplicationFactory(cs).WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.AddSingleton<IAccountIdentityProvider>(identity);
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = Auth.Name; o.DefaultChallengeScheme = Auth.Name; o.DefaultForbidScheme = Auth.Name;
            }).AddScheme<AuthenticationSchemeOptions, Auth>(Auth.Name, _ => { });
        }));

    private sealed class Identity(bool configured) : IAccountIdentityProvider
    {
        public bool Configured => configured; public string Issuer => "https://identity.example.invalid/test";
        public Task<PreparedIdentity> PrepareDisabledAsync(AccountIdentity account, string password, CancellationToken ct) => throw new NotSupportedException();
        public Task EnableAsync(string subject, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveAsync(string subject, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccountUser>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AccountUser>>(
        [
            new("s-beto", "Beto Ficticio", "beto@example.invalid", true, "member"),
            new("s-ana", "Ana Ficticia", "ana@example.invalid", true, "member"),
            new("s-off", "Deshabilitado Ficticio", "off@example.invalid", false, "member"),
            new("s-super", "Soporte Ficticio", "soporte@example.invalid", true, "superuser"),
            new("s-master", "Master", "master@example.invalid", true, "platform"),
            new("actor-test", "Solicitante Ficticio", "yo@example.invalid", true, "member"),
        ]);
        public Task<AccountUser?> FindManagedAsync(string subject, CancellationToken ct) => Task.FromResult<AccountUser?>(null);
        public Task<AccountClaims?> GetClaimsAsync(string subject, CancellationToken ct) => Task.FromResult<AccountClaims?>(null);
        public Task SendPasswordRecoveryEmailAsync(string subject, CancellationToken ct) => throw new NotSupportedException();
        public Task SetTemporaryPasswordAsync(string subject, string password, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "CollaboratorCandidatesSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "actor-test"), new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, Request.Headers["X-Collab-Role"].ToString())
            ], Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Name)));
        }
    }
}
