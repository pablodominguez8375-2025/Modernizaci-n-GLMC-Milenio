using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class PrivacyContactMemberAccessHttpTests
{
    [Fact]
    public async Task Workshop_member_reads_privacy_contact_but_not_administrator_settings()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(cs)
            .WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = WorkshopMemberAuth.Scheme;
                    o.DefaultChallengeScheme = WorkshopMemberAuth.Scheme;
                    o.DefaultForbidScheme = WorkshopMemberAuth.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, WorkshopMemberAuth>(
                    WorkshopMemberAuth.Scheme, _ => {});
            }));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
        }
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/privacy/contact", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/system/settings/", ct)).StatusCode);
    }

    private sealed class WorkshopMemberAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Scheme = "PMGM-Privacy-Member";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", "member-privacy-test"),
                new Claim(InstitutionalClaims.Scope, "lodge"),
                new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerTesoreria)
            ], Scheme);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
