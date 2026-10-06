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
using PMGM.Api.Modules.Hospitalaria.Entities;
using Xunit;
namespace PMGM.Api.Tests.Integration;
[Collection(PostgresIntegrationCollection.Name)]
public sealed class DynamicGrandHospitalariaAccessHttpTests
{
    [Fact]
    public async Task Order_reads_mutations_authority_revocation_and_private_projection_are_enforced()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(connection);
        using var factory = root.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddAuthentication(o =>
        { o.DefaultAuthenticateScheme = Auth.Name; o.DefaultChallengeScheme = Auth.Name; o.DefaultForbidScheme = Auth.Name; })
            .AddScheme<AuthenticationSchemeOptions, Auth>(Auth.Name, _ => { })));
        using var admin = factory.CreateClient(); using var client = factory.CreateClient();
        var org = new Organization { Name = "Gran Hospitalaria permisos QA", Type = "workshop" };
        var submission = new HospitalariaMonthlySubmission { OrganizationId = org.Id, PeriodYear = 2026, PeriodMonth = 10,
            CutoffDate = new DateOnly(2026,10,31), Status = "submitted", CreatedBySubject = "qa" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await db.Database.MigrateAsync(ct);
            db.Organizations.Add(org); db.HospitalariaMonthlySubmissions.Add(submission); await db.SaveChangesAsync(ct);
        }
        var subject = "grand-hosp-" + Guid.NewGuid().ToString("N"); var code = "qa-" + Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("X-Hosp-Subject", subject);
        var version = (await admin.GetFromJsonAsync<JsonElement>("/api/system/access/catalog", ct)).GetProperty("version").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/system/access/profiles", new { code, name = "Consulta Hospitalaria QA", scope = "order", menuCodes = new[] { "hospitalaria" }, expectedVersion = version++ }, ct)).StatusCode);
        async Task Grant(string[] actions) => Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/system/access/profiles/{code}/grants", new { grants = new[] { new { viewCode = "hospitalaria", actions } }, expectedVersion = version++ }, ct)).StatusCode);
        await Grant(["view"]);
        var assigned = await admin.PostAsJsonAsync("/api/system/access/assignments", new { subject, profileCode = code, organizationId = (Guid?)null, effectiveFrom = "2020-01-01", effectiveTo = (string?)null, expectedVersion = version++ }, ct);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var assignment = (await assigned.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("assignments").EnumerateArray().Single(x => x.GetProperty("profileCode").GetString() == code).GetProperty("id").GetGuid();
        const string prefix = "/api/hospitalaria";
        var response = await client.GetAsync(prefix + "/acceso", ct);
        Assert.True(response.Headers.CacheControl!.Private); Assert.True(response.Headers.CacheControl.NoStore);
        var projection = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(projection.GetProperty("managed").GetBoolean());
        Assert.Equal(new[] { "view" }, projection.GetProperty("actions").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.False(projection.TryGetProperty("subject", out _)); Assert.False(projection.TryGetProperty("assignments", out _));
        long casesBefore, snapshotsBefore;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            casesBefore = await db.DeathReplenishmentCases.LongCountAsync(ct);
            snapshotsBefore = await db.HospitalariaRegularitySnapshots.LongCountAsync(ct);
        }
        foreach (var path in new[] { "/rendiciones", "/reposiciones" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(prefix + path, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(prefix + "/reposiciones/sincronizar-defunciones", null, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(prefix + "/reposiciones/tarifa", new { amountPerActiveMember = 1000, effectiveFrom = "2999-01-01", sourceReference = "QA" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(prefix + $"/rendiciones/{submission.Id}/revision", new { decision = "observed", notes = "QA" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(prefix + $"/reposiciones/transferencias/{Guid.NewGuid()}/revision", new { decision = "observed", notes = "QA" }, ct)).StatusCode);
        var regularityRequest = new { status = "up_to_date", asOfDate = "2026-10-06", sourceReference = "QA" };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(prefix + $"/talleres/{org.Id}/regularidad", regularityRequest, ct)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            Assert.Equal(casesBefore, await db.DeathReplenishmentCases.LongCountAsync(ct));
            Assert.Equal(snapshotsBefore, await db.HospitalariaRegularitySnapshots.LongCountAsync(ct));
        }
        await Grant(["view", "create", "write"]);
        client.DefaultRequestHeaders.Add("X-Hosp-Role", InstitutionalRoles.TallerHospitalaria);
        foreach (var path in new[] { "/acceso", "/rendiciones", "/reposiciones" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(prefix + path, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(prefix + "/reposiciones/sincronizar-defunciones", null, ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Hosp-Role");
        // Authorized create reaches validation without modifying shared tariff fixtures.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(prefix + "/reposiciones/tarifa", new { amountPerActiveMember = 0, effectiveFrom = "2999-01-01", sourceReference = "QA" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(prefix + $"/talleres/{org.Id}/regularidad", regularityRequest, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(prefix + $"/rendiciones/{submission.Id}/revision", new { decision = "observed", notes = "QA" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(prefix + $"/talleres/{org.Id}/regularidad", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/system/access/assignments/{assignment}?expectedVersion={version}", ct)).StatusCode);
        foreach (var path in new[] { "/rendiciones", "/reposiciones", "/reposiciones/tarifa", $"/talleres/{org.Id}/regularidad" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(prefix + path, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(prefix + "/reposiciones/sincronizar-defunciones", null, ct)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>(prefix + "/acceso", ct)).GetProperty("actions").EnumerateArray());
        await using var check = factory.Services.CreateAsyncScope(); var dbCheck = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal("observed", (await dbCheck.HospitalariaMonthlySubmissions.SingleAsync(x => x.Id == submission.Id, ct)).Status);
        Assert.True(await dbCheck.AuditEvents.AnyAsync(x => x.Action == "hospitalaria.monthly_submission.observed" && x.EntityId == submission.Id.ToString(), ct));
    }
    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "GrandHospitalaria-CI";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim("sub", Request.Headers["X-Hosp-Subject"].FirstOrDefault() ?? "grand-hosp-admin-ci"),
                new Claim(InstitutionalClaims.Scope, "order"), new Claim(InstitutionalClaims.Role, Request.Headers["X-Hosp-Role"].FirstOrDefault() ?? InstitutionalRoles.GranLogiaAdmin) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Name)), Name)));
        }
    }
}
