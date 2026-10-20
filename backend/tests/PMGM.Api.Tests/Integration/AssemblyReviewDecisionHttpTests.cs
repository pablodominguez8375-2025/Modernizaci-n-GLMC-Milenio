using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyReviewDecisionHttpTests
{
    [Fact]
    public async Task Ri_decision_requires_explicit_role_and_is_committed_with_audit_without_suffrage()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = NewFactory(cs);
        await using (var migrations = factory.Services.CreateAsyncScope())
        {
            await migrations.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
            await migrations.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>().Database.MigrateAsync(ct);
        }

        var assembly = new AssemblyRecord
        {
            Name = "Asamblea C16 sintética",
            MeetingDate = new DateOnly(2026, 12, 20),
            CutoffDate = new DateOnly(2026, 10, 19),
            CreatedBySubject = "fixture"
        };
        var member = new AssemblyMemberStatusRecord
        {
            AssemblyId = assembly.Id,
            MemberId = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Category = "representative",
            SourceOfficeAssignmentId = Guid.NewGuid(),
            SourceEvidenceReference = "Cargo sintético C16",
            FirstNamesAtCutoff = "Nombre Ficticio",
            LastNamesAtCutoff = "Apellido Ficticio",
            RutAtCutoff = "00000000-0",
            WorkshopAtCutoff = "Taller sintético",
            VerifiedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };
        await using (var setup = factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>();
            db.Assemblies.Add(assembly);
            db.AssemblyMemberStatuses.Add(member);
            await db.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient();
        var route = $"/api/regimen-interior/asambleas/{assembly.Id}/revision/{member.PersonId}/decision";
        var payload = new
        {
            action = "request_correction",
            reason = "Solicitar antecedente vigente",
            evidenceReference = "RI-TEST-C16"
        };
        using var gsRequest = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(payload)
        };
        gsRequest.Headers.Add("X-Synthetic-Role", "grand_secretariat");
        using var rejected = await client.SendAsync(gsRequest, ct);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);

        using var unsupported = await client.PostAsJsonAsync(route,
            new { action = "confirm_eligible", reason = "No permitido", evidenceReference = "RI-TEST" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);

        using var crossAssembly = await client.PostAsJsonAsync(
            $"/api/regimen-interior/asambleas/{Guid.NewGuid()}/revision/{member.PersonId}/decision", payload, ct);
        Assert.Equal(HttpStatusCode.NotFound, crossAssembly.StatusCode);

        using var wrongMember = await client.PostAsJsonAsync(
            $"/api/regimen-interior/asambleas/{assembly.Id}/revision/{Guid.NewGuid()}/decision", payload, ct);
        Assert.Equal(HttpStatusCode.NotFound, wrongMember.StatusCode);

        using var success = await client.PostAsJsonAsync(route, payload, ct);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        var reply = await success.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.False(reply.GetProperty("mayVoteConfirmed").GetBoolean());
        Assert.False(reply.GetProperty("officiallyClosed").GetBoolean());
        Assert.True(reply.GetProperty("provisional").GetBoolean());
        Assert.Equal("request_correction", reply.GetProperty("action").GetString());
        var eventId = reply.GetProperty("decisionEventId").GetGuid();

        await using var auditScope = factory.Services.CreateAsyncScope();
        var riDb = auditScope.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>();
        var appDb = auditScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var recorded = await riDb.AssemblyReviewDecisionEvents.AsNoTracking().SingleAsync(
            x => x.Id == eventId, ct);
        Assert.Equal("ri-c16-test", recorded.DecidedBySubject);
        Assert.Equal("Solicitar antecedente vigente", recorded.Reason);
        var audit = await appDb.AuditEvents.AsNoTracking().SingleAsync(
            x => x.EntityId == eventId.ToString("D"), ct);
        Assert.Equal("regimen.asamblea.decision.created", audit.Action);
        Assert.Equal("ri-c16-test", audit.ActorSubject);
        Assert.DoesNotContain("Solicitar antecedente vigente", audit.MetadataJson ?? "");
        Assert.DoesNotContain("00000000-0", audit.MetadataJson ?? "");
    }

    private static WebApplicationFactoryAdapter NewFactory(string cs)
        => new(new PmgmWebApplicationFactory(cs)
            .WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            {
                services.RemoveAll<RegimenInteriorDbContext>();
                services.RemoveAll<DbContextOptions<RegimenInteriorDbContext>>();
                services.AddDbContext<RegimenInteriorDbContext>(options => options.UseNpgsql(cs));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = SyntheticReviewAuth.SchemeName;
                    options.DefaultChallengeScheme = SyntheticReviewAuth.SchemeName;
                    options.DefaultForbidScheme = SyntheticReviewAuth.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, SyntheticReviewAuth>(
                    SyntheticReviewAuth.SchemeName, _ => { });
            })));

    // Evita depender del tipo interno de factory.WithWebHostBuilder en el test.
    private sealed class WebApplicationFactoryAdapter(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> inner) : IDisposable
    {
        public IServiceProvider Services => inner.Services;
        public HttpClient CreateClient() => inner.CreateClient();
        public void Dispose() => inner.Dispose();
    }

    private sealed class SyntheticReviewAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "AssemblyReviewC16Synthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Synthetic-Role"].ToString() switch
            {
                "grand_secretariat" => InstitutionalRoles.GranSecretaria,
                _ => InstitutionalRoles.RegimenInterior
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "ri-c16-test"),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, role)
            ], SchemeName));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
