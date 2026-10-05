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
public sealed class DynamicHospitalariaAccessHttpTests
{
    private static readonly string? Connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory(string connection)
    {
        var root = new PmgmWebApplicationFactory(connection);
        return root.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddAuthentication(o => { o.DefaultAuthenticateScheme = Auth.Name; o.DefaultChallengeScheme = Auth.Name; o.DefaultForbidScheme = Auth.Name; }).AddScheme<AuthenticationSchemeOptions, Auth>(Auth.Name, _ => { })));
    }
    private static async Task<int> Version(HttpClient client, CancellationToken ct) => (await client.GetFromJsonAsync<JsonElement>("/api/system/access/catalog", ct)).GetProperty("version").GetInt32();
    [Fact]
    public async Task Local_reads_writes_authority_actual_Taller_and_revocation_are_enforced()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection); using var admin = factory.CreateClient(); using var client = factory.CreateClient();
        var org = new Organization { Name = "Hospitalaria permisos QA", Type = "workshop" };
        var other = new Organization { Name = "Otro Taller QA", Type = "workshop" };
        var submission = new PMGM.Api.Modules.Hospitalaria.Entities.HospitalariaMonthlySubmission { OrganizationId = org.Id, PeriodYear = 2026, PeriodMonth = 10, CutoffDate = new DateOnly(2026,10,31), Status = "draft", CreatedBySubject = "qa" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await db.Database.MigrateAsync(ct);
            db.Organizations.AddRange(org, other); db.HospitalariaMonthlySubmissions.Add(submission); await db.SaveChangesAsync(ct);
        }
        var subject = "hosp-" + Guid.NewGuid().ToString("N"); var code = "qa-" + Guid.NewGuid().ToString("N");
        var version = await Version(admin, ct);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/system/access/profiles", new { code, name = "Consulta Hospitalaria QA", scope = "lodge", menuCodes = new[] { "hospitalaria" }, expectedVersion = version++ }, ct)).StatusCode);
        async Task Grant(string[] actions) => Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/system/access/profiles/{code}/grants", new { grants = new[] { new { viewCode = "hospitalaria", actions } }, expectedVersion = version++ }, ct)).StatusCode);
        await Grant(["view"]);
        var assigned = await admin.PostAsJsonAsync("/api/system/access/assignments", new { subject, profileCode = code, organizationId = org.Id, effectiveFrom = "2020-01-01", effectiveTo = (string?)null, expectedVersion = version++ }, ct);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var assignment = (await assigned.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("assignments").EnumerateArray().Single(x => x.GetProperty("profileCode").GetString() == code).GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Add("X-Dynamic-Subject", subject); client.DefaultRequestHeaders.Add("X-Dynamic-Role", InstitutionalRoles.TallerHospitalaria); client.DefaultRequestHeaders.Add("X-Dynamic-Organization", org.Id.ToString());
        var url = $"/api/gestion-logial/hospitalaria/talleres/{org.Id}";
        var response = await client.GetAsync(url + "/acceso", ct);
        Assert.True(response.Headers.CacheControl!.Private); Assert.True(response.Headers.CacheControl.NoStore);
        var projection = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(new[] { "view" }, projection.GetProperty("actions").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.False(projection.TryGetProperty("subject", out _)); Assert.False(projection.TryGetProperty("assignments", out _));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url + "/resumen", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url + "/rendiciones", ct)).StatusCode);
        var request = new { movementType = "expense", category = "charity_aid", amount = 1000, movementDate = "2026-10-04", evidenceReference = "QA" };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(url + "/movimientos", request, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(url + "/rendiciones/2026/10", new { replenishmentDueAmount = 0, replenishmentPaidAmount = 0 }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/gestion-logial/hospitalaria/rendiciones/{submission.Id}/enviar", null, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/hospitalaria/reposiciones/casos/{Guid.NewGuid()}/talleres/{org.Id}/transferencias", new { amount = 1, transferDate = "2026-10-04", reference = "QA" }, ct)).StatusCode);
        await Grant(["view", "create", "write"]);
        client.DefaultRequestHeaders.Remove("X-Dynamic-Role"); client.DefaultRequestHeaders.Add("X-Dynamic-Role", InstitutionalRoles.TallerTesoreria);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(url + "/movimientos", request, ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Dynamic-Role"); client.DefaultRequestHeaders.Add("X-Dynamic-Role", InstitutionalRoles.TallerHospitalaria);
        var created = await client.PostAsJsonAsync(url + "/movimientos", request, ct); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var movementId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/gestion-logial/hospitalaria/movimientos/{movementId}/aprobar", null, ct)).StatusCode);
        await Grant(["view"]);
        client.DefaultRequestHeaders.Remove("X-Dynamic-Role"); client.DefaultRequestHeaders.Add("X-Dynamic-Role", InstitutionalRoles.TallerVenerable);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/gestion-logial/hospitalaria/movimientos/{movementId}/aprobar?organizationId={other.Id}", null, ct)).StatusCode);
        await Grant(["view", "write"]);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/gestion-logial/hospitalaria/movimientos/{movementId}/aprobar", null, ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Dynamic-Organization"); client.DefaultRequestHeaders.Add("X-Dynamic-Organization", other.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url + "/acceso", ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Dynamic-Organization"); client.DefaultRequestHeaders.Add("X-Dynamic-Organization", org.Id.ToString());
        client.DefaultRequestHeaders.Remove("X-Dynamic-Role"); client.DefaultRequestHeaders.Add("X-Dynamic-Role", InstitutionalRoles.TallerHospitalaria);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/system/access/assignments/{assignment}?expectedVersion={version}", ct)).StatusCode);
        foreach (var path in new[] { url + "/resumen", url + "/rendiciones", url + "/acuerdos-socorro", url + "/revisiones-consejo", $"/api/hospitalaria/talleres/{org.Id}/reposiciones" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path, ct)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>(url + "/acceso", ct)).GetProperty("actions").EnumerateArray());
        await using var check = factory.Services.CreateAsyncScope(); var dbCheck = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal(1, await dbCheck.LodgeHospitalariaMovements.CountAsync(x => x.OrganizationId == org.Id, ct));
        Assert.True(await dbCheck.AuditEvents.AnyAsync(x => x.Action == "lodge.hospitalaria.movement.recorded" && x.EntityId == movementId.ToString(), ct));
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
