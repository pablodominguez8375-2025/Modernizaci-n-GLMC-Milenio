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

/// <summary>#574 — «Ver como»: solo lectura, sesión propia y vigente, rutas permitidas, identidad del objetivo, auditoría del operador.</summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class SupportViewAsPostgreSqlTests
{
    private static readonly string Hermano = Guid.NewGuid().ToString();
    private static readonly string Admin = Guid.NewGuid().ToString();
    private static readonly string Superusuario = Guid.NewGuid().ToString();

    [Fact]
    public async Task Start_requires_master_reason_and_non_privileged_target()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = await CreateAsync(cs);
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-View-Role", InstitutionalRoles.GranLogiaAdmin);
        Assert.Equal(HttpStatusCode.Forbidden, (await Start(client, Hermano, "Revisar pantalla del Hermano", ct)).StatusCode);

        client.DefaultRequestHeaders.Remove("X-View-Role");
        client.DefaultRequestHeaders.Add("X-View-Role", InstitutionalRoles.PlatformSuperAdmin);
        Assert.Equal(HttpStatusCode.BadRequest, (await Start(client, Hermano, "corto", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Start(client, "no-es-guid", "Revisar pantalla del Hermano", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Start(client, "master-op", "Verme a mí mismo, prueba", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Start(client, Guid.NewGuid().ToString(), "Cuenta que no existe", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Start(client, Admin, "Ver como un administrador", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Start(client, Superusuario, "Ver como un superusuario", ct)).StatusCode);

        var ok = await Start(client, Hermano, "Revisar pantalla del Hermano", ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var json = await ok.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(json.GetProperty("readOnly").GetBoolean());
        Assert.Equal("Hermano Ficticio", json.GetProperty("targetName").GetString());
        var expires = json.GetProperty("expiresAtUtc").GetDateTimeOffset();
        Assert.InRange((expires - DateTimeOffset.UtcNow).TotalMinutes, 28, 30.1);
    }

    [Fact]
    public async Task View_as_is_read_only_allowlisted_and_shows_the_target_identity()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = await CreateAsync(cs);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-View-Role", InstitutionalRoles.PlatformSuperAdmin);

        var first = (await (await Start(client, Hermano, "Primera revisión de pantalla", ct)).Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("sessionId").GetString()!;
        var second = (await (await Start(client, Hermano, "Segunda revisión de pantalla", ct)).Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("sessionId").GetString()!;
        Assert.NotEqual(first, second);

        // La primera sesión quedó cerrada al abrir la segunda.
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/session/me", first, ct)).StatusCode);

        // Lectura permitida: la respuesta corresponde al Hermano, no al Máster.
        var me = await Get(client, "/api/session/me", second, ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal("active", me.Headers.GetValues("X-Support-View-As").Single());
        var text = await me.Content.ReadAsStringAsync(ct);
        Assert.Contains("Hermano Ficticio", text);
        Assert.DoesNotContain("platform_superadmin", text);

        // Escritura: bloqueada.
        var write = await Send(client, HttpMethod.Post, "/api/session/me", second, ct);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Contains("SUPPORT_VIEW_READ_ONLY", await write.Content.ReadAsStringAsync(ct));

        // Ruta fuera de la lista cerrada: bloqueada, aunque el Máster sí podría verla.
        var denied = await Get(client, "/api/system/user-accounts", second, ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("SUPPORT_VIEW_RESOURCE_DENIED", await denied.Content.ReadAsStringAsync(ct));

        // Sesión de otro operador: bloqueada.
        using var other = factory.CreateClient();
        other.DefaultRequestHeaders.Add("X-View-Role", InstitutionalRoles.PlatformSuperAdmin);
        other.DefaultRequestHeaders.Add("X-View-Sub", "master-otro");
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(other, "/api/session/me", second, ct)).StatusCode);

        // Terminar: la sesión deja de servir.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/system/support/view-as/{second}/end", null, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/session/me", second, ct)).StatusCode);

        // Expirada: bloqueada.
        var third = (await (await Start(client, Hermano, "Tercera revisión de pantalla", ct)).Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("sessionId").GetGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var s = await db.SupportViewSessions.FirstAsync(x => x.Id == third, ct);
            s.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync(ct);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/session/me", third.ToString(), ct)).StatusCode);

        // Auditoría: siempre el operador, nunca datos del objetivo.
        await using var scope2 = factory.Services.CreateAsyncScope();
        var audits = await scope2.ServiceProvider.GetRequiredService<PmgmDbContext>().AuditEvents
            .Where(a => a.Action.StartsWith("support.view_as")).ToListAsync(ct);
        Assert.Contains(audits, a => a.Action == "support.view_as.read");
        Assert.Contains(audits, a => a.Action == "support.view_as.denied");
        Assert.Contains(audits, a => a.Action == "support.view_as.ended");
        Assert.All(audits, a => Assert.Equal("master-op", a.ActorSubject));
        Assert.All(audits, a => Assert.DoesNotContain("example.invalid", a.MetadataJson ?? ""));
    }

    [Fact]
    public async Task Profile_view_is_read_only_synthetic_and_never_exposes_the_master_or_a_real_person()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = await CreateAsync(cs);
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-View-Role", InstitutionalRoles.GranLogiaAdmin);
        Assert.Equal(HttpStatusCode.Forbidden, (await StartProfile(client, "tesorero", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/system/support/view-as/profiles", ct)).StatusCode);

        client.DefaultRequestHeaders.Remove("X-View-Role");
        client.DefaultRequestHeaders.Add("X-View-Role", InstitutionalRoles.PlatformSuperAdmin);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/system/support/view-as/profiles", ct);
        Assert.Contains(list.GetProperty("items").EnumerateArray(), x => x.GetProperty("key").GetString() == "tesorero");
        Assert.Equal(HttpStatusCode.BadRequest, (await StartProfile(client, "platform_superadmin", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/system/support/view-as", new { profileKey = "tesorero", targetSubject = Hermano }, ct)).StatusCode);

        var ok = await StartProfile(client, "tesorero", ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var json = await ok.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(json.GetProperty("readOnly").GetBoolean());
        Assert.Equal("Tesorero · Taller 00", json.GetProperty("targetName").GetString());
        var sid = json.GetProperty("sessionId").GetString()!;

        var me = await Get(client, "/api/session/me", sid, ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var text = await me.Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("platform_superadmin", text);
        Assert.DoesNotContain("Hermano Ficticio", text);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Post, "/api/session/me", sid, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Delete, "/api/members/" + Guid.NewGuid(), sid, ct)).StatusCode);

        // Gran Logia: alcance de toda la Orden, solo la lista cerrada de rutas.
        var gl = await (await StartProfile(client, "gran-secretario", ct)).Content.ReadFromJsonAsync<JsonElement>(ct);
        var gsid = gl.GetProperty("sessionId").GetString()!;
        Assert.Equal("Gran Secretario", gl.GetProperty("targetName").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/session/me", gsid, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/system/user-accounts", gsid, ct)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var audits = await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().AuditEvents.Where(a => a.Action.StartsWith("support.view_as")).ToListAsync(ct);
        Assert.Contains(audits, a => a.Action == "support.view_as.started" && (a.MetadataJson ?? "").Contains("tesorero"));
        Assert.All(audits, a => Assert.Equal("master-op", a.ActorSubject));
    }

    private static Task<HttpResponseMessage> StartProfile(HttpClient c, string key, CancellationToken ct)
        => c.PostAsJsonAsync("/api/system/support/view-as", new { profileKey = key }, ct);
    private static Task<HttpResponseMessage> Start(HttpClient c, string target, string reason, CancellationToken ct)
        => c.PostAsJsonAsync("/api/system/support/view-as", new { targetSubject = target, reason }, ct);
    private static Task<HttpResponseMessage> Get(HttpClient c, string url, string session, CancellationToken ct) => Send(c, HttpMethod.Get, url, session, ct);
    private static Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method, string url, string session, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Support-View-As", session);
        return c.SendAsync(request, ct);
    }

    private static async Task<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>> CreateAsync(string cs)
    {
        var factory = new PmgmWebApplicationFactory(cs).WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.AddSingleton<IAccountIdentityProvider>(new Identity());
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = Auth.Name; o.DefaultChallengeScheme = Auth.Name; o.DefaultForbidScheme = Auth.Name;
            }).AddScheme<AuthenticationSchemeOptions, Auth>(Auth.Name, _ => { });
        }));
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(TestContext.Current.CancellationToken);
        return factory;
    }

    private sealed class Identity : IAccountIdentityProvider
    {
        public bool Configured => true; public string Issuer => "https://identity.example.invalid/test";
        public Task<PreparedIdentity> PrepareDisabledAsync(AccountIdentity account, string password, CancellationToken ct) => throw new NotSupportedException();
        public Task EnableAsync(string subject, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveAsync(string subject, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccountUser>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AccountUser>>([]);
        public Task<AccountUser?> FindManagedAsync(string subject, CancellationToken ct) => Task.FromResult<AccountUser?>(null);
        public Task<AccountClaims?> GetClaimsAsync(string subject, CancellationToken ct) => Task.FromResult<AccountClaims?>(subject switch
        {
            _ when subject == Hermano => new AccountClaims(Hermano, "Hermano Ficticio", "member", true, [InstitutionalRoles.TallerTesoreria], null, []),
            _ when subject == Admin => new AccountClaims(Admin, "Admin Ficticio", "member", true, [InstitutionalRoles.GranLogiaAdmin], "order", []),
            _ when subject == Superusuario => new AccountClaims(Superusuario, "Soporte Ficticio", "superuser", true, [], "order", []),
            _ => null
        });
        public Task SendPasswordRecoveryEmailAsync(string subject, CancellationToken ct) => throw new NotSupportedException();
        public Task SetTemporaryPasswordAsync(string subject, string password, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "SupportViewAsSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var sub = Request.Headers["X-View-Sub"].FirstOrDefault() ?? "master-op";
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", sub), new Claim("name", "Master Ficticio"), new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, Request.Headers["X-View-Role"].ToString())
            ], Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Name)));
        }
    }
}
