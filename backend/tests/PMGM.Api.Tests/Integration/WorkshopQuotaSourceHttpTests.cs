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
using Xunit;
namespace PMGM.Api.Tests.Integration;
[Collection(PostgresIntegrationCollection.Name)]
public sealed class WorkshopQuotaSourceHttpTests
{
    [Fact]
    public async Task Legacy_writes_are_removed_and_only_audited_profile_changes_update_zone()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connection).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = "QuotaSource"; options.DefaultChallengeScheme = "QuotaSource"; options.DefaultForbidScheme = "QuotaSource"; })
                .AddScheme<AuthenticationSchemeOptions, QuotaAuth>("QuotaSource", _ => { })));
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            await scope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>().Database.MigrateAsync(ct);
            db.Organizations.Add(new Organization { Id = id, Name = "QA zona fuente", Type = "workshop", OrienteCode = "santiago", City = "Santiago", Country = "Chile", TreasuryTerritory = "peru" });
            await db.SaveChangesAsync(ct);
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            const string relative = "docs/modelo-datos/sql/2026-10-06-verificar-zonas-cuotas.sql";
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, relative))) directory = directory.Parent;
            Assert.NotNull(directory);
            var sql = (await File.ReadAllTextAsync(Path.Combine(directory.FullName, relative), ct))
                .Replace("BEGIN TRANSACTION READ ONLY;", "").Replace("ROLLBACK;", "");
            await db.Database.OpenConnectionAsync(ct);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(ct);
            var found = false;
            while (await reader.ReadAsync(ct))
                if (reader.GetGuid(0) == id)
                {
                    found = true;
                    Assert.Equal("peru", reader.GetString(2)); Assert.Equal("santiago", reader.GetString(3));
                }
            Assert.True(found);
        }
        var zoneUrl = $"/api/tesoreria/talleres/{id}/oriente";
        foreach (var zone in new[] { "santiago", "other_oriente", "peru" })
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsJsonAsync(zoneUrl, new { territory = zone }, ct)).StatusCode);
        Assert.Equal("santiago", (await client.GetFromJsonAsync<JsonElement>(zoneUrl, ct)).GetProperty("territory").GetString());
        var profileUrl = $"/api/institutional/organizations/{id}/profile";
        var profile = await client.GetFromJsonAsync<JsonElement>(profileUrl, ct);
        Assert.Equal("santiago", profile.GetProperty("organization").GetProperty("treasuryTerritory").GetString());
        Assert.False(string.IsNullOrWhiteSpace(profile.GetProperty("quotaDecreeNumber").GetString()));
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal("peru", (await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Organizations.SingleAsync(x => x.Id == id, ct)).TreasuryTerritory);
        foreach (var row in new[] { (Code: "other_chile", City: "Valparaíso", Zone: "other_oriente"), (Code: "peru", City: "Lima", Zone: "peru"), (Code: "santiago", City: "Santiago", Zone: "santiago") })
        {
            var response = await client.PutAsJsonAsync(profileUrl + "/metadata", new { name = "QA zona fuente", establishedOn = (string?)null, city = row.City, country = "ignored", orienteCode = row.Code }, ct);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(row.Zone, (await client.GetFromJsonAsync<JsonElement>(zoneUrl, ct)).GetProperty("territory").GetString());
        }
        await using var check = factory.Services.CreateAsyncScope();
        var verify = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var item = await verify.Organizations.SingleAsync(x => x.Id == id, ct);
        Assert.Equal("santiago", item.OrienteCode); Assert.Equal("santiago", item.TreasuryTerritory);
        Assert.Equal(3, await verify.AuditEvents.CountAsync(x => x.EntityId == id.ToString() && x.Action == "organization.workshop_profile.metadata_updated", ct));
    }
    private sealed class QuotaAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim("sub", "quota-source-ci"), new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin), new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranSecretaria), new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranTesoreria) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "QuotaSource")), "QuotaSource")));
        }
    }
}
