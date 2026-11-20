using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.InitialLoadApply;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// #694 end-to-end Master flow on REAL PostgreSQL, SYNTHETIC CSV: preview → (denied before open) → open window →
/// apply → replay → read-back → close → (denied after close). The real handlers run against a real HttpContext
/// whose JWT scheme is replaced by a fake authenticator; nothing is mocked below the HTTP layer.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class InitialLoadMasterFlowPostgreSqlTests
{
    private const string Issuer = "https://kc.example.invalid/realms/centenario";
    private const string MasterSub = "master-synthetic-subject";
    private static string? Conn => Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
    private static readonly string Secret = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private sealed class FakeAuth(ClaimsPrincipal? principal) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(principal is null ? AuthenticateResult.NoResult()
                : AuthenticateResult.Success(new AuthenticationTicket(principal, JwtBearerDefaults.AuthenticationScheme)));
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        { context.Response.StatusCode = 401; return Task.CompletedTask; }
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        { context.Response.StatusCode = 403; return Task.CompletedTask; }
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private static ClaimsPrincipal Master(string sub = MasterSub) => new(new ClaimsIdentity(
    [
        new Claim("iss", Issuer), new Claim(ClaimTypes.NameIdentifier, sub), new Claim("preferred_username", "admin"),
        new Claim(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin),
        new Claim(InstitutionalClaims.Scope, "order")
    ], "test-validated-bearer"));

    private static IConfiguration Config(bool enabled = true, string? digest = null) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = Issuer,
            ["InitialLoad:MasterSubject"] = MasterSub,
            ["InitialLoad:MasterIssuer"] = Issuer,
            ["InitialLoad:ApplyEnabled"] = enabled ? "true" : "false",
            ["InitialLoad:ReceiptSecret"] = Secret,
            ["InitialLoad:ExternalApprovalDigest"] = digest ?? new string('c', 64)
        }).Build();

    private static async Task<(int Status, JsonElement Body)> SendAsync(
        Func<HttpContext, Task<IResult>> handler, object? body, ClaimsPrincipal? principal)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<IAuthenticationService>(new FakeAuth(principal));
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(body is null ? [] : JsonSerializer.SerializeToUtf8Bytes(body));
        context.Response.Body = new MemoryStream();
        var result = await handler(context);
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private static Dictionary<string, string> Csv()
    {
        static string H(string f) => string.Join(",", InitialLoadTemplateEndpoints.Sheets.Single(s => s.Name == f).Columns) + "\r\n";
        return new()
        {
            ["Talleres.csv"] = H("Talleres.csv") + "t1,9951,Taller sintético,SYA,Santiago,Chile\r\n",
            ["Hermanos.csv"] = H("Hermanos.csv") + "h1,rut,12.345.678-5,Ana,Pérez,qa@example.invalid,t1,3,active,regular\r\n",
            ["CargosVigentes.csv"] = H("CargosVigentes.csv") + "h1,t1,worshipful_master,2026-03-01,2026\r\n"
        };
    }

    [Fact]
    public async Task Master_flow_preview_open_apply_replay_readback_close_and_denials()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await using (var core0 = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(iso.Connection).Options))
            await core0.Database.MigrateAsync(ct);
        await using var db = new BootstrapDbContext(new DbContextOptionsBuilder<BootstrapDbContext>().UseNpgsql(iso.Connection).Options);
        await db.Database.MigrateAsync(ct);
        db.OfficeDefinitions.Add(new PMGM.Api.Modules.Bootstrap.Entities.OfficeDefinition
        {
            Code = "worshipful_master", Name = "Venerable Maestro (sintético)", Category = "administrative",
            OrganizationType = BootstrapCodes.WorkshopType, IsPrimary = true, IsActive = true, SortOrder = 10
        });
        await db.SaveChangesAsync(ct);
        await using var core = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(iso.Connection).Options);
        var access = new InstitutionalAccessService();
        var cfg = Config();
        var files = Csv();

        Task<(int, JsonElement)> Preview(ClaimsPrincipal? p, IConfiguration? c = null) => SendAsync(
            ctx => InitialLoadCsvPreviewEndpoints.PreviewAsync(ctx, access, db, c ?? cfg, ct), files, p);
        Task<(int, JsonElement)> Open(string? reason, ClaimsPrincipal? p, IConfiguration? c = null) => SendAsync(
            ctx => InitialLoadAdminEndpoints.OpenAsync(ctx, access, db, c ?? cfg, ct), new { reason }, p);
        Task<(int, JsonElement)> Close(ClaimsPrincipal? p) => SendAsync(
            ctx => InitialLoadAdminEndpoints.CloseAsync(ctx, access, db, cfg, ct), new { reason = "Cierre de la carga inicial sintética" }, p);
        Task<(int, JsonElement)> Apply(object body, ClaimsPrincipal? p, IConfiguration? c = null) => SendAsync(
            ctx => InitialLoadApplyEndpoint.ApplyAsync(ctx, access, db, new InitialLoadApplyOrchestrator(db), c ?? cfg, ct), body, p);
        Task<(int, JsonElement)> Result(ClaimsPrincipal? p) => SendAsync(
            ctx => InitialLoadAdminEndpoints.ResultAsync(ctx, access, db, core, cfg, ct), null, p);

        // 1. Preview before opening is a dry run: applicable, with a receipt.
        var (pStatus, pBody) = await Preview(Master());
        Assert.Equal(200, pStatus);
        Assert.True(pBody.GetProperty("applicable").GetBoolean());
        var receipt = pBody.GetProperty("receipt").GetString()!;
        var applyBody = new { files, receipt };

        // 2. Denials: flag off → 404; no/other principal → 403; apply before open → 409.
        Assert.Equal(404, (await Apply(applyBody, Master(), Config(enabled: false))).Item1);
        Assert.Equal(403, (await Apply(applyBody, null)).Item1);
        Assert.Equal(403, (await Apply(applyBody, Master("another-subject"))).Item1);
        var early = await Apply(applyBody, Master());
        Assert.Equal(409, early.Item1);
        Assert.Equal("WINDOW_NOT_OPEN", early.Item2.GetProperty("code").GetString());

        // 3. Open: needs a reason; only the registered Master.
        Assert.Equal(403, (await Open("Apertura de la carga inicial sintética", Master("another-subject"))).Item1);
        var noReason = await Open(null, Master());
        Assert.Equal(422, noReason.Item1);
        Assert.Equal("AUTHORIZATION_REQUIRED", noReason.Item2.GetProperty("code").GetString());
        // Invalid/absent external-approval reference: fail closed, nothing claimed or written.
        foreach (var bad in new[] { "", "xyz", new string('C', 64), new string('c', 63) })
        {
            var denied = await Open("Apertura de la carga inicial sintética", Master(), Config(digest: bad));
            Assert.Equal(503, denied.Item1);
        }
        Assert.Equal(413, (await Open(new string('x', 5000), Master())).Item1);
        Assert.Equal("not_opened", (await Result(Master())).Item2.GetProperty("windowStatus").GetString());
        var opened = await Open("Apertura de la carga inicial sintética", Master());
        Assert.Equal(200, opened.Item1);
        Assert.Equal("open", opened.Item2.GetProperty("status").GetString());

        // 4. Apply for real, then replay.
        var applied = await Apply(applyBody, Master());
        Assert.Equal(200, applied.Item1);
        Assert.True(applied.Item2.GetProperty("applied").GetBoolean());
        Assert.Equal(1, applied.Item2.GetProperty("counts").GetProperty("officesCreated").GetInt32());
        var replay = await Apply(applyBody, Master());
        Assert.Equal(200, replay.Item1);
        Assert.True(replay.Item2.GetProperty("alreadyApplied").GetBoolean());

        // 5. Real read-back (counts, no PII) and a direct DB check of the created rows.
        var read = await Result(Master());
        Assert.Equal(200, read.Item1);
        Assert.Equal(1, read.Item2.GetProperty("workshops").GetInt32());
        Assert.Equal(1, read.Item2.GetProperty("members").GetInt32());
        Assert.Equal(1, read.Item2.GetProperty("memberships").GetInt32());
        Assert.Equal(1, read.Item2.GetProperty("officeAssignments").GetInt32());
        Assert.DoesNotContain("12.345.678", read.Item2.GetRawText());
        Assert.Equal("Pérez", (await core.People.SingleAsync(ct)).LastNames);
        Assert.Equal(403, (await Result(null)).Item1);

        // 6. Close is terminal: second close, apply and preview are denied; nothing else changes.
        var closed = await Close(Master());
        Assert.Equal(200, closed.Item1);
        Assert.Equal("closed", closed.Item2.GetProperty("status").GetString());
        Assert.Equal(422, (await Close(Master())).Item1);
        Assert.Equal(409, (await Apply(applyBody, Master())).Item1);
        Assert.Equal(409, (await Preview(Master())).Item1);
        Assert.Equal(1, await db.Organizations.AsNoTracking().CountAsync(x => x.Origin == "initial_load", ct));
        Assert.Equal(1, await core.People.CountAsync(ct));
    }

    /// <summary>The same flow through the REAL ASP.NET pipeline (routing, DI, authorization filters), with the test authentication scheme emitting a JWT-shaped Master.</summary>
    [Fact]
    public async Task Http_pipeline_master_flow_and_non_master_and_flag_off()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await using (var core0 = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(iso.Connection).Options))
            await core0.Database.MigrateAsync(ct);
        await using (var boot = new BootstrapDbContext(new DbContextOptionsBuilder<BootstrapDbContext>().UseNpgsql(iso.Connection).Options))
        {
            await boot.Database.MigrateAsync(ct);
            boot.OfficeDefinitions.Add(new PMGM.Api.Modules.Bootstrap.Entities.OfficeDefinition
            {
                Code = "worshipful_master", Name = "Venerable Maestro (sintético)", Category = "administrative",
                OrganizationType = BootstrapCodes.WorkshopType, IsPrimary = true, IsActive = true, SortOrder = 10
            });
            await boot.SaveChangesAsync(ct);
        }

        System.Net.Http.HttpClient Client(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> f, bool master, string subject = MasterSub)
        {
            var c = f.CreateClient();
            if (master) { c.DefaultRequestHeaders.Add("X-Test-Master-Subject", subject); c.DefaultRequestHeaders.Add("X-Test-Master-Issuer", Issuer); }
            return c;
        }
        static System.Net.Http.StringContent Json(object o) => new(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");
        async Task<(int, JsonElement)> Do(System.Net.Http.HttpClient c, string method, string path, object? body = null)
        {
            using var request = new System.Net.Http.HttpRequestMessage(new System.Net.Http.HttpMethod(method), "/api/platform/bootstrap/initial-load/" + path);
            if (body is not null) request.Content = Json(body);
            using var response = await c.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            return ((int)response.StatusCode, text.Length > 0 && text[0] is '{' ? JsonDocument.Parse(text).RootElement.Clone() : default);
        }
        var files = Csv();
        void Settings(Microsoft.AspNetCore.Hosting.IWebHostBuilder b, bool enabled)
        {
            b.UseSetting("ConnectionStrings:MainDatabase", iso.Connection);
            b.UseSetting("Authentication:Authority", Issuer);
            b.UseSetting("InitialLoad:MasterSubject", MasterSub);
            b.UseSetting("InitialLoad:MasterIssuer", Issuer);
            b.UseSetting("InitialLoad:ApplyEnabled", enabled ? "true" : "false");
            b.UseSetting("InitialLoad:ReceiptSecret", Secret);
            b.UseSetting("InitialLoad:ExternalApprovalDigest", new string('c', 64));
            // The endpoints authenticate the "Bearer" scheme explicitly; in CI that scheme's handler is the test authenticator
            // (the same practice as every other HTTP workflow test), so the Master principal reaches the real handlers.
            b.ConfigureServices(services => services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(o =>
            {
                var bearer = o.Schemes.Single(x => x.Name == JwtBearerDefaults.AuthenticationScheme);
                bearer.HandlerType = typeof(LodgeManagementTestAuthenticationHandler);
            }));
        }

        // Flag off: every new route is 404 even for the Master.
        using (var off = new LodgeManagementWebApplicationFactory(iso.Connection).WithWebHostBuilder(b => Settings(b, false)))
        using (var c = Client(off, true))
        {
            Assert.Equal(404, (await Do(c, "POST", "apply", new { files, receipt = "x" })).Item1);
            Assert.Equal(404, (await Do(c, "POST", "window/open", new { reason = "Apertura de la carga inicial" })).Item1);
            Assert.Equal(404, (await Do(c, "GET", "result")).Item1);
        }

        using var on = new LodgeManagementWebApplicationFactory(iso.Connection).WithWebHostBuilder(b => Settings(b, true));
        using var anon = Client(on, false);
        using var other = Client(on, true, "another-subject");
        using var master = Client(on, true);
        // Non-Master (plain admin or another subject) never passes.
        Assert.Equal(403, (await Do(anon, "POST", "window/open", new { reason = "Apertura de la carga inicial" })).Item1);
        Assert.Equal(403, (await Do(other, "POST", "window/open", new { reason = "Apertura de la carga inicial" })).Item1);
        Assert.Equal(403, (await Do(other, "GET", "result")).Item1);

        var pv = await Do(master, "POST", "preview", files);
        Assert.Equal(200, pv.Item1);
        var receipt = pv.Item2.GetProperty("receipt").GetString()!;
        Assert.Equal(409, (await Do(master, "POST", "apply", new { files, receipt })).Item1);
        Assert.Equal(200, (await Do(master, "POST", "window/open", new { reason = "Apertura de la carga inicial" })).Item1);
        Assert.Equal(200, (await Do(master, "POST", "apply", new { files, receipt })).Item1);
        var rb = await Do(master, "GET", "result");
        Assert.Equal(200, rb.Item1);
        Assert.Equal(1, rb.Item2.GetProperty("members").GetInt32());
        Assert.Equal(1, rb.Item2.GetProperty("officeAssignments").GetInt32());
        Assert.Equal(200, (await Do(master, "POST", "window/close", new { reason = "Cierre de la carga inicial" })).Item1);
        Assert.Equal(409, (await Do(master, "POST", "apply", new { files, receipt })).Item1);
    }

    private sealed class IsolatedDb : IAsyncDisposable
    {
        private readonly string _admin;
        private readonly string _name;
        public string Connection { get; }
        private IsolatedDb(string admin, string name, string connection) { _admin = admin; _name = name; Connection = connection; }

        public static async Task<IsolatedDb> CreateAsync(string adminConnection, CancellationToken ct)
        {
            var name = "pmgm_flow_" + Guid.NewGuid().ToString("N")[..12];
            await using (var conn = new NpgsqlConnection(adminConnection))
            {
                await conn.OpenAsync(ct);
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn);
                await create.ExecuteNonQueryAsync(ct);
            }
            var cs = new NpgsqlConnectionStringBuilder(adminConnection) { Database = name }.ConnectionString;
            return new IsolatedDb(adminConnection, name, cs);
        }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var conn = new NpgsqlConnection(_admin);
            await conn.OpenAsync(CancellationToken.None);
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", conn);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
