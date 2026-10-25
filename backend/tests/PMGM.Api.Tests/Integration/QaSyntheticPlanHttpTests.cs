using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class QaSyntheticPlanHttpTests
{
    private const string Endpoint = "/api/system/qa-synthetic/plan-metadata";

    private const string Plan = """
        {
          "schema": "PMGM-QA-OPERATIVE-DATASET-v1",
          "classification": "SYNTHETIC_ONLY",
          "importStatus": "NOT_IMPORTED",
          "preflightStatus": "READY_FOR_MAPPING_ONLY",
          "sourceSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "writesPerformed": 0,
          "mapped": { "Organization": 4, "Person": 12, "Member": 12, "Membership": 12 },
          "unmapped": {
            "offices": 3, "quotas": 9, "cash_receipts": 7,
            "quota_allocations": 7, "hospitalaria": 2, "meetings": 5, "attendance": 11
          }
        }
        """;

    private static QaSyntheticPlanTestFactory? FactoryOrNull()
    {
        var db = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        return string.IsNullOrWhiteSpace(db) ? null : new QaSyntheticPlanTestFactory(db);
    }

    [Fact]
    public async Task Deny_unauthenticated_or_non_admin_even_in_QA()
    {
        using var factory = FactoryOrNull();
        if (factory is null) return;
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        var notAuthenticated = await client.PostAsJsonAsync(Endpoint,
            JsonSerializer.Deserialize<JsonElement>(Plan), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, notAuthenticated.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Test-Unauthenticated");
        client.DefaultRequestHeaders.Add("X-Test-Role", InstitutionalRoles.TallerSegundoVigilante);
        var forbidden = await client.PostAsJsonAsync(Endpoint,
            JsonSerializer.Deserialize<JsonElement>(Plan), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Admin_can_only_review_untrusted_metadata_without_any_writes()
    {
        using var factory = FactoryOrNull();
        if (factory is null) return;
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Endpoint,
            JsonSerializer.Deserialize<JsonElement>(Plan), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var output = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("REVIEW_ONLY", output.GetProperty("status").GetString());
        Assert.Equal("UNTRUSTED_CLIENT_SUMMARY", output.GetProperty("sourceVerification").GetString());
        Assert.True(output.GetProperty("needsIndependentDatasetVerification").GetBoolean());
        Assert.False(output.GetProperty("canApply").GetBoolean());
        Assert.Equal(0, output.GetProperty("writesPerformed").GetInt32());
        var serialized = output.ToString();
        Assert.DoesNotContain("email", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rut", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lastNames", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_private_field_and_excessive_body_without_echo()
    {
        using var factory = FactoryOrNull();
        if (factory is null) return;
        using var client = factory.CreateClient();

        var injected = Plan.Replace("\"writesPerformed\": 0",
            "\"writesPerformed\": 0, \"privateNotes\": \"nombre real\"", StringComparison.Ordinal);
        var bad = await client.PostAsync(Endpoint,
            new StringContent(injected, System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var text = await bad.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("INVALID_PLAN_METADATA", text, StringComparison.Ordinal);
        Assert.DoesNotContain("nombre real", text, StringComparison.Ordinal);
        Assert.DoesNotContain("privateNotes", text, StringComparison.Ordinal);

        var huge = "{\"unused\":\"" + new string('x', 8200) + "\"}";
        var tooLarge = await client.PostAsync(Endpoint,
            new StringContent(huge, System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
        var oversized = await tooLarge.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("BODY_TOO_LARGE", oversized, StringComparison.Ordinal);
    }

    private sealed class QaSyntheticPlanTestFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("QA");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["QaSyntheticImport:PlanEnabled"] = "true",
                    ["Database:ApplyMigrationsOnStartup"] = "false"
                }));
            builder.ConfigureTestServices(services =>
            {
                // Remove only the data worker; retain TestServer's hosting service.
                foreach (var hosted in services.Where(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType?.Name == "HospitalariaContributionWorker").ToArray())
                    services.Remove(hosted);
                services.RemoveAll<PmgmDbContext>();
                services.RemoveAll<DbContextOptions<PmgmDbContext>>();
                services.AddDbContext<PmgmDbContext>(options => options.UseNpgsql(connectionString));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = QaSyntheticTestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = QaSyntheticTestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = QaSyntheticTestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, QaSyntheticTestAuthHandler>(
                    QaSyntheticTestAuthHandler.SchemeName, _ => { });
            });
        }
    }

    private sealed class QaSyntheticTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "PMGM-QaPlan-Testing";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-Unauthenticated"].ToString() == "true")
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[]
            {
                new Claim("sub", "qa-plan-test-admin"),
                new Claim(ClaimTypes.NameIdentifier, "qa-plan-test-admin"),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role,
                    Request.Headers["X-Test-Role"].FirstOrDefault() ?? InstitutionalRoles.GranLogiaAdmin)
            };
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(
                    new ClaimsIdentity(claims, SchemeName)), SchemeName)));
        }
    }
}
