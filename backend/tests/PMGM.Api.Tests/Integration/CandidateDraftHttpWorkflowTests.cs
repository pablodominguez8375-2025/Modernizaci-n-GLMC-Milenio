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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Core.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidateDraftHttpWorkflowTests
{
    [Fact]
    public async Task Lodge_secretary_creates_persistent_draft_without_ceremony_date()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CandidateDraftWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid organizationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var coreDb = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await coreDb.Database.MigrateAsync(cancellationToken);
            var intakeDb = scope.ServiceProvider.GetRequiredService<CandidateIntakeDbContext>();
            await intakeDb.Database.MigrateAsync(cancellationToken);

            var organization = new Organization
            {
                Name = $"Taller Insinuados CI {Guid.NewGuid():N}",
                Number = "INS-CI",
                Type = "workshop"
            };
            coreDb.Organizations.Add(organization);
            await coreDb.SaveChangesAsync(cancellationToken);
            organizationId = organization.Id;
        }

        client.DefaultRequestHeaders.Add("X-Test-Organization", organizationId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", InstitutionalRoles.TallerSecretaria);

        var response = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes",
            new
            {
                firstNames = "Persona",
                paternalSurname = "Insinuada",
                maternalSurname = "CI",
                insinuationDate = new DateOnly(2026, 9, 19)
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var requestId = json.GetProperty("ceremonyRequestId").GetGuid();
        Assert.Equal(JsonValueKind.Null, json.GetProperty("proposedDate").ValueKind);
        Assert.Equal(CeremonyCodes.RequestStatus.Draft, json.GetProperty("requestStatus").GetString());
        Assert.True(json.GetProperty("profileAvailable").GetBoolean());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var coreDb = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var ceremony = await coreDb.CeremonyRequests
                .Include(x => x.CandidatePerson)
                .SingleAsync(x => x.Id == requestId, cancellationToken);
            Assert.Null(ceremony.ProposedDate);
            Assert.Equal(organizationId, ceremony.OrganizationId);
            Assert.Equal("Persona", ceremony.CandidatePerson!.FirstNames);
            Assert.Contains(await coreDb.AuditEvents.Select(x => x.Action).ToListAsync(cancellationToken),
                action => action == "candidate.intake.draft_created");

            var intakeDb = scope.ServiceProvider.GetRequiredService<CandidateIntakeDbContext>();
            var profile = await intakeDb.CandidateIntakeProfiles.SingleAsync(
                x => x.CeremonyRequestId == requestId,
                cancellationToken);
            Assert.Equal(new DateOnly(2026, 9, 19), profile.InsinuationDate);
            Assert.Equal("Insinuada", profile.PaternalSurname);
        }

        using var grandSecretariatClient = factory.CreateClient();
        grandSecretariatClient.DefaultRequestHeaders.Add("X-Test-Role", InstitutionalRoles.GranSecretaria);
        grandSecretariatClient.DefaultRequestHeaders.Add("X-Test-Scope", "order");
        var queueResponse = await grandSecretariatClient.GetAsync(
            "/api/insinuados/revision-gran-secretaria",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);
        var queue = await queueResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.DoesNotContain(
            queue.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("ceremonyRequestId").GetGuid() == requestId);
    }
}

internal sealed class CandidateDraftWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PmgmDbContext>();
            services.RemoveAll<DbContextOptions<PmgmDbContext>>();
            services.RemoveAll<CandidateIntakeDbContext>();
            services.RemoveAll<DbContextOptions<CandidateIntakeDbContext>>();
            services.AddDbContext<PmgmDbContext>(options => options.UseNpgsql(connectionString));
            services.AddDbContext<CandidateIntakeDbContext>(options => options.UseNpgsql(connectionString));

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = CandidateDraftAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = CandidateDraftAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = CandidateDraftAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, CandidateDraftAuthenticationHandler>(
                    CandidateDraftAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }
}

internal sealed class CandidateDraftAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PMGM-Candidate-Draft-Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new("sub", "ci-candidate-draft"),
            new(ClaimTypes.NameIdentifier, "ci-candidate-draft"),
            new(ClaimTypes.Name, "CI Candidate Draft")
        };
        if (Request.Headers.TryGetValue("X-Test-Role", out var role))
            claims.Add(new Claim(InstitutionalClaims.Role, role.ToString()));
        if (Request.Headers.TryGetValue("X-Test-Organization", out var organization))
            claims.Add(new Claim(InstitutionalClaims.Organization, organization.ToString()));
        if (Request.Headers.TryGetValue("X-Test-Scope", out var scope))
            claims.Add(new Claim(InstitutionalClaims.Scope, scope.ToString()));

        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
