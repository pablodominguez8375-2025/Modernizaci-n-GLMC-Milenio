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
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Hospitalaria;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class PostgreSqlHttpWorkflowTests
{
    [Fact]
    public async Task WageIncrease_full_http_workflow_authorizes_and_persists_audit()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid organizationId;
        Guid memberId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            var organization = new Organization
            {
                Name = $"Taller HTTP CI {Guid.NewGuid():N}",
                Number = "HTTP-CI",
                Type = "workshop"
            };
            var person = new Person
            {
                FirstNames = "Hermano",
                LastNames = "Integración"
            };
            var member = new Member
            {
                PersonId = person.Id,
                Person = person,
                InstitutionalNumber = $"CI-{Guid.NewGuid():N}"
            };
            var membership = new Membership
            {
                MemberId = member.Id,
                Member = member,
                OrganizationId = organization.Id,
                Organization = organization,
                MembershipType = "regular",
                StartDate = new DateOnly(2026, 1, 1),
                Status = MembershipCodes.MembershipStatus.Active
            };

            db.AddRange(organization, person, member, membership);
            await db.SaveChangesAsync(cancellationToken);

            organizationId = organization.Id;
            memberId = member.Id;
        }

        var asOf = new DateOnly(2026, 9, 7);

        var treasuryResponse = await client.PostAsJsonAsync(
            $"/api/tesoreria/talleres/{organizationId}/regularidad",
            new
            {
                status = TreasuryCodes.RegularityStatus.UpToDate,
                asOfDate = asOf,
                sourceReference = "CI-HTTP-TREASURY",
                notes = (string?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, treasuryResponse.StatusCode);

        var hospitalariaResponse = await client.PostAsJsonAsync(
            $"/api/hospitalaria/talleres/{organizationId}/regularidad",
            new
            {
                status = HospitalariaCodes.RegularityStatus.UpToDate,
                asOfDate = asOf,
                sourceReference = "CI-HTTP-HOSPITALARIA",
                notes = (string?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, hospitalariaResponse.StatusCode);

        var ceremonyResponse = await client.PostAsJsonAsync(
            "/api/ceremonias/solicitudes",
            new
            {
                organizationId,
                ceremonyType = CeremonyCodes.Type.WageIncrease,
                memberId,
                candidatePersonId = (Guid?)null,
                proposedDate = new DateOnly(2026, 10, 1),
                notes = (string?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, ceremonyResponse.StatusCode);

        var ceremonyJson = await ceremonyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var ceremonyId = ceremonyJson.GetProperty("id").GetGuid();

        var validationResponse = await client.PostAsJsonAsync(
            $"/api/ceremonias/solicitudes/{ceremonyId}/validaciones/regimen-interior",
            new
            {
                status = CeremonyCodes.ValidationStatus.Approved,
                sourceReference = "CI-HTTP-RI",
                notes = (string?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, validationResponse.StatusCode);

        var eligibilityResponse = await client.GetAsync(
            $"/api/ceremonias/solicitudes/{ceremonyId}/elegibilidad",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, eligibilityResponse.StatusCode);

        var eligibilityJson = await eligibilityResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.True(eligibilityJson.GetProperty("canAuthorize").GetBoolean());

        var authorizeResponse = await client.PostAsync(
            $"/api/ceremonias/solicitudes/{ceremonyId}/autorizar",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, authorizeResponse.StatusCode);

        var authorizeJson = await authorizeResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(CeremonyCodes.RequestStatus.Authorized, authorizeJson.GetProperty("status").GetString());

        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var db = verificationScope.ServiceProvider.GetRequiredService<PmgmDbContext>();

            var persistedCeremony = await db.CeremonyRequests
                .AsNoTracking()
                .SingleAsync(x => x.Id == ceremonyId, cancellationToken);
            Assert.Equal(CeremonyCodes.RequestStatus.Authorized, persistedCeremony.Status);

            var auditActions = await db.AuditEvents
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId)
                .Select(x => x.Action)
                .ToListAsync(cancellationToken);

            Assert.Contains("treasury.workshop_regularity.recorded", auditActions);
            Assert.Contains("hospitalaria.workshop_regularity.recorded", auditActions);
            Assert.Contains("ceremony.request.created", auditActions);
            Assert.Contains("ceremony.internal_affairs_validation.recorded", auditActions);
            Assert.Contains("ceremony.authorization.approved", auditActions);
        }
    }
}

internal sealed class PmgmWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PmgmDbContext>();
            services.RemoveAll<DbContextOptions<PmgmDbContext>>();
            services.AddDbContext<PmgmDbContext>(options => options.UseNpgsql(connectionString));

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PMGM-CI-Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("sub", "ci-http-admin"),
            new Claim(ClaimTypes.NameIdentifier, "ci-http-admin"),
            new Claim(ClaimTypes.Name, "CI HTTP Admin"),
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
