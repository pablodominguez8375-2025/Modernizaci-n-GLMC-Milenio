using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyRosterListingHttpTests
{
    [Fact]
    public async Task Listings_mask_rut_and_only_internal_affairs_can_lift_the_debt_block()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(cs)
            .WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll<RegimenInteriorDbContext>();
                s.RemoveAll<DbContextOptions<RegimenInteriorDbContext>>();
                s.AddDbContext<RegimenInteriorDbContext>(o => o.UseNpgsql(cs));
                s.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = ListingTestAuth.SchemeName;
                    o.DefaultChallengeScheme = ListingTestAuth.SchemeName;
                    o.DefaultForbidScheme = ListingTestAuth.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, ListingTestAuth>(
                    ListingTestAuth.SchemeName, _ => {});
            }));

        var assembly = new AssemblyRecord
        {
            Name = "Asamblea de listados",
            MeetingDate = new DateOnly(2026, 11, 19),
            CutoffDate = new DateOnly(2026, 10, 19),
            CreatedBySubject = "qa-interno"
        };
        var orgId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
            var db = scope.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Assemblies.Add(assembly);
            var member = new AssemblyMemberStatusRecord
            {
                AssemblyId = assembly.Id,
                MemberId = Guid.NewGuid(), PersonId = Guid.NewGuid(),
                OrganizationId = orgId,
                Category = "representative",
                SourceOfficeAssignmentId = Guid.NewGuid(),
                SourceEvidenceReference = "VM-TEST",
                FirstNamesAtCutoff = "Nombre",
                LastNamesAtCutoff = "Apellido",
                RutAtCutoff = "12345678-5",
                WorkshopAtCutoff = "Taller moroso sintético",
                VerifiedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
            };
            db.AssemblyMemberStatuses.Add(member);
            db.AssemblyEligibilities.Add(new AssemblyEligibilityRecord
            {
                AssemblyId = assembly.Id, MemberStatusId = member.Id,
                Status = AssemblyVoterEligibilityPolicy.AttendanceOnly,
                MayAttend = true, MayVote = false,
                ReasonCode = AssemblyRosterListingBuilder.DebtReasonCode,
                EvaluatedBySubject = "qa-interno"
            });
            await db.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient();
        var listUrl = $"/api/regimen-interior/asambleas/{assembly.Id}/listados";
        var lockUrl = $"/api/regimen-interior/asambleas/{assembly.Id}/talleres/{orgId}/bloqueo-saldo";

        var first = await client.GetAsync(listUrl, ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var text = await first.Content.ReadAsStringAsync(ct);
        Assert.Contains("12.345.***-5", text);
        Assert.DoesNotContain("12345678", text);
        Assert.DoesNotContain("12.345.678", text);
        Assert.Contains("\"sufragantes\":[]", text);

        var body = new { action = "lift", reason = "Convenio de pago acreditado", evidenceReference = "ACTA-RI-1" };

        // Gran Secretaría puede leer, pero no levantar el bloqueo.
        client.DefaultRequestHeaders.Add("X-Assembly-Test-Role", InstitutionalRoles.GranSecretaria);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(listUrl, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(lockUrl, body, ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Assembly-Test-Role");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(lockUrl,
            new { action = "lift", reason = "corto", evidenceReference = "x" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(lockUrl, body, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(lockUrl, body, ct)).StatusCode);

        var lifted = await (await client.GetAsync(listUrl, ct)).Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("\"sufragantes\":[]", lifted);

        var revoke = new { action = "revoke", reason = "Convenio incumplido por el Taller", evidenceReference = "ACTA-RI-2" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(lockUrl, revoke, ct)).StatusCode);
        var again = await (await client.GetAsync(listUrl, ct)).Content.ReadAsStringAsync(ct);
        Assert.Contains("\"sufragantes\":[]", again);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            $"/api/regimen-interior/asambleas/{assembly.Id}/talleres/{Guid.NewGuid()}/bloqueo-saldo", body, ct)).StatusCode);
    }

    private sealed class ListingTestAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "AssemblyListingSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", "ri-test"),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, Request.Headers["X-Assembly-Test-Role"].FirstOrDefault() ?? InstitutionalRoles.RegimenInterior)
            ], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
