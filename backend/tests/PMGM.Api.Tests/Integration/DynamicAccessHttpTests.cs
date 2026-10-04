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
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury.Entities;
using PMGM.Api.Modules.Treasury;
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
        var persisted = await dbCheck.DynamicAccessSnapshots.AsNoTracking().SingleAsync(s => s.Version == version + 1, ct);
        Assert.Contains(code, persisted.Payload);
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
    [Fact]
    public async Task Treasury_projection_and_real_endpoints_restrict_mutations_and_revoke_without_legacy_fallback()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection); using var client = factory.CreateClient();
        var org = new Organization { Name = "Tesorería permisos QA", Type = "workshop", TreasuryTerritory = "santiago" };
        var subject = "treasury-" + Guid.NewGuid().ToString("N"); var code = "qa-" + Guid.NewGuid().ToString("N");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await db.Database.MigrateAsync(ct);
            db.Organizations.Add(org); await db.SaveChangesAsync(ct);
        }
        var version = await Version(client, ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/system/access/profiles", new { code, name = "Consulta financiera QA", scope = "lodge", menuCodes = new[] { "treasury" }, expectedVersion = version++ }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/system/access/profiles/{code}/grants", new { grants = new[] { new { viewCode = "lodgetreasury", actions = new[] { "view", "print" } } }, expectedVersion = version++ }, ct)).StatusCode);
        var assignmentResponse = await client.PostAsJsonAsync("/api/system/access/assignments", new { subject, profileCode = code, organizationId = org.Id, effectiveFrom = "2020-01-01", effectiveTo = (string?)null, expectedVersion = version++ }, ct);
        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);
        var assignment = (await assignmentResponse.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("assignments").EnumerateArray().Single(a => a.GetProperty("profileCode").GetString() == code).GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Add("X-Dynamic-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Dynamic-Organization", org.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Dynamic-Role", InstitutionalRoles.TallerTesoreria);
        var projection = await client.GetFromJsonAsync<JsonElement>($"/api/session/treasury-access?organizationId={org.Id}", ct);
        Assert.True(projection.GetProperty("managed").GetBoolean());
        Assert.Equal(new[] { "view", "print" }, projection.GetProperty("actions").EnumerateArray().Select(a => a.GetString()).ToArray());
        Assert.False(projection.TryGetProperty("assignments", out _)); Assert.False(projection.TryGetProperty("subject", out _));
        var root = $"/api/gestion-logial/tesoreria/talleres/{org.Id}";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(root + "/resumen?year=2026&month=10", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(root + "/ingresos", new { category = "QA", amount = 1000, incomeDate = "2026-10-04", description = "Ingreso sintético" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(root + "/configuracion", new { openingBalance = 0, openingBalanceDate = "2026-01-01", incomeCategories = "QA", expenseCategories = "QA" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/tesoreria/talleres/{org.Id}/cuadros", new { periodYear = 2026, periodMonth = 10, cutoffDate = "2026-10-31", sourceReference = "QA" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(root + "/imprimir", null, ct)).StatusCode);
        using var admin = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/system/access/profiles/{code}/grants", new { grants = new[] { new { viewCode = "lodgetreasury", actions = new[] { "view", "write", "print" } } }, expectedVersion = version++ }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(root + "/ingresos", new { category = "QA", amount = 1000, incomeDate = "2026-10-04", description = "Ingreso sintético autorizado" }, ct)).StatusCode);
        // Revoke through a separate administrator request; the already-authenticated Treasurer must lose access immediately.
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/system/access/assignments/{assignment}?expectedVersion={version}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(root + "/resumen?year=2026&month=10", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(root + "/imprimir", null, ct)).StatusCode);
        await using var check = factory.Services.CreateAsyncScope(); var dbCheck = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal(1, await dbCheck.LodgeTreasuryIncomes.CountAsync(i => i.OrganizationId == org.Id, ct));
        Assert.True(await dbCheck.AuditEvents.AnyAsync(a => a.Action == "lodge.treasury.print_requested" && a.OrganizationId == org.Id, ct));
    }

    [Fact]
    public async Task Treasury_record_ids_resolve_real_organization_and_full_grants_do_not_create_approval_authority()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection); using var client = factory.CreateClient();
        var subject = "record-" + Guid.NewGuid().ToString("N");
        var org = new Organization { Name = "Origen QA", Type = "workshop", TreasuryTerritory = "santiago" };
        var other = new Organization { Name = "Otro QA", Type = "workshop", TreasuryTerritory = "santiago" };
        var member = new Member { Person = new Person { FirstNames = "Hermano", LastNames = "Ficticio" }, InstitutionalNumber = "ACCESS-" + Guid.NewGuid().ToString("N") };
        var receipt = new LodgeMemberReceipt { OrganizationId = org.Id, Member = member, Amount = 1000, Currency = "CLP", PaymentMethod = "transfer", PaymentDate = new DateOnly(2026, 10, 1), ReceiptNumber = "QA-" + Guid.NewGuid().ToString("N"), IdempotencyKey = Guid.NewGuid().ToString(), RecordedBySubject = "qa" };
        var expense = new LodgeTreasuryExpense { OrganizationId = org.Id, Category = "QA", Amount = 1000, ExpenseDate = new DateOnly(2026, 10, 1), Description = "Egreso sintético", ApprovalStatus = "pending_approval", RecordedBySubject = "qa" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await db.Database.MigrateAsync(ct);
            db.Organizations.AddRange(org, other); db.LodgeMemberReceipts.Add(receipt); db.LodgeTreasuryExpenses.Add(expense);
            var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
            var profile = new DynamicProfile(Guid.NewGuid(), "qa-" + Guid.NewGuid().ToString("N"), "Consulta QA", "lodge", false, true, ["treasury"], [new("lodgetreasury", ["view"])]);
            catalog.Profiles.Add(profile);
            catalog.Assignments.Add(new(Guid.NewGuid(), subject, profile.Code, org.Id, new DateOnly(2020, 1, 1), null, true));
            // Full grant at another Taller must not authorize a receipt in the actual origin.
            var full = profile with { Id = Guid.NewGuid(), Code = "qa-" + Guid.NewGuid().ToString("N"), Grants = [new("lodgetreasury", DynamicAccessEndpoints.AllowedActions)] };
            catalog.Profiles.Add(full); catalog.Assignments.Add(new(Guid.NewGuid(), subject, full.Code, other.Id, new DateOnly(2020, 1, 1), null, true));
            db.DynamicAccessSnapshots.Add(new() { Version = catalog.Version + 1, Payload = JsonSerializer.Serialize(catalog with { Version = catalog.Version + 1 }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
            await db.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-Dynamic-Subject", subject); client.DefaultRequestHeaders.Add("X-Dynamic-Role", InstitutionalRoles.TallerTesoreria); client.DefaultRequestHeaders.Add("X-Dynamic-Organization", org.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/gestion-logial/tesoreria/recibos/{receipt.Id}/imputaciones?organizationId={other.Id}", new { allocations = new[] { new { chargeId = Guid.NewGuid(), amount = 1000 } } }, ct)).StatusCode);
        foreach (var kind in new[] { "correction", "void" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/gestion-logial/tesoreria/recibos/{receipt.Id}/ajustes", new { kind, effectiveDate = "2026-10-04", reason = "QA", idempotencyKey = Guid.NewGuid().ToString(), allocations = Array.Empty<object>() }, ct)).StatusCode);
        // Even without a technical restriction, a Treasurer cannot approve the Venerable's expense.
        client.DefaultRequestHeaders.Remove("X-Dynamic-Subject"); client.DefaultRequestHeaders.Add("X-Dynamic-Subject", "unmanaged-qa");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/gestion-logial/tesoreria/egresos/{expense.Id}/aprobar", null, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/session/treasury-access?organizationId={other.Id}", ct)).StatusCode);
    }

    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "DynamicAccess-CI";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey("X-Dynamic-Anonymous")) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new Claim("sub", Request.Headers["X-Dynamic-Subject"].FirstOrDefault() ?? "dynamic-ci"), new Claim(InstitutionalClaims.Scope, "order"), new Claim(InstitutionalClaims.Role, Request.Headers["X-Dynamic-Role"].FirstOrDefault() ?? InstitutionalRoles.GranLogiaAdmin) };
            if (Guid.TryParse(Request.Headers["X-Dynamic-Organization"].FirstOrDefault(), out var org)) claims.Add(new Claim(InstitutionalClaims.Organization, org.ToString()));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Name)), Name)));
        }
    }
}
